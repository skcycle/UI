namespace MotionControl.Application.DTOs;

public sealed record MoveAxisCommandDto(
    int AxisNo,
    double Position,
    double Velocity,
    double Acceleration,
    double Deceleration);
