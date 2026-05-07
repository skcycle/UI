# Final Cleanup Audit

Checked repository for previously discussed missed fixes and confirmed the following are already in code:

- `App.xaml` no longer uses `StartupUri`
- `MainWindow.xaml` uses `WindowStartupLocation`
- `App.xaml.cs` uses `System.Windows.Application`
- `App.xaml.cs` imports `Microsoft.Extensions.DependencyInjection`
- `MainWindow.xaml.cs` has both parameterless and DI constructors
- `ServiceRegistration.cs` is stubbed/obsolete and no longer instantiates outdated services
- `PollingHostedService` no longer depends on `MainWindowViewModel` or `IUiRefreshNotifier`
- `DispatcherUiRefreshNotifier` no longer depends on `Dispatcher`
- `HostBuilderFactory` uses `Bind(...)` instead of `Get<T>()`
- `AxisViewModel` and `AxisDebugViewModel` use `SoftLimit.Minimum/Maximum`
- `DataGridCheckBoxColumn` readonly state bindings in `MainWindow.xaml` use `Mode=OneWay`

Remaining expected issues after this point are likely runtime/device-integration issues rather than the earlier compile/startup mismatches.
