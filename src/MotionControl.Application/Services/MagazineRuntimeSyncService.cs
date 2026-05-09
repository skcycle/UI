using System.Linq;
using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class MagazineRuntimeSyncService(Machine machine)
    : RuntimeSyncServiceBase<MagazineConfigItem, Magazine>(machine), IMagazineRuntimeSyncService
{
    public override Task ApplyAsync(MagazineConfigItem magazine, CancellationToken cancellationToken = default)
    {
        EnsureIoPointExists(magazine.MaterialPresentInputAddress, isOutput: false);
        EnsureIoPointExists(magazine.CurrentLayerHasMaterialInputAddress, isOutput: false);
        EnsureIoPointExists(magazine.TrayKeyingInputAddress, isOutput: false);

        var existing = FindExisting(magazine, m => m.Name);
        if (existing is null)
        {
            var positions = magazine.Positions.Select(p => new MagazinePosition(
                p.Name, p.Description,
                string.IsNullOrWhiteSpace(p.Kind) ? MagazinePositionKinds.Normal : p.Kind,
                p.X, p.Y, p.Z));
            var created = new Magazine(
                magazine.Name, magazine.Description,
                magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo,
                magazine.MaterialPresentInputAddress,
                magazine.CurrentLayerHasMaterialInputAddress,
                magazine.TrayKeyingInputAddress,
                magazine.LayerCount, magazine.LayerHeight, magazine.PickLiftHeight, magazine.ScanSettlingMs,
                positions);
            created.EnsureDefaultPositions();
            Machine.AddMagazine(created);
        }
        else
        {
            existing.UpdateMetadata(
                magazine.Name, magazine.Description,
                magazine.XAxisNo, magazine.YAxisNo, magazine.ZAxisNo,
                magazine.MaterialPresentInputAddress,
                magazine.CurrentLayerHasMaterialInputAddress,
                magazine.TrayKeyingInputAddress,
                magazine.LayerCount, magazine.LayerHeight, magazine.PickLiftHeight, magazine.ScanSettlingMs);
            existing.Positions.Clear();
            foreach (var position in magazine.Positions)
            {
                existing.Positions.Add(new MagazinePosition(
                    position.Name, position.Description,
                    string.IsNullOrWhiteSpace(position.Kind) ? MagazinePositionKinds.Normal : position.Kind,
                    position.X, position.Y, position.Z));
            }
            existing.EnsureDefaultPositions();
        }
        return Task.CompletedTask;
    }

    protected override IEnumerable<Magazine> GetAllRuntime() => Machine.Magazines;

    protected override string GetConfigName(MagazineConfigItem config) => config.Name;

    public override Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        Machine.RemoveMagazine(name);
        return Task.CompletedTask;
    }

    protected override void RemoveRuntime(Magazine item) => Machine.RemoveMagazine(item.Name);
}
