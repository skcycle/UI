using MotionControl.Application.DTOs;
using MotionControl.Application.Interfaces;
using MotionControl.Control.Interfaces;
using MotionControl.Device.Abstractions.Results;
using MotionControl.Domain.Entities;
using MotionControl.Domain.ValueObjects;

namespace MotionControl.Application.Services;

public sealed class MotionAppService(
    IAxisControlService axisControlService,
    IHomingService homingService,
    Machine machine) : IMotionAppService
{
    public async Task<DeviceResult> EnableAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default)
    {
        var axisResult = FindAxis(command.AxisNo);
        if (!axisResult.Success) return DeviceResult.Fail(axisResult.ErrorMessage ?? $"轴 {command.AxisNo} 不存在");
        return await axisControlService.EnableAxisAsync(axisResult.Value!, cancellationToken);
    }

    public async Task<DeviceResult> DisableAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default)
    {
        var axisResult = FindAxis(command.AxisNo);
        if (!axisResult.Success) return DeviceResult.Fail(axisResult.ErrorMessage ?? $"轴 {command.AxisNo} 不存在");
        return await axisControlService.DisableAxisAsync(axisResult.Value!, cancellationToken);
    }

    public async Task<DeviceResult> HomeAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default)
    {
        var axisResult = FindAxis(command.AxisNo);
        if (!axisResult.Success) return DeviceResult.Fail(axisResult.ErrorMessage ?? $"轴 {command.AxisNo} 不存在");
        return await homingService.HomeAxisAsync(axisResult.Value!, cancellationToken);
    }

    public async Task<DeviceResult> MoveAbsoluteAsync(MoveAxisCommandDto command, CancellationToken cancellationToken = default)
    {
        var axisResult = FindAxis(command.AxisNo);
        if (!axisResult.Success) return DeviceResult.Fail(axisResult.ErrorMessage ?? $"轴 {command.AxisNo} 不存在");
        var axis = axisResult.Value!;
        var pulseEquivalent = axis.PulseEquivalent <= 0 ? 1000 : axis.PulseEquivalent;
        var pulsePosition = command.Position * pulseEquivalent;
        var engineeringVelocity = command.Velocity > 0 ? command.Velocity : axis.WorkVelocity;
        var engineeringAcceleration = command.Acceleration > 0 ? command.Acceleration : axis.Acceleration;
        var engineeringDeceleration = command.Deceleration > 0 ? command.Deceleration : axis.Deceleration;
        var pulseVelocity = engineeringVelocity * pulseEquivalent;
        var pulseAcceleration = engineeringAcceleration * pulseEquivalent;
        var pulseDeceleration = engineeringDeceleration * pulseEquivalent;
        var result = await axisControlService.MoveAbsoluteAsync(axis, pulsePosition, pulseVelocity, pulseAcceleration, pulseDeceleration, cancellationToken);
        if (!result.Success)
            return DeviceResult.Fail($"轴 {axis.Id.Value} ({axis.Name}) 定位失败");
        return DeviceResult.Ok();
    }

    public async Task<DeviceResult> StopAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default)
    {
        var axisResult = FindAxis(command.AxisNo);
        if (!axisResult.Success) return DeviceResult.Fail(axisResult.ErrorMessage ?? $"轴 {command.AxisNo} 不存在");
        return await axisControlService.StopAsync(axisResult.Value!, cancellationToken);
    }

    public async Task<DeviceResult> JogAxisAsync(JogAxisCommandDto command, CancellationToken cancellationToken = default)
    {
        var axisResult = FindAxis(command.AxisNo);
        if (!axisResult.Success) return DeviceResult.Fail(axisResult.ErrorMessage ?? $"轴 {command.AxisNo} 不存在");
        var axis = axisResult.Value!;
        var pulseEquivalent = axis.PulseEquivalent <= 0 ? 1000 : axis.PulseEquivalent;
        var engineeringVelocity = command.Velocity > 0 ? command.Velocity : axis.SetupVelocity;
        var pulseVelocity = engineeringVelocity * pulseEquivalent;
        return await axisControlService.JogAsync(axis, pulseVelocity, command.PositiveDirection, cancellationToken);
    }

    private DeviceResult<Axis> FindAxis(int axisNo)
    {
        var axis = machine.Axes.FirstOrDefault(a => a.Id.Value == axisNo);
        return axis is null
            ? DeviceResult<Axis>.Fail($"轴 {axisNo} 不存在")
            : DeviceResult<Axis>.Ok(axis);
    }
}
