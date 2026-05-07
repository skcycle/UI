using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IWorkHeadRuntimeSyncService
{
    Task ApplyAsync(WorkHeadConfigItem workHead, CancellationToken cancellationToken = default);
    Task ReloadAsync(IEnumerable<WorkHeadConfigItem> workHeads, CancellationToken cancellationToken = default);
    Task RemoveAsync(string name, CancellationToken cancellationToken = default);
}
