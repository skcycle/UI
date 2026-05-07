# Architecture Round 5

## Added in Round 5

- Microsoft.Extensions.Hosting based host bootstrap
- Microsoft DI based service registration
- MachineFactory to isolate default domain construction
- MainWindow constructor injection
- App startup / shutdown integrated with generic host lifecycle

## Current Startup Flow

1. App.OnStartup()
2. HostBuilderFactory.BuildHost()
3. Register domain/services/viewmodels/main window in DI
4. Start host
5. Resolve MainWindow from DI
6. MainWindow loads and starts polling service

## Why This Matters

This removes the previous hand-built object graph from window code and makes the project easier to grow toward:
- config-driven construction
- hosted background services
- testable service composition
- later introduction of real logging/config/options patterns

## Next Recommended Phase

- Move PollingHostedService toward IHostedService / BackgroundService
- Add dispatcher-safe UI refresh abstraction
- Add options binding from appsettings.json
- Introduce repositories for axis mapping / alarm / recipe configuration
