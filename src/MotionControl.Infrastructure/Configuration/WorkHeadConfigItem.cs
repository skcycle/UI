namespace MotionControl.Infrastructure.Configuration;

public sealed class WorkHeadConfigItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int XAxisNo { get; set; } = -1;
    public int YAxisNo { get; set; } = -1;
    public int ZAxisNo { get; set; } = -1;
    public int RAxisNo { get; set; } = -1;
    public int VacuumOutputAddress { get; set; } = -1;
    public int BlowOutputAddress { get; set; } = -1;
    public int VacuumInputAddress { get; set; } = -1;
    public int GeneralOutputAddress1 { get; set; } = -1;
    public int GeneralOutputAddress2 { get; set; } = -1;
    public int GeneralInputAddress1 { get; set; } = -1;
    public int GeneralInputAddress2 { get; set; } = -1;
    public int VacuumTimeoutMs { get; set; } = 3000;
    public double SafeZ { get; set; }
    public List<WorkHeadPositionConfigItem> Positions { get; set; } = new();
}

public class WorkHeadPositionConfigItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double R { get; set; }
}
