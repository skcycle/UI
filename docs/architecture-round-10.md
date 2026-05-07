# Architecture Round 10

## Added in Round 10

- Home strategy interface and 4 strategy skeletons
- EtherCatSlaveViewModel
- DashboardViewModel slave list projection
- AxisViewModel visibility for HomeMode / ServoBinding / SoftLimit
- Refined AxisStateMachine and SystemStateMachine transitions

## Key Improvements

### Homing moved one step toward strategy objects
HomeMode enum is still present, but the control layer now has:
- IHomeStrategy
- DefaultHomeStrategy
- LimitThenIndexHomeStrategy
- IndexOnlyHomeStrategy
- SlaveFollowMasterHomeStrategy

This sets up the direction for replacing switch/if logic with dedicated homing strategy objects.

### EtherCAT visibility improved
DashboardViewModel now projects controller slave snapshots into EtherCatSlaveViewModel rows, so the UI layer can render a slave list instead of only aggregate counts.

### Axis metadata is now visible in the UI layer
AxisViewModel now exposes:
- HomeMode
- ServoBinding
- SoftLimitDisplay

This makes the earlier mapping work visible to the presentation layer.

### State machines refined one step
- AxisStateMachine now distinguishes Homing from Standstill when servo is on but the axis is not homed
- SystemStateMachine now considers slave alarms and homing activity

## Next Recommended Phase

- Wire IHomeStrategy resolution into HomingService
- Show EtherCAT slave rows and axis metadata directly in WPF XAML
- Add explicit stopping / fault recovery transitions driven by commands and device feedback
- Introduce richer EtherCAT module categories (servo, IO, coupler, safety)
