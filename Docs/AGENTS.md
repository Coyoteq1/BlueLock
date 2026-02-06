# AGENTS.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

VAutomationEvents is a **V Rising dedicated server mod** built with BepInEx IL2CPP that provides automation, arena management, PvP lifecycle systems, and extensive gameplay customization. The plugin integrates deeply with Unity's Entity Component System (ECS) used by V Rising's ProjectM framework.

**Target Framework:** .NET 6.0  
**Build System:** MSBuild (.csproj)  
**Plugin Framework:** BepInEx Unity IL2CPP 6.0.0-be.733  
**Game Version:** V Rising 1.1+  

## Build Commands

### Standard Build
```powershell
dotnet build VAutomationevents.csproj
```
Builds and automatically deploys to: `C:\Program Files (x86)\Steam\VRising_Server\BepInEx\plugins`

### Clean Build
```powershell
dotnet clean
dotnet build
```

### Deploy Without Rebuild
```powershell
dotnet build /t:CopyToBepInEx
```

### Notes on Build Process
- The build automatically creates timestamped backups in `C:\Program Files (x86)\Steam\VRising_Server\Backups\VAutomationEvents\`
- JSON config files are automatically copied to `BepInEx\config`
- All DLLs in the output directory are deployed to the plugins folder
- Custom MSBuild targets handle deployment (`CopyToBepInEx`, `CleanBepInEx`)

## Architecture

### ECS-First Design Philosophy

This codebase follows a **strict ECS (Entity Component System) architecture**. See CONTRIBUTING.md for the complete design directive. Critical principles:

1. **All runtime state lives in ECS components** - Never use static dictionaries, lists, or singleton C# objects for gameplay state
2. **Systems are pure orchestrators** - They read components, detect transitions, and enqueue structural changes via `EntityCommandBuffer`
3. **Components are data-only** - No methods, no logic, no service references
4. **Event entities are immutable** - ProjectM events like `DamageEvent`, `TeleportRequest` are separate entities; modify the event or destroy it, never remove components from the target entity

### Core Directory Structure

```
Core/
  Components/          # ECS component definitions (ZoneComponents.cs)
  Harmony/             # Harmony patches (ServerShutdownInterceptor.cs)
  Lifecycle/           # PVP lifecycle systems
  Logging/             # Logging infrastructure (VAutoLogger.cs, LogComponents.cs)
  Patterns/            # Singleton and ServiceManager patterns
  Configuration/       # Config loaders
  Constants.cs         # Global constants
  ECSServiceManager.cs # IL2CPP-compatible ECS service orchestrator
  UnifiedRuntimeSystem.cs # Lazy-initialized runtime system

Services/
  Systems/             # ECS-compliant systems (ZoneTrackingService, AutomationProcessingService, etc.)
  Commands/            # Command handlers (EndGameKitCommandSystem, SpawnCommandSystem)
  World/               # World services (TeleportService, WorldSpawnService, etc.)
  Portal/              # Portal management
  Zones/               # Zone/glow zone services
  Config/              # Config services

EndGameKit/            # Loadout system for arena entry/exit
  Services/            # Equipment, consumable, jewel, stat services
  Configuration/       # Kit profile definitions
  Helpers/             # Player and equipment helpers

Commands/              # Chat command implementations (most commented out; system is evolving)

