namespace MotionControl.Domain.Enums;

public enum SystemState
{
    PowerOff = 0,
    Initializing = 1,
    Connecting = 2,
    Syncing = 3,
    Idle = 4,
    Standby = 5,
    Ready = 6,
    Manual = 7,
    Auto = 8,
    Warning = 9,
    Alarm = 10,
    Fault = 11,
    EmergencyStop = 12,
    FaultRecovering = 13
}
