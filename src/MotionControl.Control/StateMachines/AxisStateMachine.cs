using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.StateMachines;

public sealed class AxisStateMachine
{
    public AxisState GetNextState(Axis axis)
    {
        if (axis.HasAlarm)
        {
            return AxisState.Alarm;
        }

        if (axis.ServoState == ServoState.Off)
        {
            return AxisState.Disabled;
        }

        if (axis.State == AxisState.Homing && !axis.IsHomed)
        {
            return AxisState.Homing;
        }

        if (axis.State == AxisState.Stopping && Math.Abs(axis.CurrentVelocity) > 0.001d)
        {
            return AxisState.Stopping;
        }

        if (Math.Abs(axis.CurrentVelocity) > 0.001d)
        {
            return axis.MotionMode == MotionMode.Jog ? AxisState.Jogging : AxisState.Moving;
        }

        return AxisState.Standstill;
    }

    public AxisState OnEnableSucceeded(Axis axis) => GetNextState(axis);

    public AxisState OnDisableSucceeded() => AxisState.Disabled;

    public AxisState OnHomeIssued() => AxisState.Homing;

    public AxisState OnHomeSucceeded(Axis axis) => GetNextState(axis);

    public AxisState OnMoveIssued() => AxisState.Moving;

    public AxisState OnJogIssued() => AxisState.Jogging;

    public AxisState OnStopIssued() => AxisState.Stopping;

    public AxisState OnStopSucceeded(Axis axis) => GetNextState(axis);

    public AxisState OnAlarmResetSucceeded(Axis axis) => GetNextState(axis);
}
