using System.Linq;
using System.Collections.ObjectModel;
using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class CylinderEventLogViewModel
{
    private readonly CylinderEventRuntimeState _cylinderEventRuntimeState;
    private CylinderEventItemViewModel[] _lastEvents = Array.Empty<CylinderEventItemViewModel>();

    public CylinderEventLogViewModel(CylinderEventRuntimeState cylinderEventRuntimeState)
    {
        _cylinderEventRuntimeState = cylinderEventRuntimeState;
        Events = new ObservableCollection<CylinderEventItemViewModel>();
        _cylinderEventRuntimeState.EventsChanged += Refresh;
    }

    public ObservableCollection<CylinderEventItemViewModel> Events { get; }

    public void Refresh()
    {
        var events = _cylinderEventRuntimeState.RecentEvents
            .Select(item => new CylinderEventItemViewModel(item))
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
