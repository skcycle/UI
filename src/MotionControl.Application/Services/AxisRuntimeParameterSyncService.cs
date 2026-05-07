using MotionControl.Application.Interfaces;
using MotionControl.Domain.Entities;
using MotionControl.Domain.ValueObjects;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Application.Services;

/// <summary>
/// 负责将 Axis 配置同步到 Machine 的运行时对象。
/// 当运行时 Axis 不存在时，这里负责补建对象；
/// 当配置变化时，这里负责把参数应用到现有 Axis。
/// </summary>
public sealed class AxisRuntimeParameterSyncService(Machine machine) : IAxisRuntimeParameterSyncService
{
    public Task ApplyAsync(AxisMappingItem axisMappingItem, CancellationToken cancellationToken = default)
    {
        var axis = machine.Axes.FirstOrDefault(item => item.Id.Value == axisMappingItem.AxisNo);
        if (axis is null)
        {
            axis = new Axis(new AxisId(axisMappingItem.AxisNo), string.IsNullOrWhiteSpace(axisMappingItem.Name) ? $"Axis {axisMappingItem.AxisNo}" : axisMappingItem.Name, axisMappingItem.AxisNo);
            machine.AddAxis(axis);
        }

        if (axisMappingItem.SoftLimitNegative.HasValue && axisMappingItem.SoftLimitPositive.HasValue)
        {
            axis.SetSoftLimit(new SoftLimit(axisMappingItem.SoftLimitNegative.Value, axisMappingItem.SoftLimitPositive.Value));
        }

        axis.SetHomeMode(axisMappingItem.HomeMode);
        axis.SetServoBinding(axisMappingItem.ServoBinding);
        if (axisMappingItem.WorkVelocity.HasValue) axis.SetWorkVelocity(axisMappingItem.WorkVelocity.Value);
        if (axisMappingItem.SetupVelocity.HasValue) axis.SetSetupVelocity(axisMappingItem.SetupVelocity.Value);
        if (axisMappingItem.Acceleration.HasValue) axis.SetAcceleration(axisMappingItem.Acceleration.Value);
        if (axisMappingItem.Deceleration.HasValue) axis.SetDeceleration(axisMappingItem.Deceleration.Value);
        if (axisMappingItem.PulseEquivalent.HasValue) axis.SetPulseEquivalent(axisMappingItem.PulseEquivalent.Value);
        if (!string.IsNullOrEmpty(axisMappingItem.Name)) axis.SetName(axisMappingItem.Name);

        return Task.CompletedTask;
    }
}
