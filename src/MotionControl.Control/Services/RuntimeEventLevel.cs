namespace MotionControl.Control.Services;

/// <summary>
/// 事件日志级别枚举，提供编译期类型安全。
/// </summary>
public enum RuntimeEventLevel
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// 事件类型枚举，对应 RuntimeEventLevels 中的 EventType 常量。
/// </summary>
public enum RuntimeEventType
{
    Command,
    Succeeded,
    Failed,
    Recovered,
    Timeout,
    Conflict,
    Changed,
    Scan,
    ScanSummary,
    Teach,
    AlarmCleared
}

/// <summary>
/// 运行时状态枚举，对应 RuntimeEventLevels 中的 Status 常量。
/// </summary>
public enum RuntimeEventStatus
{
    Started,
    Succeeded,
    Failed,
    Raised,
    Cleared,
    Running,
    Ready,
    Lost,
    Requested,
    Triggered,
    ForceReset
}

/// <summary>
/// 事件来源模块枚举。
/// </summary>
public enum RuntimeEventModule
{
    System,
    Controller,
    Axis,
    Io,
    Cylinder,
    Magazine,
    WorkHead,
    PositionSetup,
    Unknown
}
