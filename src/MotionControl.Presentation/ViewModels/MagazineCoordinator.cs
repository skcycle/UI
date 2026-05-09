using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// Magazine Monitor 页内的联动协调器。
/// 负责 MagazineMonitor 的选中状态变化时，刷新相关命令的 CanExecute 状态，
/// 不直接处理配置文件和运行时模型同步。
/// </summary>
public sealed class MagazineCoordinator(
    MagazineMonitorViewModel magazineMonitor,
    Action refreshMagazineCommands,
    Action refreshMagazinePositionCommands)
{
    public void Initialize()
    {
        magazineMonitor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MagazineMonitorViewModel.SelectedMagazine))
            {
                refreshMagazineCommands();
            }
        };

        magazineMonitor.SelectedMagazinePositionChanged += () =>
        {
            refreshMagazinePositionCommands();
        };
    }
}
