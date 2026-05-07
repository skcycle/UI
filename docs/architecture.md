# Architecture

## Solution Structure

- MotionControl.App: WPF application entry
- MotionControl.Presentation: ViewModels / Commands / UI-facing models
- MotionControl.Application: use-case orchestration
- MotionControl.Domain: axis, machine, group domain model
- MotionControl.Control: motion and homing control services
- MotionControl.Device.Abstractions: hardware abstraction interfaces
- MotionControl.Device.Zmc: ZMC432EtherCAT adapter layer
- MotionControl.Infrastructure: logging / config / persistence
- MotionControl.Diagnostics: safety and diagnostics
- MotionControl.Contracts: shared contracts

## Current Scaffold Status

This is an initial engineering scaffold. Next phase should focus on:
1. Wiring dependency injection
2. Completing ZMC native API wrapper
3. Building controller polling service
4. Implementing WPF monitoring and debug pages
5. Adding state machines and safety interlocks
