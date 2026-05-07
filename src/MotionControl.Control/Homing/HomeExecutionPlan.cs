namespace MotionControl.Control.Homing;

public sealed class HomeExecutionPlan
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
}
