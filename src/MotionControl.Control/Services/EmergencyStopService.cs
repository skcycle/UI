using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MotionControl.Control.Services;
using MotionControl.Control.StateMachines;
using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Services;

/// <summary>
/// 急停状态机生命周期管理。
///
/// <para><b>触发链路</b></para>
/// Trigger → RapidStop(2) → 逐个轴 Stop + Disable → 设置 EmergencyStop 状态 → 锁定所有操作
///
/// <para><b>解锁链路（需确认 + 等待）</b></para>
/// RequestClear → 记录申请时间 → 操作员确认后才能 Clear → 进入 FaultRecovering
///
/// <para><b>安全约束</b></para>
/// - 触发后最少等待 <see cref="MinimumEmergencyStopDuration"/> 才能申请解锁
/// - 解锁申请后 15 秒内未确认则申请过期
/// - 解锁前提：报警已清除且系统不再处于 Fault 状态
/// </summary>
public sealed class EmergencyStopService
{
    /// <summary>急停后至少等待此时间才允许解锁申请</summary>
    public static readonly TimeSpan MinimumEmergencyStopDuration = TimeSpan.FromSeconds(3);

    /// <summary>解锁申请有效窗口（超时需重新申请）</summary>
    public static readonly TimeSpan ClearRequestTimeout = TimeSpan.FromSeconds(15);

    private readonly SystemStateMachine _systemStateMachine;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private readonly ILogger<EmergencyStopService> _logger;

    private readonly object _lock = new();
    private bool _isEmergencyStopped;
    private DateTimeOffset _emergencyStopTime;
    private DateTimeOffset _clearRequestTime;
    private bool _clearRequested;
    private string _emergencyStopReason = string.Empty;
    private string _clearOperator = string.Empty;

    public EmergencyStopService(
        SystemStateMachine systemStateMachine,
        CommandFeedbackRuntimeState commandFeedbackRuntimeState,
        ILogger<EmergencyStopService>? logger = null)
    {
        _systemStateMachine = systemStateMachine;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        _logger = logger ?? NullLogger<EmergencyStopService>.Instance;
    }

    // ── 查询状态 ──

    public bool IsEmergencyStopped
    {
        get { lock (_lock) return _isEmergencyStopped; }
    }

    public DateTimeOffset? EmergencyStopTime
    {
        get { lock (_lock) return _isEmergencyStopped ? _emergencyStopTime : null; }
    }

    public string EmergencyStopReason
    {
        get { lock (_lock) return _isEmergencyStopped ? _emergencyStopReason : string.Empty; }
    }

    public bool IsClearRequested
    {
        get { lock (_lock) return _clearRequested; }
    }

    public bool HasClearRequestExpired
    {
        get
        {
            lock (_lock)
            {
                return _clearRequested && (DateTimeOffset.UtcNow - _clearRequestTime) > ClearRequestTimeout;
            }
        }
    }

    // ── 急停触发 ──

    /// <summary>
    /// 触发急停。
    /// 调用方负责先执行 RapidStop(2) 和轴级停止。
    /// 此方法设置状态机并锁定系统。
    /// </summary>
    public EmergencyStopResult Trigger(Machine machine, string reason = "Operator initiated emergency stop")
    {
        lock (_lock)
        {
            if (_isEmergencyStopped)
            {
                _logger.LogWarning("E-STOP already active, re-triggering");
            }

            _isEmergencyStopped = true;
            _emergencyStopTime = DateTimeOffset.UtcNow;
            _emergencyStopReason = reason;
            _clearRequested = false;
            _clearOperator = string.Empty;

            // 标记所有轴为报警状态
            foreach (var axis in machine.Axes)
            {
                axis.SetAlarm();
            }

            // 状态机转换
            var nextState = _systemStateMachine.OnEmergencyStopRequested();
            machine.SetSystemState(nextState);

            _commandFeedbackRuntimeState.Add(new CommandFeedback
            {
                CommandName = "EmergencyStop",
                Status = "Triggered",
                Message = reason
            });

            _logger.LogCritical("E-STOP triggered: {Reason} at {Time}", reason, _emergencyStopTime);

            return new EmergencyStopResult
            {
                IsActive = true,
                TriggerTime = _emergencyStopTime,
                Reason = reason,
                IsClearAllowed = false
            };
        }
    }

    // ── 两步解锁 ──

