using MotionControl.Device.Abstractions.Models;
using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.StateMachines;

public sealed class SystemStateMachine
{
    public SystemState OnInitializeRequested() => SystemState.Initializing;

    public SystemState OnConnectingRequested() => SystemState.Connecting;

    public SystemState OnSyncingRequested() => SystemState.Syncing;

    public SystemState OnRecoveryStarted() => SystemState.FaultRecovering;

    public SystemState OnEmergencyStopRequested() => SystemState.EmergencyStop;

    /// <summary>
    /// E-STOP 清除后的状态转换。
    /// 传入 null 表示无法获取控制器状态（此时进入 FaultRecovering）。
    /// 传入有效状态：根据当前连接和报警情况判断。
    /// </summary>
    public SystemState OnEmergencyStopCleared(Machine machine, EtherCatControllerStatus? controllerStatus)
    {
        if (controllerStatus is null || !controllerStatus.IsConnected)
        {
            return SystemState.FaultRecovering;
        }

        // E-STOP 清除后重新评估系统状态
        var hasAxisAlarm = machine.Axes.Any(axis => axis.HasAlarm);
        var hasSystemAlarm = machine.Alarms.Any(alarm => alarm.IsActive && alarm.Code != "SYS-EMERGENCY-STOP");

        if (hasAxisAlarm || hasSystemAlarm)
        {
            return SystemState.Alarm;
        }

        if (!controllerStatus.IsOperational)
        {
            return SystemState.Syncing;
        }

        return SystemState.Standby;
    }

    public SystemState OnRecoveryCompleted(Machine machine, EtherCatControllerStatus? controllerStatus)
    {
        if (controllerStatus is null)
        {
            return SystemState.FaultRecovering;
        }

        return OnPolling(machine, controllerStatus);
    }

    public SystemState OnPolling(Machine machine, EtherCatControllerStatus controllerStatus)
        => GetNextState(machine, controllerStatus);

    public SystemState GetNextState(Machine machine, EtherCatControllerStatus controllerStatus)
    {
        // E-STOP 状态在所有检查之前判断——进入 E-STOP 后只能手动清除，不能被自动状态机覆盖
        if (machine.CurrentState == SystemState.EmergencyStop)
        {
            return SystemState.EmergencyStop;
        }

        var hasAxisAlarm = machine.Axes.Any(axis => axis.HasAlarm);
        var hasSystemAlarm = machine.Alarms.Any(alarm => alarm.IsActive);
        var hasSlaveAlarm = controllerStatus.HasAnySlaveAlarm;
        var hasAnyAlarm = hasAxisAlarm || hasSystemAlarm || hasSlaveAlarm;
        var anyAxisHoming = machine.Axes.Any(axis => axis.State == AxisState.Homing);
        var anyAxisMoving = machine.Axes.Any(axis => axis.State == AxisState.Moving);
        var anyServoOn = machine.Axes.Any(axis => axis.ServoState == ServoState.On);

        if (machine.CurrentState == SystemState.FaultRecovering)
        {
            if (!controllerStatus.IsConnected)
            {
                return SystemState.FaultRecovering;
            }

            return hasAnyAlarm ? SystemState.FaultRecovering : SystemState.Standby;
        }

        if (!controllerStatus.IsConnected)
        {
            return machine.CurrentState is SystemState.Initializing or SystemState.Connecting
                ? SystemState.Connecting
                : SystemState.Fault;
        }

        if (!controllerStatus.IsOperational)
        {
            return SystemState.Syncing;
        }

        if (hasAnyAlarm)
        {
            return SystemState.Alarm;
        }

        if (controllerStatus.HasOfflineSlave)
        {
            return SystemState.Warning;
        }

        if (anyAxisHoming || anyAxisMoving)
        {
            return SystemState.Manual;
        }

        if (anyServoOn)
        {
            return SystemState.Ready;
        }

        return SystemState.Standby;
    }
}
