using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IMagazineRuntimeSyncService
{
    Task ApplyAsync(MagazineConfigItem magazine, CancellationToken cancellationToken = default);
    Task ReloadAsync(IEnumerable<MagazineConfigItem> magazines, CancellationToken cancellationToken = default);
    Task RemoveAsync(string name, CancellationToken cancellationToken = default);
}
