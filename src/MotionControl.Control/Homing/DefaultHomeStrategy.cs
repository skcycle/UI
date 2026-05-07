using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Homing;

public sealed class DefaultHomeStrategy : IHomeStrategy
{
    public HomeMode HomeMode => HomeMode.Default;

    public HomeExecutionPlan BuildPlan(Axis axis) => new()
    {
        Title = "Default Homing",
        Steps = new[] { $"Axis {axis.ControllerAxisNo}: reset homed state", "Use default homing behavior placeholder" }
    };

    public Task ExecuteAsync(Axis axis, CancellationToken cancellationToken = default)
    {
        axis.ClearHomed();
        return Task.CompletedTask;
    }
}
