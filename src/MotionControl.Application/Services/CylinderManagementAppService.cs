using MotionControl.Application.Interfaces;
using MotionControl.Application.Validation;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class CylinderManagementAppService(
    ICylinderConfigAppService cylinderConfigAppService,
    ICylinderRuntimeSyncService cylinderRuntimeSyncService,
    IIoConfigAppService ioConfigAppService) : ICylinderManagementAppService
{
    public async Task<CylinderConfigItem> AddCylinderAsync(CancellationToken cancellationToken = default)
    {
        var item = await cylinderConfigAppService.AddCylinderAsync(cancellationToken);
        await cylinderRuntimeSyncService.ApplyAsync(item, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteCylinderAsync(string name, CancellationToken cancellationToken = default)
    {
        var removed = await cylinderConfigAppService.DeleteCylinderAsync(name, cancellationToken);
        if (!removed)
        {
            return false;
        }

        await cylinderRuntimeSyncService.RemoveAsync(name, cancellationToken);
        return true;
    }

    public async Task SaveCylindersAsync(IEnumerable<CylinderConfigItem> cylinders, CancellationToken cancellationToken = default)
    {
        var items = cylinders.ToList();
        CylinderConfigValidator.Validate(items);

        var ioPoints = await ioConfigAppService.LoadIoPointsAsync(cancellationToken);
        var missingRefs = items
            .SelectMany(item => new[]
            {
                (item.Name, Label: "Extend DI", Address: item.ExtendSensorInputAddress, IsOutput: false),
                (item.Name, Label: "Retract DI", Address: item.RetractSensorInputAddress, IsOutput: false),
                (item.Name, Label: "Extend DO", Address: item.ExtendOutputAddress, IsOutput: true),
                (item.Name, Label: "Retract DO", Address: item.RetractOutputAddress, IsOutput: true)
            })
            .Where(x => x.Address >= 0 && !ioPoints.Any(io => io.IsOutput == x.IsOutput && io.Address == x.Address))
            .ToList();

        if (missingRefs.Count > 0)
        {
            var details = string.Join("; ", missingRefs.Select(x => $"{x.Name}:{x.Label}={x.Address}"));
            throw new InvalidOperationException($"Cylinder 引用了未在 IO Monitor 建立的点位，请先创建对应 IO。缺失: {details}");
        }

        await cylinderConfigAppService.SaveCylindersAsync(items, cancellationToken);
        await cylinderRuntimeSyncService.ReloadAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<CylinderConfigItem>> LoadCylindersAsync(CancellationToken cancellationToken = default)
    {
        var items = await cylinderConfigAppService.LoadCylindersAsync(cancellationToken);
        await cylinderRuntimeSyncService.ReloadAsync(items, cancellationToken);
        return items;
    }
}
