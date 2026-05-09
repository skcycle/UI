using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class CylinderConfigAppService : ICylinderConfigAppService
{
    private readonly AppSettingsRepository _repo;

    public CylinderConfigAppService(string appSettingsPath)
    {
        _repo = new AppSettingsRepository(appSettingsPath);
    }

    public async Task<IReadOnlyList<CylinderConfigItem>> LoadCylindersAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        return root.CylinderMapping.Cylinders.OrderBy(item => item.Name).ToList();
    }

    public async Task<CylinderConfigItem> AddCylinderAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var index = root.CylinderMapping.Cylinders.Count;
        var item = new CylinderConfigItem
        {
            Name = $"Cylinder_{index}",
            Description = string.Empty,
            ExtendSensorInputAddress = 0,
            RetractSensorInputAddress = -1,
            ExtendOutputAddress = 0,
            RetractOutputAddress = -1,
            ActionTimeoutMs = 3000
        };
        root.CylinderMapping.Cylinders.Add(item);
        await _repo.SaveAsync(root, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteCylinderAsync(string name, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var removed = root.CylinderMapping.Cylinders.RemoveAll(
            item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed) return false;
        await _repo.SaveAsync(root, cancellationToken);
        return true;
    }

    public async Task SaveCylindersAsync(IEnumerable<CylinderConfigItem> cylinders, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        root.CylinderMapping.Cylinders = cylinders.OrderBy(item => item.Name).ToList();
        await _repo.SaveAsync(root, cancellationToken);
    }
}
