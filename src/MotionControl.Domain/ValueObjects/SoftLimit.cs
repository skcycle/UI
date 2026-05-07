namespace MotionControl.Domain.ValueObjects;

public sealed record SoftLimit(double Minimum, double Maximum)
{
    public bool Contains(double position) => position >= Minimum && position <= Maximum;
}
