using System.Linq;
using System.Collections.ObjectModel;
using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class WorkHeadEventLogViewModel
{
    private readonly WorkHeadEventRuntimeState _workHeadEventRuntimeState;
    private WorkHeadEventItemViewModel[] _lastEvents = Array.Empty<WorkHeadEventItemViewModel>();

    public WorkHeadEventLogViewModel(WorkHeadEventRuntimeState workHeadEventRuntimeState)
    {
        _workHeadEventRuntimeState = workHeadEventRuntimeState;
        Events = new ObservableCollection<WorkHeadEventItemViewModel>();
        _workHeadEventRuntimeState.EventsChanged += Refresh;
    }

    public ObservableCollection<WorkHeadEventItemViewModel> Events { get; }

    public void Refresh()
    {
        var events = _workHeadEventRuntimeState.RecentEvents
            .Select(item => new WorkHeadEventItemViewModel(item))
            .ToArray();

        if (events.Length != _lastEvents.Length || (events.Length > 0 && (_lastEvents.Length == 0 || !events.SequenceEqual(_lastEvents))))
        {
            Events.Clear();
            foreach (var item in events.TakeLast(30).Reverse())
            {
                Events.Add(item);
            }

            _lastEvents = events;
        }
    }
}
