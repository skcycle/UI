using System.Windows;

namespace MotionControl.Presentation.Dialogs;

/// <summary>
/// 弹窗服务接口（定义在 Presentation 层，供 ViewModel 使用）
/// </summary>
public interface IDialogService
{
    void ShowWarning(string message, string title = "警告");
    void ShowError(string message, string title = "错误");
    void ShowInfo(string message, string title = "提示");
    void ShowSuccess(string message, string title = "成功");
    void ShowAlarm(string message, string title = "报警");
    MessageBoxResult Confirm(string message, string title = "确认");
    MessageBoxResult ConfirmWithCancel(string message, string title = "确认");
}
