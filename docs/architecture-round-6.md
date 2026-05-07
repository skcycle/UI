# Architecture Round 6

## Added in Round 6

- appsettings.json
- ZmcController options binding
- AxisMapping options binding
- IUiRefreshNotifier abstraction
- ImmediateUiRefreshNotifier placeholder implementation
- PollingHostedService converted to BackgroundService

## Current Configuration Model

### ZmcController
- IpAddress
- AxisCount
- PollingIntervalMs

### AxisMapping
- AxisNames[32]

## Current Background Loop Model

PollingHostedService now runs as a host-managed BackgroundService and reads polling interval from configuration. UI refresh is routed through IUiRefreshNotifier so the current implementation can later be replaced by a Dispatcher-aware version.

## Next Recommended Phase

- Replace ImmediateUiRefreshNotifier with Dispatcher-based notifier
- Split polling into axis / IO / alarm polling responsibilities
- Add appsettings for EtherCAT / alarm / recipe / logging sections
- Introduce repositories for persistent configuration
