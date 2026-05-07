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

    // ── 统一 Level 判定规则 ──

    /// <summary>
    /// 根据 EventType 或 Status 判断日志级别。
    /// 规则：Failed/Timeout/Conflict/Alarm/Error/Aborted → Error
    ///        Warning/Degraded/Retrying → Warning
    ///        其他 → Info
    /// </summary>
    public static string DetermineLevel(string? status, string? eventType)
    {
        var combined = $"{(status ?? "")}|{(eventType ?? "")}".ToUpperInvariant();

        if (combined.Contains("FAILED") || combined.Contains("FAIL") ||
            combined.Contains("ERROR") || combined.Contains("ERR") ||
            combined.Contains("TIMEOUT") || combined.Contains("CONFLICT") ||
            combined.Contains("ALARM") || combined.Contains("ABORTED") ||
            combined.Contains("CRITICAL") || combined.Contains("FATAL"))
            return "Error";

        if (combined.Contains("WARNING") || combined.Contains("WARN") ||
            combined.Contains("DEGRADED") || combined.Contains("RETRYING"))
            return "Warning";

        return "Info";
    }
}
