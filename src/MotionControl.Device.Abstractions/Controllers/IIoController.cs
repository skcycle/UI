using MotionControl.Device.Abstractions.Models;
using MotionControl.Device.Abstractions.Results;

namespace MotionControl.Device.Abstractions.Controllers;

/// <summary>
/// IO 读写能力接口。
/// 只负责 DI/DO 的读取与写入，避免和轴运动控制能力耦合。
/// </summary>
public interface IIoController
{
    Task<bool> GetIoPointValueAsync(int address, bool isOutput, CancellationToken cancellationToken = default);
    Task<DeviceResult> SetIoPointValueAsync(int address, bool value, CancellationToken cancellationToken = default);
    /// <summary>批量读取 IO 点（一个 Task.Run 内完成所有 native 调用）</summary>
    Task<IoPointValue[]> GetIoPointValuesBatchAsync((int address, bool isOutput)[] points, CancellationToken cancellationToken = default);
}
