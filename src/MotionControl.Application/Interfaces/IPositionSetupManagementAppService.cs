using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IPositionSetupManagementAppService
{
    Task<IReadOnlyList<PositionSetupConfigItem>> LoadPositionsAsync(CancellationToken cancellationToken = default);
    Task<PositionSetupConfigItem> AddPositionAsync(CancellationToken cancellationToken = default);
    Task SavePositionsAsync(IEnumerable<PositionSetupConfigItem> positions, CancellationToken cancellationToken = default);
}
