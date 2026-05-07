# Architecture Round 13

## Added in Round 13

- HomePlanRuntimeState
- HomeExecutionPlan title support
- AxisDebugViewModel property change notification
- Axis Debug UI now renders HomeExecutionPlan
- EtherCAT slave model now includes ModuleType and FaultText
- EtherCAT monitor grid now shows richer slave diagnostics

## Key Improvements

### Axis Debug is now reactive
AxisDebugViewModel now implements INotifyPropertyChanged and updates its selected-axis metadata fields when SelectedAxisNo changes. This makes the debug panel more like a real operator/debug surface instead of a static form.

### Homing plans are now visible in UI
HomingService stores the current HomeExecutionPlan into HomePlanRuntimeState, and Axis Debug now renders:
- plan title
- step list

That makes the strategy layer visible instead of remaining hidden in service code.

### EtherCAT diagnostics got richer
EtherCatSlaveStatus / EtherCatSlaveViewModel now include:
- ModuleType
- FaultText

The EtherCAT Monitor tab now shows those fields directly.

## Important Note

The current HomeExecutionPlan is populated when homing is requested, and the UI has the structure to display it. This is a good pre-SDK boundary because real vendor-specific homing command sequencing can later populate the same execution-plan model with actual steps and statuses.

## Next Recommended Phase

- Make HomeExecutionPlan update before/after command execution with progress state
- Add AxisDebug selected-axis dropdown/list instead of manual axis number typing
- Add command feedback model for Enable/Home/Move/Stop command lifecycle
- Add alarm text/category/source model and render it in Alarm page
