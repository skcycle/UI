using System;
using System.Windows;
using System.Windows.Threading;

namespace MotionControl.Presentation.ViewModels;

public sealed class DispatcherUiRefreshNotifier(Action refreshAction) : IUiRefreshNotifier
{
    public void RequestRefresh()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            refreshAction();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            refreshAction();
            return;
        }

        _ = dispatcher.BeginInvoke(refreshAction, DispatcherPriority.Background);
    }
}
