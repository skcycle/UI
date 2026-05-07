using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MotionControl.Application.Interfaces;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;
namespace MotionControl.Presentation.ViewModels;

public sealed class WorkHeadMonitorViewModel : INotifyPropertyChanged
{
    private readonly Machine _machine;
    private readonly IoControlService _ioControlService;
    private readonly IMotionAppService _motionAppService;
    private readonly Func<bool> _canControl;
    private readonly WorkHeadEventRuntimeState _workHeadEventRuntimeState;
    private WorkHeadItemViewModel? _selectedWorkHead;

    public WorkHeadMonitorViewModel(Machine machine, IoControlService ioControlService, IMotionAppService motionAppService, WorkHeadEventRuntimeState workHeadEventRuntimeState, Func<bool> canControl)
    {
        _machine = machine;
        _ioControlService = ioControlService;
        _motionAppService = motionAppService;
        _workHeadEventRuntimeState = workHeadEventRuntimeState;
        _canControl = canControl;
        WorkHeads = new ObservableCollection<WorkHeadItemViewModel>(_machine.WorkHeads.Select(BuildViewModel));
    }

    public ObservableCollection<WorkHeadItemViewModel> WorkHeads { get; }
    public WorkHeadItemViewModel? SelectedWorkHead
    {
        get => _selectedWorkHead;
        set
        {
            if (_selectedWorkHead == value) return;
            _selectedWorkHead = value;
            OnPropertyChanged();
            if (value is null) return;
            RaiseCanExecuteChanged(value.TeachPositionCommand);
            RaiseCanExecuteChanged(value.MovePositionCommand);
            RaiseCanExecuteChanged(value.VacuumCommand);
            RaiseCanExecuteChanged(value.BlowCommand);
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshAll()
    {
        foreach (var workHead in WorkHeads) workHead.Refresh();
    }

    public void ReloadFromMachine()
    {
        WorkHeads.Clear();
        foreach (var workHead in _machine.WorkHeads)
        {
            WorkHeads.Add(BuildViewModel(workHead));
        }
        SelectedWorkHead = WorkHeads.FirstOrDefault();
    }

    public void AddWorkHead(WorkHead workHead)
    {
        var vm = BuildViewModel(workHead);
        WorkHeads.Add(vm);
        SelectedWorkHead = vm;
    }

    public void RemoveWorkHead(string name)
    {
        var existing = WorkHeads.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return;
        WorkHeads.Remove(existing);
        if (SelectedWorkHead == existing) SelectedWorkHead = WorkHeads.FirstOrDefault();
    }

    private WorkHeadItemViewModel BuildViewModel(WorkHead workHead) => new(workHead, _machine, _ioControlService, _motionAppService, _workHeadEventRuntimeState, _canControl);

    private static void RaiseCanExecuteChanged(System.Windows.Input.ICommand command)
    {
        if (command is MotionControl.Presentation.Commands.RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    private void SyncCollections()
    {
        var sourceNames = _machine.WorkHeads.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = WorkHeads.Count - 1; i >= 0; i--) if (!sourceNames.Contains(WorkHeads[i].Name)) WorkHeads.RemoveAt(i);
        foreach (var workHead in _machine.WorkHeads)
        {
            if (WorkHeads.All(item => !string.Equals(item.Name, workHead.Name, StringComparison.OrdinalIgnoreCase))) WorkHeads.Add(BuildViewModel(workHead));
        }
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
