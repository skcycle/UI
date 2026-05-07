namespace MotionControl.Infrastructure.Configuration;

public sealed class CylinderConfigItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ExtendSensorInputAddress { get; set; }
    public int RetractSensorInputAddress { get; set; }
    public int ExtendOutputAddress { get; set; }
    public int RetractOutputAddress { get; set; }
    public int ActionTimeoutMs { get; set; } = 3000;
}
