using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Services;

/// <summary>
/// 通信看门狗服务。
/// 跟踪控制器连接健康状态，断开时冻结所有运动指令下发。
///
/// <para><b>行为</b></para>
/// - 连续成功 N 次轮询 → 恢复健康状态
/// - 连续失败 M 次 → 标记通信丢失 → 禁止运动指令
/// - 提供指数退避重连时间建议
///
/// <para><b>指令冻结</b></para>
/// 当 <see cref="IsConnectionHealthy"/> 为 false 时，
/// 所有运动指令（Enable/Disable/Move/Jog/Home）必须被上层拦截。
/// Stop 和 E-STOP 不受影响——断开时也要尽力停止。
/// </summary>
public sealed class WatchdogService
{
    /// <summary>连续失败 N 次后标记为通信丢失</summary>
    private const int ConnectionLostThreshold = 3;

    /// <summary>连续成功 N 次后恢复健康</summary>
    private const int ConnectionRecoveredThreshold = 2;

    /// <summary>指数退避步进表（索引 = 重连尝试次数，0-based）</summary>
    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromSeconds(1),   // 第 1 次重连: 1s
        TimeSpan.FromSeconds(3),   // 第 2 次重连: 3s
        TimeSpan.FromSeconds(10),  // 第 3 次重连: 10s
        TimeSpan.FromSeconds(30),  // 第 4 次重连: 30s
        TimeSpan.FromSeconds(60),  // 第 5 次及以后: 60s
    };

    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private readonly ILogger<WatchdogService> _logger;

    private readonly object _lock = new();
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private bool _isConnectionHealthy;
    private DateTimeOffset _lastSuccessTime;
    private DateTimeOffset _lastFailureTime;
    private int _reconnectAttemptCount;
    private int _totalFailures;
    private int _totalRecoveries;

    public WatchdogService(
        CommandFeedbackRuntimeState commandFeedbackRuntimeState,
        ILogger<WatchdogService>? logger = null)
    {
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        _logger = logger ?? NullLogger<WatchdogService>.Instance;
        _isConnectionHealthy = false; // 初始状态：未连接，不健康
        _lastSuccessTime = DateTimeOffset.MinValue;
        _lastFailureTime = DateTimeOffset.MinValue;
    }

    // ── 查询状态 ──

    /// <summary>通信是否健康（可安全下发运动指令）</summary>
    public bool IsConnectionHealthy
    {
        get { lock (_lock) return _isConnectionHealthy; }
    }

    public int ConsecutiveFailures
    {
        get { lock (_lock) return _consecutiveFailures; }
    }

    public int ConsecutiveSuccesses
    {
        get { lock (_lock) return _consecutiveSuccesses; }
    }

    public int ReconnectAttemptCount
    {
        get { lock (_lock) return _reconnectAttemptCount; }
    }

    public int TotalFailures
    {
        get { lock (_lock) return _totalFailures; }
    }

    public int TotalRecoveries
    {
        get { lock (_lock) return _totalRecoveries; }
    }

    public DateTimeOffset LastSuccessTime
    {
        get { lock (_lock) return _lastSuccessTime; }
    }

    public DateTimeOffset LastFailureTime
    {
        get { lock (_lock) return _lastFailureTime; }
    }

    /// <summary>距离上次成功通信的时长。用于判断是否超过硬超时。</summary>
    public TimeSpan TimeSinceLastSuccess => DateTimeOffset.UtcNow - _lastSuccessTime;

    /// <summary>当前推荐的重连等待时间</summary>
    public TimeSpan CurrentReconnectDelay
    {
        get
        {
            lock (_lock)
            {
                var index = Math.Min(_reconnectAttemptCount, BackoffSchedule.Length - 1);
                return BackoffSchedule[index];
            }
        }
    }

    /// <summary>是否应该尝试重连</summary>
    public bool ShouldAttemptReconnect
    {
        get
        {
            lock (_lock)
            {
                return !_isConnectionHealthy && _consecutiveFailures >= ConnectionLostThreshold;
            }
        }
    }

    // ── 事件记录 ──

    /// <summary>记录一次成功的轮询</summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _lastSuccessTime = DateTimeOffset.UtcNow;
            _consecutiveSuccesses++;
            _consecutiveFailures = 0;
            _reconnectAttemptCount = 0;

            // 联系成功恢复阈值后标记健康
            if (!_isConnectionHealthy && _consecutiveSuccesses >= ConnectionRecoveredThreshold)
            {
                _isConnectionHealthy = true;
                _totalRecoveries++;
                _logger.LogInformation("Watchdog: connection recovered (total recoveries={TotalRecoveries})", _totalRecoveries);

                _commandFeedbackRuntimeState.Add(new CommandFeedback
                {
                    CommandName = "Watchdog",
                    Status = "Recovered",
                    Message = $"Connection restored after {_totalFailures} total failures, {_totalRecoveries} recoveries"
                });
            }
        }
    }

    /// <summary>记录一次失败的轮询（通信异常）</summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;
            _consecutiveFailures++;
            _totalFailures++;
            _consecutiveSuccesses = 0;

            // 首次达到丢失阈值
            if (_isConnectionHealthy && _consecutiveFailures >= ConnectionLostThreshold)
            {
                _isConnectionHealthy = false;
                _logger.LogWarning("Watchdog: connection lost (consecutive failures={Failures})", _consecutiveFailures);

                _commandFeedbackRuntimeState.Add(new CommandFeedback
                {
                    CommandName = "Watchdog",
                    Status = "Lost",
                    Message = $"Connection lost after {_consecutiveFailures} consecutive failures"
                });
            }
        }
    }

    /// <summary>记录一次重连尝试（不改变健康状态，仅递增尝试计数）</summary>
    public void RecordReconnectAttempt()
    {
        lock (_lock)
        {
            _reconnectAttemptCount++;
            _logger.LogDebug("Watchdog: reconnect attempt #{Attempt} scheduled", _reconnectAttemptCount);
        }
    }

    /// <summary>标记连接已建立（Connect 成功后调用）</summary>
    public void MarkConnected()
    {
        lock (_lock)
        {
            _isConnectionHealthy = true;
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 1;
            _reconnectAttemptCount = 0;
            _lastSuccessTime = DateTimeOffset.UtcNow;
            _logger.LogInformation("Watchdog: connection established");
        }
    }

    /// <summary>标记连接已断开（Disconnect 后调用）</summary>
    public void MarkDisconnected()
    {
        lock (_lock)
        {
            _isConnectionHealthy = false;
            _consecutiveSuccesses = 0;
            _logger.LogInformation("Watchdog: connection marked disconnected");
        }
    }

    /// <summary>完全重置（系统退出/重启时使用）</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 0;
            _isConnectionHealthy = false;
            _reconnectAttemptCount = 0;
            _logger.LogInformation("Watchdog: fully reset");
        }
    }

    /// <summary>
    /// 检查运动指令是否被允许下发。
    /// 通信不健康 + 系统不在急停状态 → 拒绝。
    /// Stop 和 E-STOP 始终允许（尽力而为）。
    /// </summary>
    public bool IsCommandAllowed(Machine machine)
    {
        // 急停状态下 Stop/E-STOP 必须允许，由上层处理
        if (machine.CurrentState == SystemState.EmergencyStop)
            return true;

        return IsConnectionHealthy;
    }

    // ── 诊断信息 ──

    /// <summary>获取当前看门狗状态的摘要信息</summary>
    public WatchdogStatusSnapshot GetStatusSnapshot()
    {
        lock (_lock)
        {
            return new WatchdogStatusSnapshot
            {
                IsHealthy = _isConnectionHealthy,
                ConsecutiveFailures = _consecutiveFailures,
                ConsecutiveSuccesses = _consecutiveSuccesses,
                TotalFailures = _totalFailures,
                TotalRecoveries = _totalRecoveries,
                ReconnectAttemptCount = _reconnectAttemptCount,
                LastSuccessTime = _lastSuccessTime,
                LastFailureTime = _lastFailureTime,
                TimeSinceLastSuccess = _lastSuccessTime == DateTimeOffset.MinValue
                    ? TimeSpan.MaxValue
                    : DateTimeOffset.UtcNow - _lastSuccessTime
            };
        }
    }
}

/// <summary>看门狗状态快照（线程安全的瞬时拷贝）</summary>
public sealed class WatchdogStatusSnapshot
{
    public bool IsHealthy { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int ConsecutiveSuccesses { get; init; }
    public int TotalFailures { get; init; }
    public int TotalRecoveries { get; init; }
    public int ReconnectAttemptCount { get; init; }
    public DateTimeOffset LastSuccessTime { get; init; }
    public DateTimeOffset LastFailureTime { get; init; }
    public TimeSpan TimeSinceLastSuccess { get; init; }
}
