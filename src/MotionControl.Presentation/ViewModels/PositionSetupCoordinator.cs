using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// PositionSetup Monitor 页内的联动协调器。
/// 负责 PositionSetupMonitor.SelectedItem.SelectedPosition 子属性变化时，
/// 刷新相关命令的 CanExecute 状态。
/// 注意：PositionSetupMonitor.SelectedItem 本身的切换事件由 MainWindowViewModel
/// 自行管理（_lastPositionSetupSelectedItem 生命周期），本协调者只负责
/// SelectedPosition 子属性变化时的命令刷新。
/// </summary>
public sealed class PositionSetupCoordinator(
    PositionSetupMonitorViewModel positionSetupMonitor,
    RelayCommand deletePositionSetupPositionCommand,
    RelayCommand teachPositionSetupCommand,
    RelayCommand movePositionSetupCommand)
{
    public void Initialize()
    {
        positionSetupMonitor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PositionSetupMonitorViewModel.SelectedItem) + ".SelectedPosition")
            {
                deletePositionSetupPositionCommand.RaiseCanExecuteChanged();
                teachPositionSetupCommand.RaiseCanExecuteChanged();
                movePositionSetupCommand.RaiseCanExecuteChanged();
            }
        };
    }
}
