using MotionControl.Device.Abstractions.Models;

namespace MotionControl.Control.Services;

public sealed class ControllerRuntimeState
{
    public EtherCatControllerStatus? LastControllerStatus { get; private set; }

    public bool IsConnected => LastControllerStatus?.IsConnected ?? false;
    public bool IsOperational => LastControllerStatus?.IsOperational ?? false;
    public bool HasOfflineSlave => LastControllerStatus?.HasOfflineSlave ?? false;
    public bool HasAnySlaveAlarm => LastControllerStatus?.HasAnySlaveAlarm ?? false;
    public string SummaryState => LastControllerStatus?.SummaryState ?? "Unknown";

    public void Update(EtherCatControllerStatus controllerStatus)
    {
        LastControllerStatus = controllerStatus;
    }
}
