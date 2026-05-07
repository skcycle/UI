using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Diagnostics.Services;

public sealed class SafetyInterlockService
{
    // ── 轴级安全联锁 ──

    /// <summary>轴有报警时禁止使能。</summary>
    public bool CanEnableAxis(Axis axis) => !axis.HasAlarm;

    /// <summary>轴有报警或未完成回零时禁止运动。</summary>
    public bool CanStartMove(Axis axis) => !axis.HasAlarm && axis.IsHomed;

    /// <summary>轴有报警时禁止回零。</summary>
    public bool CanStartHome(Axis axis) => !axis.HasAlarm;

    // ── 系统级安全联锁 ──

    /// <summary>
    /// 检查系统状态是否允许执行一般操作（Enable/Disable/Move/Jog/Home）。
    /// 急停、故障、故障恢复、未连接 状态下拒绝。
    /// </summary>
    public bool IsSystemOperationAllowed(Machine machine)
    {
        if (machine.CurrentState == SystemState.EmergencyStop) return false;
        if (machine.CurrentState == SystemState.Fault) return false;
        if (machine.CurrentState == SystemState.FaultRecovering) return false;
        if (!machine.IsConnected) return false;
        return true;
    }

    /// <summary>
    /// 检查是否允许对指定轴执行运动操作。
    /// 同时检查系统状态和轴级条件。
    /// </summary>
    public OperationPermissionResult CheckOperationPermission(Axis axis, Machine machine, string operation)
    {
        // 第一层：系统级检查
        if (!IsSystemOperationAllowed(machine))
        {
            if (machine.CurrentState == SystemState.EmergencyStop)
                return OperationPermissionResult.Denied("System is in emergency stop");

            if (machine.CurrentState == SystemState.Fault)
                return OperationPermissionResult.Denied("System is in fault state");

            if (machine.CurrentState == SystemState.FaultRecovering)
                return OperationPermissionResult.Denied("System is in fault recovery");

            if (!machine.IsConnected)
                return OperationPermissionResult.Denied("Controller not connected — commands are frozen");
        }

        // 第二层：轴级检查
        return operation switch
        {
            "Enable" when !CanEnableAxis(axis) =>
                OperationPermissionResult.Denied($"Axis {axis.Name} has active alarm, cannot enable"),

            "Move" or "Jog" when !CanStartMove(axis) =>
                OperationPermissionResult.Denied(axis.HasAlarm
                    ? $"Axis {axis.Name} has active alarm, cannot move"
                    : $"Axis {axis.Name} is not homed, cannot move"),

            "Home" when !CanStartHome(axis) =>
                OperationPermissionResult.Denied($"Axis {axis.Name} has active alarm, cannot home"),

            _ => OperationPermissionResult.Allowed()
        };
    }
}

/// <summary>操作权限检查结果</summary>
public sealed class OperationPermissionResult
{
    public bool IsAllowed { get; init; }
    public string DenialReason { get; init; } = string.Empty;

    public static OperationPermissionResult Allowed() => new() { IsAllowed = true };

    public static OperationPermissionResult Denied(string reason) => new() { IsAllowed = false, DenialReason = reason };
}
