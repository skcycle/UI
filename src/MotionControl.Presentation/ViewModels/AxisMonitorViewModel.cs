using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;

namespace MotionControl.Presentation.ViewModels;

public sealed class AxisMonitorViewModel : INotifyPropertyChanged
{
    private readonly Machine _machine;
    private readonly MotionControl.Presentation.Dialogs.IDialogService _dialogService;
    private readonly CommandFeedbackRuntimeState _commandFeedbackRuntimeState;
    private AxisViewModel? _selectedAxis;

    public AxisMonitorViewModel(Machine machine, MotionControl.Control.Interfaces.IAxisControlService axisControlService, MotionControl.Presentation.Dialogs.IDialogService dialogService, CommandFeedbackRuntimeState commandFeedbackRuntimeState)
    {
        _machine = machine;
        _axisControlService = axisControlService;
        _dialogService = dialogService;
        _commandFeedbackRuntimeState = commandFeedbackRuntimeState;
        Axes = new ObservableCollection<AxisViewModel>(machine.Axes.Select(axis => new AxisViewModel(axis, machine, axisControlService, dialogService, commandFeedbackRuntimeState)));
        _selectedAxis = Axes.FirstOrDefault();
    }

    public ObservableCollection<AxisViewModel> Axes { get; }

    public AxisViewModel? SelectedAxis
    {
        get => _selectedAxis;
        set
        {
            if (_selectedAxis == value)
            {
                return;
            }

            _selectedAxis = value;
            OnPropertyChanged();
            SelectedAxisChanged?.Invoke(value);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<AxisViewModel?>? SelectedAxisChanged;

    public void RefreshAll()
    {
        SyncAxes();

        foreach (var axis in Axes)
        {
            axis.Refresh();
        }
    }

    public AxisViewModel AddAxis(Axis axis)
    {
        var viewModel = new AxisViewModel(axis, _machine, GetAxisControlService(), _dialogService, _commandFeedbackRuntimeState);
        Axes.Add(viewModel);
        SelectedAxis = viewModel;
        return viewModel;
    }

    public void ReloadFromMachine()
    {
        Axes.Clear();
        foreach (var axis in _machine.Axes)
        {
            Axes.Add(new AxisViewModel(axis, _machine, GetAxisControlService(), _dialogService, _commandFeedbackRuntimeState));
        }
        SelectedAxis = Axes.FirstOrDefault();
    }

    public void RemoveAxis(int axisNo)
    {
        var existing = Axes.FirstOrDefault(axis => axis.AxisNo == axisNo);
        if (existing is null)
        {
            return;
        }

        var wasSelected = SelectedAxis == existing;
        Axes.Remove(existing);
        if (wasSelected)
        {
            SelectedAxis = Axes.FirstOrDefault();
        }
    }

    private void SyncAxes()
    {
        var machineAxisNos = _machine.Axes.Select(axis => axis.Id.Value).ToHashSet();
        for (var index = Axes.Count - 1; index >= 0; index--)
        {
            if (!machineAxisNos.Contains(Axes[index].AxisNo))
            {
                Axes.RemoveAt(index);
            }
        }

        foreach (var axis in _machine.Axes)
        {
            if (Axes.All(existing => existing.AxisNo != axis.Id.Value))
            {
                Axes.Add(new AxisViewModel(axis, _machine, GetAxisControlService(), _dialogService, _commandFeedbackRuntimeState));
            }
        }
    }

    private readonly MotionControl.Control.Interfaces.IAxisControlService _axisControlService;

    private MotionControl.Control.Interfaces.IAxisControlService GetAxisControlService() => _axisControlService;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
