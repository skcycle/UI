using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Homing;

public sealed class SlaveFollowMasterHomeStrategy : IHomeStrategy
{
    public HomeMode HomeMode => HomeMode.SlaveFollowMaster;

    public HomeExecutionPlan BuildPlan(Axis axis) => new()
    {
        Title = "Slave Follow Master",
        Steps = new[]
        {
            $"Axis {axis.ControllerAxisNo}: wait for master axis homing completion",
            "Apply master/slave alignment offset",
            "Mark slave axis homed"
        }
    };

    public Task ExecuteAsync(Axis axis, CancellationToken cancellationToken = default)
    {
        axis.MarkHomed();
        return Task.CompletedTask;
    }
}
