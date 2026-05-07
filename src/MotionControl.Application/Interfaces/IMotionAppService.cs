using MotionControl.Application.DTOs;
using MotionControl.Device.Abstractions.Results;

namespace MotionControl.Application.Interfaces;

public interface IMotionAppService
{
    Task<DeviceResult> EnableAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default);
    Task<DeviceResult> DisableAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default);
    Task<DeviceResult> HomeAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default);
    Task<DeviceResult> MoveAbsoluteAsync(MoveAxisCommandDto command, CancellationToken cancellationToken = default);
    Task<DeviceResult> StopAxisAsync(AxisCommandDto command, CancellationToken cancellationToken = default);
    Task<DeviceResult> JogAxisAsync(JogAxisCommandDto command, CancellationToken cancellationToken = default);
}
