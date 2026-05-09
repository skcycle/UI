namespace MotionControl.Infrastructure.Configuration;

/// <summary>
/// appsettings.json 的完整根结构。
/// 所有 ConfigAppService 共用此类，不再各自定义重复的内部类。
/// </summary>
public sealed class AppSettingsRoot
{
    public ZmcControllerConfig ZmcController { get; set; } = new();
    public AxisMappingOptions AxisMapping { get; set; } = new();
    public IoMappingOptions IoMapping { get; set; } = new();
    public CylinderMappingOptions CylinderMapping { get; set; } = new();
    public MagazineMappingOptions MagazineMapping { get; set; } = new();
    public WorkHeadMappingOptions WorkHeadMapping { get; set; } = new();
    public PositionSetupMappingOptions PositionSetupMapping { get; set; } = new();
}

public sealed class ZmcControllerConfig
{
    public string IpAddress { get; set; } = "127.0.0.1";
    public int AxisCount { get; set; } = 32;
    public int PollingIntervalMs { get; set; } = 200;
}
