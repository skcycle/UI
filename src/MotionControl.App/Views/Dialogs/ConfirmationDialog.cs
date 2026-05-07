using System.Windows;

namespace MotionControl.App.Views.Dialogs;

/// <summary>
/// 确认框（Yes/No 或 Yes/No/Cancel）
/// </summary>
public class ConfirmationDialog : DialogWindow
{
    public ConfirmationDialog(string message, string title = "确认", DialogIcon icon = DialogIcon.Warning, DialogButton buttons = DialogButton.YesNo)
    {
        Title = title;
        Message = message;
        DialogKind = icon;
        Buttons = buttons;
    }

    public static MessageBoxResult Show(string message, string title = "确认", DialogIcon icon = DialogIcon.Warning, DialogButton buttons = DialogButton.YesNo)
    {
        var dialog = new ConfirmationDialog(message, title, icon, buttons);
        var owner = System.Windows.Application.Current?.MainWindow;

        if (owner != null && owner != dialog)
            dialog.Owner = owner;

        dialog.ShowDialog();
        return dialog.Result;
    }
}
