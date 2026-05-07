using MotionControl.Control.Homing;

namespace MotionControl.Control.Services;

public sealed class HomePlanRuntimeState
{
    public HomeExecutionPlan? CurrentPlan { get; private set; }

    public void Update(HomeExecutionPlan plan)
    {
        CurrentPlan = plan;
    }
}
