using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Control.Services;
using MotionControl.Domain.Entities;

namespace MotionControl.Presentation.ViewModels;

public sealed class CylinderMonitorViewModel : INotifyPropertyChanged
{
    public event Action<CylinderItemViewModel?>? SelectedCylinderChanged;
    private readonly Machine _machine;
    private readonly IoControlService _ioControlService;
    private readonly CylinderEventRuntimeState _cylinderEventRuntimeState;
    private readonly Func<bool> _canControl;
    private CylinderItemViewModel? _selectedCylinder;

    public CylinderMonitorViewModel(Machine machine, IoControlService ioControlService, CylinderEventRuntimeState cylinderEventRuntimeState, Func<bool> canControl)
    {
        _machine = machine;
        _ioControlService = ioControlService;
        _cylinderEventRuntimeState = cylinderEventRuntimeState;
        _canControl = canControl;
        Cylinders = new ObservableCollection<CylinderItemViewModel>(_machine.Cylinders.Select(c => BuildViewModel(c)));
    }

    public ObservableCollection<CylinderItemViewModel> Cylinders { get; }

    public CylinderItemViewModel? SelectedCylinder
    {
        get => _selectedCylinder;
        set
        {
            if (_selectedCylinder == value) return;
            _selectedCylinder = value;
            OnPropertyChanged();
            SelectedCylinderChanged?.Invoke(value);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshAll()
    {
        foreach (var cylinder in Cylinders)
        {
            cylinder.Refresh();
        }
    }

    public void ReloadFromMachine()
    {
        Cylinders.Clear();
        foreach (var cylinder in _machine.Cylinders)
        {
            Cylinders.Add(BuildViewModel(cylinder));
        }
        SelectedCylinder = Cylinders.FirstOrDefault();
    }

    public void AddCylinder(Cylinder cylinder)
    {
        var viewModel = BuildViewModel(cylinder);
        Cylinders.Add(viewModel);
        SelectedCylinder = viewModel;
    }

    public void RemoveCylinder(string name)
    {
        var existing = Cylinders.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        var wasSelected = SelectedCylinder == existing;
        Cylinders.Remove(existing);
        if (wasSelected)
        {
            SelectedCylinder = Cylinders.FirstOrDefault();
        }
    }

    private CylinderItemViewModel BuildViewModel(Cylinder cylinder) => new(cylinder, _machine, _ioControlService, _cylinderEventRuntimeState, _canControl);

    private void SyncCollections()
    {
        var sourceNames = _machine.Cylinders.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = Cylinders.Count - 1; index >= 0; index--)
        {
            if (!sourceNames.Contains(Cylinders[index].Name))
            {
                Cylinders.RemoveAt(index);
            }
        }

        foreach (var cylinder in _machine.Cylinders)
        {
            var existing = Cylinders.FirstOrDefault(item => string.Equals(item.Name, cylinder.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Cylinders.Add(BuildViewModel(cylinder));
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