    /// <summary>
    /// 第一步：操作员申请解锁急停。
    /// 必须等待最小持续时间、确认报警已清除后才能申请。
    /// </summary>
    public EmergencyStopClearRequestResult RequestClear(Machine machine, string operatorName)
    {
        lock (_lock)
        {
            if (!_isEmergencyStopped)
            {
                _logger.LogInformation("E-STOP clear request skipped: not currently in emergency stop");
                return new EmergencyStopClearRequestResult
                {
                    Success = false,
                    Message = "System is not in emergency stop state",
                    CanRetry = false
                };
            }

            var elapsed = DateTimeOffset.UtcNow - _emergencyStopTime;
            if (elapsed < MinimumEmergencyStopDuration)
            {
                var remaining = MinimumEmergencyStopDuration - elapsed;
                _logger.LogWarning("E-STOP clear request denied: minimum wait not met ({Remaining:g} remaining)", remaining);
                return new EmergencyStopClearRequestResult
                {
                    Success = false,
                    Message = $"Emergency stop minimum duration not met. Please wait {remaining.TotalSeconds:F0} seconds.",
                    WaitRemaining = remaining,
                    CanRetry = true
                };
            }

            // 检查是否还有活动报警
            var hasActiveAlarms = machine.Alarms.Any(a => a.IsActive);
            if (hasActiveAlarms)
            {
                _logger.LogWarning("E-STOP clear request denied: active alarms exist");
                return new EmergencyStopClearRequestResult
                {
                    Success = false,
                    Message = "Cannot clear emergency stop while alarms are active. Clear alarms first.",
                    CanRetry = true
                };
            }

            _clearRequested = true;
            _clearRequestTime = DateTimeOffset.UtcNow;
            _clearOperator = operatorName;

            _commandFeedbackRuntimeState.Add(new CommandFeedback
            {
                CommandName = "EmergencyStopClear",
                Status = "Requested",
                Message = $"Clear requested by {operatorName} at {_clearRequestTime:O}"
            });

            _logger.LogInformation("E-STOP clear requested by {Operator} at {Time}", operatorName, _clearRequestTime);

            return new EmergencyStopClearRequestResult
            {
                Success = true,
                Message = "Emergency stop clear requested. Operator confirmation required.",
                RequestTime = _clearRequestTime,
                Timeout = ClearRequestTimeout,
                CanRetry = false
            };
        }
    }

    /// <summary>
    /// 第二步：确认并执行急停解锁。
    /// 必须上次申请未过期、操作员匹配才能执行。
    /// </summary>
    public EmergencyStopClearResult Clear(Machine machine, string operatorName)
    {
        lock (_lock)
        {
            if (!_isEmergencyStopped)
            {
                return new EmergencyStopClearResult
                {
                    Success = false,
                    Message = "System is not in emergency stop state"
                };
            }

            if (!_clearRequested)
            {
                return new EmergencyStopClearResult
                {
                    Success = false,
                    Message = "No clear request has been made. Call RequestClear first."
                };
            }

            if (HasClearRequestExpired)
            {
                _clearRequested = false;
                _clearOperator = string.Empty;
                return new EmergencyStopClearResult
                {
                    Success = false,
                    Message = "Clear request has expired. Please request again."
                };
            }

            if (!string.Equals(operatorName, _clearOperator, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("E-STOP clear denied: operator mismatch (requested by {RequestedBy}, cleared by {ClearedBy})",
                    _clearOperator, operatorName);
                return new EmergencyStopClearResult
                {
                    Success = false,
                    Message = $"Clear must be confirmed by the same operator who requested it ({_clearOperator})."
                };
            }

            // 执行解锁
            _isEmergencyStopped = false;
            _clearRequested = false;

            // 清除所有轴的报警标记（驱动报警需由 ResetAlarm 单独清除）
            foreach (var axis in machine.Axes)
            {
                axis.ClearAlarm();
            }

            machine.SetSystemState(_systemStateMachine.OnEmergencyStopCleared(machine, null));

            _commandFeedbackRuntimeState.Add(new CommandFeedback
            {
                CommandName = "EmergencyStopClear",
                Status = "Cleared",
                Message = $"Emergency stop cleared by {operatorName} at {DateTimeOffset.UtcNow:O}. " +
                          $"E-STOP was active for {(DateTimeOffset.UtcNow - _emergencyStopTime).TotalSeconds:F1}s"
            });

            _logger.LogWarning("E-STOP cleared by {Operator}. Was active for {Duration:F1}s",
                operatorName, (DateTimeOffset.UtcNow - _emergencyStopTime).TotalSeconds);

            return new EmergencyStopClearResult
            {
                Success = true,
                Message = "Emergency stop cleared. System entering fault recovery.",
                EmergencyStopDuration = DateTimeOffset.UtcNow - _emergencyStopTime,
                NextState = SystemState.FaultRecovering
            };
        }
    }

    // ── 强制重置（仅用于系统重启/初始化） ──

    /// <summary>
    /// 强制重置 E-STOP 状态（仅在系统启动/完全重启时使用）。
    /// 绕过确认流程，不应用于生产操作。
    /// </summary>
    public void ForceReset(Machine machine, string reason)
    {
        lock (_lock)
        {
            _isEmergencyStopped = false;
            _clearRequested = false;
            _clearOperator = string.Empty;

            foreach (var axis in machine.Axes)
            {
                axis.ClearAlarm();
            }

            _logger.LogWarning("E-STOP force-reset: {Reason}", reason);

            _commandFeedbackRuntimeState.Add(new CommandFeedback
            {
                CommandName = "EmergencyStop",
                Status = "ForceReset",
                Message = reason
            });
        }
    }
}

// ── 结果类型 ──

public sealed class EmergencyStopResult
{
    public bool IsActive { get; init; }
    public DateTimeOffset TriggerTime { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IsClearAllowed { get; init; }
}

public sealed class EmergencyStopClearRequestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan? WaitRemaining { get; init; }
    public DateTimeOffset? RequestTime { get; init; }
    public TimeSpan? Timeout { get; init; }
    public bool CanRetry { get; init; }
}

public sealed class EmergencyStopClearResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan? EmergencyStopDuration { get; init; }
    public SystemState? NextState { get; init; }
}
