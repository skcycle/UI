using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IMagazineConfigAppService
{
    Task<IReadOnlyList<MagazineConfigItem>> LoadMagazinesAsync(CancellationToken cancellationToken = default);
    Task<MagazineConfigItem> AddMagazineAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteMagazineAsync(string name, CancellationToken cancellationToken = default);
    Task SaveMagazinesAsync(IEnumerable<MagazineConfigItem> magazines, CancellationToken cancellationToken = default);
}
