namespace MotionControl.Device.Abstractions.Models;

public sealed class EtherCatControllerStatus
{
    public bool IsConnected { get; init; }
    public bool IsOperational { get; init; }
    public int ExpectedSlaveCount { get; init; }
    public int OnlineSlaveCount { get; init; }
    public int OfflineSlaveCount { get; init; }
    public int AlarmSlaveCount { get; init; }
    public bool HasOfflineSlave { get; init; }
    public bool HasAnySlaveAlarm { get; init; }
    public string SummaryState { get; init; } = "Unknown";
    public string NetworkState { get; init; } = "Unknown";
    public string ControllerModel { get; init; } = "ZMC432EtherCAT";
    public IReadOnlyCollection<EtherCatSlaveStatus> Slaves { get; init; } = Array.Empty<EtherCatSlaveStatus>();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
