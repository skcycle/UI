using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IAxisManagementAppService
{
    Task<AxisMappingItem> AddAxisAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteAxisAsync(int axisNo, CancellationToken cancellationToken = default);
    Task SaveAxisAsync(AxisMappingItem axisMappingItem, CancellationToken cancellationToken = default);
    Task<AxisMappingItem?> LoadAxisAsync(int axisNo, CancellationToken cancellationToken = default);
    Task<List<AxisMappingItem>> LoadAllAxesAsync(CancellationToken cancellationToken = default);
    Task<AxisMappingItem> CreateAxisForRuntimeAsync(CancellationToken cancellationToken = default);
    Task SaveAllAxesAsync(List<AxisMappingItem> items, CancellationToken cancellationToken = default);
    Task SyncAxisToRuntimeAsync(AxisMappingItem item, CancellationToken cancellationToken = default);
}
