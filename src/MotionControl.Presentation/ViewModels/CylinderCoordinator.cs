using MotionControl.Presentation.Commands;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// Cylinder Monitor 页内的联动协调器。
/// 负责 CylinderMonitor 的选中状态变化时，刷新相关命令的 CanExecute 状态，
/// 不直接处理配置文件和运行时模型同步。
/// </summary>
public sealed class CylinderCoordinator(
    CylinderMonitorViewModel cylinderMonitor,
    Action refreshCylinderCommands)
{
    public void Initialize()
    {
        cylinderMonitor.SelectedCylinderChanged += _ => refreshCylinderCommands();
    }
}
