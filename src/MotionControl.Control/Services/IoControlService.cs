using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Device.Abstractions.Results;
using MotionControl.Diagnostics.Services;
using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Services;

public sealed class IoControlService(
    IIoController motionController,
    Machine machine,
    IoEventRuntimeState ioEventRuntimeState,
    SafetyInterlockService safetyInterlock)
{
    public async Task<DeviceResult> SetOutputAsync(int address, bool value, CancellationToken cancellationToken = default)
    {
        // 系统级安全检查：急停/故障/断连时冻结 IO 输出
        if (!safetyInterlock.IsSystemOperationAllowed(machine))
        {
            var reason = machine.CurrentState == SystemState.EmergencyStop
                ? "System is in emergency stop"
                : machine.CurrentState == SystemState.Fault
                    ? "System is in fault state"
                    : "Controller not connected";
            return DeviceResult.Fail($"[FROZEN] {reason} — IO output blocked");
        }

        var result = await motionController.SetIoPointValueAsync(address, value, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        var ioPoint = machine.IoPoints.FirstOrDefault(x => x.IsOutput && x.Address == address);
        if (ioPoint is null)
        {
            ioPoint = new IoPoint($"DO {address}", address, true);
            machine.AddIoPoint(ioPoint);
        }

        ioPoint.Update(value);
        ioEventRuntimeState.Add(new IoEventRecord
        {
            Name = ioPoint.Name,
            Address = ioPoint.Address,
            IsOutput = true,
            Value = value,
            Message = $"{ioPoint.Name} -> {(value ? "ON" : "OFF")}"
        });

        return result;
    }
}
