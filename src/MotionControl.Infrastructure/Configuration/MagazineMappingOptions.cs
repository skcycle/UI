namespace MotionControl.Infrastructure.Configuration;

public sealed class MagazineMappingOptions
{
    public List<MagazineConfigItem> Magazines { get; set; } = new();
}
