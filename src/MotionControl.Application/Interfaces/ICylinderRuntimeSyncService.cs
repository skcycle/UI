using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface ICylinderRuntimeSyncService
{
    Task ApplyAsync(CylinderConfigItem cylinder, CancellationToken cancellationToken = default);
    Task ReloadAsync(IEnumerable<CylinderConfigItem> cylinders, CancellationToken cancellationToken = default);
    Task RemoveAsync(string name, CancellationToken cancellationToken = default);
}
