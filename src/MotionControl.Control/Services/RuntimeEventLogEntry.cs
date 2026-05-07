namespace MotionControl.Control.Services;

/// <summary>
/// 统一运行时事件日志条目，用于 SQLite 持久化。
/// 6 个 runtime state 各按自己的字段映射规则填充。
/// </summary>
public sealed class RuntimeEventLogEntry
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Module { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Level { get; init; } = "Info";
    public int? AxisNo { get; init; }
    public string? ObjectName { get; init; }
    public int? Address { get; init; }
    public bool? IsOutput { get; init; }
    public bool? BoolValue { get; init; }
    public string? Status { get; init; }
    public string? CommandName { get; init; }
    public string? Message { get; init; }
    public string? PayloadJson { get; init; }
}
