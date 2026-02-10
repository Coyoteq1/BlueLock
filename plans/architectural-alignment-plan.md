# Architectural Alignment Plan

## Overview

This plan aligns V Rising automation mods (`VAutomationCore`, `VAutoZone`, `VAutoTraps`, `Vlifecycle`) with reference patterns from production-grade mods (`Bloodcraft`, `KindredArenas`) while maintaining the defensive architecture benefits.

## Current State Analysis

### VAutomationCore Current Architecture

| Component | Current Pattern | Reference Pattern | Gap |
|-----------|-----------------|-------------------|-----|
| **Core Access** | `VRCore.Initialize()` defensive | Direct `GetWorld()` with throw | Moderate |
| **Logger** | `Plugin.Logger` per file | Centralized `CoreLogger` with channels | High |
| **Config** | Manual JSON parsing | `ConfigService` with Lazy<T> | High |
| **ECS Queries** | Direct EntityManager | `EntityQueryHelper` utilities | Medium |
| **Commands** | Direct implementation | `CommandBase` with safety wrappers | High |

### VAutoZone Current Architecture

| Component | Current Pattern | Reference Pattern | Gap |
|-----------|-----------------|-------------------|-----|
| **Core Access** | `ArenaVRCore.Initialize()` | UnifiedCore | High |
| **Glow Service** | Static service | Modular service pattern | Medium |
| **Zone Events** | Bridge pattern | Direct system hooks | Medium |
| **Commands** | Direct implementation | CommandBase | High |

### VAutoTraps Current Architecture

| Component | Current Pattern | Reference Pattern | Gap |
|-----------|-----------------|-------------------|-----|
| **Core Access** | `VRCore.Initialize()` | UnifiedCore | High |
| **Trap Service** | Static methods | ServiceFactory pattern | Medium |
| **Spawn Logic** | Inline | Configurable rules | Medium |
| **Commands** | Direct implementation | CommandBase | High |

### Vlifecycle Current Architecture

| Component | Current Pattern | Reference Pattern | Gap |
|-----------|-----------------|-------------------|-----|
| **Singleton** | MonoBehaviour | Static-only service | High |
| **Lifecycle** | Manual state | Event-driven system | Medium |
| **Patches** | Thin delegates | Configurable conditions | Medium |
| **Commands** | Direct implementation | CommandBase | High |

---

## Phase 1: Core Infrastructure Alignment

### 1.1 Consolidate Core Access

**Current:** Multiple `VRCore` classes (`VAutomationCore/Core_VRCore.cs`, `VAutoZone/Core/Arena/VRCore.cs`, `VAutoTraps/Core/VRCore.cs`)

**Target:** Single `UnifiedCore` class

```csharp
// VAutomationCore/Core/UnifiedCore.cs (ALREADY CREATED)
public static class UnifiedCore
{
    private static World? _server;
    private static bool _initialized;
    
    public static World Server => GetWorld("Server") ?? throw new InvalidOperationException("Server world not found");
    public static EntityManager EntityManager => Server.EntityManager;
    
    public static void Initialize()
    {
        if (_initialized) return;
        _server = GetWorld("Server");
        _initialized = true;
    }
}
```

**Migration Steps:**
1. [x] Create `UnifiedCore` class
2. [ ] Update all `VRCore` references to `UnifiedCore`
3. [ ] Remove duplicate `VRCore` files
4. [ ] Update all service initializers

### 1.2 Centralize Logging

**Current:** `Plugin.Logger` scattered across files

**Target:** `CoreLogger` with channels and caller info

```csharp
// VAutomationCore/Core/Logging/CoreLogger.cs (ALREADY CREATED)
public class CoreLogger
{
    private readonly ManualLogSource _staticLog;
    private readonly string _channel;
    
    public void LogInfo(string message, [CallerMemberName] string caller = null);
    public void LogWarning(string message, [CallerMemberName] string caller = null);
    public void LogError(string message, [CallerMemberName] string caller = null);
    public void Exception(Exception ex, [CallerMemberName] string caller = null);
}
```

**Migration Steps:**
1. [x] Create `CoreLogger` class
2. [ ] Replace all `Plugin.Logger.LogInfo()` with `CoreLogger.LogInfo()`
3. [ ] Add channel context to all log calls
4. [ ] Ensure `[CallerMemberName]` is used for debuggability

### 1.3 Standardize Configuration

**Current:** Manual JSON/TOML parsing per service

**Target:** `ConfigService` with Lazy<T> and hot-reload

