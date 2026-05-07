using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IAxisRuntimeParameterSyncService
{
    Task ApplyAsync(AxisMappingItem axisMappingItem, CancellationToken cancellationToken = default);
}
