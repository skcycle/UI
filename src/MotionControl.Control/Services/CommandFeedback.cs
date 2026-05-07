namespace MotionControl.Control.Services;

public sealed class CommandFeedback
{
    public string CommandName { get; init; } = string.Empty;
    public int? AxisNo { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
