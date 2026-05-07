using MotionControl.Application.Interfaces;
using MotionControl.Control.Interfaces;
using MotionControl.Control.Services;
using MotionControl.Control.StateMachines;
using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Diagnostics.Services;
using MotionControl.Domain.Entities;

namespace MotionControl.Application.Services;

public sealed class SystemAppService(
    Machine machine,
    ControllerPollingService controllerPollingService,
    SystemStateMachine systemStateMachine,
    IAxisControlService axisControlService,
    ISafetyController motionController,
    EmergencyStopService emergencyStopService,
    WatchdogService watchdog,
    FaultRecoveryService faultRecoveryService) : ISystemAppService
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        machine.SetSystemState(systemStateMachine.OnInitializeRequested());
        watchdog.Reset();
        emergencyStopService.ForceReset(machine, "System initialization");
        await controllerPollingService.StartAsync(cancellationToken);
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return controllerPollingService.PollOnceAsync(cancellationToken);
    }

    // ── 急停 ──

    public async Task EmergencyStopAsync(string reason = "Operator initiated emergency stop", CancellationToken cancellationToken = default)
    {
        // 1. 硬件急停：RapidStop(2) — 2=最快减速停止
        var rapidStopResult = await motionController.RapidStopAsync(2, cancellationToken);

        // 2. 逐轴停止（如果 RapidStop 失败或作为补充）
        if (!rapidStopResult.Success)
        {
            foreach (var axis in machine.Axes)
            {
                try
                {
                    await axisControlService.StopAsync(axis, cancellationToken);
                }
                catch
                {
                    // 急停期间尽力而为，不因单个轴失败中断后续停止
                }
            }
        }

        // 3. 逐轴失能（通过 AxisControlService，Stop/Disable 在 E-STOP 期间始终允许）
        foreach (var axis in machine.Axes)
        {
            try
            {
                await axisControlService.DisableAxisAsync(axis, cancellationToken);
            }
            catch
            {
                // 急停期间尽力而为
            }
        }

        // 4. 触发 E-STOP 状态机
        emergencyStopService.Trigger(machine, reason);

        // 5. 记录系统报警
        machine.UpsertAlarm(
            "SYS-EMERGENCY-STOP",
            reason,
            "System",
            "Safety",
            "Critical");

        // 6. 立即刷新一次状态
        await controllerPollingService.PollOnceAsync(cancellationToken);
    }

    public async Task<EmergencyStopClearRequestResult> RequestClearEmergencyStopAsync(string operatorName, CancellationToken cancellationToken = default)
    {
        var result = emergencyStopService.RequestClear(machine, operatorName);
        await controllerPollingService.PollOnceAsync(cancellationToken);
        return result;
    }

    public async Task<EmergencyStopClearResult> ClearEmergencyStopAsync(string operatorName, CancellationToken cancellationToken = default)
    {
        var result = emergencyStopService.Clear(machine, operatorName);

        if (result.Success)
        {
            // 清除系统报警
            machine.ClearAlarm("SYS-EMERGENCY-STOP");

            // 进入故障恢复流程
            faultRecoveryService.BeginRecovery(machine);
            machine.SetSystemState(result.NextState!.Value);

            await controllerPollingService.PollOnceAsync(cancellationToken);
        }

        return result;
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await controllerPollingService.ReconnectAsync(cancellationToken);
    }

    // ── 状态查询 ──

    public bool IsEmergencyStopped => emergencyStopService.IsEmergencyStopped;

    public EmergencyStopService GetEmergencyStopService() => emergencyStopService;

    public WatchdogService GetWatchdogService() => watchdog;
}
