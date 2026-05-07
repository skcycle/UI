# Architecture Round 3

## Added in Round 3

- PollingHostedService
- DashboardViewModel
- AlarmViewModel
- SafetyInterlockService
- Axis debug commands for move / stop / jog
- MainWindow tab-based WPF shell

## Current UI Tabs

- Dashboard
- Axis Monitor
- Axis Debug
- Alarm

## Current Continuous Polling Plan

PollingHostedService is introduced as the recurring polling skeleton. It is not fully wired into app lifetime yet. The next refinement should:
1. Move composition to a real DI container
2. Start PollingHostedService after controller initialization
3. Marshal UI refresh safely onto Dispatcher
4. Add stop/dispose lifecycle on app shutdown
