using MotionControl.Domain.Enums;

namespace MotionControl.Infrastructure.Configuration;

public sealed class AxisMappingItem
{
    public int AxisNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public bool IsMaster { get; set; }
    public string? MasterAxisName { get; set; }
    public double? SoftLimitPositive { get; set; }
    public double? SoftLimitNegative { get; set; }
    public double? WorkVelocity { get; set; }
    public double? SetupVelocity { get; set; }
    public double? Acceleration { get; set; }
    public double? Deceleration { get; set; }
    public double? PulseEquivalent { get; set; }
    public HomeMode HomeMode { get; set; } = HomeMode.Default;
    public string ServoBinding { get; set; } = string.Empty;
}
