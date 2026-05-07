using System.Windows;
using MotionControl.Presentation.Dialogs;

namespace MotionControl.Presentation.ViewModels;

internal static class UiGuards
{
    public static bool Confirm(IDialogService dialogService, string title, string message)
        => dialogService.Confirm(message, title) == MessageBoxResult.Yes;
}