```csharp
// VAutomationCore/Core/Config/ConfigService.cs (ALREADY CREATED)
public static class ConfigService
{
    private static readonly Lazy<ModConfig> _config = new(LoadConfig);
    
    public static ModConfig Config => _config.Value;
    
    public static void Reload() { /* Hot-reload support */ }
}
```

**Migration Steps:**
1. [x] Create `ConfigService` base class
2. [ ] Migrate VAutoZone `GlowZonesConfig` to ConfigService
3. [ ] Migrate VAutoTraps `TrapConfig` to ConfigService
4. [ ] Migrate Vlifecycle `LifecycleConfig` to ConfigService
5. [ ] Add file watcher for hot-reload

---

## Phase 2: Service Layer Alignment

### 2.1 Service Initialization Pattern

**Current:** Individual `Initialize()` methods per service

**Target:** `ServiceInitializer` with dependency ordering

```csharp
// VAutomationCore/Core/ServiceInitializer.cs (ALREADY CREATED)
public static class ServiceInitializer
{
    private static CoreLogger _log = null!;
    
    public static void InitializeAll(CoreLogger log)
    {
        _log = log;
        _log.Info("Initializing all services...");
        
        // Order matters - dependencies first
        TryInitialize("UnifiedCore", UnifiedCore.Initialize);
        TryInitialize("ConfigService", ConfigService.Initialize);
        TryInitialize("ZoneGlowBorderService", ZoneGlowBorderService.Initialize);
        TryInitialize("ArenaGlowBorderService", ArenaGlowBorderService.Initialize);
        TryInitialize("LifecycleActionHandlers", LifecycleActionHandlers.Initialize);
        
        _log.Info("All services initialized");
    }
}
```

**Migration Steps:**
1. [x] Create `ServiceInitializer` class
2. [ ] Add `Initialize()` methods to all services
3. [ ] Update Plugin.cs to call `ServiceInitializer.InitializeAll()`
4. [ ] Ensure proper initialization order

### 2.2 VAutoZone Service Alignment

**Target Architecture:**

```
VAutoZone/
├── Services/
│   ├── ZoneGlowBorderService.cs     → UnifiedCore + CoreLogger
│   ├── ArenaGlowBorderService.cs    → UnifiedCore + CoreLogger
│   ├── GlowService.cs               → ConfigService integration
│   ├── ArenaZoneConfigLoader.cs     → ConfigService migration
│   ├── PlayerSnapshotService.cs     → UnifiedCore access
│   └── ZoneEventBridge.cs           → Event-driven callbacks
├── Core/
│   └── Arena/VAutoZoneCommands.cs   → CommandBase migration
└── Models/
    ├── GlowZoneEntry.cs             → Config models
    └── GlowZonesConfig.cs           → ConfigService integration
```

**Migration Steps:**
1. [x] Migrate `ZoneGlowBorderService` to UnifiedCore
2. [x] Migrate `ArenaGlowBorderService` to UnifiedCore
3. [x] Migrate `GlowService` to ConfigService
4. [x] Migrate `PlayerSnapshotService` to UnifiedCore
5. [x] Migrate `ZoneEventBridge` to EventBridge pattern
6. [ ] Migrate `VAutoZoneCommands` to CommandBase

### 2.3 VAutoTraps Service Alignment

**Target Architecture:**

```
VAutoTraps/
├── Services/
│   ├── Traps/
│   │   ├── ContainerTrapService.cs  → UnifiedCore + CoreLogger
│   │   ├── TrapZoneService.cs      → ConfigService integration
│   │   └── ChestSpawnService.cs    → UnifiedCore access
│   ├── Rules/
│   │   ├── TrapSpawnRules.cs       → Rule-based spawning
│   │   └── KillStreakRules.cs      → Kill streak tracking
│   └── Triggers/
│       ├── PvPTrigger.cs            → PvP event integration
│       └── TimeTrigger.cs          → Time-based triggers
└── Commands/
    └── Core/TrapCommands.cs         → CommandBase migration
```

**Migration Steps:**
1. [x] Migrate `ContainerTrapService` to UnifiedCore
2. [x] Migrate `ChestSpawnService` to UnifiedCore
3. [x] Create `TrapSpawnRules` with rule engine
4. [x] Create `KillStreakRules` for streak tracking
5. [ ] Migrate `TrapCommands` to CommandBase

### 2.4 Vlifecycle Service Alignment

**Target Architecture:**

