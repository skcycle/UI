namespace MotionControl.Domain.Enums;

public enum MotionMode
{
    None = 0,
    Jog = 1,
    Absolute = 2,
    Relative = 3,
    Home = 4,
    GroupSync = 5,
    Interpolation = 6
}
