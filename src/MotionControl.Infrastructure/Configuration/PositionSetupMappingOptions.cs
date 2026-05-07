namespace MotionControl.Infrastructure.Configuration;

public sealed class PositionSetupMappingOptions
{
    public List<PositionSetupConfigItem> Positions { get; set; } = new();
}
