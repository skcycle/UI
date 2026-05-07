# Architecture Round 15

## Added in Round 15

- FaultRecoveryService
- Dashboard command feedback projection
- DI wiring for CommandFeedbackRuntimeState and FaultRecoveryService

## Key Improvements

### Command feedback is now visible
DashboardViewModel now consumes CommandFeedbackRuntimeState and exposes the latest few entries as readable strings. The Dashboard tab now includes a Recent Command Feedback area so operator/debug feedback is no longer hidden inside service state.

### Fault recovery path exists as a real service boundary
FaultRecoveryService introduces an explicit service for entering and completing `SystemState.FaultRecovering`. This is still scaffold-level, but it is the right place to attach later controller reset / alarm reset / re-enable sequences.

### Pre-SDK readiness improved
The project now has clearer runtime semantics around:
- commands
- feedback
- fault recovery
- visible UI state

That is important before a real vendor SDK arrives, because it reduces the amount of architecture churn during device integration.

## Next Recommended Phase

- Wire FaultRecoveryService into UI/debug actions
- Add explicit command progress states and possibly a command history grid
- Let alarm polling create/clear domain alarms dynamically
- Do a pre-SDK cleanup pass on IMotionController / ZmcMotionController / facade boundaries
