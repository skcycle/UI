using MotionControl.Device.Abstractions.Models;
using MotionControl.Device.Abstractions.Results;

namespace MotionControl.Device.Abstractions.Controllers;

/// <summary>
/// EtherCAT 总线状态能力接口。
/// 负责连接、断开和控制器/从站状态读取，
/// 不承担轴运动控制或 IO 读写职责。
/// </summary>
public interface IEtherCatController
{
    Task<DeviceResult> ConnectAsync(CancellationToken cancellationToken = default);
    Task<DeviceResult> DisconnectAsync(CancellationToken cancellationToken = default);
    Task<EtherCatControllerStatus> GetControllerStatusAsync(CancellationToken cancellationToken = default);
}
