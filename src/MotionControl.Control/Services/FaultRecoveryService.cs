using MotionControl.Control.StateMachines;
using MotionControl.Diagnostics.Services;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;

namespace MotionControl.Control.Services;

public sealed class FaultRecoveryService(
    CommandFeedbackRuntimeState commandFeedbackRuntimeState,
    ControllerRuntimeState controllerRuntimeState,
    SystemStateMachine systemStateMachine,
    WatchdogService watchdog)
{
    public void BeginRecovery(Machine machine)
    {
        machine.SetSystemState(systemStateMachine.OnRecoveryStarted());
        commandFeedbackRuntimeState.Add(new CommandFeedback
        {
            CommandName = "FaultRecovery",
            Status = "Running",
            Message = "System entered fault recovery state"
        });
    }

    public void CompleteRecovery(Machine machine)
    {
        var nextState = systemStateMachine.OnRecoveryCompleted(machine, controllerRuntimeState.LastControllerStatus);
        machine.SetSystemState(nextState);

        var status = nextState == MotionControl.Domain.Enums.SystemState.FaultRecovering
            ? "Blocked"
            : "Succeeded";

        var message = nextState == MotionControl.Domain.Enums.SystemState.FaultRecovering
            ? "Fault recovery cannot complete while controller/alarm conditions remain active. " +
              $"Watchdog status: healthy={watchdog.IsConnectionHealthy}, failures={watchdog.ConsecutiveFailures}"
            : "System fault recovery completed";

        commandFeedbackRuntimeState.Add(new CommandFeedback
        {
            CommandName = "FaultRecovery",
            Status = status,
            Message = message
        });
    }
}
