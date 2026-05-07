namespace MotionControl.Device.Zmc.Config;

public sealed class ZmcControllerOptions
{
    public string IpAddress { get; set; } = "127.0.0.1";
    public int AxisCount { get; set; } = 32;
    public int PollingIntervalMs { get; set; } = 100;

    /// <summary>
    /// 启用 EtherCAT 仿真模式。
    /// true: 使用占位提供者生成假数据，AlarmPollingService 基于假数据产生报警。
    /// false: 从控制器总线真实读取 EtherCAT 从站状态（需要实机）。
    /// </summary>
    public bool EtherCatSimulation { get; set; }
}
