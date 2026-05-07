using MotionControl.Device.Abstractions.Results;

namespace MotionControl.Device.Abstractions.Controllers;

/// <summary>
/// 安全控制能力接口。
/// 当前主要承担急停类能力，后续可扩展真实安全链路相关实现。
/// </summary>
public interface ISafetyController
{
    Task<DeviceResult> RapidStopAsync(int mode, CancellationToken cancellationToken = default);
}
