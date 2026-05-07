namespace MotionControl.Infrastructure.Configuration;

public sealed class CylinderMappingOptions
{
    public List<CylinderConfigItem> Cylinders { get; set; } = new();
}
