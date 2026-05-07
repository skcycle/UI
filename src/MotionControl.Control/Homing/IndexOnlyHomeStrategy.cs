using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Homing;

public sealed class IndexOnlyHomeStrategy : IHomeStrategy
{
    public HomeMode HomeMode => HomeMode.IndexOnly;

    public HomeExecutionPlan BuildPlan(Axis axis) => new()
    {
        Title = "Index Only",
        Steps = new[]
        {
            $"Axis {axis.ControllerAxisNo}: search index pulse only",
            "Latch encoder index as home",
            "Mark axis homed"
        }
    };

    public Task ExecuteAsync(Axis axis, CancellationToken cancellationToken = default)
    {
        axis.MarkHomed();
        return Task.CompletedTask;
    }
}
