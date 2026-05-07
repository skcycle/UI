namespace MotionControl.Application.DTOs;

public sealed record JogAxisCommandDto(
    int AxisNo,
    double Velocity,
    bool PositiveDirection);
