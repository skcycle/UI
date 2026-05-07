# Architecture Round 11

## Added in Round 11

- HomingService now resolves and executes IHomeStrategy implementations
- DI registration for all home strategies
- Dashboard WPF tab now shows system/EtherCAT summary and slave list
- Axis Monitor WPF tab now shows HomeMode / ServoBinding / SoftLimit metadata

## Key Improvements

### Strategy-driven homing is now wired into the service layer
HomingService no longer hardcodes `axis.MarkHomed()` directly. It now:
1. Calls the motion controller home command
2. Resolves the matching IHomeStrategy by axis.HomeMode
3. Executes the strategy against the domain axis

This is still scaffold-level behavior, but the service boundary now supports real per-axis homing policies.

### Dashboard now exposes real visible value
The Dashboard tab now shows:
- system state
- EtherCAT network state
- EtherCAT connected flag
- online slave count
- alarm count
- EtherCAT slave table

### Axis metadata is visible in WPF
The Axis Monitor tab now shows:
- HomeMode
- ServoBinding
- SoftLimit

This means the config → domain → viewmodel → XAML chain is now visible end to end.

## Next Recommended Phase

- Make Axis Debug react to selected axis metadata
- Add dedicated EtherCAT monitor tab with richer slave/module diagnostics
- Replace placeholder home strategy behavior with real command sequencing
- Surface current system state color/indicator styling in XAML
