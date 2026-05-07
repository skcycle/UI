using MotionControl.Diagnostics.Services;
using MotionControl.Control.Services;

namespace MotionControl.Application.Interfaces;

public interface ISystemAppService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>触发急停。立即停止所有轴并锁定系统。</summary>
    Task EmergencyStopAsync(string reason = "Operator initiated emergency stop", CancellationToken cancellationToken = default);

    /// <summary>第一步：申请解锁急停（需要满足最小持续时间且无活动报警）</summary>
    Task<EmergencyStopClearRequestResult> RequestClearEmergencyStopAsync(string operatorName, CancellationToken cancellationToken = default);

    /// <summary>第二步：确认并执行急停解锁。必须在申请有效期内由同一操作员确认。</summary>
    Task<EmergencyStopClearResult> ClearEmergencyStopAsync(string operatorName, CancellationToken cancellationToken = default);

    Task ReconnectAsync(CancellationToken cancellationToken = default);

    bool IsEmergencyStopped { get; }

    EmergencyStopService GetEmergencyStopService();
    WatchdogService GetWatchdogService();
}
