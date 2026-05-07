# Architecture Round 9

## Added in Round 9

- HomeMode enum
- EtherCatSlaveStatus model
- ControllerRuntimeState for UI-visible controller snapshot
- MachineFactory now applies axis mapping metadata into domain Axis initialization
- DashboardViewModel now surfaces EtherCAT network state and online slave count

## Key Improvements

### Axis mapping now reaches the domain model
Axis initialization now applies:
- axis display name
- soft limit range
- typed HomeMode
- servo binding metadata

This means configuration is no longer just stored in options, it begins to shape the actual domain aggregate.

### UI can now see controller state
ControllerPollingService stores the latest EtherCAT controller snapshot into ControllerRuntimeState. MainWindowViewModel passes that into DashboardViewModel refresh, so the dashboard can show:
- SystemState
- EtherCAT network state
- EtherCAT connection flag
- online slave count

### Home mode is no longer stringly-typed
AxisMappingItem now uses the HomeMode enum from the domain layer, which is a better base for later home strategy objects.

### EtherCAT model expanded one step deeper
EtherCatControllerStatus now includes slave snapshots via EtherCatSlaveStatus. This is still placeholder data, but it creates the correct shape for future fieldbus diagnostics.

## Next Recommended Phase

- Surface slave/module rows in Dashboard or dedicated EtherCAT monitor page
- Replace HomeMode enum with strategy/config object mapping
- Bind Axis soft limit / home mode / servo binding onto axis UI cards
- Add alarm sources from EtherCAT slave status and controller diagnostics
