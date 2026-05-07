namespace MotionControl.Device.Abstractions.Models;

public sealed class EtherCatSlaveStatus
{
    public int SlaveNo { get; init; }
    public string Name { get; init; } = string.Empty;
    public string State { get; init; } = "Unknown";
    public string ModuleType { get; init; } = "Servo";
    public string ModuleState { get; init; } = "Unknown";
    public string TopologyPath { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string FaultText { get; init; } = string.Empty;
    public bool IsOnline { get; init; }
    public bool HasAlarm { get; init; }
}
