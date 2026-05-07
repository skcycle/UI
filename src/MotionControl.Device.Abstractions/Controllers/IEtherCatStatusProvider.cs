using MotionControl.Device.Abstractions.Models;

namespace MotionControl.Device.Abstractions.Controllers;

/// <summary>
/// EtherCAT 状态提供者接口。
/// 用来隔离“真实状态采集”和“占位/mock 状态生成”两类实现，
/// 避免这部分逻辑直接耦合在控制器适配层中。
/// </summary>
public interface IEtherCatStatusProvider
{
    EtherCatControllerStatus CreateStatus(bool isConnected, int axisCount);
}
