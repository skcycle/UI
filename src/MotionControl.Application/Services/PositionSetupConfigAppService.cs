using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class PositionSetupConfigAppService : IPositionSetupConfigAppService
{
    private readonly AppSettingsRepository _repo;

    public PositionSetupConfigAppService(string appSettingsPath)
    {
        _repo = new AppSettingsRepository(appSettingsPath);
    }

    public async Task<IReadOnlyList<PositionSetupConfigItem>> LoadPositionsAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        return root.PositionSetupMapping.Positions
            .Select(Normalize)
            .OrderBy(item => item.Name)
            .ToList();
    }

    public async Task<PositionSetupConfigItem> AddPositionAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var index = root.PositionSetupMapping.Positions.Count + 1;
        var item = new PositionSetupConfigItem
        {
            Name = $"PositionSetup {index}",
            Positions = new List<PositionSetupPositionConfigItem> { new() { Name = "Position 1" } }
        };
        root.PositionSetupMapping.Positions.Add(item);
        await _repo.SaveAsync(root, cancellationToken);
        return item;
    }

    public async Task SavePositionsAsync(IEnumerable<PositionSetupConfigItem> positions, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        root.PositionSetupMapping.Positions = positions
            .Select(Normalize)
            .OrderBy(item => item.Name)
            .ToList();
        await _repo.SaveAsync(root, cancellationToken);
    }

    private static PositionSetupConfigItem Normalize(PositionSetupConfigItem item)
    {
        item.Positions ??= new List<PositionSetupPositionConfigItem>();
        return item;
    }
}
