using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IIoRuntimeSyncService
{
    Task ApplyAsync(IoPointConfigItem ioPoint, CancellationToken cancellationToken = default);
    Task ReloadAsync(IEnumerable<IoPointConfigItem> ioPoints, CancellationToken cancellationToken = default);
    Task RemoveAsync(bool isOutput, int address, CancellationToken cancellationToken = default);
}
