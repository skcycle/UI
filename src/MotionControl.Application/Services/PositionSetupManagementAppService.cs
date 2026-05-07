using MotionControl.Application.Interfaces;
using MotionControl.Application.Validation;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class PositionSetupManagementAppService(IPositionSetupConfigAppService configAppService) : IPositionSetupManagementAppService
{
    public Task<IReadOnlyList<PositionSetupConfigItem>> LoadPositionsAsync(CancellationToken cancellationToken = default)
        => configAppService.LoadPositionsAsync(cancellationToken);

    public Task<PositionSetupConfigItem> AddPositionAsync(CancellationToken cancellationToken = default)
        => configAppService.AddPositionAsync(cancellationToken);

    public Task SavePositionsAsync(IEnumerable<PositionSetupConfigItem> positions, CancellationToken cancellationToken = default)
    {
        var items = positions.ToList();
        PositionSetupConfigValidator.Validate(items);
        return configAppService.SavePositionsAsync(items, cancellationToken);
    }
}
