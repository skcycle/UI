namespace MotionControl.Domain.Entities;

using System.Collections.ObjectModel;

public sealed class Magazine
{
    public Magazine(
        string name,
        string description,
        int xAxisNo,
        int yAxisNo,
        int zAxisNo,
        int materialPresentInputAddress,
        int currentLayerHasMaterialInputAddress,
        int trayKeyingInputAddress,
        int layerCount = 1,
        double layerHeight = 0,
        double pickLiftHeight = 0,
        int scanSettlingMs = 200,
        IEnumerable<MagazinePosition>? positions = null)
    {
        Name = name;
        Description = description;
        XAxisNo = xAxisNo;
        YAxisNo = yAxisNo;
        ZAxisNo = zAxisNo;
        MaterialPresentInputAddress = materialPresentInputAddress;
        CurrentLayerHasMaterialInputAddress = currentLayerHasMaterialInputAddress;
        TrayKeyingInputAddress = trayKeyingInputAddress;
        LayerCount = layerCount;
        LayerHeight = layerHeight;
        PickLiftHeight = pickLiftHeight;
        ScanSettlingMs = scanSettlingMs;
        Positions = new ObservableCollection<MagazinePosition>(positions ?? CreateDefaultPositions());
        SelectedPositionName = Positions.FirstOrDefault()?.Name;
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public int XAxisNo { get; private set; }
    public int YAxisNo { get; private set; }
    public int ZAxisNo { get; private set; }
    public int MaterialPresentInputAddress { get; private set; }
    public int CurrentLayerHasMaterialInputAddress { get; private set; }
    public int TrayKeyingInputAddress { get; private set; }
    public int LayerCount { get; private set; }
    public double LayerHeight { get; private set; }
    public double PickLiftHeight { get; private set; }
    public int ScanSettlingMs { get; private set; }
    public ObservableCollection<MagazinePosition> Positions { get; }
    public string? SelectedPositionName { get; set; }

    public void UpdateMetadata(
        string name,
        string description,
        int xAxisNo,
        int yAxisNo,
        int zAxisNo,
        int materialPresentInputAddress,
        int currentLayerHasMaterialInputAddress,
        int trayKeyingInputAddress,
        int layerCount,
        double layerHeight,
        double pickLiftHeight,
        int scanSettlingMs)
    {
        Name = name;
        Description = description;
        XAxisNo = xAxisNo;
        YAxisNo = yAxisNo;
        ZAxisNo = zAxisNo;
        MaterialPresentInputAddress = materialPresentInputAddress;
        CurrentLayerHasMaterialInputAddress = currentLayerHasMaterialInputAddress;
        TrayKeyingInputAddress = trayKeyingInputAddress;
        LayerCount = layerCount;
        LayerHeight = layerHeight;
        PickLiftHeight = pickLiftHeight;
        ScanSettlingMs = scanSettlingMs;
    }

    public void AddPosition(MagazinePosition position)
    {
        Positions.Add(position);
        SelectedPositionName = position.Name;
    }

    public bool DeleteSelectedPosition()
    {
        var selected = GetSelectedPosition();
        if (selected is null || IsSystemPosition(selected)) return false;
        Positions.Remove(selected);
        SelectedPositionName = Positions.FirstOrDefault()?.Name;
        return true;
    }

    public MagazinePosition? GetSelectedPosition()
    {
        return Positions.FirstOrDefault(p => string.Equals(p.Name, SelectedPositionName, StringComparison.OrdinalIgnoreCase))
               ?? Positions.FirstOrDefault();
    }

    public bool IsSelectedPositionSystemDefault()
    {
        var selected = GetSelectedPosition();
        return selected is not null && IsSystemPosition(selected);
    }

    public void EnsureDefaultPositions()
    {
        EnsureSystemPosition(MagazinePositionKinds.PickStart, "取料起始位");
        EnsureSystemPosition(MagazinePositionKinds.InspectStart, "检测起始位");
        SelectedPositionName ??= Positions.FirstOrDefault()?.Name;
    }

    private void EnsureSystemPosition(string kind, string name)
    {
        var existing = Positions.FirstOrDefault(p => string.Equals(p.Kind, kind, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = name;
            existing.Kind = kind;
            return;
        }

        Positions.Insert(Positions.Count > 0 ? Math.Min(1, Positions.Count) : 0, new MagazinePosition(name, string.Empty, kind, 0, 0, 0));

        var ordered = Positions
            .OrderBy(p => GetKindOrder(p.Kind))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Positions.Clear();
        foreach (var item in ordered) Positions.Add(item);
    }

    private static int GetKindOrder(string? kind)
    {
        return kind switch
        {
            MagazinePositionKinds.PickStart => 0,
            MagazinePositionKinds.InspectStart => 1,
            _ => 2
        };
    }

    private static bool IsSystemPosition(MagazinePosition position)
    {
        return string.Equals(position.Kind, MagazinePositionKinds.PickStart, StringComparison.OrdinalIgnoreCase)
               || string.Equals(position.Kind, MagazinePositionKinds.InspectStart, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<MagazinePosition> CreateDefaultPositions()
    {
        return new[]
        {
            new MagazinePosition("取料起始位", string.Empty, MagazinePositionKinds.PickStart, 0, 0, 0),
            new MagazinePosition("检测起始位", string.Empty, MagazinePositionKinds.InspectStart, 0, 0, 0)
        };
    }
}
