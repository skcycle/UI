# Windows Build and Debug Notes

## Current Environment Limitation

The current agent environment does not have `dotnet` installed and is Linux-based, so WPF projects and Windows-native DLL loading cannot be compiled/executed here.

## Fixes Applied Before Windows Validation

- Added ZMC DLLs and ini file to `MotionControl.Device.Zmc.csproj` with `CopyToOutputDirectory=PreserveNewest`
- Replaced placeholder native bridge with first-pass `DllImport` bindings
- Corrected disconnect state handling in `ZmcMotionController`
- Added missing `System.Collections.Generic` import for `AxisDebugViewModel`

## What To Validate In VS2022 On Windows

1. Restore NuGet packages
2. Build solution
3. Confirm output directory contains:
   - `zauxdll.dll`
   - `zmotion.dll`
   - `zmotiondll.ini`
4. Run app and validate:
   - controller connect
   - axis feedback polling
   - enable/disable
   - move absolute
   - jog
   - stop
   - home

## Most Likely Remaining Runtime Issues

- command strings may need controller-specific adjustment
- axis status bit decoding needs real hardware validation
- DLL search path / native dependency loading may need minor tuning on Windows
- some SDK functions may require additional parameter initialization before movement
