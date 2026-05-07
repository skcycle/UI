namespace MotionControl.Domain.Enums;

public enum AxisState
{
    Disabled = 0,
    Standstill = 1,
    Homing = 2,
    Jogging = 3,
    Moving = 4,
    Stopping = 5,
    Alarm = 6
}
