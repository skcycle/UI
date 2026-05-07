using System.Collections.ObjectModel;
using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class MagazineEventLogViewModel
{
    private readonly MagazineEventRuntimeState _runtimeState;

    public MagazineEventLogViewModel(MagazineEventRuntimeState runtimeState)
    {
        _runtimeState = runtimeState;
        Events = new ObservableCollection<MagazineEventItemViewModel>();
        _runtimeState.EventsChanged += Refresh;
        Refresh();
    }

    public ObservableCollection<MagazineEventItemViewModel> Events { get; }

    public void Refresh()
    {
        Events.Clear();
        foreach (var item in _runtimeState.RecentEvents.Reverse())
        {
            Events.Add(new MagazineEventItemViewModel(item));
        }
    }
}
