# Architecture Round 12

## Added in Round 12

- HomeExecutionPlan
- IHomeStrategy.BuildPlan(...) semantics
- EtherCatMonitorViewModel
- Axis Debug metadata panel in WPF
- Dedicated EtherCAT Monitor tab
- Dashboard card-style status panels

## Key Improvements

### Homing strategies now express execution semantics
Each homing strategy now builds a HomeExecutionPlan containing placeholder step text. This is still scaffold-level, but the strategies now carry both:
- behavior hook (ExecuteAsync)
- execution semantics / step intent (BuildPlan)

That is a better foundation for future real homing command sequencing.

### Axis Debug is now linked to selected-axis metadata
AxisDebugViewModel now reads the selected axis from Machine and exposes:
- SelectedAxisHomeMode
- SelectedAxisServoBinding
- SelectedAxisSoftLimit

The debug page now has a side panel showing those fields.

### EtherCAT monitoring has its own tab
A dedicated EtherCAT Monitor tab now renders the slave list directly instead of relying only on the Dashboard.

### Dashboard became more visual
The Dashboard status summary was upgraded from plain text stacks to card-like bordered sections for faster operator scanning.

## Next Recommended Phase

- Make AxisDebug selected axis reactive with property change notification
- Render HomeExecutionPlan steps in the debug UI
- Add EtherCAT slave categories / module types / fault text
- Introduce command feedback and transition states such as Stopping / FaultRecovering
