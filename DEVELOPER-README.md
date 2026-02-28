# VAutomationCore - Developer API Reference

This is the developer documentation for the VAutomationCore NuGet package. For end-user documentation, see the main [README.md](./README.md).

## Installation

### NuGet Package
```xml
<PackageReference Include="VAutomationCore" Version="1.0.0" />
```

For prerelease versions:
```xml
<PackageReference Include="VAutomationCore" Version="1.0.1-beta.3" />
```

- NuGet: https://www.nuget.org/packages/VAutomationCore
- Latest prerelease: https://www.nuget.org/packages/VAutomationCore/1.0.1-beta.3

## Quick Start

```csharp
using System.Reflection;
using VampireCommandFramework;
using VAutomationCore.Core.Api;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Services;

public override void Load()
{
    var log = new CoreLogger("Module");
    ServiceInitializer.InitializeLogger(log);
    ServiceInitializer.RegisterInitializer("your_service", YourService.Initialize);
    ServiceInitializer.RegisterValidator("your_service", () => YourService.IsReady);
    ServiceInitializer.InitializeAll(log);

    CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());
}
```

## Core API Surface

### Runtime/Execution
- `CoreExecution`: Safe sync/async execution wrappers with retry support
- `OperationResult` / `OperationResult<T>`: Standard success/failure return model
- `RetryPolicy`: Retry configuration for resilient operations

```csharp
var op = CoreExecution.RunWithRetry(
    () => { /* work */ },
    operationName: "startup-work",
    retryPolicy: RetryPolicy.Default,
    logger: logger
);
```

### Service/State
- `ServiceRegistry`: Singleton registration/resolution for module services
- `EntityMap`: Alias-to-entity reference map used by flow/job execution
- `EntityAliasMapper`: Component alias registration + component query/set helpers

### Flow APIs
- `FlowService`: Register, resolve, and execute action flows
- `FlowDefinition` / `FlowStep`: Flow model types
- `FlowExecutionResult`: Execution outcome with success/failure details

```csharp
FlowService.RegisterActionAlias("heal", "HealSelf");
FlowService.RegisterFlow("startup", new[]
{
    new FlowStep("heal")
}, replace: true);

var map = new EntityMap();
var result = FlowService.Execute("startup", map);
```

### Auth/Console APIs
- `ConsoleRoleAuthService`: Admin/developer auth session handling
- `CoreAuthCommands`: Built-in VCF commands (`.coreauth ...`)
- `CoreJobFlowCommands`: Built-in VCF commands (`.jobs ...`)

### Configuration
- `ConfigService<T>`: Generic config file management with JSON serialization
- `ServiceInitializer`: Startup orchestration for services

## Built-in Commands

### Auth Commands (`.coreauth`)
- `.coreauth login dev <password>` - Developer login
- `.coreauth login admin <password>` - Admin login
- `.coreauth status` - Check auth status
- `.coreauth logout` - End session

### Job Commands (`.jobs`)
- `.jobs flow add/remove/list` - Manage flows
- `.jobs alias self/user/clear/list` - Manage aliases
- `.jobs run <flow>` - Execute flow (requires Developer auth)

## Service Registration Pattern

```csharp
// 1. Create a service class
public class MyService
{
    public static bool IsReady { get; private set; }
    
    public static void Initialize(CoreLogger log)
    {
        log.LogInfo("Initializing MyService...");
        // Initialize logic
        IsReady = true;
    }
}

// 2. Register in Load()
ServiceInitializer.RegisterInitializer("myservice", MyService.Initialize);
ServiceInitializer.RegisterValidator("myservice", () => MyService.IsReady);
ServiceInitializer.InitializeAll(log);
```

## ECS Integration

The framework provides helpers for working with V Rising's ECS system:

- Use predefined `EntityQueries` when possible
- Always dispose `NativeArray` with try-finally blocks
- Check component existence with `EntityManager.HasComponent<T>()`

```csharp
var entities = query.ToEntityArray(Allocator.Temp);
try
{
    foreach (var entity in entities)
    {
        if (!EntityManager.HasComponent<SomeComponent>(entity)) continue;
        // Process entity
    }
}
finally
{
    entities.Dispose();
}
```

## Namespace Reference

| Namespace | Purpose |
|-----------|---------|
| `VAutomationCore.Core` | Core services and utilities |
| `VAutomationCore.Core.Api` | Public API (FlowService, EntityMap, etc.) |
| `VAutomationCore.Core.Services` | Service infrastructure |
| `VAutomationCore.Core.Logging` | Logging abstractions |
| `VAutomationCore.Core.Commands` | Command handlers |

## Related Documentation

- [Jobs and Flows API](./docs/api/Jobs-and-Flows-API.md)
- [Server API](./docs/api/Server-API.md)
- [Player API](./docs/api/Player-API.md)
- [Command API](./docs/api/Command-API.md)
- [Templates and ECS Jobs API](./docs/api/Templates-and-ECS-Jobs-API.md)

## Changelog

### v1.0.1
- **Features**: Added role-based auth and job flow APIs for developers
- **Enhancements**: Added arena ECS commands and zone template management (Bluelock)
- **Improvements**: Updated dependencies and plugin versions
- **Documentation**: Added PR template, CI workflow, and comprehensive test suite

### v1.0.0
- **Initial Release**: Core framework with ECS access, commands, and shared services
- **Features**: 
  - Service registry and dependency injection system
  - Flow and job execution framework with retry policies
  - Entity mapping and alias management
  - Role-based authentication (Admin/Developer)
  - Built-in commands for auth and job management
  - Configuration system with JSON serialization
  - ECS integration helpers

## Related Documentation

- [Jobs and Flows API](./docs/api/Jobs-and-Flows-API.md)
- [Server API](./docs/api/Server-API.md)
- [Player API](./docs/api/Player-API.md)
- [Command API](./docs/api/Command-API.md)
- [Templates and ECS Jobs API](./docs/api/Templates-and-ECS-Jobs-API.md)
- [Castle API](./docs/api/Castle-API.md)
- [Teleport and Actions API](./docs/api/Teleport-and-Actions-API.md)
- [Mount Chunk Template Notes](./docs/api/Mount-Chunk-Template-Notes.md)

## Community and Support

- **Discord**: [V Rising Mods Community](https://discord.gg/68JZU5zaq7)
- **GitHub Issues**: https://github.com/Coyoteq1/D-VAutomationCore-VAutomationCore/issues
- **NuGet Package**: https://www.nuget.org/packages/VAutomationCore
- **Source Code**: https://github.com/Coyoteq1/D-VAutomationCore-VAutomationCore

## Build and Development

### Requirements
- .NET 6.0 SDK or later
- V Rising Dedicated Server files
- BepInEx 5.x

### Build Instructions
```bash
# Clone the repository
git clone https://github.com/Coyoteq1/D-VAutomationCore-VAutomationCore.git

# Build the solution
dotnet build VAutomationCore.sln

# Run tests
dotnet test VAutomationCore.sln
```

### Project Structure
- `Core/`: Core framework services and utilities
- `Services/`: Plugin-specific services
- `Patches/`: Harmony patches for game methods
- `docs/`: API documentation
- `tests/`: Test suite
- `scripts/`: Build and deployment scripts

## Contributing

- Check issues for bugs and feature requests
- Create a feature branch for your changes
- Add tests for new functionality
- Submit a pull request with clear descriptions
- Follow the coding style guidelines