```
Vlifecycle/
├── Services/
│   ├── Lifecycle/
│   │   ├── Singleton.cs            → Remove, use static services
│   │   ├── ArenaLifecycleManager.cs → Config-driven rules
│   │   ├── ConnectionEventPatches.cs→ Harmony patches
│   │   └── LifecycleActionHandlers.cs→ UnifiedCore + CoreLogger
│   └── Events/
│       ├── PlayerJoinEvent.cs       → EventBus pattern
│       ├── PlayerLeaveEvent.cs     → EventBus pattern
│       └── PvPStateEvent.cs        → EventBus pattern
└── Commands/
    └── LifecycleCommands.cs        → CommandBase migration
```

**Migration Steps:**
1. [x] Migrate `LifecycleActionHandlers` to UnifiedCore
2. [x] Migrate `ConnectionEventPatches` to thin patches
3. [ ] Remove `Singleton.cs` (use static services instead)
4. [ ] Create `ArenaLifecycleManager` with ConfigService
5. [ ] Migrate `LifecycleCommands` to CommandBase

---

## Phase 3: Command Architecture Alignment

### 3.1 CommandBase Implementation

**Target:** Base class with common functionality

```csharp
// VAutomationCore/Core/Commands/CommandBase.cs (ALREADY CREATED)
public abstract class CommandBase
{
    protected static CoreLogger Log => CoreLogger.ForContext(LogSource);
    protected static EntityManager EM => UnifiedCore.EntityManager;
    
    protected abstract void Execute(ChatCommandContext ctx);
    
    // Permission handling
    protected static bool HasPermission(ChatCommandContext ctx, PermissionLevel required);
    protected static void RequirePermission(ChatCommandContext ctx, PermissionLevel required);
    
    // Cooldown management
    protected static bool IsOnCooldown(string commandName, ulong playerId, TimeSpan cooldown, out TimeSpan remaining);
    protected static void SetCooldown(string commandName, ulong playerId, TimeSpan cooldown);
    protected static void RequireCooldown(string commandName, ulong playerId, TimeSpan cooldown);
    
    // Rich feedback
    protected static void SendFeedback(ChatCommandContext ctx, FeedbackType type, string message);
    protected static void SendSuccess(ChatCommandContext ctx, string message, string? data = null);
    protected static void SendError(ChatCommandContext ctx, string message, string? explanation = null);
    protected static void SendLocation(ChatCommandContext ctx, string label, float3 position);
    protected static void SendCount(ChatCommandContext ctx, string label, int count);
    
    // Safe execution
    protected static void ExecuteSafely(ChatCommandContext ctx, string commandName, Action action);
}
```

**Migration Steps:**
1. [x] Create `CommandBase` abstract class
2. [x] Create `CommandException` for command-specific errors
3. [x] Create `ChatColor` for consistent feedback colors
4. [x] Define `PermissionLevel` enum
5. [x] Define `FeedbackType` enum
6. [ ] Migrate all existing commands to inherit from CommandBase

### 3.2 Command Migration Examples

**Before (Current):**

```csharp
// VAutoZone/Core/Arena/VAutoZoneCommands.cs
[Command("zone_glow_build", "Rebuild all zone glows")]
public static void CmdZoneGlowBuild(CommandContext ctx, bool rebuild = false)
{
    ArenaVRCore.Initialize();
    if (ArenaVRCore.Server == null)
    {
        ctx.Error("ArenaVRCore not initialized");
        return;
    }
    
    ZoneGlowBorderService.BuildAll(rebuild);
    ctx.Reply("Zone glows built successfully");
}
```

**After (Target):**

```csharp
// VAutoZone/Core/Arena/VAutoZoneCommands.cs
public static class VAutoZoneCommands : CommandBase
{
    private const string CommandName = "zone_glow_build";
    
    [Command("zone_glow_build", "Rebuild all zone glows")]
    public static void CmdZoneGlowBuild(ChatCommandContext ctx, bool rebuild = false)
    {
        ExecuteSafely(ctx, CommandName, () =>
        {
            RequirePermission(ctx, PermissionLevel.Admin);
            
            ZoneGlowBorderService.BuildAll(rebuild);
            
            SendSuccess(ctx, "Zone glows built successfully", 
                rebuild ? "(rebuilt)" : "");
            Log.Info($"Zone glows built by {GetPlayerInfo(ctx).Name}");
        });
    }
}
```

### 3.3 Command Audit

