using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionControl.Infrastructure.Configuration;

namespace MotionControl.Presentation.ViewModels;

/// <summary>
/// 管理 PositionSetup 父对象列表。
/// </summary>
public sealed class PositionSetupMonitorViewModel : INotifyPropertyChanged
{
    public ObservableCollection<PositionSetupItemViewModel> Items { get; } = new();

    private PositionSetupItemViewModel? _selectedItem;
    public PositionSetupItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem == value) return;
            if (_selectedItem is not null)
            {
                _selectedItem.PropertyChanged -= OnSelectedItemPropertyChanged;
            }
            _selectedItem = value;
            OnPropertyChanged();
            if (_selectedItem is not null)
            {
                _selectedItem.PropertyChanged += OnSelectedItemPropertyChanged;
                _selectedItem.HasSelectedPositionChanged += () => OnPropertyChanged("SelectedItem.SelectedPosition");
            }
        }
    }

    // Bubble SelectedPosition changes from the child item up so
    // DataGrid bindings and CanExecute re-evaluate correctly.
    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PositionSetupItemViewModel.SelectedPosition) ||
            e.PropertyName == nameof(PositionSetupItemViewModel.HasSelectedPosition) ||
            string.IsNullOrEmpty(e.PropertyName))
        {
            OnPropertyChanged("SelectedItem.SelectedPosition");
        }
    }

    public void Load(IEnumerable<PositionSetupConfigItem> items)
    {
        if (_selectedItem is not null)
        {
            _selectedItem.PropertyChanged -= OnSelectedItemPropertyChanged;
        }
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new PositionSetupItemViewModel(item));
        }
        SelectedItem = Items.FirstOrDefault();
    }

    public void Add(PositionSetupConfigItem item)
    {
        var vm = new PositionSetupItemViewModel(item);
        Items.Add(vm);
        SelectedItem = vm;
    }

    public void RemoveSelected()
    {
        if (SelectedItem is null) return;
        var current = SelectedItem;
        Items.Remove(current);
        SelectedItem = Items.FirstOrDefault();
    }

    public IReadOnlyList<PositionSetupConfigItem> ToConfigs() => Items.Select(item => item.ToConfig()).ToList();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
