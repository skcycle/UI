using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionControl.Presentation.ViewModels;

public sealed class EtherCatMonitorViewModel : INotifyPropertyChanged
{
    public EtherCatMonitorViewModel(DashboardViewModel dashboardViewModel)
    {
        Dashboard = dashboardViewModel;
        _slaves = new ObservableCollection<EtherCatSlaveViewModel>(dashboardViewModel.EtherCatSlaves);
        dashboardViewModel.PropertyChanged += OnDashboardPropertyChanged;
    }

    public DashboardViewModel Dashboard { get; }

    private readonly ObservableCollection<EtherCatSlaveViewModel> _slaves;
    public ObservableCollection<EtherCatSlaveViewModel> Slaves => _slaves;

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.EtherCatSlaves))
        {
            _slaves.Clear();
            foreach (var slave in Dashboard.EtherCatSlaves)
            {
                _slaves.Add(slave);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
