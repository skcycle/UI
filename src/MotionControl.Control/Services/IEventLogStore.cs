namespace MotionControl.Control.Services;

/// <summary>
/// 事件日志持久化存储接口。
/// Enqueue 不阻塞调用方，后台消费写入 SQLite。
/// </summary>
public interface IEventLogStore
{
    /// <summary>异步入队，调用方不等待磁盘写入</summary>
    void Enqueue(RuntimeEventLogEntry entry);

    /// <summary>启动时异步预热（建立连接/初始化）</summary>
    Task WarmupAsync(CancellationToken ct = default);

    /// <summary>查询最近 N 条事件</summary>
    Task<IReadOnlyList<RuntimeEventLogEntry>> QueryRecentAsync(int count, CancellationToken ct = default);

    /// <summary>按时间/模块/轴号/级别/对象名/命令/状态/地址/IO/布尔值/消息模糊查询，支持 SQL 级分页</summary>
    Task<IReadOnlyList<RuntimeEventLogEntry>> QueryAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? module = null,
        int? axisNo = null,
        string? level = null,
        string? objectName = null,
        string? commandName = null,
        string? status = null,
        int? address = null,
        bool? isOutput = null,
        bool? boolValue = null,
        string? messageSearch = null,
        int? maxRows = null,
        int? offset = null,
        CancellationToken ct = default);

    /// <summary>按相同条件查询总数（供分页用）</summary>
    Task<int> QueryCountAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? module = null,
        int? axisNo = null,
        string? level = null,
        string? objectName = null,
        string? commandName = null,
        string? status = null,
        int? address = null,
        bool? isOutput = null,
        bool? boolValue = null,
        string? messageSearch = null,
        CancellationToken ct = default);

    /// <summary>删除指定时间之前的记录</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>手动刷盘：排空 Channel 队列中所有待写事件，不关闭 Writer（仍可继续入队）。</summary>
    Task FlushPendingAsync(CancellationToken ct = default);

    /// <summary>停机专用：标记 Writer 关闭，排空剩余事件，写入后不再可写。</summary>
    Task CompleteAndDrainAsync(CancellationToken ct = default);

    /// <summary>Channel 满时被丢弃的事件总数</summary>
    long DroppedCount { get; }

    /// <summary>Channel 当前积压数量</summary>
    int PendingCount { get; }

    /// <summary>数据库文件路径</summary>
    string DatabasePath { get; }

    /// <summary>保留天数</summary>
    int RetentionDays { get; }

    /// <summary>查询数据库中已有值的模块列表（动态生成 ModuleOptions 用）</summary>
    Task<IReadOnlyList<string>> GetDistinctModulesAsync(CancellationToken ct = default);

    /// <summary>诊断写入一条测试事件</summary>
    void EnqueueTestEntry(string module, string eventType, string level, string? message = null);

    /// <summary>清空所有事件记录（诊断用）</summary>
    Task ClearAllAsync(CancellationToken ct = default);
}
