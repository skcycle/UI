namespace MotionControl.Infrastructure.Configuration;

public sealed class WorkHeadMappingOptions
{
    public List<WorkHeadConfigItem> WorkHeads { get; set; } = new();
}
