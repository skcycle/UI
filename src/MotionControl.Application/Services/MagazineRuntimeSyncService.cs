using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class MagazineRuntimeSyncService(Machine machine) : IMagazineRuntimeSyncService
{
    public Task ApplyAsync(MagazineConfigItem magazine, CancellationToken cancellationToken = default)
    {
        EnsureIoPointExists(magazine.MaterialPresentInputAddress, false);
        EnsureIoPointExists(magazine.CurrentLayerHasMaterialInputAddress, false);
        EnsureIoPointExists(magazine.TrayKeyingInputAddress, false);

        var existing = machine.Magazines.FirstOrDefault(item => string.Equals(item.Name, magazine.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            var positions = magazine.Positions.Select(p => new MagazinePosition(p.Name, p.Description, string.IsNullOrWhiteSpace(p.Kind) ? MagazinePositionKinds.Normal : p.Kind, p.X, p.Y, p.Z));
            var created = new Magazine(magazine.Name, magazine.Description, magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo, magazine.MaterialPresentInputAddress, magazine.CurrentLayerHasMaterialInputAddress, magazine.TrayKeyingInputAddress, magazine.LayerCount, magazine.LayerHeight, magazine.PickLiftHeight, magazine.ScanSettlingMs, positions);
            created.EnsureDefaultPositions();
            machine.AddMagazine(created);
            return Task.CompletedTask;
        }

        existing.UpdateMetadata(magazine.Name, magazine.Description, magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo, magazine.MaterialPresentInputAddress, magazine.CurrentLayerHasMaterialInputAddress, magazine.TrayKeyingInputAddress, magazine.LayerCount, magazine.LayerHeight, magazine.PickLiftHeight, magazine.ScanSettlingMs);
        existing.Positions.Clear();
        foreach (var position in magazine.Positions)
        {
            existing.Positions.Add(new MagazinePosition(
                position.Name,
                position.Description,
                string.IsNullOrWhiteSpace(position.Kind) ? MagazinePositionKinds.Normal : position.Kind,
                position.X,
                position.Y,
                position.Z));
        }
        existing.EnsureDefaultPositions();
        return Task.CompletedTask;
    }

    public Task ReloadAsync(IEnumerable<MagazineConfigItem> magazines, CancellationToken cancellationToken = default)
    {
        foreach (var item in machine.Magazines.ToList()) machine.RemoveMagazine(item.Name);
        foreach (var item in magazines) ApplyAsync(item, cancellationToken);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        machine.RemoveMagazine(name);
        return Task.CompletedTask;
    }

    private void EnsureIoPointExists(int address, bool isOutput)
    {
        if (address < 0 || machine.IoPoints.Any(item => item.IsOutput == isOutput && item.Address == address)) return;
        machine.AddIoPoint(new IoPoint($"{(isOutput ? "DO" : "DI")} {address}", address, isOutput, "Auto-created for Magazine"));
    }
}
