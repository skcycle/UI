using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class MagazineManagementAppService(
    IMagazineConfigAppService magazineConfigAppService,
    IMagazineRuntimeSyncService magazineRuntimeSyncService,
    IIoConfigAppService ioConfigAppService) : IMagazineManagementAppService
{
    public async Task<MagazineConfigItem> AddMagazineAsync(CancellationToken cancellationToken = default)
    {
        var item = await magazineConfigAppService.AddMagazineAsync(cancellationToken);
        await magazineRuntimeSyncService.ApplyAsync(item, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteMagazineAsync(string name, CancellationToken cancellationToken = default)
    {
        var removed = await magazineConfigAppService.DeleteMagazineAsync(name, cancellationToken);
        if (!removed) return false;
        await magazineRuntimeSyncService.RemoveAsync(name, cancellationToken);
        return true;
    }

    public async Task SaveMagazinesAsync(IEnumerable<MagazineConfigItem> magazines, CancellationToken cancellationToken = default)
    {
        var items = magazines.ToList();
        var ioPoints = await ioConfigAppService.LoadIoPointsAsync(cancellationToken);
        var missingRefs = items.SelectMany(item => new[]
        {
            (item.Name, Label: "料仓有无 DI", Address: item.MaterialPresentInputAddress, IsOutput: false),
            (item.Name, Label: "当前层有料 DI", Address: item.CurrentLayerHasMaterialInputAddress, IsOutput: false),
            (item.Name, Label: "料盘防呆 DI", Address: item.TrayKeyingInputAddress, IsOutput: false)
        }).Where(x => x.Address >= 0 && !ioPoints.Any(io => io.IsOutput == x.IsOutput && io.Address == x.Address)).ToList();

        if (missingRefs.Count > 0)
        {
            var details = string.Join("; ", missingRefs.Select(x => $"{x.Name}:{x.Label}={x.Address}"));
            throw new InvalidOperationException($"Magazine 引用了未在 IO Monitor 建立的点位，请先创建对应 IO。缺失: {details}");
        }

        await magazineConfigAppService.SaveMagazinesAsync(items, cancellationToken);
        await magazineRuntimeSyncService.ReloadAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<MagazineConfigItem>> LoadMagazinesAsync(CancellationToken cancellationToken = default)
    {
        var items = await magazineConfigAppService.LoadMagazinesAsync(cancellationToken);
        await magazineRuntimeSyncService.ReloadAsync(items, cancellationToken);
        return items;
    }
}
