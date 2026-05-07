using MotionControl.Application.Interfaces;
using MotionControl.Application.Validation;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class WorkHeadManagementAppService(
    IWorkHeadConfigAppService workHeadConfigAppService,
    IWorkHeadRuntimeSyncService workHeadRuntimeSyncService,
    IIoConfigAppService ioConfigAppService) : IWorkHeadManagementAppService
{
    public async Task<WorkHeadConfigItem> AddWorkHeadAsync(CancellationToken cancellationToken = default)
    {
        var item = await workHeadConfigAppService.AddWorkHeadAsync(cancellationToken);
        await workHeadRuntimeSyncService.ApplyAsync(item, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteWorkHeadAsync(string name, CancellationToken cancellationToken = default)
    {
        var removed = await workHeadConfigAppService.DeleteWorkHeadAsync(name, cancellationToken);
        if (!removed) return false;
        await workHeadRuntimeSyncService.RemoveAsync(name, cancellationToken);
        return true;
    }

    public async Task SaveWorkHeadsAsync(IEnumerable<WorkHeadConfigItem> workHeads, CancellationToken cancellationToken = default)
    {
        var items = workHeads.ToList();
        WorkHeadConfigValidator.Validate(items);

        var ioPoints = await ioConfigAppService.LoadIoPointsAsync(cancellationToken);
        var missingRefs = items.SelectMany(item => new[]
        {
            (item.Name, Label: "Vacuum DO", Address: item.VacuumOutputAddress, IsOutput: true),
            (item.Name, Label: "Blow DO", Address: item.BlowOutputAddress, IsOutput: true),
            (item.Name, Label: "General DO 1", Address: item.GeneralOutputAddress1, IsOutput: true),
            (item.Name, Label: "General DO 2", Address: item.GeneralOutputAddress2, IsOutput: true),
            (item.Name, Label: "Vacuum DI", Address: item.VacuumInputAddress, IsOutput: false),
            (item.Name, Label: "General DI 1", Address: item.GeneralInputAddress1, IsOutput: false),
            (item.Name, Label: "General DI 2", Address: item.GeneralInputAddress2, IsOutput: false)
        }).Where(x => x.Address >= 0 && !ioPoints.Any(io => io.IsOutput == x.IsOutput && io.Address == x.Address)).ToList();

        if (missingRefs.Count > 0)
        {
            var details = string.Join("; ", missingRefs.Select(x => $"{x.Name}:{x.Label}={x.Address}"));
            throw new InvalidOperationException($"WorkHead 引用了未在 IO Monitor 建立的点位，请先创建对应 IO。缺失: {details}");
        }

        await workHeadConfigAppService.SaveWorkHeadsAsync(items, cancellationToken);
        await workHeadRuntimeSyncService.ReloadAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkHeadConfigItem>> LoadWorkHeadsAsync(CancellationToken cancellationToken = default)
    {
        var items = await workHeadConfigAppService.LoadWorkHeadsAsync(cancellationToken);
        await workHeadRuntimeSyncService.ReloadAsync(items, cancellationToken);
        return items;
    }
}
