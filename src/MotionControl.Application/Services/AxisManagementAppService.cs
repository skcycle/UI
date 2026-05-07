using MotionControl.Application.Interfaces;
using MotionControl.Application.Validation;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

/// <summary>
/// Axis 配置相关的用例编排服务。
/// 负责组合配置读写与运行时同步，
/// 使 Axis 的 Add/Delete/Load/Save 统一走应用层入口。
/// </summary>
public sealed class AxisManagementAppService(
    IAxisParameterAppService axisParameterAppService,
    IAxisRuntimeParameterSyncService axisRuntimeParameterSyncService) : IAxisManagementAppService
{
    public async Task<AxisMappingItem> AddAxisAsync(CancellationToken cancellationToken = default)
    {
        var item = await axisParameterAppService.AddAxisAsync(cancellationToken);
        await axisRuntimeParameterSyncService.ApplyAsync(item, cancellationToken);
        return item;
    }

    public Task<bool> DeleteAxisAsync(int axisNo, CancellationToken cancellationToken = default)
        => axisParameterAppService.DeleteAxisAsync(axisNo, cancellationToken);

    public async Task SaveAxisAsync(AxisMappingItem axisMappingItem, CancellationToken cancellationToken = default)
    {
        AxisConfigValidator.Validate(axisMappingItem);
        await axisParameterAppService.SaveAxisParametersAsync(axisMappingItem, cancellationToken);
        await axisRuntimeParameterSyncService.ApplyAsync(axisMappingItem, cancellationToken);
    }

    public Task<AxisMappingItem?> LoadAxisAsync(int axisNo, CancellationToken cancellationToken = default)
        => axisParameterAppService.LoadAxisParametersAsync(axisNo, cancellationToken);

    public Task<List<AxisMappingItem>> LoadAllAxesAsync(CancellationToken cancellationToken = default)
        => axisParameterAppService.LoadAllAxesAsync(cancellationToken);

    public async Task<AxisMappingItem> CreateAxisForRuntimeAsync(CancellationToken cancellationToken = default)
    {
        var existing = await axisParameterAppService.LoadAllAxesAsync(cancellationToken);
        var nextAxisNo = existing.Count == 0 ? 0 : existing.Max(a => a.AxisNo) + 1;
        var item = new AxisMappingItem
        {
            AxisNo = nextAxisNo,
            Name = $"Axis {nextAxisNo}",
            Group = string.Empty,
            IsMaster = false,
            SoftLimitPositive = 1000,
            SoftLimitNegative = -1000,
            WorkVelocity = 200,
            SetupVelocity = 50,
            PulseEquivalent = 1000,
            HomeMode = MotionControl.Domain.Enums.HomeMode.Default,
            ServoBinding = string.Empty
        };
        await axisRuntimeParameterSyncService.ApplyAsync(item, cancellationToken);
        return item;
    }

    public Task SaveAllAxesAsync(List<AxisMappingItem> items, CancellationToken cancellationToken = default)
        => axisParameterAppService.SaveAllAxesAsync(items, cancellationToken);

    public async Task SyncAxisToRuntimeAsync(AxisMappingItem item, CancellationToken cancellationToken = default)
    {
        await axisRuntimeParameterSyncService.ApplyAsync(item, cancellationToken);
    }
}
