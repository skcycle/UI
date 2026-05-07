# Architecture Round 14

## Added in Round 14

- CommandFeedback model
- CommandFeedbackRuntimeState
- AlarmItemViewModel
- Alarm grid view in WPF

## Key Improvements

### Command lifecycle semantics started to exist
AxisControlService and HomingService now write command feedback entries for:
- Enable
- Disable
- Move
- Stop
- ResetAlarm
- Home

The feedback model captures:
- command name
- axis number
- status
- message
- timestamp

This is important because pre-SDK readiness is not only about API boundaries, but also about having the right runtime semantics for later real hardware feedback.

### Command execution now nudges state transitions
Examples added:
- Stop sets axis state to `Stopping` before command execution
- Move sets axis state to `Moving`
- Enable sets axis state to `Standstill`
- Disable sets axis state to `Disabled`
- Home sets axis state to `Homing`

These are still scaffold transitions, but they help bridge commands and state machines.

### Alarm model became richer
Alarm now includes:
- Source
- Category
- Severity

AlarmViewModel now projects active alarms into AlarmItemViewModel, and the Alarm tab renders a proper grid instead of only a list of alarm axes.

## Next Recommended Phase

- Surface CommandFeedbackRuntimeState into Dashboard/Debug UI
- Add explicit command progress states (Queued/Running/Succeeded/Failed)
- Let alarm polling populate real alarm records instead of placeholder cleared alarms
- Add fault reset flow that drives SystemState.FaultRecovering
