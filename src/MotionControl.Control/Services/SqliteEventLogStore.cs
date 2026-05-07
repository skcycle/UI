using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MotionControl.Control.Services;

/// <summary>
/// SQLite 事件日志持久化实现。
///
/// <para><b>写入策略</b></para>
/// Enqueue() → Channel → 后台消费者批量写 SQLite。
/// 调用方不阻塞，控制循环延迟零影响。
///
/// <para><b>WAL 模式</b></para>
/// 轮询线程写 + UI 线程查互不阻塞。
///
/// <para><b>自动清理</b></para>
/// 启动时删除超过 30 天的记录。
///
/// <para><b>丢弃监控</b></para>
/// Channel 满时 DropOldest 模式，<see cref="DroppedCount"/> 记录丢弃数。
/// 每 60 秒输出一次丢弃摘要日志。
/// </summary>
public sealed class SqliteEventLogStore : IEventLogStore, IDisposable
{
    private const int DefaultRetentionDays = 30;
    private const int ChannelCapacity = 10000;
    private const int BatchWriteSize = 100;
    private const int BatchWriteIntervalMs = 200;
    private const int DroppedSummaryIntervalMs = 60000;

    private readonly Channel<RuntimeEventLogEntry> _channel;
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly ILogger<SqliteEventLogStore> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumerTask;

    private long _droppedCount;
    private long _lastDroppedSummaryCount;
    private int _pendingCount;

    public long DroppedCount => Interlocked.Read(ref _droppedCount);
    public int PendingCount => _pendingCount;
    public string DatabasePath => _dbPath;
    public int RetentionDays => DefaultRetentionDays;

    public SqliteEventLogStore(string dbPath, ILogger<SqliteEventLogStore>? logger = null)
    {
        _logger = logger ?? NullLogger<SqliteEventLogStore>.Instance;
        _dbPath = dbPath;
        _connectionString = $"Data Source={dbPath}";

        InitDatabase();

        _channel = Channel.CreateBounded<RuntimeEventLogEntry>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _consumerTask = Task.Run(() => ConsumeAsync(_cts.Token));
    }

    // ── 初始化 ──

    private void InitDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA temp_store=MEMORY;
            PRAGMA busy_timeout=5000;

