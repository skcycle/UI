namespace MotionControl.App.Views.Dialogs;

/// <summary>
/// 警告/错误提示框（仅 OK 按钮）
/// </summary>
public class AlertDialog : DialogWindow
{
    public AlertDialog(string message, string title = "提示", DialogIcon icon = DialogIcon.Warning)
    {
        Title = title;
        Message = message;
        DialogKind = icon;
        Buttons = DialogButton.OK;
    }

    public static void Show(string message, string title = "提示", DialogIcon icon = DialogIcon.Warning)
    {
        var dialog = new AlertDialog(message, title, icon);
        var owner = System.Windows.Application.Current?.MainWindow;

        if (owner != null && owner != dialog)
            dialog.Owner = owner;

        dialog.ShowDialog();
    }
}
