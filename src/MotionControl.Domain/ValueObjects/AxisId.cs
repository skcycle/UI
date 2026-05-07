namespace MotionControl.Domain.ValueObjects;

public readonly record struct AxisId(int Value)
{
    public override string ToString() => $"Axis-{Value}";
}
