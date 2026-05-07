using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IIoConfigAppService
{
    Task<IReadOnlyList<IoPointConfigItem>> LoadIoPointsAsync(CancellationToken cancellationToken = default);
    Task<IoPointConfigItem> AddIoPointAsync(bool isOutput, CancellationToken cancellationToken = default);
    Task<bool> DeleteIoPointAsync(bool isOutput, int address, CancellationToken cancellationToken = default);
    Task SaveIoPointsAsync(IEnumerable<IoPointConfigItem> ioPoints, CancellationToken cancellationToken = default);
}
