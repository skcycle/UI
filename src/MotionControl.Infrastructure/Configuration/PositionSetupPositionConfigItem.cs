namespace MotionControl.Infrastructure.Configuration;

/// <summary>
/// PositionSetup 下的一个具名位置点。
/// 多个位置点共享父对象的轴映射和 SafeZ。
/// </summary>
public sealed class PositionSetupPositionConfigItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double XxPosition { get; set; }
    public double XPosition { get; set; }
    public double YPosition { get; set; }
    public double ZPosition { get; set; }
    public double UPosition { get; set; }
    public double VPosition { get; set; }
    public double WPosition { get; set; }
}
