# Architecture Round 7

## Added in Round 7

- DispatcherUiRefreshNotifier
- AxisMappingItem / richer AxisMappingOptions
- AxisPollingService
- IoPollingService
- AlarmPollingService
- AxisStateMachine

## Current Polling Split

ControllerPollingService now orchestrates three dedicated polling responsibilities:
- AxisPollingService
- IoPollingService
- AlarmPollingService

This is important because motion control software usually evolves these at different rates and with different fault-handling logic.

## Current UI Refresh Strategy

The host now uses DispatcherUiRefreshNotifier so UI refresh requests can be marshaled onto the WPF Dispatcher.

## Current State Machine Start

AxisStateMachine is only a first placeholder, but it establishes the pattern that axis state transitions should live in a dedicated component rather than be scattered through services or view models.

## Next Recommended Phase

- Push AxisStateMachine into polling/control flows
- Add SystemStateMachine
- Add EtherCAT controller status model
- Expand AxisMappingItem with soft limits, home mode, and servo binding metadata
