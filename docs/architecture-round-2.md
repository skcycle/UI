# Architecture Round 2

## Added in Round 2

- ControllerPollingService
- SystemAppService
- AxisMonitorViewModel
- AxisDebugViewModel
- MainWindowViewModel wiring
- Bootstrap-based service composition for the first runnable chain

## Current Main Flow

1. MainWindow loads
2. ServiceRegistration builds all core services and view models
3. MainWindowViewModel.InitializeAsync()
4. SystemAppService.InitializeAsync()
5. ControllerPollingService.ConnectAsync() + PollOnceAsync()
6. AxisMonitorViewModel refreshes display state

## Next Recommended Phase

- Replace manual bootstrap with real DI container
- Add recurring polling timer/background worker
- Add move command UI inputs
- Add alarm page and safety interlock service
- Replace ZmcMotionController placeholder methods with real SDK integration
