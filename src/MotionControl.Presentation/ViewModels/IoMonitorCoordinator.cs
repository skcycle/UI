using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// IO Monitor 页内的联动协调器。
/// 负责删除按钮可用性、Load/Reload 后的选择状态恢复等 UI 协调工作，
/// 不直接处理配置文件和运行时模型同步。
/// </summary>
public sealed class IoMonitorCoordinator(
    IoMonitorViewModel ioMonitor,
    RelayCommand deleteInputCommand,
    RelayCommand deleteOutputCommand)
{
    public void Initialize()
    {
        ioMonitor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IoMonitorViewModel.SelectedInput))
            {
                deleteInputCommand.RaiseCanExecuteChanged();
            }
            else if (args.PropertyName == nameof(IoMonitorViewModel.SelectedOutput))
            {
                deleteOutputCommand.RaiseCanExecuteChanged();
            }
        };
    }

    public void AfterLoadOrReload()
    {
        ioMonitor.SelectedInput = null;
        ioMonitor.SelectedOutput = null;
        ioMonitor.ReloadFromMachine();
        deleteInputCommand.RaiseCanExecuteChanged();
        deleteOutputCommand.RaiseCanExecuteChanged();
    }

    public void AfterDelete(bool isOutput, int address)
    {
        ioMonitor.RemoveIoPoint(isOutput, address);
        if (isOutput)
        {
            deleteOutputCommand.RaiseCanExecuteChanged();
        }
        else
        {
            deleteInputCommand.RaiseCanExecuteChanged();
        }
    }
}
