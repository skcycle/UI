namespace MotionControl.Domain.Enums;

public enum HomeMode
{
    Default = 0,
    LimitThenIndex = 1,
    IndexOnly = 2,
    SlaveFollowMaster = 3
}
