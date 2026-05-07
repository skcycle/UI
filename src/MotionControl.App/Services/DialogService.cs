using System.Windows;
using MotionControl.App.Views.Dialogs;
using MotionControl.Presentation.Dialogs;

namespace MotionControl.App.Services;

/// <summary>
/// App 层实现：统一弹窗服务，替换所有 System.Windows.MessageBox 调用
/// </summary>
public class DialogService : IDialogService
{
    public DialogService() { }

    public void ShowWarning(string message, string title = "警告") =>
        AlertDialog.Show(message, title, DialogIcon.Warning);

    public void ShowError(string message, string title = "错误") =>
        AlertDialog.Show(message, title, DialogIcon.Error);

    public void ShowInfo(string message, string title = "提示") =>
        AlertDialog.Show(message, title, DialogIcon.Info);

    public void ShowSuccess(string message, string title = "成功") =>
        AlertDialog.Show(message, title, DialogIcon.Success);

    public void ShowAlarm(string message, string title = "报警") =>
        AlertDialog.Show(message, title, DialogIcon.Alarm);

    public MessageBoxResult Confirm(string message, string title = "确认") =>
        ConfirmationDialog.Show(message, title, DialogIcon.Warning, DialogButton.YesNo);

    public MessageBoxResult ConfirmWithCancel(string message, string title = "确认") =>
        ConfirmationDialog.Show(message, title, DialogIcon.Warning, DialogButton.YesNoCancel);
}
