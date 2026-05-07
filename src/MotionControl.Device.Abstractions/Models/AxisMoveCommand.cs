namespace MotionControl.Device.Abstractions.Models;

public sealed record AxisMoveCommand(
    double Position,
    double Velocity,
    double Acceleration,
    double Deceleration);
