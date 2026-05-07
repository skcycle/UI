# ZMC SDK Integration Plan

## Imported SDK Assets

The following files were imported from the provided repository into `vendor/zmc-sdk/`:

- `zauxdll.dll`
- `zmotion.dll`
- `zmotiondll.ini`
- `zauxdll2.h`
- `zmotion.h`
- `Zmcaux.cs`

## Primary Integration Points

### 1. `src/MotionControl.Device.Zmc/Native/ZmcNativeApi.cs`
This file now contains the first batch of real `DllImport` declarations mapped from the provided SDK wrapper/header set.

### 2. `src/MotionControl.Device.Zmc/Native/ZmcAxisNativeFacade.cs`
This facade now owns:
- Ethernet connect / disconnect
- command execution
- axis move / jog / stop / home calls
- axis feedback reads
- IO reads

This is the main native boundary that should continue absorbing vendor-specific details.

### 3. `src/MotionControl.Device.Zmc/Controllers/ZmcMotionController.cs`
This controller now uses the native facade for:
- connect / disconnect
- home
- move
- stop
- axis feedback polling

### 4. `src/MotionControl.Device.Zmc/Translators/ZmcStatusTranslator.cs`
This translator now turns native feedback snapshots into domain/application-facing `AxisFeedback`.

## What Still Needs Refinement After Real Validation

Because the current environment cannot run the Windows DLLs, the following still require real-device or Windows validation:

- exact axis status bit decoding
- servo/alarm/limit bit mapping
- proper enable/disable command choice if project command strings need adjustment
- homing command semantics (`DATUM(...)`) validation
- EtherCAT slave/module status direct read strategy
- alarm reset mapping

## Recommended Next Step On Windows

1. Place `vendor/zmc-sdk/*.dll` alongside the app output or configure copy-to-output.
2. Build in Visual Studio 2022 on Windows.
3. Validate:
   - connect
   - single-axis feedback polling
   - enable/disable
   - absolute move
   - jog
   - home
   - stop
4. Adjust translator and facade commands according to actual controller return codes / status words.

## Architectural Status

The project is now at the point where future SDK work should be mostly concentrated in:
- `ZmcNativeApi`
- `ZmcAxisNativeFacade`
- `ZmcMotionController`
- `ZmcStatusTranslator`

That means the upper layers are largely ready for real SDK-backed integration.
