namespace MotionControl.Control.Services;

/// <summary>
/// 事件日志持久化存储接口。
/// Enqueue 不阻塞调用方，后台消费写入 SQLite。
/// </summary>
public interface IEventLogStore
{
    /// <summary>异步入队，调用方不等待磁盘写入</summary>
    void Enqueue(RuntimeEventLogEntry entry);

    /// <summary>查询最近 N 条事件</summary>
    Task<IReadOnlyList<RuntimeEventLogEntry>> QueryRecentAsync(int count, CancellationToken ct = default);

    /// <summary>按时间/模块/轴号/级别查询</summary>
    Task<IReadOnlyList<RuntimeEventLogEntry>> QueryAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? module = null,
        int? axisNo = null,
        string? level = null,
        CancellationToken ct = default);

    /// <summary>删除指定时间之前的记录</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default);
}