| Mod | Command | Status | Priority |
|-----|---------|--------|----------|
| VAutoZone | `zone_glow_build` | Pending | High |
| VAutoZone | `zone_glow_clear` | Pending | High |
| VAutoZone | `zone_glow_status` | Pending | High |
| VAutoZone | `zone_glow_rotate` | Pending | Medium |
| VAutoZone | `arena_glow_build` | Pending | High |
| VAutoZone | `arena_glow_clear` | Pending | High |
| VAutoTraps | `trap_build` | Pending | High |
| VAutoTraps | `trap_clear` | Pending | High |
| VAutoTraps | `trap_spawn` | Pending | High |
| VAutoTraps | `trap_status` | Pending | Medium |
| VAutoTraps | `chest_spawn` | Pending | High |
| Vlifecycle | `lifecycle_status` | Pending | Medium |
| Vlifecycle | `lifecycle_pause` | Pending | Medium |
| Vlifecycle | `lifecycle_resume` | Pending | Medium |

---

## Phase 4: ECS Pattern Alignment

### 4.1 Entity Query Utilities

**Target:** Helper methods for common queries

```csharp
// VAutomationCore/Core/ECS/EntityQueryHelper.cs (ALREADY CREATED)
public static class EntityQueryHelper
{
    public static EntityQuery CreateQuery(
        ComponentType[] allTypes,
        ComponentType[]? anyTypes = null,
        ComponentType[]? noneTypes = null);
    
    public static NativeArray<Entity> QueryEntities(
        EntityQuery query,
        Allocator allocator = Allocator.Temp);
    
    public static int QueryCount(EntityQuery query);
    
    public static bool TryGetSingleton<T>(out T component) where T : struct, IComponentData;
}
```

**Migration Steps:**
1. [x] Create `EntityQueryHelper` class
2. [x] Create `EntityExtensions` for common operations
3. [ ] Replace direct EntityManager queries with helpers
4. [ ] Ensure proper Allocator usage (Temp vs TempJob)

### 4.2 Native Collection Safety

**Reference Pattern (Bloodcraft):**

```csharp
// Bloodcraft-style safe query
var entities = query.ToEntityArray(Allocator.Temp);
try
{
    foreach (var entity in entities)
    {
        // Process entity
    }
}
finally
{
    entities.Dispose();
}
```

**Current Issues:**
- Missing try/finally blocks
- Using wrong allocator (TempJob in single-frame context)
- Not checking query validity

**Fixes Required:**
1. [ ] Wrap all `ToEntityArray` calls in try/finally
2. [ ] Use `Allocator.Temp` for single-frame operations
3. [ ] Add null checks before query operations
4. [ ] Log query failures with context

---

## Phase 5: Harmony Patch Alignment

### 5.1 Thin Patch Pattern

**Target:** Minimal patches that delegate to services

```csharp
// Vlifecycle/Services/Lifecycle/ConnectionEventPatches.cs (ALREADY MIGRATED)
public static class ConnectionEventPatches
{
    [HarmonyPatch(typeof(ServerGameManager), nameof(ServerGameManager.OnPlayerConnected))]
    [HarmonyPrefix]
    public static void OnPlayerConnected_Prefix(Player player)
    {
        try
        {
            LifecycleActionHandlers.OnPlayerJoin(player);
        }
        catch (Exception ex)
        {
            Log?.Exception(ex, nameof(OnPlayerConnected_Prefix));
        }
    }
}
```

### 5.2 Patch Safety Checklist

| Patch | File | Status | Safety Check |
|-------|------|--------|--------------|
| OnPlayerConnected | ConnectionEventPatches.cs | Migrated | try/catch added |
| OnPlayerDisconnected | ConnectionEventPatches.cs | Migrated | try/catch added |
| PrefabCollection.OnUpdate | Pending | Medium | None |
| PlayerZoneSystem.OnUpdate | Pending | Medium | None |
| ContainerSystem.OnUpdate | Pending | Medium | None |

**Safety Requirements:**
1. [ ] All patches wrap handler calls in try/catch
2. [ ] Patches log exceptions with caller context
3. [ ] Patches never throw (always graceful failure)
4. [ ] Prefix/postfix returns void unless changing control flow

---

## Phase 6: Testing & Validation

### 6.1 Integration Tests

**Test Categories:**

1. **Core Initialization**
   - [ ] UnifiedCore initializes on server start
   - [ ] ConfigService loads before services
   - [ ] ServiceInitializer orders dependencies correctly

2. **Service Functionality**
   - [ ] ZoneGlowBorderService builds glows
   - [ ] ArenaGlowBorderService manages arena borders
   - [ ] LifecycleActionHandlers responds to join/leave
   - [ ] ContainerTrapService spawns traps

