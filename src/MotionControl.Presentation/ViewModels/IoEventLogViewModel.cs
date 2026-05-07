using System.Linq;
using System.Collections.ObjectModel;
using MotionControl.Control.Services;

namespace MotionControl.Presentation.ViewModels;

public sealed class IoEventLogViewModel
{
    private readonly IoEventRuntimeState _ioEventRuntimeState;
    private IoEventItemViewModel[] _lastEvents = Array.Empty<IoEventItemViewModel>();

    public IoEventLogViewModel(IoEventRuntimeState ioEventRuntimeState)
    {
        _ioEventRuntimeState = ioEventRuntimeState;
        Events = new ObservableCollection<IoEventItemViewModel>();
        _ioEventRuntimeState.EventsChanged += Refresh;
    }

    public ObservableCollection<IoEventItemViewModel> Events { get; }

    public void Refresh()
    {
        var ioEvents = _ioEventRuntimeState.RecentEvents
            .Select(f => new IoEventItemViewModel(f))
            .ToArray();

        // Use length comparison to avoid SequenceEqual false negatives on same-content different-reference arrays
        var newLength = ioEvents.Length;
        var lastLength = _lastEvents.Length;
        if (newLength != lastLength || (newLength > 0 && (lastLength == 0 || !ioEvents.SequenceEqual(_lastEvents))))
        {
            Events.Clear();
            foreach (var evt in ioEvents.TakeLast(30).Reverse())
            {
                Events.Add(evt);
            }

            _lastEvents = ioEvents;
        }
    }
}
