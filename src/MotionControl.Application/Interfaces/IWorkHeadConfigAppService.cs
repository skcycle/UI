using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IWorkHeadConfigAppService
{
    Task<IReadOnlyList<WorkHeadConfigItem>> LoadWorkHeadsAsync(CancellationToken cancellationToken = default);
    Task<WorkHeadConfigItem> AddWorkHeadAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteWorkHeadAsync(string name, CancellationToken cancellationToken = default);
    Task SaveWorkHeadsAsync(IEnumerable<WorkHeadConfigItem> workHeads, CancellationToken cancellationToken = default);
}
