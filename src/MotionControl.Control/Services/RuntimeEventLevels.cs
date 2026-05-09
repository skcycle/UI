namespace MotionControl.Control.Services;

/// <summary>
/// 统一的事件日志语义常量。
/// 所有 EventType / Level / Status 字符串必须使用本类常量，禁止硬编码字符串。
///
/// <para><b>Level 规范</b></para>
/// <list type="bullet">
///   <item>Info    — 正常操作记录</item>
///   <item>Warning — 需要关注但不影响运行</item>
///   <item>Error   — 操作失败、报警触发</item>
///   <item>Critical — 系统级致命错误 (预留)</item>
/// </list>
///
/// <para><b>EventType 规范</b></para>
/// <list type="bullet">
///   <item>Command    — 操作发起</item>
///   <item>Succeeded  — 操作成功</item>
///   <item>Failed     — 操作失败</item>
///   <item>Recovered  — 从故障恢复</item>
///   <item>Timeout    — 超时</item>
///   <item>Conflict   — 冲突/资源争用</item>
///   <item>Changed    — 状态变更</item>
///   <item>Scan       — 逐层扫描</item>
///   <item>ScanSummary — 扫描汇总</item>
///   <item>Teach      — 示教</item>
///   <item>AlarmCleared — 报警清除</item>
/// </list>
///
/// <para><b>关系</b></para>
/// Alarm 只承担告警生命周期 (Raised/Cleared)。<br />
/// EventLog 承担过程记录和上下文。<br />
/// Level 由 RuntimeEventLogEntry.DetermineLevel() 自动根据 EventType 判定。
/// </summary>
public static class RuntimeEventLevels
{
    // ── Level ──────────────────────────────────────────────────

    public const string Info     = "Info";
    public const string Warning  = "Warning";
    public const string Error    = "Error";
    public const string Critical = "Critical";

    // ── EventType ──────────────────────────────────────────────

    public const string EventCommand      = "Command";
    public const string EventSucceeded    = "Succeeded";
    public const string EventFailed       = "Failed";
    public const string EventRecovered    = "Recovered";
    public const string EventTimeout      = "Timeout";
    public const string EventConflict     = "Conflict";
    public const string EventChanged      = "Changed";
    public const string EventScan         = "Scan";
    public const string EventScanSummary  = "ScanSummary";
    public const string EventTeach        = "Teach";
    public const string EventAlarmCleared = "AlarmCleared";

    // ── Status (Alarm / runtime state) ────────────────────────

    public const string StatusStarted   = "Started";
    public const string StatusSucceeded = "Succeeded";
    public const string StatusFailed    = "Failed";
    public const string StatusRaised    = "Raised";
    public const string StatusCleared   = "Cleared";
    public const string StatusRunning   = "Running";
    public const string StatusReady     = "Ready";
    public const string StatusLost      = "Lost";
    public const string StatusRequested = "Requested";
    public const string StatusTriggered = "Triggered";
    public const string StatusForceReset = "ForceReset";

    // ── 枚举 ⇄ 字符串 转换 ────────────────────────────────────

    public static RuntimeEventLevel ToLevel(string? level)
        => level switch
        {
            Error   => RuntimeEventLevel.Error,
            Warning => RuntimeEventLevel.Warning,
            Critical => RuntimeEventLevel.Critical,
            _       => RuntimeEventLevel.Info
        };

    public static string ToLevelString(RuntimeEventLevel level)
        => level switch
        {
            RuntimeEventLevel.Error    => Error,
            RuntimeEventLevel.Warning  => Warning,
            RuntimeEventLevel.Critical => Critical,
            _                          => Info
        };

    public static RuntimeEventType? TryParseEventType(string? s)
    {
        if (s is null) return null;
        return s switch
        {
            EventCommand      => RuntimeEventType.Command,
            EventSucceeded    => RuntimeEventType.Succeeded,
            EventFailed       => RuntimeEventType.Failed,
            EventRecovered    => RuntimeEventType.Recovered,
            EventTimeout      => RuntimeEventType.Timeout,
            EventConflict     => RuntimeEventType.Conflict,
            EventChanged      => RuntimeEventType.Changed,
            EventScan         => RuntimeEventType.Scan,
            EventScanSummary  => RuntimeEventType.ScanSummary,
            EventTeach        => RuntimeEventType.Teach,
            EventAlarmCleared => RuntimeEventType.AlarmCleared,
            _                 => null
        };
    }

    public static string ToEventTypeString(RuntimeEventType t)
        => t switch
        {
            RuntimeEventType.Command      => EventCommand,
            RuntimeEventType.Succeeded    => EventSucceeded,
            RuntimeEventType.Failed       => EventFailed,
            RuntimeEventType.Recovered    => EventRecovered,
            RuntimeEventType.Timeout      => EventTimeout,
            RuntimeEventType.Conflict     => EventConflict,
            RuntimeEventType.Changed      => EventChanged,
            RuntimeEventType.Scan         => EventScan,
            RuntimeEventType.ScanSummary  => EventScanSummary,
            RuntimeEventType.Teach        => EventTeach,
            RuntimeEventType.AlarmCleared => EventAlarmCleared,
            _                             => EventCommand
        };

    public static RuntimeEventStatus? TryParseStatus(string? s)
        => s switch
        {
            StatusStarted    => RuntimeEventStatus.Started,
            StatusSucceeded => RuntimeEventStatus.Succeeded,
            StatusFailed    => RuntimeEventStatus.Failed,
            StatusRaised    => RuntimeEventStatus.Raised,
            StatusCleared   => RuntimeEventStatus.Cleared,
            StatusRunning   => RuntimeEventStatus.Running,
            StatusReady     => RuntimeEventStatus.Ready,
            StatusLost      => RuntimeEventStatus.Lost,
            StatusRequested => RuntimeEventStatus.Requested,
            StatusTriggered => RuntimeEventStatus.Triggered,
            StatusForceReset => RuntimeEventStatus.ForceReset,
            _               => null
        };

    public static string ToStatusString(RuntimeEventStatus s)
        => s switch
        {
            RuntimeEventStatus.Started    => StatusStarted,
            RuntimeEventStatus.Succeeded => StatusSucceeded,
            RuntimeEventStatus.Failed    => StatusFailed,
            RuntimeEventStatus.Raised    => StatusRaised,
            RuntimeEventStatus.Cleared   => StatusCleared,
            RuntimeEventStatus.Running   => StatusRunning,
            RuntimeEventStatus.Ready     => StatusReady,
            RuntimeEventStatus.Lost      => StatusLost,
            RuntimeEventStatus.Requested => StatusRequested,
            RuntimeEventStatus.Triggered => StatusTriggered,
            RuntimeEventStatus.ForceReset => StatusForceReset,
            _                           => StatusStarted
        };

    public static RuntimeEventModule TryParseModule(string? module)
        => module switch
        {
            "System"        => RuntimeEventModule.System,
            "Controller"    => RuntimeEventModule.Controller,
            "Axis"          => RuntimeEventModule.Axis,
            "Io"            => RuntimeEventModule.Io,
            "Cylinder"      => RuntimeEventModule.Cylinder,
            "Magazine"      => RuntimeEventModule.Magazine,
            "WorkHead"      => RuntimeEventModule.WorkHead,
            "PositionSetup" => RuntimeEventModule.PositionSetup,
            _               => RuntimeEventModule.Unknown
        };
}
