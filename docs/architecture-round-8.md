# Architecture Round 8

## Added in Round 8

- SystemStateMachine
- EtherCatControllerStatus model
- AxisStateMachine integration into axis polling flow
- Extended AxisMappingItem with soft limit, home mode, and servo binding metadata
- Controller polling updates machine system state from controller status + machine state

## Current State Flow

### Axis level
1. Read axis feedback from controller
2. Update domain axis feedback
3. Evaluate next axis state in AxisStateMachine
4. Apply state back to domain axis

### System level
1. Poll controller status
2. Evaluate machine + alarm + servo + network conditions
3. Compute next SystemState in SystemStateMachine
4. Apply state back to Machine

## Current EtherCAT Status Model

EtherCatControllerStatus currently tracks:
- IsConnected
- IsOperational
- OnlineSlaveCount
- NetworkState
- ControllerModel
- Timestamp

This is still a placeholder, but it establishes the system boundary for future real EtherCAT diagnostics.

## Next Recommended Phase

- Add dedicated EtherCAT slave/module status model
- Surface controller/system state in DashboardViewModel
- Bind axis mapping metadata into domain axis initialization
- Add home strategy objects instead of string-based HomeMode
