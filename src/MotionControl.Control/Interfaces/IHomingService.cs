using MotionControl.Device.Abstractions.Results;
using MotionControl.Domain.Entities;

namespace MotionControl.Control.Interfaces;

public interface IHomingService
{
    Task<DeviceResult> HomeAxisAsync(Axis axis, CancellationToken cancellationToken = default);
}
