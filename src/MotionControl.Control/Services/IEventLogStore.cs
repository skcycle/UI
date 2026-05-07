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

    /// <summary>按时间/模块/轴号/级别/对象名查询</summary>
    Task<IReadOnlyList<RuntimeEventLogEntry>> QueryAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? module = null,
        int? axisNo = null,
        string? level = null,
        string? objectName = null,
        CancellationToken ct = default);

    /// <summary>删除指定时间之前的记录</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>排空 Channel 中剩余事件，用于优雅关闭</summary>
    Task FlushAsync(CancellationToken ct = default);

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
