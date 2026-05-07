using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Interfaces;

public interface IAxisParameterAppService
{
    Task<AxisMappingItem?> LoadAxisParametersAsync(int axisNo, CancellationToken cancellationToken = default);
    Task<List<AxisMappingItem>> LoadAllAxesAsync(CancellationToken cancellationToken = default);
    Task SaveAxisParametersAsync(AxisMappingItem axisMappingItem, CancellationToken cancellationToken = default);
    Task SaveAllAxesAsync(List<AxisMappingItem> items, CancellationToken cancellationToken = default);
    Task<AxisMappingItem> AddAxisAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteAxisAsync(int axisNo, CancellationToken cancellationToken = default);
}
