using System.Collections.ObjectModel;
using MotionControl.Domain.Entities;

namespace MotionControl.Presentation.ViewModels;

public sealed class AlarmViewModel
{
    private readonly Machine _machine;
    private string[] _lastActiveAlarmAxes = Array.Empty<string>();
    private AlarmItemViewModel[] _lastActiveAlarms = Array.Empty<AlarmItemViewModel>();

    public AlarmViewModel(Machine machine)
    {
        _machine = machine;
        ActiveAlarmAxes = new ObservableCollection<string>();
        ActiveAlarms = new ObservableCollection<AlarmItemViewModel>();
    }

    public ObservableCollection<string> ActiveAlarmAxes { get; }
    public ObservableCollection<AlarmItemViewModel> ActiveAlarms { get; }

    public void Refresh()
    {
        var latestAlarmAxes = _machine.Axes
            .Where(a => a.HasAlarm)
            .Select(axis => $"{axis.Name} (AxisNo={axis.ControllerAxisNo})")
            .ToArray();

        if (!_lastActiveAlarmAxes.SequenceEqual(latestAlarmAxes))
        {
            ActiveAlarmAxes.Clear();
            foreach (var axis in latestAlarmAxes)
            {
                ActiveAlarmAxes.Add(axis);
            }

            _lastActiveAlarmAxes = latestAlarmAxes;
        }

        var latestAlarms = _machine.Alarms
            .Where(item => item.IsActive)
            .Select(alarm => new AlarmItemViewModel(alarm))
            .ToArray();

        if (!_lastActiveAlarms.SequenceEqual(latestAlarms))
        {
            ActiveAlarms.Clear();
            foreach (var alarm in latestAlarms)
            {
                ActiveAlarms.Add(alarm);
            }

            _lastActiveAlarms = latestAlarms;
        }
    }
}
