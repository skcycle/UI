using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Homing;

public sealed class LimitThenIndexHomeStrategy : IHomeStrategy
{
    public HomeMode HomeMode => HomeMode.LimitThenIndex;

    public HomeExecutionPlan BuildPlan(Axis axis) => new()
    {
        Title = "Limit Then Index",
        Steps = new[]
        {
            $"Axis {axis.ControllerAxisNo}: move toward hardware limit",
            "Detect index pulse",
            "Latch home position and mark homed"
        }
    };

    public Task ExecuteAsync(Axis axis, CancellationToken cancellationToken = default)
    {
        axis.MarkHomed();
        return Task.CompletedTask;
    }
}
