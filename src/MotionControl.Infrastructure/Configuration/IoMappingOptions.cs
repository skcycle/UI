namespace MotionControl.Infrastructure.Configuration;

public sealed class IoMappingOptions
{
    public List<IoPointConfigItem> Points { get; set; } = new();
}
