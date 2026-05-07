using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Domain.Entities;

namespace MotionControl.Presentation.ViewModels;

public sealed class MagazineMonitorViewModel : INotifyPropertyChanged
{
    private readonly Machine _machine;
    private MagazineItemViewModel? _selectedMagazine;

    public MagazineMonitorViewModel(Machine machine)
    {
        _machine = machine;
        Magazines = new ObservableCollection<MagazineItemViewModel>(_machine.Magazines.Select(BuildViewModel));
    }

    public ObservableCollection<MagazineItemViewModel> Magazines { get; }
    public event Action? SelectedMagazinePositionChanged;

    public MagazineItemViewModel? SelectedMagazine
    {
        get => _selectedMagazine;
        set
        {
            if (_selectedMagazine == value) return;
            if (_selectedMagazine is not null)
            {
                _selectedMagazine.PropertyChanged -= OnSelectedMagazinePropertyChanged;
            }

            _selectedMagazine = value;

            if (value is not null)
            {
                value.PropertyChanged += OnSelectedMagazinePropertyChanged;
            }

            OnPropertyChanged();
            SelectedMagazinePositionChanged?.Invoke();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshAll()
    {
        foreach (var magazine in Magazines) magazine.Refresh();
    }

    public void ReloadFromMachine()
    {
        Magazines.Clear();
        foreach (var magazine in _machine.Magazines)
        {
            Magazines.Add(BuildViewModel(magazine));
        }
        SelectedMagazine = Magazines.FirstOrDefault();
    }

    public void AddMagazine(Magazine magazine)
    {
        var vm = BuildViewModel(magazine);
        Magazines.Add(vm);
        SelectedMagazine = vm;
    }

    public void RemoveMagazine(string name)
    {
        var existing = Magazines.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return;
        Magazines.Remove(existing);
        if (SelectedMagazine == existing) SelectedMagazine = Magazines.FirstOrDefault();
    }

    private MagazineItemViewModel BuildViewModel(Magazine magazine) => new(magazine, _machine);

    private void OnSelectedMagazinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MagazineItemViewModel.SelectedPosition))
        {
            SelectedMagazinePositionChanged?.Invoke();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