Data/                  # Runtime data structures
```

### Key System Integration Points

When creating new systems, follow the **ECS System Integration Plan** (docs/ECS_SYSTEM_INTEGRATION_PLAN.md):

- **General gameplay automation**: `[UpdateInGroup(typeof(ProjectM.UpdateGroup))]`
- **Combat/PvP logic**: Use `[UpdateBefore(typeof(ProjectM.Gameplay.Systems.DealDamageSystem))]` for interception
- **Spawn customization**: `[UpdateAfter(typeof(ProjectM.Gameplay.Systems.SpawnCharacterSystem))]`
- **Zone detection**: Separate spatial detection from transition effects (ZoneDetectionSystem vs ZoneTransitionSystem)

### Critical V Rising ECS Rules

1. **Structural Changes**: NEVER call `EntityManager.Add/RemoveComponent` inside query loops. Always use an `EntityCommandBuffer`:
   ```csharp
   var ecb = World.GetExistingSystemManaged<BeginSimulationEntityCommandBufferSystem>();
   ecb.AddComponent<MyComponent>(entity);
   ```

2. **Event Component Handling**: ProjectM event entities (DamageEvent, TeleportRequest) must be handled by modifying or destroying the event entity, not by removing components

3. **Native Collections**: Use `ToEntityArray(Allocator.Temp)` and dispose in `try/finally`. Never use `Allocator.TempJob` in ProjectM ECS

4. **Query Safety**: Always use system-defined queries when available

## Zone & Arena Systems

Zones are **data entities**, not logic containers:

- Every zone is an entity with `ZoneBoundary` component
- Zone types: MainArena, PvPArena, GlowZone, SafeZone, Custom
- Zone membership tracked via `DynamicBuffer<ZoneMembershipElement>`
- A player may have multiple zone memberships only if types differ
- Effects applied **on transitions only**, never per-frame

Configuration: `config/VAuto.Arena/arena_zones.json`

## Configuration System

### Primary Config Files

- **BepInEx Config**: `BepInEx/config/VAuto.cfg` (auto-generated from Plugin.cs config bindings)
- **Advanced JSON**: `BepInEx/config/VAuto-Advanced-Config.json` (services, commands, lifecycle)
- **Arena Zones**: `config/VAuto.Arena/arena_zones.json`
- **PVP Items**: `pvp_item.json` (lifecycle actions)
- **Glow Zones**: `glow_zones.json` (visual zone markers)
- **EndGameKit**: `EndGameKit.json` (loadout profiles)

### Hot-Reload Support

The system supports hot-reloading of JSON configs without server restart (every 5 seconds). See Plugin.cs `CheckForConfigChanges()`.

## Command System

Commands use **VampireCommandFramework** and are registered via `[Command]` attributes:

```csharp
[Command("mycommand", description: "My command description", adminOnly: true)]
public void MyCommand(ChatCommandContext ctx, string arg1, int arg2)
{
    // Implementation
}
```

Command registration happens automatically in `Plugin.Load()` via `CommandRegistry.RegisterAll()`.

Many command files are commented out in the .csproj as the system transitions to ECS-compliant patterns.

## EndGameKit System

EndGameKit manages player loadouts for arena entry/exit with strict execution order:

1. Validate player
2. Equip gear
3. Apply consumables
4. Attach jewels
5. Apply stat extensions
6. Mark applied

Configuration: `EndGameKit.json` with profiles containing equipment slots, consumables, jewels, and stat modifiers.

## Logging

Use the centralized logger:
```csharp
Plugin.Log.LogInfo("[SYSTEM] Message");
Plugin.Log.LogWarning("[SYSTEM] Warning");
Plugin.Log.LogError("[SYSTEM] Error");
```

Logging should be:
- Transition-based (not per-frame)
- Debug-gated where possible
- Prefixed with `[SYSTEM_NAME]` for clarity

## Testing & Validation

**No automated test framework is currently in place.** Testing is done in-game on the V Rising server.

### Manual Testing Workflow
1. Build the project
2. Plugin auto-deploys to server plugins folder
3. Start V Rising server
4. Monitor BepInEx log: `BepInEx/LogOutput.log`
5. Test commands in-game via chat (prefix with `.`)
6. Verify ECS behavior via debug commands

### Common Test Commands
- `.services` - List service status
- `.zones` - List active zones
- `.pvp-status` - PVP lifecycle status
- `.endgamekit apply <profile>` - Apply kit profile

## Development Constraints

### IL2CPP Limitations
- Standard C# patterns may not work (reflection, delegates, generics)
- Use Il2CppInterop types when interfacing with game code
- Avoid LINQ in hot paths
- Prefer arrays over lists for large collections

### Performance Constraints
- No O(players × zones) logic in single systems
- No per-frame allocations in hot paths
- Frame budget discipline (MaxEntitiesPerFrame config)

### Multiplayer Safety
- All logic must be deterministic server-side
- No reliance on client prediction state
- All authoritative decisions from ECS state

## Project-Specific Patterns

### Service Registration
Services are registered via `ECSServiceManager` in Plugin.cs:
```csharp
_ecsServiceManager = new ECSServiceManager(this, entityManager);
_ecsServiceManager.Initialize();
```

### Harmony Patching
Harmony patches are in `Core/Harmony/` and should only signal ECS (add components/flags), never contain gameplay logic.

### Version Gating
Use `SemanticVersion` and `VersionGate` (Core/) for compatibility checks with game versions.

## Common Workflows

### Adding a New Zone Type
1. Add to `ZoneType` enum (Core/Components/ZoneComponents.cs)
2. Update zone detection logic (Services/Systems/ZoneTrackingService.cs)
3. Create transition system if special effects needed
4. Update arena_zones.json with example
5. Document in docs/CONFIGURATION_REFERENCE.md

### Adding a New Command
1. Create command class or add to existing (Services/Commands/)
2. Add `[Command]` attribute with proper permissions
3. Implement using ECS patterns (no direct entity manipulation)
4. Update COMMANDS_LOGGING_CONFIG_PLAN.md if logging needed
5. Uncomment in VAutomationevents.csproj `<Compile Include=...>`

### Adding a New ECS System
1. Create system class in Services/Systems/
2. Add `[UpdateInGroup]` and `[UpdateBefore/After]` attributes
3. Register in ECSServiceManager.cs
4. Follow CONTRIBUTING.md component/system separation rules
5. Test frame budget impact

## Scripts & Utilities

### Generate-Prefabs.ps1
Downloads and generates C# PrefabGUID definitions from decaprime/VRising-Modding repository:
```powershell
.\scripts\Generate-Prefabs.ps1
```
Output: `Data/Prefabs.cs`

## Documentation

Comprehensive documentation in `docs/`:
- **ECS_SYSTEM_INTEGRATION_PLAN.md** - System placement strategy
- **CODE_REVIEW_REPORT.md** - Current compliance assessment
- **REFACTORING_PLAN.md** - Phased refactoring roadmap
- **CONTRIBUTING.md** - ECS design directive (MANDATORY reading)
- **CONFIGURATION_REFERENCE.md** - All config schemas
- **COMMAND_REFERENCE.md** - Command documentation

## Git Workflow

Current branch: `refactor/ecs-compliance`

The project is actively refactoring towards full ECS compliance. Many legacy systems are commented out in the .csproj as they're migrated.

## Key Dependencies

- **BepInEx.Unity.IL2CPP** (6.0.0-be.733) - Plugin framework
- **VampireCommandFramework** - Chat command system
- **Unity.Entities** - ECS framework
- **Unity.Mathematics** - Math library (float3, quaternion, etc.)
- **ProjectM** - V Rising game assemblies
- **0Harmony** - Runtime patching

All dependencies are referenced from `libs/` directory (not in repo, must be provided by V Rising installation).

## Performance Considerations

- **MaxEntitiesPerFrame**: 1000 (configurable)
- **Zone detection**: Spatial queries optimized with radius checks
- **Hot paths**: Avoid allocations, prefer stack/temp allocators
- **Memory cleanup**: Configurable interval (default 600s)

## Security Notes

- Admin commands require `RequireAdminForDangerousCommands` config
- Command logging enabled by default
- Anti-cheat validation available
- Never expose secrets in logs or commands
