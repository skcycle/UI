namespace MotionControl.Infrastructure.Configuration;

public sealed class IoPointConfigItem
{
    public string Name { get; set; } = string.Empty;
    public int Address { get; set; }
    public bool IsOutput { get; set; }
    public string Description { get; set; } = string.Empty;
}
