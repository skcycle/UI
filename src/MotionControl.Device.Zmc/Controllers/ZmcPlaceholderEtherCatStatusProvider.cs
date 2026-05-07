using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Device.Abstractions.Models;

namespace MotionControl.Device.Zmc.Controllers;

/// <summary>
/// ZMC 下的 EtherCAT 占位状态提供者。
/// 当前返回的是用于仿真/占位展示的从站状态，
/// 后续如果接入真实 EtherCAT slave 查询，应替换为新的 provider 实现。
/// </summary>
public sealed class ZmcPlaceholderEtherCatStatusProvider : IEtherCatStatusProvider
{
    public EtherCatControllerStatus CreateStatus(bool isConnected, int axisCount)
    {
        var slaves = Enumerable.Range(1, Math.Min(axisCount, 4))
            .Select(index => new EtherCatSlaveStatus
            {
                SlaveNo = index,
                Name = $"Servo-{index:00}",
                State = isConnected ? "OP" : "INIT",
                ModuleType = index == 1 ? "Coupler" : "Servo",
                ModuleState = isConnected ? "Healthy" : "Offline",
                TopologyPath = $"ECAT/{index:00}",
                VendorId = index == 1 ? "ZMC" : "GenericDrive",
                ProductCode = index == 1 ? "Coupler-01" : $"Servo-Drv-{index:00}",
                FaultText = isConnected ? string.Empty : "Controller offline",
                IsOnline = isConnected,
                HasAlarm = false
            })
            .ToArray();

        var onlineSlaveCount = slaves.Count(slave => slave.IsOnline);
        var offlineSlaveCount = slaves.Length - onlineSlaveCount;
        var alarmSlaveCount = slaves.Count(slave => slave.HasAlarm);

        return new EtherCatControllerStatus
        {
            IsConnected = isConnected,
            IsOperational = isConnected,
            ExpectedSlaveCount = slaves.Length,
            OnlineSlaveCount = onlineSlaveCount,
            OfflineSlaveCount = offlineSlaveCount,
            AlarmSlaveCount = alarmSlaveCount,
            HasOfflineSlave = offlineSlaveCount > 0,
            HasAnySlaveAlarm = alarmSlaveCount > 0,
            SummaryState = !isConnected ? "Disconnected" : alarmSlaveCount > 0 ? "Alarm" : offlineSlaveCount > 0 ? "Warning" : "Healthy",
            NetworkState = isConnected ? "Operational" : "Disconnected",
            ControllerModel = "ZMC432EtherCAT",
            Slaves = slaves
        };
    }
}
