namespace MotionControl.Infrastructure.Configuration;

/// <summary>
/// PositionSetup 父对象配置。
/// 包含轴映射、SafeZ，以及该对象下所有具名位置点。
/// </summary>
public sealed class PositionSetupConfigItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>安全抬升 Z 位移（该对象下所有位置共用）</summary>
    public double SafeZ { get; set; }

    /// <summary>轴映射（Xx/X/Y/Z/U/V/W），该对象下所有位置共用</summary>
    public int XxAxisNo { get; set; } = -1;
    public int XAxisNo { get; set; } = -1;
    public int YAxisNo { get; set; } = -1;
    public int ZAxisNo { get; set; } = -1;
    public int UAxisNo { get; set; } = -1;
    public int VAxisNo { get; set; } = -1;
    public int WAxisNo { get; set; } = -1;

    /// <summary>该对象下的所有具名位置点</summary>
    public List<PositionSetupPositionConfigItem> Positions { get; set; } = new();
}
