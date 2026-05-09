using MotionControl.Application.Interfaces;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

public sealed class WorkHeadConfigAppService : IWorkHeadConfigAppService
{
    private readonly AppSettingsRepository _repo;

    public WorkHeadConfigAppService(string appSettingsPath)
    {
        _repo = new AppSettingsRepository(appSettingsPath);
    }

    public async Task<IReadOnlyList<WorkHeadConfigItem>> LoadWorkHeadsAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        return root.WorkHeadMapping.WorkHeads.OrderBy(item => item.Name).ToList();
    }

    public async Task<WorkHeadConfigItem> AddWorkHeadAsync(CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var index = root.WorkHeadMapping.WorkHeads.Count;
        var item = new WorkHeadConfigItem
        {
            Name = $"WorkHead {index + 1}",
            Description = string.Empty,
            XAxisNo = -1, YAxisNo = -1, ZAxisNo = -1, RAxisNo = -1,
            VacuumOutputAddress = -1, BlowOutputAddress = -1, VacuumInputAddress = -1,
            GeneralOutputAddress1 = -1, GeneralOutputAddress2 = -1,
            GeneralInputAddress1 = -1, GeneralInputAddress2 = -1
        };
        root.WorkHeadMapping.WorkHeads.Add(item);
        await _repo.SaveAsync(root, cancellationToken);
        return item;
    }

    public async Task<bool> DeleteWorkHeadAsync(string name, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        var removed = root.WorkHeadMapping.WorkHeads.RemoveAll(
            item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed) return false;
        await _repo.SaveAsync(root, cancellationToken);
        return true;
    }

    public async Task SaveWorkHeadsAsync(IEnumerable<WorkHeadConfigItem> workHeads, CancellationToken cancellationToken = default)
    {
        var root = await _repo.LoadAsync<AppSettingsRoot>(cancellationToken);
        root.WorkHeadMapping.WorkHeads = workHeads.OrderBy(item => item.Name).ToList();
        await _repo.SaveAsync(root, cancellationToken);
    }
}
