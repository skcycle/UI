using MotionControl.Domain.Entities;
using MotionControl.Domain.Enums;

namespace MotionControl.Control.Homing;

public interface IHomeStrategy
{
    HomeMode HomeMode { get; }
    HomeExecutionPlan BuildPlan(Axis axis);
    Task ExecuteAsync(Axis axis, CancellationToken cancellationToken = default);
}
