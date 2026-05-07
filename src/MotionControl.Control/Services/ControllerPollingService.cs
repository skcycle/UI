using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MotionControl.Control.StateMachines;
using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Device.Abstractions.Results;
using MotionControl.Diagnostics.Services;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Services;

public sealed class ControllerPollingService(
    IEtherCatController motionController,
    Machine machine,
    ControllerRuntimeState controllerRuntimeState,
    AxisPollingService axisPollingService,
    IoPollingService ioPollingService,
    AlarmPollingService alarmPollingService,
    SystemStateMachine systemStateMachine,
    CommandFeedbackRuntimeState commandFeedbackRuntimeState,
    WatchdogService watchdog,
    ILogger<ControllerPollingService>? logger = null)
{
    private readonly ILogger<ControllerPollingService> _logger = logger ?? NullLogger<ControllerPollingService>.Instance;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private readonly object _startStopLock = new();
    private int _reconnectInProgress;
    private bool _isRunning;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_startStopLock)
        {
            if (_isRunning) return;
        }

        _logger.LogInformation("Controller polling starting…");

        var connectingState = systemStateMachine.OnConnectingRequested();
        if (machine.CurrentState != connectingState)
        {
            commandFeedbackRuntimeState.Add(new CommandFeedback { CommandName = "SystemState", Status = "Changed", Message = $"{machine.CurrentState} -> {connectingState}" });
            machine.SetSystemState(connectingState);
        }

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(10));
        var result = DeviceResult.Fail("Connect never attempted");
        for (var attempt = 0; attempt < 3 && !result.Success; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            result = await motionController.ConnectAsync(connectCts.Token);
        }

        lock (_startStopLock)
        {
            if (!result.Success)
            {
                _logger.LogError("Controller connect failed after all retries: {Error}", result.ErrorMessage);
                machine.SetConnected(false);
                watchdog.MarkDisconnected();
                machine.UpsertAlarm("SYS-CONTROLLER-DISCONNECTED", $"Controller connect failed: {result.ErrorMessage}", "System", "Communication", "Error");
                commandFeedbackRuntimeState.Add(new CommandFeedback { CommandName = "Connect", Status = "Failed", Message = result.ErrorMessage ?? "Unknown error" });
                machine.SetSystemState(SystemState.Fault);
                _isRunning = false;
                return;
            }

            _logger.LogInformation("Controller connected successfully");
            machine.SetConnected(true);
            watchdog.MarkConnected();
            _isRunning = true;
            _reconnectInProgress = 0;
        }

        var syncingState = systemStateMachine.OnSyncingRequested();
        if (machine.CurrentState != syncingState)
        {
            commandFeedbackRuntimeState.Add(new CommandFeedback { CommandName = "SystemState", Status = "Changed", Message = $"{machine.CurrentState} -> {syncingState}" });
            machine.SetSystemState(syncingState);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_startStopLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
        }

        _logger.LogInformation("Controller polling stopping");
        await motionController.DisconnectAsync(cancellationToken);
        machine.SetConnected(false);
        watchdog.MarkDisconnected();
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Controller reconnect initiated");
        watchdog.RecordReconnectAttempt();
        await StopAsync(cancellationToken);
        await StartAsync(cancellationToken);
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        lock (_startStopLock)
        {
            if (!_isRunning) return;
        }

        if (!await _pollLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            await axisPollingService.PollAsync(cancellationToken);
            await ioPollingService.PollAsync(cancellationToken);
            var controllerStatus = await motionController.GetControllerStatusAsync(cancellationToken);
            machine.SetConnected(controllerStatus.IsConnected);
            controllerRuntimeState.Update(controllerStatus);
            await alarmPollingService.PollAsync(cancellationToken);

            // Watchdog: 本轮成功
            watchdog.RecordSuccess();

            var previousSystemState = machine.CurrentState;
            var nextSystemState = systemStateMachine.OnPolling(machine, controllerStatus);
            if (previousSystemState != nextSystemState)
            {
                _logger.LogDebug("System state transition: {Previous} -> {Next}", previousSystemState, nextSystemState);
                commandFeedbackRuntimeState.Add(new CommandFeedback
                {
                    CommandName = "SystemState",
                    Status = "Changed",
                    Message = $"{previousSystemState} -> {nextSystemState}"
                });
                machine.SetSystemState(nextSystemState);
            }

            _reconnectInProgress = 0;
        }
        catch (Exception ex)
        {
            // Watchdog: 本轮失败
            watchdog.RecordFailure();
            machine.SetConnected(false);
            controllerRuntimeState.Update(new MotionControl.Device.Abstractions.Models.EtherCatControllerStatus
            {
                IsConnected = false,
                NetworkState = "Disconnected",
                OnlineSlaveCount = 0,
                Slaves = Array.Empty<MotionControl.Device.Abstractions.Models.EtherCatSlaveStatus>(),
            });

            _logger.LogWarning(ex,
                "Poll cycle failed (consecutive failures={WatchdogFailures}, total failures={TotalFailures})",
                watchdog.ConsecutiveFailures, watchdog.TotalFailures);

            // 看门狗驱动的重连判断
            if (watchdog.ShouldAttemptReconnect
                && Interlocked.CompareExchange(ref _reconnectInProgress, 1, 0) == 0)
            {
                var delay = watchdog.CurrentReconnectDelay;
                _logger.LogWarning("Watchdog: scheduling reconnect in {DelayMs}ms (attempt #{Attempt})",
                    (int)delay.TotalMilliseconds, watchdog.ReconnectAttemptCount + 1);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delay);
                        _logger.LogInformation("Watchdog reconnect attempt #{Attempt} starting", watchdog.ReconnectAttemptCount + 1);
                        await ReconnectAsync(CancellationToken.None);
                        _logger.LogInformation("Watchdog reconnect succeeded");
                    }
                    catch (Exception reconnectEx)
                    {
                        _logger.LogError(reconnectEx, "Watchdog reconnect attempt failed");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _reconnectInProgress, 0);
                    }
                });
            }
        }
        finally
        {
            _pollLock.Release();
        }
    }
}
