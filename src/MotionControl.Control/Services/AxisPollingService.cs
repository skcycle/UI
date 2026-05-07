using System.Linq;
using MotionControl.Control.StateMachines;
using MotionControl.Device.Abstractions.Controllers;
using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;
using MotionControl.Device.Abstractions.Models;

namespace MotionControl.Control.Services;

public sealed class AxisPollingService(
    IAxisMotionController motionController,
    Machine machine,
    AxisStateMachine axisStateMachine,
    CommandFeedbackRuntimeState commandFeedbackRuntimeState)
{
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        var axisNos = machine.Axes.Select(a => a.ControllerAxisNo).ToArray();
        if (axisNos.Length == 0) return;

        // 一次批量读所有轴反馈（单个 Task.Run，同步循环内完成）
        AxisFeedback[] feedbacks;
        try
        {
            feedbacks = await motionController.GetAxisFeedbacksBatchAsync(axisNos, cancellationToken);
        }
        catch
        {
            return; // 批量读取失败，本轮跳过
        }

        var axesList = machine.Axes.ToList();
        for (int i = 0; i < axesList.Count && i < feedbacks.Length; i++)
        {
            var axis = axesList[i];
            var feedback = feedbacks[i];

            if (!feedback.IsValid)
                continue;

            axis.UpdateFeedback(
                feedback.CommandPosition,
                feedback.EncoderPosition,
                feedback.CurrentVelocity,
                feedback.AxisState,
                feedback.ServoState,
                feedback.HasAlarm,
                feedback.IsHomed,
                feedback.PositiveHardLimitTriggered,
                feedback.NegativeHardLimitTriggered,
                feedback.PositiveSoftLimitTriggered,
                feedback.NegativeSoftLimitTriggered);

            var previousState = axis.State;
            var nextState = axisStateMachine.GetNextState(axis);
            if (previousState != nextState)
            {
                axis.ApplyState(nextState);

                var shouldRecordRuntimeEvent = !(previousState is AxisState.Standstill && nextState is AxisState.Moving)
                    && !(previousState is AxisState.Moving && nextState is AxisState.Standstill)
                    && !(previousState is AxisState.Standstill && nextState is AxisState.Disabled)
                    && !(previousState is AxisState.Disabled && nextState is AxisState.Standstill);

                if (shouldRecordRuntimeEvent)
                {
                    commandFeedbackRuntimeState.Add(new CommandFeedback
                    {
                        CommandName = "AxisState",
                        AxisNo = axis.ControllerAxisNo,
                        Status = "Changed",
                        Message = $"{previousState} -> {nextState}"
                    });
                }
            }
        }
    }
}
