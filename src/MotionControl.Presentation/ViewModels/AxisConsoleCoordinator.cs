using MotionControl.Application.Interfaces;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// Axis Console 页内的联动协调器。
/// 负责 AxisMonitor、AxisDebug、AxisParameterEditor 之间的选中同步，
/// 不承担业务逻辑和配置持久化职责。
/// </summary>
public sealed class AxisConsoleCoordinator(
    AxisMonitorViewModel axisMonitor,
    AxisDebugViewModel axisDebug,
    AxisParameterEditorViewModel axisParameterEditor)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        axisMonitor.SelectedAxisChanged += OnAxisMonitorSelectedAxisChanged;
        axisDebug.SelectedAxisChanged += OnAxisDebugSelectedAxisChanged;

        if (axisMonitor.SelectedAxis is not null)
        {
            await SyncSelectedAxisAsync(axisMonitor.SelectedAxis.AxisNo);
        }
    }

    private async void OnAxisMonitorSelectedAxisChanged(AxisViewModel? axis)
    {
        if (axis is null)
        {
            return;
        }

        await SyncSelectedAxisAsync(axis.AxisNo);
    }

    private async void OnAxisDebugSelectedAxisChanged(int axisNo)
    {
        await SyncSelectedAxisAsync(axisNo);
    }

    public async Task SyncSelectedAxisAsync(int axisNo)
    {
        axisDebug.SelectedAxisNo = axisNo;
        await axisParameterEditor.SyncAxisNoAsync(axisNo);
    }
}
