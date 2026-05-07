using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface ICylinderManagementAppService
{
    Task<CylinderConfigItem> AddCylinderAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteCylinderAsync(string name, CancellationToken cancellationToken = default);
    Task SaveCylindersAsync(IEnumerable<CylinderConfigItem> cylinders, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CylinderConfigItem>> LoadCylindersAsync(CancellationToken cancellationToken = default);
}
