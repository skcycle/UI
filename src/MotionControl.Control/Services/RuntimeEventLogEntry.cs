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
    public string Level { get; init; } = RuntimeEventLevels.Info;
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
    /// 规则：EventFailed/EventTimeout/EventConflict/AlarmRaised/Error → Error
    ///        Warning/Degraded/Retrying → Warning
    ///        其他 → Info
    /// </summary>
    public static string DetermineLevel(string? status, string? eventType)
    {
        var s = status ?? "";
        var e = eventType ?? "";

        // 优先使用常量判断，确保字符串不会飘
        if (s == RuntimeEventLevels.StatusFailed || e == RuntimeEventLevels.EventFailed
            || s == RuntimeEventLevels.StatusStarted && e.Contains("Timeout")
            || e == RuntimeEventLevels.EventTimeout || e == RuntimeEventLevels.EventConflict
            || s == RuntimeEventLevels.StatusRaised && e.Contains("Alarm")
            || s.Contains("Error") || e.Contains("Error")
            || s.Contains("Timeout") || s.Contains("Critical") || s.Contains("Fatal"))
            return RuntimeEventLevels.Error;

        if (s.Contains("Warning") || s.Contains("Warn") || e.Contains("Warning")
            || s.Contains("Degraded") || s.Contains("Retrying"))
            return RuntimeEventLevels.Warning;

        return RuntimeEventLevels.Info;
    }
}
