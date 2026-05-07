namespace MotionControl.Infrastructure.Configuration;

public sealed class MagazineConfigItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int XAxisNo { get; set; } = -1;
    public int YAxisNo { get; set; } = -1;
    public int ZAxisNo { get; set; } = -1;
    public int MaterialPresentInputAddress { get; set; } = -1;
    public int CurrentLayerHasMaterialInputAddress { get; set; } = -1;
    public int TrayKeyingInputAddress { get; set; } = -1;
    public int LayerCount { get; set; } = 1;
    public double LayerHeight { get; set; }
    public double PickLiftHeight { get; set; }
    public int ScanSettlingMs { get; set; } = 200;
    public List<MagazinePositionConfigItem> Positions { get; set; } = new();
}
