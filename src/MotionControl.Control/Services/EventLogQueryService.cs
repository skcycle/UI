using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MotionControl.Control.Services;

/// <summary>
/// 事件日志查询服务。
/// 封装对 SQLite 的查询，供 ViewModel / Dashboard 使用。
/// </summary>
public sealed class EventLogQueryService : IEventLogStore
{
    private readonly IEventLogStore _eventLogStore;
    private readonly ILogger<EventLogQueryService> _logger;

    public EventLogQueryService(IEventLogStore eventLogStore, ILogger<EventLogQueryService>? logger = null)
    {
        _eventLogStore = eventLogStore;
        _logger = logger ?? NullLogger<EventLogQueryService>.Instance;
    }

    /// <summary>最近 N 条事件</summary>
    public Task<IReadOnlyList<RuntimeEventLogEntry>> QueryRecentAsync(int count = 200, CancellationToken ct = default)
        => _eventLogStore.QueryRecentAsync(count, ct);

    /// <summary>通用条件查询</summary>
    public Task<IReadOnlyList<RuntimeEventLogEntry>> QueryAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? module = null,
        int? axisNo = null,
        string? level = null,
        string? objectName = null,
        CancellationToken ct = default)
        => _eventLogStore.QueryAsync(fromUtc, toUtc, module, axisNo, level, objectName, ct);

    /// <summary>最近 N 秒内的 Error 事件</summary>
    public Task<IReadOnlyList<RuntimeEventLogEntry>> QueryRecentErrorsAsync(int secondsBack = 300, CancellationToken ct = default)
        => _eventLogStore.QueryAsync(
            fromUtc: DateTime.UtcNow.AddSeconds(-secondsBack),
            level: "Error",
            ct: ct);

    /// <summary>指定轴的报警事件</summary>
    public Task<IReadOnlyList<RuntimeEventLogEntry>> QueryAxisAlarmsAsync(int axisNo, DateTime? fromUtc = null, CancellationToken ct = default)
        => _eventLogStore.QueryAsync(
            fromUtc: fromUtc,
            axisNo: axisNo,
            level: "Error",
            ct: ct);

    /// <summary>指定时间窗口内的操作记录</summary>
    public Task<IReadOnlyList<RuntimeEventLogEntry>> QueryOperationsAsync(DateTime fromUtc, DateTime toUtc, int? axisNo = null, CancellationToken ct = default)
        => _eventLogStore.QueryAsync(
            fromUtc: fromUtc,
            toUtc: toUtc,
            module: "AxisCommand",
            axisNo: axisNo,
            ct: ct);

    /// <summary>按模块查询</summary>
    public Task<IReadOnlyList<RuntimeEventLogEntry>> QueryByModuleAsync(string module, DateTime? fromUtc = null, int count = 500, CancellationToken ct = default)
        => _eventLogStore.QueryAsync(
            fromUtc: fromUtc,
            module: module,
            ct: ct);

    // ── 诊断 ──

    public long DroppedCount => _eventLogStore.DroppedCount;
    public int PendingCount => _eventLogStore.PendingCount;
    public string DatabasePath => _eventLogStore.DatabasePath;
    public int RetentionDays => _eventLogStore.RetentionDays;

    // ── IEventLogStore forwarders ──

    public void Enqueue(RuntimeEventLogEntry entry) => _eventLogStore.Enqueue(entry);
    public Task WarmupAsync(CancellationToken ct = default) => _eventLogStore.WarmupAsync(ct);
    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default) => _eventLogStore.DeleteOlderThanAsync(cutoffUtc, ct);
    public Task FlushAsync(CancellationToken ct = default) => _eventLogStore.FlushAsync(ct);
    public Task<IReadOnlyList<string>> GetDistinctModulesAsync(CancellationToken ct = default) => _eventLogStore.GetDistinctModulesAsync(ct);
    public void EnqueueTestEntry(string module, string eventType, string level, string? message = null) => _eventLogStore.EnqueueTestEntry(module, eventType, level, message);
    public Task ClearAllAsync(CancellationToken ct = default) => _eventLogStore.ClearAllAsync(ct);
}
