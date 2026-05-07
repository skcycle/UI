using MotionControl.Device.Abstractions.Results;
using MotionControl.Domain.Entities;

namespace MotionControl.Control.Interfaces;

public interface IAxisControlService
{
    Task<bool> IsServoOnAsync(Axis axis, CancellationToken cancellationToken = default);
    Task<DeviceResult> EnableAxisAsync(Axis axis, CancellationToken cancellationToken = default);
    Task<DeviceResult> DisableAxisAsync(Axis axis, CancellationToken cancellationToken = default);
    Task<DeviceResult> MoveAbsoluteAsync(Axis axis, double position, double velocity, double acceleration, double deceleration, CancellationToken cancellationToken = default);
    Task<DeviceResult> JogAsync(Axis axis, double velocity, bool positiveDirection, CancellationToken cancellationToken = default);
    Task<DeviceResult> StopAsync(Axis axis, CancellationToken cancellationToken = default);
    Task<DeviceResult> ResetAlarmAsync(Axis axis, CancellationToken cancellationToken = default);
}