            CREATE TABLE IF NOT EXISTS runtime_events (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                ts_utc    TEXT    NOT NULL,
                module    TEXT    NOT NULL,
                event_type TEXT   NOT NULL,
                level     TEXT    NOT NULL,
                axis_no   INTEGER NULL,
                object_name TEXT  NULL,
                address   INTEGER NULL,
                is_output INTEGER NULL,
                bool_value INTEGER NULL,
                status    TEXT    NULL,
                command_name TEXT NULL,
                message   TEXT    NULL,
                payload_json TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_runtime_events_ts
                ON runtime_events(ts_utc);
            CREATE INDEX IF NOT EXISTS idx_runtime_events_module_ts
                ON runtime_events(module, ts_utc);
            CREATE INDEX IF NOT EXISTS idx_runtime_events_axis_ts
                ON runtime_events(axis_no, ts_utc);
            CREATE INDEX IF NOT EXISTS idx_runtime_events_event_type_ts
                ON runtime_events(event_type, ts_utc);
            CREATE INDEX IF NOT EXISTS idx_runtime_events_level_ts
                ON runtime_events(level, ts_utc);
        ";
        cmd.ExecuteNonQuery();

        _logger.LogInformation("SQLite event log store initialized at {Path}", _connectionString);

        var cutoff = DateTime.UtcNow.AddDays(-DefaultRetentionDays);
        var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM runtime_events WHERE ts_utc < @cutoff";
        deleteCmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("O"));
        var deleted = deleteCmd.ExecuteNonQuery();
        if (deleted > 0)
            _logger.LogInformation("Cleaned {Count} old event records (older than {Cutoff:O})", deleted, cutoff);
    }

    // ── 入队 ──

    public void Enqueue(RuntimeEventLogEntry entry)
    {
        if (!_channel.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _droppedCount);
        }
    }

    // ── 排空 ──

    public async Task FlushAsync(CancellationToken ct = default)
    {
        _channel.Writer.Complete();
        var remaining = new List<RuntimeEventLogEntry>();

        await foreach (var entry in _channel.Reader.ReadAllAsync(ct))
        {
            remaining.Add(entry);
        }

        if (remaining.Count > 0)
        {
            foreach (var chunk in remaining.Chunk(BatchWriteSize))
            {
                await WriteBatchAsync(chunk.ToList(), ct);
            }
            _logger.LogInformation("Flushed {Count} pending events on demand", remaining.Count);
        }
    }

    // ── 消费者 ──

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var batch = new List<RuntimeEventLogEntry>(BatchWriteSize);
        var lastDroppedDump = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var first = await _channel.Reader.ReadAsync(ct);
                batch.Clear();
                batch.Add(first);

                using var timeoutCts = new CancellationTokenSource(BatchWriteIntervalMs);
                try
                {
                    while (batch.Count < BatchWriteSize)
                    {
                        var item = await _channel.Reader.ReadAsync(timeoutCts.Token);
                        batch.Add(item);
                    }
                }
                catch (OperationCanceledException) { /* timeout, write now */ }

                await WriteBatchAsync(batch, ct);

                // 定期输出丢弃摘要
                if ((DateTime.UtcNow - lastDroppedDump).TotalMilliseconds > DroppedSummaryIntervalMs)
                {
                    DumpDroppedSummary();
                    lastDroppedDump = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Event log consumer error"); }
        }

        await DrainRemainingAsync();
    }

    private void DumpDroppedSummary()
    {
        var total = Interlocked.Read(ref _droppedCount);
        var diff = total - Interlocked.Read(ref _lastDroppedSummaryCount);
        Interlocked.Exchange(ref _lastDroppedSummaryCount, total);

        _pendingCount = _channel.Reader.Count;

        if (diff > 0)
            _logger.LogWarning("Event log dropped {Diff} entries in last {Interval}s (total={Total}, pending={Pending})",
                diff, DroppedSummaryIntervalMs / 1000, total, _pendingCount);
        else if (total > 0)
            _logger.LogInformation("Event log stable: total dropped={Total}, pending={Pending}", total, _pendingCount);
    }

    private async Task WriteBatchAsync(List<RuntimeEventLogEntry> batch, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var tx = conn.BeginTransaction();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO runtime_events
                (ts_utc, module, event_type, level, axis_no, object_name, address,
                 is_output, bool_value, status, command_name, message, payload_json)
            VALUES
                (@ts, @mod, @evt, @lvl, @ax, @obj, @addr,
                 @isout, @bval, @st, @cmd, @msg, @payload)
        ";

        var tsParam   = cmd.Parameters.Add("@ts", SqliteType.Text);
        var modParam  = cmd.Parameters.Add("@mod", SqliteType.Text);
        var evtParam  = cmd.Parameters.Add("@evt", SqliteType.Text);
        var lvlParam  = cmd.Parameters.Add("@lvl", SqliteType.Text);
        var axParam   = cmd.Parameters.Add("@ax", SqliteType.Integer);
        var objParam  = cmd.Parameters.Add("@obj", SqliteType.Text);
        var addrParam = cmd.Parameters.Add("@addr", SqliteType.Integer);
        var isoutParam = cmd.Parameters.Add("@isout", SqliteType.Integer);
        var bvalParam = cmd.Parameters.Add("@bval", SqliteType.Integer);
        var stParam   = cmd.Parameters.Add("@st", SqliteType.Text);
        var cmdNameParam = cmd.Parameters.Add("@cmd", SqliteType.Text);
        var msgParam  = cmd.Parameters.Add("@msg", SqliteType.Text);
        var payloadParam = cmd.Parameters.Add("@payload", SqliteType.Text);

        foreach (var entry in batch)
        {
            tsParam.Value = entry.TimestampUtc.ToString("O");
            modParam.Value = entry.Module;
            evtParam.Value = entry.EventType;
            lvlParam.Value = entry.Level;
            axParam.Value = (object?)entry.AxisNo ?? DBNull.Value;
            objParam.Value = (object?)entry.ObjectName ?? DBNull.Value;
            addrParam.Value = (object?)entry.Address ?? DBNull.Value;
            isoutParam.Value = entry.IsOutput.HasValue ? (entry.IsOutput.Value ? 1 : 0) : DBNull.Value;
            bvalParam.Value = entry.BoolValue.HasValue ? (entry.BoolValue.Value ? 1 : 0) : DBNull.Value;
            stParam.Value = (object?)entry.Status ?? DBNull.Value;
            cmdNameParam.Value = (object?)entry.CommandName ?? DBNull.Value;
            msgParam.Value = (object?)entry.Message ?? DBNull.Value;
            payloadParam.Value = (object?)entry.PayloadJson ?? DBNull.Value;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private async Task DrainRemainingAsync()
    {
        try
        {
            _channel.Writer.Complete();
            var remaining = new List<RuntimeEventLogEntry>();
            await foreach (var entry in _channel.Reader.ReadAllAsync())
            {
                remaining.Add(entry);
            }

            if (remaining.Count > 0)
            {
                foreach (var chunk in remaining.Chunk(BatchWriteSize))
                {
                    await WriteBatchAsync(chunk.ToList(), CancellationToken.None);
                }
                _logger.LogInformation("Drained {Count} remaining event log entries on shutdown", remaining.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error draining event log on shutdown");
        }
    }

    // ── 查询 ──

    public async Task<IReadOnlyList<RuntimeEventLogEntry>> QueryRecentAsync(int count, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM runtime_events ORDER BY ts_utc DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", count);

        return await ReadEntriesAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<RuntimeEventLogEntry>> QueryAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? module = null,
        int? axisNo = null,
        string? level = null,
        string? objectName = null,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var where = new List<string>();
        var cmd = conn.CreateCommand();

        if (fromUtc.HasValue)
        {
            where.Add("ts_utc >= @fromUtc");
            cmd.Parameters.AddWithValue("@fromUtc", fromUtc.Value.ToString("O"));
        }
        if (toUtc.HasValue)
        {
            where.Add("ts_utc <= @toUtc");
            cmd.Parameters.AddWithValue("@toUtc", toUtc.Value.ToString("O"));
        }
        if (!string.IsNullOrEmpty(module))
        {
            where.Add("module = @module");
            cmd.Parameters.AddWithValue("@module", module);
        }
        if (axisNo.HasValue)
        {
            where.Add("axis_no = @axisNo");
            cmd.Parameters.AddWithValue("@axisNo", axisNo.Value);
        }
        if (!string.IsNullOrEmpty(level))
        {
            where.Add("level = @level");
            cmd.Parameters.AddWithValue("@level", level);
        }
        if (!string.IsNullOrEmpty(objectName))
        {
            where.Add("object_name = @objectName");
            cmd.Parameters.AddWithValue("@objectName", objectName);
        }

        cmd.CommandText = where.Count == 0
            ? "SELECT * FROM runtime_events ORDER BY ts_utc DESC LIMIT 500"
            : $"SELECT * FROM runtime_events WHERE {string.Join(" AND ", where)} ORDER BY ts_utc DESC LIMIT 1000";

        return await ReadEntriesAsync(cmd, ct);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM runtime_events WHERE ts_utc < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", cutoffUtc.ToString("O"));

        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Deleted {Count} old event records (cutoff={Cutoff:O})", deleted, cutoffUtc);
        return deleted;
    }

    public async Task<IReadOnlyList<string>> GetDistinctModulesAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT module FROM runtime_events ORDER BY module";

        var modules = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            modules.Add(reader.GetString(0));
        }
        return modules;
    }

    public async Task WarmupAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM runtime_events";
        _ = await cmd.ExecuteScalarAsync(ct);
        _logger.LogInformation("SQLite event log warmup complete");
    }

    public void EnqueueTestEntry(string module, string eventType, string level, string? message = null)
    {
        Enqueue(new RuntimeEventLogEntry
        {
            Module = module,
            EventType = eventType,
            Level = level,
            Message = message ?? $"Test entry from {module}",
            TimestampUtc = DateTime.UtcNow
        });
    }

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM runtime_events";
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogWarning("Cleared all {Count} event log entries (diagnostic operation)", deleted);
    }

    private static async Task<IReadOnlyList<RuntimeEventLogEntry>> ReadEntriesAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var results = new List<RuntimeEventLogEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadRow(reader));
        }

        return results;
    }

    private static RuntimeEventLogEntry ReadRow(SqliteDataReader r)
    {
        return new RuntimeEventLogEntry
        {
            TimestampUtc = DateTime.TryParse(r.GetString(r.GetOrdinal("ts_utc")), out var ts) ? ts : DateTime.MinValue,
            Module = r.GetString(r.GetOrdinal("module")),
            EventType = r.GetString(r.GetOrdinal("event_type")),
            Level = r.GetString(r.GetOrdinal("level")),
            AxisNo = r.IsDBNull(r.GetOrdinal("axis_no")) ? null : r.GetInt32(r.GetOrdinal("axis_no")),
            ObjectName = r.IsDBNull(r.GetOrdinal("object_name")) ? null : r.GetString(r.GetOrdinal("object_name")),
            Address = r.IsDBNull(r.GetOrdinal("address")) ? null : r.GetInt32(r.GetOrdinal("address")),
            IsOutput = r.IsDBNull(r.GetOrdinal("is_output")) ? null : r.GetInt32(r.GetOrdinal("is_output")) != 0,
            BoolValue = r.IsDBNull(r.GetOrdinal("bool_value")) ? null : r.GetInt32(r.GetOrdinal("bool_value")) != 0,
            Status = r.IsDBNull(r.GetOrdinal("status")) ? null : r.GetString(r.GetOrdinal("status")),
            CommandName = r.IsDBNull(r.GetOrdinal("command_name")) ? null : r.GetString(r.GetOrdinal("command_name")),
            Message = r.IsDBNull(r.GetOrdinal("message")) ? null : r.GetString(r.GetOrdinal("message")),
            PayloadJson = r.IsDBNull(r.GetOrdinal("payload_json")) ? null : r.GetString(r.GetOrdinal("payload_json")),
        };
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _consumerTask.Wait(TimeSpan.FromSeconds(5)); }
        catch { /* best effort */ }
        _cts.Dispose();
    }
}
