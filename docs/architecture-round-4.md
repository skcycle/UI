# Architecture Round 4

## Added in Round 4

- ApplicationContext for app-level composition
- PollingHostedService lifecycle hookup on MainWindow load/close
- Alarm and IoPoint domain entities
- Machine aggregate expanded with IO points and alarms
- ZmcAxisNativeFacade placeholder introduced
- ZmcMotionController now routes motion commands through native facade placeholders

## Current Lifecycle

1. MainWindow constructs ApplicationContext
2. ApplicationContext builds all domain/services/viewmodels
3. MainWindow Loaded -> MainWindowViewModel.InitializeAsync()
4. PollingHostedService starts recurring 200ms polling
5. MainWindow Closed -> PollingHostedService.StopAsync()

## Remaining Gaps

- Replace manual composition with Microsoft DI host
- Marshal polling UI refresh back to WPF Dispatcher safely
- Add real alarm generation and IO refresh logic
- Implement actual ZMC SDK bridge in native facade
- Add dedicated EtherCAT controller state model
