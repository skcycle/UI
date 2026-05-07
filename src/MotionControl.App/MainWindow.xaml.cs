using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MotionControl.Presentation.ViewModels;

namespace MotionControl.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel? _viewModel;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || _viewModel is null)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    // ── Navigation ──

    private void NavDashboard_Click(object sender, RoutedEventArgs e) => NavigateTo(0, MainWindowViewModel.NavigationPage.Dashboard);
    private void NavEtherCat_Click(object sender, RoutedEventArgs e) => NavigateTo(1, MainWindowViewModel.NavigationPage.EtherCat);
    private void NavAxis_Click(object sender, RoutedEventArgs e) => NavigateTo(2, MainWindowViewModel.NavigationPage.Axis);
    private void NavIo_Click(object sender, RoutedEventArgs e) => NavigateTo(3, MainWindowViewModel.NavigationPage.Io);
    private void NavCylinder_Click(object sender, RoutedEventArgs e) => NavigateTo(4, MainWindowViewModel.NavigationPage.Cylinder);
    private void NavMagazine_Click(object sender, RoutedEventArgs e) => NavigateTo(5, MainWindowViewModel.NavigationPage.Magazine);
    private void NavWorkHead_Click(object sender, RoutedEventArgs e) => NavigateTo(6, MainWindowViewModel.NavigationPage.WorkHead);
    private void NavPositionSetup_Click(object sender, RoutedEventArgs e) => NavigateTo(7, MainWindowViewModel.NavigationPage.PositionSetup);
    private void NavAlarm_Click(object sender, RoutedEventArgs e) => NavigateTo(8, MainWindowViewModel.NavigationPage.Alarm);

    private void NavigateTo(int tabIndex, MainWindowViewModel.NavigationPage page)
    {
        if (FindName("MainTabControl") is TabControl tabControl)
        {
            tabControl.SelectedIndex = tabIndex;
        }

        SetNavigationSelection(tabIndex);

        if (_viewModel is not null)
        {
            _viewModel.SelectedPage = page;
        }
    }

    private void SetNavigationSelection(int tabIndex)
    {
        if (FindName("NavDashboardButton") is System.Windows.Controls.Primitives.ToggleButton dashboardButton)
            dashboardButton.IsChecked = tabIndex == 0;
        if (FindName("NavEtherCatButton") is System.Windows.Controls.Primitives.ToggleButton etherCatButton)
            etherCatButton.IsChecked = tabIndex == 1;
        if (FindName("NavAxisButton") is System.Windows.Controls.Primitives.ToggleButton axisButton)
            axisButton.IsChecked = tabIndex == 2;
        if (FindName("NavIoButton") is System.Windows.Controls.Primitives.ToggleButton ioButton)
            ioButton.IsChecked = tabIndex == 3;
        if (FindName("NavCylinderButton") is System.Windows.Controls.Primitives.ToggleButton cylinderButton)
            cylinderButton.IsChecked = tabIndex == 4;
        if (FindName("NavMagazineButton") is System.Windows.Controls.Primitives.ToggleButton magazineButton)
            magazineButton.IsChecked = tabIndex == 5;
        if (FindName("NavWorkHeadButton") is System.Windows.Controls.Primitives.ToggleButton workHeadButton)
            workHeadButton.IsChecked = tabIndex == 6;
        if (FindName("NavPositionSetupButton") is System.Windows.Controls.Primitives.ToggleButton positionSetupButton)
            positionSetupButton.IsChecked = tabIndex == 7;
        if (FindName("NavAlarmButton") is System.Windows.Controls.Primitives.ToggleButton alarmButton)
            alarmButton.IsChecked = tabIndex == 8;
    }
}
