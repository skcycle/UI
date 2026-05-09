using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// WorkHead Monitor 页内的联动协调器。
/// 负责 WorkHeadMonitor 的选中状态变化时，刷新相关命令的 CanExecute 状态，
/// 不直接处理配置文件和运行时模型同步。
/// </summary>
public sealed class WorkHeadCoordinator(
    WorkHeadMonitorViewModel workHeadMonitor,
    Action refreshWorkHeadCommands)
{
    public void Initialize()
    {
        workHeadMonitor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WorkHeadMonitorViewModel.SelectedWorkHead))
            {
                refreshWorkHeadCommands();
            }
        };
    }
}
