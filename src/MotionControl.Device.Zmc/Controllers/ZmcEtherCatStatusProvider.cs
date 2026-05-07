using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Device.Abstractions.Models;
using MotionControl.Device.Zmc.Native;

namespace MotionControl.Device.Zmc.Controllers;

/// <summary>
/// 基于 ZMC Bus API 的真实 EtherCAT 从站状态提供者。
/// 通过 facade 从控制器总线读取实际的节点数量、信息和通讯状态。
/// </summary>
public sealed class ZmcEtherCatStatusProvider(ZmcAxisNativeFacade axisNativeFacade) : IEtherCatStatusProvider
{
    public EtherCatControllerStatus CreateStatus(bool isConnected, int axisCount)
    {
        if (!isConnected || !axisNativeFacade.IsConnected)
            return CreateDisconnectedStatus();

        try
        {
            return ReadBusStatus();
        }
        catch
        {
            return CreateDisconnectedStatus();
        }
    }

    private static EtherCatControllerStatus CreateDisconnectedStatus()
    {
        return new EtherCatControllerStatus
        {
            IsConnected = false,
            IsOperational = false,
            NetworkState = "Disconnected",
            SummaryState = "Disconnected",
            Slaves = Array.Empty<EtherCatSlaveStatus>(),
        };
    }

    private EtherCatControllerStatus ReadBusStatus()
    {
        int nodeCount = 0;
        var result = axisNativeFacade.GetBusNodeCount(ref nodeCount);
        if (result != 0 || nodeCount <= 0)
            return CreateDisconnectedStatus();

        nodeCount = Math.Min(nodeCount, 128);
        var slaves = new EtherCatSlaveStatus[nodeCount];

        for (var i = 0; i < nodeCount; i++)
        {
            var node = (uint)(i + 1);
            slaves[i] = ReadSlaveStatus(node, i);
        }

        var onlineSlaveCount = slaves.Count(s => s.IsOnline);
        var offlineSlaveCount = slaves.Length - onlineSlaveCount;
        var alarmSlaveCount = slaves.Count(s => s.HasAlarm);

        return new EtherCatControllerStatus
        {
            IsConnected = true,
            IsOperational = onlineSlaveCount > 0,
            ExpectedSlaveCount = slaves.Length,
            OnlineSlaveCount = onlineSlaveCount,
            OfflineSlaveCount = offlineSlaveCount,
            AlarmSlaveCount = alarmSlaveCount,
            HasOfflineSlave = offlineSlaveCount > 0,
            HasAnySlaveAlarm = alarmSlaveCount > 0,
            SummaryState = alarmSlaveCount > 0 ? "Alarm" : offlineSlaveCount > 0 ? "Warning" : "Healthy",
            NetworkState = "Operational",
            ControllerModel = "ZMC432EtherCAT",
            Slaves = slaves,
        };
    }

    private EtherCatSlaveStatus ReadSlaveStatus(uint node, int index)
    {
        var vendorId = ReadNodeInfoValue(node, 0);
        var productCode = ReadNodeInfoValue(node, 1);
        var nodeStatus = ReadNodeStatusValue(node);

        var isOnline = nodeStatus.HasValue && IsNodeOperational(nodeStatus.Value);

        return new EtherCatSlaveStatus
        {
            SlaveNo = (int)node,
            Name = DetermineSlaveName(index, vendorId),
            State = isOnline ? "OP" : "INIT",
            ModuleType = index == 0 ? "Coupler" : "Servo",
            ModuleState = isOnline ? "Healthy" : "Offline",
            TopologyPath = $"ECAT/{node:00}",
            VendorId = vendorId?.ToString("X8") ?? "Unknown",
            ProductCode = productCode?.ToString("X8") ?? "Unknown",
            FaultText = isOnline ? string.Empty : "Bus node offline",
            IsOnline = isOnline,
            HasAlarm = nodeStatus.HasValue && HasAlarmStatus(nodeStatus.Value),
        };
    }

    private static string DetermineSlaveName(int index, int? vendorId)
    {
        if (index == 0) return "ECAT-Coupler";
        return vendorId switch
        {
            0x00000002 => $"Beckhoff-EL{index:00}",
            0x00000083 => $"Panasonic-A6B-{index:00}",
            _ => $"Servo-{index + 1:00}"
        };
    }

    private int? ReadNodeInfoValue(uint node, uint sel)
    {
        int value = 0;
        var result = axisNativeFacade.GetBusNodeInfo(node, sel, ref value);
        return result == 0 ? value : null;
    }

    private uint? ReadNodeStatusValue(uint node)
    {
        uint status = 0;
        var result = axisNativeFacade.GetBusNodeStatus(node, ref status);
        return result == 0 ? status : null;
    }

    /// <summary>EtherCAT 节点状态位: bit0=INIT, bit1=PREOP, bit2=SAFEOP, bit3=OP, bit4=ERR</summary>
    private static bool IsNodeOperational(uint status) => (status & 0x08) != 0;

    private static bool HasAlarmStatus(uint status) => (status & 0x10) != 0;
}