3. **Command Execution**
   - [ ] Admin commands execute with permission check
   - [ ] Cooldowns prevent spam
   - [ ] Error handling provides user feedback
   - [ ] Logging captures all executions

4. **ECS Operations**
   - [ ] Entity queries return correct results
   - [ ] Native collections dispose properly
   - [ ] Component data reads/writes succeed

### 6.2 Performance Benchmarks

**Key Metrics:**

| Operation | Target | Current | Status |
|-----------|--------|---------|--------|
| UnifiedCore access | < 1ms | TBD | Pending |
| ConfigService load | < 100ms | TBD | Pending |
| Glow border build | < 500ms | TBD | Pending |
| Command execution | < 10ms | TBD | Pending |

---

## Implementation Order

### Priority 1: Critical Path

1. [x] Create UnifiedCore
2. [x] Create CoreLogger
3. [x] Create ConfigService
4. [x] Create ServiceInitializer
5. [x] Create CommandBase
6. [x] Migrate ZoneGlowBorderService
7. [x] Migrate ArenaGlowBorderService
8. [x] Migrate LifecycleActionHandlers
9. [x] Update VAutoZone Plugin.cs
10. [x] Update VAutoTraps Plugin.cs
11. [x] Update Vlifecycle Plugin.cs

### Priority 2: Feature Completion

12. [ ] Migrate all commands to CommandBase
13. [ ] Migrate GlowService to ConfigService
14. [ ] Migrate ContainerTrapService to UnifiedCore
15. [ ] Migrate ChestSpawnService to UnifiedCore
16. [ ] Create KillStreakRules
17. [ ] Remove deprecated VRCore files

### Priority 3: Polish

18. [ ] Add hot-reload to ConfigService
19. [ ] Add permission levels to all commands
20. [ ] Add cooldowns to frequently-used commands
21. [ ] Add comprehensive logging to all services
22. [ ] Create unit tests for Core classes
23. [ ] Document API with XML comments

---

## Rollback Plan

If issues arise, rollback procedure:

1. **Config Issue:** Revert ConfigService changes, use old JSON parsing
2. **Core Access Issue:** Keep VRCore alongside UnifiedCore during transition
3. **Command Issue:** Keep old command implementations until new ones are tested
4. **Patch Issue:** Disable individual patches via ConfigService

---

## Success Criteria

### Functional Requirements

- [ ] All existing commands work identically
- [ ] No new null reference exceptions
- [ ] Config hot-reload works without restart
- [ ] Commands have proper permission checks
- [ ] Cooldowns prevent command spam

### Non-Functional Requirements

- [ ] Centralized logging reduces log noise
- [ ] Initialization order is deterministic
- [ ] Code is easier to understand and maintain
- [ ] ECS operations are memory-safe
- [ ] Patches never crash the server

### Reference Alignment

- [ ] Core access follows Bloodcraft patterns
- [ ] Logging follows KindredArenas patterns
- [ ] Commands follow KindredCommands patterns
- [ ] Config follows Bloodcraft patterns
- [ ] ECS follows Bloodcraft patterns

---

## Timeline

| Phase | Estimated Time | Dependencies |
|-------|---------------|--------------|
| Phase 1: Core Infrastructure | 2-4 hours | None |
| Phase 2: Service Layer | 4-6 hours | Phase 1 |
| Phase 3: Commands | 2-4 hours | Phase 1, 2 |
| Phase 4: ECS Patterns | 1-2 hours | Phase 1 |
| Phase 5: Harmony Patches | 1-2 hours | Phase 1, 2 |
| Phase 6: Testing | 2-4 hours | All phases |

**Total Estimated Time:** 12-22 hours

---

## Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Breaking changes to game API | High | Medium | Keep old VRCore during transition |
| Memory leaks in ECS | High | Low | Code review + testing |
| Config migration issues | Medium | Low | Test with sample configs |
| Command permission bypass | High | Low | Security audit after migration |
| Performance regression | Medium | Low | Benchmark before/after |

---

## References

- **Bloodcraft Architecture:** `temp_reference/Bloodcraft/Core.cs`, `Bloodcraft/Services/*.cs`
- **KindredArenas Architecture:** `temp_reference/KindredArenas/Core.cs`, `KindredArenas/ECSExtensions.cs`
- **KindredCommands:** Reference command patterns in `KindredCommands/Commands/`
- **V Rising ECS Docs:** Unity Entities documentation for component patterns

---

## Document Metadata

- **Version:** 1.0.0
- **Created:** 2024-02-10
- **Author:** V Rising Mod Team
- **Status:** In Progress
- **Next Review:** After Phase 1 completion
