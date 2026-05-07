namespace MotionControl.Infrastructure.Configuration;

public sealed class AxisMappingOptions
{
    public List<string> AxisNames { get; set; } = new();
    public List<AxisMappingItem> Axes { get; set; } = new();
}
