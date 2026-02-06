# V Rising Modding: Debugging & Ecosystem Integration Guide

**Project:** VAutomationEvents  
**Created:** 2026-02-02  
**Version:** 1.0.0  
**Purpose:** Comprehensive debugging methodologies, search patterns, and cross-mod compatibility strategies for VAutomationEvents within the V Rising modding ecosystem

---

## Table of Contents

1. [Active Project Analysis](#active-project-analysis)
2. [BepInEx IL2CPP Debugging Methodologies](#bepinex-il2cpp-debugging-methodologies)
3. [ECS Integration Patterns & Bottlenecks](#ecs-integration-patterns--bottlenecks)
4. [V Rising Mod Ecosystem Integration](#v-rising-mod-ecosystem-integration)
5. [Search Patterns for Troubleshooting](#search-patterns-for-troubleshooting)
6. [Cross-Mod Compatibility Approaches](#cross-mod-compatibility-approaches)
7. [Actionable Recommendations](#actionable-recommendations)

---

## Active Project Analysis

### Currently Compiled Files (from .csproj)

Based on [`VAutomationevents.csproj`](../VAutomationevents.csproj) analysis, the following files are **actively compiled**:

#### Core Systems (ACTIVE)
```
✅ Plugin.cs                                    # Main entry point
✅ MyPluginInfo.cs                              # Plugin metadata
✅ Core/VRCore.cs                               # Core runtime
✅ Core/ServiceContainer.cs                     # Service DI container
✅ Core/PluginContext.cs                        # Shared plugin context
✅ Core/SemanticVersion.cs                      # Version parsing
✅ Core/VersionPolicy.cs                        # Version compatibility rules
✅ Core/VersionGate.cs                          # Version gating logic
✅ Core/Logging/VAutoLogger.cs                  # Centralized logging
✅ Data/Prefabs.cs                              # Generated prefab GUIDs (4000+ entries)
✅ JSONConverters.cs                            # JSON serialization
```

#### Service Layer (ACTIVE)
```
✅ Core/Services/Data/GameDataModels.cs         # Data DTOs
✅ Core/Services/Data/ChestRewardTypes.cs       # Chest reward definitions
✅ Core/Services/Persistence/DataPersistenceService.cs
✅ Core/Services/Persistence/QueueService.cs
✅ Core/Services/Persistence/SessionService.cs
✅ Core/Services/Rules/TrapSpawnRules.cs        # Trap spawning logic
✅ Core/Services/Traps/TrapZoneService.cs       # Trap zone management
✅ Core/Services/Traps/ChestSpawnService.cs     # Chest spawning
✅ Core/Services/Announcement/AnnouncementService.cs
```

#### Lifecycle Services (ACTIVE)
```
✅ Services/Lifecycle/ArenaStateStore.cs
✅ Services/Lifecycle/ArenaEnterService.cs
✅ Services/Lifecycle/ArenaExitService.cs
✅ Services/Lifecycle/SpellbookService.cs
✅ Services/Lifecycle/BuffBoonsService.cs
```

#### Zone Systems (ACTIVE)
```
✅ Services/Zones/ZoneModels.cs
✅ Services/Zones/ZoneRegistry.cs
✅ Services/Zones/ZoneTrackingSystem.cs
```

#### Glow Systems (ACTIVE)
```
✅ Services/Glow/GlowModels.cs
✅ Services/Glow/GlowManager.cs
✅ Services/Glow/GlowZoneService.cs
```

#### Commands (ACTIVE)
```
✅ Commands/Arena/ArenaEnterExitCommands.cs
✅ Commands/Core/GlowCommands.cs
✅ Commands/Core/ZoneCommands.cs
✅ Commands/Core/PlayerCommands.cs
✅ Commands/Core/KillStreakCommands.cs          # Kill streak tracking
✅ Commands/Core/VBloodRepairCommands.cs        # V Blood repair commands
✅ Commands/Core/ZoneTrapCommands.cs
✅ Commands/Core/SpawnChestCommands.cs
✅ Commands/Core/AnnounceCommands.cs
```

#### Harmony Patches (ACTIVE)
```
✅ Core/Harmony/RepairVBloodProgressionSystemPatch.cs  # V Blood repair patch
✅ Core/Harmony/DeathEventHook.cs               # Death event interception
```

### Commented Out (ECS Compatibility Issues)

**Critical Finding:** Many ECS systems are commented out due to Unity.Entities compilation errors:

```
// Unity.Entities compatibility issues (Lines 260-290)
❌ Core/Components/Lifecycle/LifecycleComponents.cs
❌ Core/Components/Lifecycle/RepairComponents.cs
❌ Core/Components/Lifecycle/VBloodComponents.cs
❌ Core/Components/Lifecycle/SpellbookComponents.cs
❌ Core/Components/Lifecycle/VBloodSnapshotComponents.cs
❌ Core/Components/KillStreakComponents.cs
❌ Core/Extensions/ECSExtensions.cs
❌ Core/Systems/KillStreakTrackingSystem.cs
❌ Services/Systems/SpellbookOpenSystem.cs
❌ Services/Systems/SpellbookRestoreSystem.cs
❌ Services/Systems/NotificationDispatchSystem.cs
❌ Services/Systems/Lifecycle/LifecycleZoneTransitionSystem.cs
❌ Services/Systems/Lifecycle/LifecycleAutoEnterSystem.cs
❌ Services/Systems/Lifecycle/PlayerLifecycleInitSystem.cs
❌ Services/Systems/Lifecycle/AutoRepairSystem.cs
❌ Services/Systems/Lifecycle/VBloodUnlockSystem.cs
❌ Services/Systems/Lifecycle/LifecycleSpellbookSystem.cs
❌ Services/Systems/Lifecycle/VBloodSnapshotRestoreSystems.cs
```

#### Architectural Gap

**Problem:** The project aims for "strict ECS compliance" (per [`CONTRIBUTING.md`](../CONTRIBUTING.md)) but has **non-ECS workarounds** due to compilation failures:

1. **Static Dictionaries in Commands** - [`KillStreakCommands.cs:17-18`](../Commands/Core/KillStreakCommands.cs:17-18):
   ```csharp
   private static readonly ConcurrentDictionary<ulong, int> _playerStreaks = new();
   private static readonly ConcurrentDictionary<ulong, DateTime> _lastKillTime = new();
   ```
   ⚠️ **Violates ECS Principle #1:** "All runtime state lives in ECS components"

2. **Harmony Patches Without ECS Integration** - [`DeathEventHook.cs:46`](../Core/Harmony/DeathEventHook.cs:46):
   ```csharp
   TrapSpawnRules.OnPlayerDeath(killerPlatformId, victimPlatformId);
   ```
   ⚠️ Calls static method instead of ECS system

3. **Service-Based Architecture** - Many "services" are static classes, not ECS systems

---

## BepInEx IL2CPP Debugging Methodologies

### 1. Log-Based Debugging (Primary Method)

**Why:** IL2CPP doesn't support traditional .NET debuggers. BepInEx log is the main debugging tool.

#### Strategic Logging Pattern

```csharp
// Core/Logging/VAutoLogger.cs pattern
public static class DebugLog
{
    private const string PREFIX = "[VAuto]";
    
    // System lifecycle
    public static void SystemInit(string systemName)
        => Plugin.Log.LogInfo($"{PREFIX}[INIT] {systemName} initialized");
    
    public static void SystemUpdate(string systemName, int entityCount)
        => Plugin.Log.LogDebug($"{PREFIX}[UPDATE] {systemName} processing {entityCount} entities");
    
    // Entity operations
    public static void EntityCreated(string componentName, Entity entity)
        => Plugin.Log.LogInfo($"{PREFIX}[ENTITY] {componentName} created on {entity}");
    
    public static void ComponentAdded(Entity entity, string componentName)
        => Plugin.Log.LogDebug($"{PREFIX}[COMPONENT] Added {componentName} to {entity}");
    
    // State transitions
    public static void StateTransition(Entity entity, string from, string to)
        => Plugin.Log.LogInfo($"{PREFIX}[STATE] {entity}: {from} → {to}");
    
    // Errors with context
    public static void QueryFailed(string systemName, Exception ex)
        => Plugin.Log.LogError($"{PREFIX}[QUERY_ERROR] {systemName}: {ex.Message}\n{ex.StackTrace}");
}
```

#### Log Filtering Strategies

```powershell
# Find all system initializations
Select-String -Path "BepInEx\LogOutput.log" -Pattern "\[INIT\]"

# Track entity lifecycle
Select-String -Path "BepInEx\LogOutput.log" -Pattern "\[ENTITY\]"

# Find errors in specific system
Select-String -Path "BepInEx\LogOutput.log" -Pattern "KillStreakTracking.*ERROR"

# Performance bottlenecks
Select-String  -Path "BepInEx\LogOutput.log" -Pattern "Frame budget exceeded"
```

### 2. Entity Query Debugging

**Problem:** ECS queries fail silently when component requirements aren't met.

#### Debug Query Pattern

```csharp
protected override void OnUpdate()
{
    // Before query
    Plugin.Log.LogDebug($"[{Name()}] Query executing...");
    
    var entities = _query.ToEntityArray(Allocator.Temp);
    Plugin.Log.LogDebug($"[{Name()}] Found {entities.Length} entities");
    
    try
    {
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            
            // Validate component existence
            if (!EntityManager.Exists(entity))
            {
                Plugin.Log.LogWarning($"[{Name()}] Entity {entity} destroyed during iteration");
                continue;
            }
            
            if (!EntityManager.HasComponent<MyComponent>(entity))
            {
                Plugin.Log.LogError($"[{Name()}] Entity {entity} missing MyComponent!");
                continue;
            }
            
            // Process entity
            var comp = EntityManager.GetComponentData<MyComponent>(entity);
            Plugin.Log.LogDebug($"[{Name()}] Processing {entity}: value={comp.Value}");
        }
    }
    finally
    {
        entities.Dispose();
        Plugin.Log.LogDebug($"[{Name()}] Query complete");
    }
}
```

### 3. IL2CPP Interop Debugging

**Problem:** IL2CPPInterop types don't behave like standard C# types.

#### Type Casting Validation

```csharp
// Check if object is IL2CPP type
public static bool TryGetIl2CppType<T>(object obj, out T result) where T : Il2CppObjectBase
{
    try
    {
        result = obj.Cast<T>();
        Plugin.Log.LogDebug($"[IL2CPP] Successfully cast to {typeof(T).Name}");
        return true;
    }
    catch (Exception ex)
    {
        Plugin.Log.LogError($"[IL2CPP] Cast failed: {ex.Message}");
        result = null;
        return false;
    }
}
```

#### Common IL2CPP Pitfalls

| Issue | Symptom | Solution |
|-------|---------|----------|
| **Missing Cast** | `InvalidCastException` | Use `.Cast<Il2CppType>()` |
| **Null Reference** | `NullReferenceException` on valid object | Check `obj != null` AND `.Pointer != IntPtr.Zero` |
| **Collection Iteration** | `IndexOutOfRangeException` | Use `Il2CppSystem.Collections.Generic.List<T>` |
| **String Conversion** | Garbled text | Use `someString.ToString()` explicitly |

### 4. Harmony Patch Debugging

**Pattern:** Log before/after patch execution to verify interception.

```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.TargetMethod))]
public static class TargetMethod_Patch
{
    [HarmonyPrefix]
    static void Prefix(/* parameters */)
    {
        Plugin.Log.LogInfo("[PATCH] TargetMethod PREFIX executed");
        // Pre-execution logic
    }
    
   [HarmonyPostfix]
    static void Postfix(/* parameters */, ref bool __result)
    {
        Plugin.Log.LogInfo($"[PATCH] TargetMethod POSTFIX: result={__result}");
        // Post-execution logic
    }
}
```

#### Debugging Patch Failures

```powershell
# Check if patch was applied
Select-String -Path "BepInEx\LogOutput.log" -Pattern "Harmony.*TargetMethod"

# Find Harmony errors
Select-String -Path "BepInEx\LogOutput.log" -Pattern "Harmony.*ERROR"
```

### 5. Memory Leak Detection

**Problem:** Native collections not disposed cause memory leaks in IL2CPP.

#### Leak Prevention Pattern

```csharp
private NativeArray<Entity> _tempEntities;
private bool _disposed = false;

protected override void OnUpdate()
{
    _tempEntities = _query.ToEntityArray(Allocator.Temp);
    try
    {
        // Process entities
    }
    finally
    {
        if (_tempEntities.IsCreated)
        {
            _tempEntities.Dispose();
            Plugin.Log.LogDebug("[MEMORY] Disposed NativeArray");
        }
    }
}

protected override void OnDestroy()
{
    if (!_disposed)
    {
        // Cleanup
        _disposed = true;
        Plugin.Log.LogInfo($"[MEMORY] {GetType().Name} destroyed");
    }
}
```

---

## ECS Integration Patterns & Bottlenecks

### Current State Assessment

**Hybrid Architecture:** Mix of ECS systems (commented out) and non-ECS services (active).

| Pattern | Current Implementation | ECS-Compliant? | Performance Impact |
|---------|----------------------|----------------|-------------------|
| **Kill Streak Tracking** | Static `ConcurrentDictionary` | ❌ No | Medium - not scaling issue yet |
| **Zone Detection** | Service-based | ⚠️ Partial | Low - infrequent checks |
| **Trap Spawning** | Static `TrapSpawnRules` | ❌ No | Low - event-driven |
| **Announcements** | Static `AnnouncementService` | ❌ No | Low - rare |
| **Player Data** | Cached in services | ⚠️ Partial | Medium - cache invalidation complexity |

### Identified Bottlenecks

#### 1. Component Compilation Failures

**Root Cause:** Unity.Entities version mismatch or missing IL2CPP interop setup.

**Evidence:**
- All lifecycle components commented out (lines 260-275 in .csproj)
- ECS extensions commented out (line 277)
- Systems depending on these components also commented out

**Impact:**
- Cannot use ECS for player lifecycle state
- Forces use of static dictionaries
- Violates architectural principles

**Resolution Path:**
1. Verify `Unity.Entities.dll` version matches ProjectM's Unity version
2. Check `Il2CppInterop` version compatibility
3. Test minimal component compilation:
   ```csharp
   public struct TestComponent : IComponentData
   {
       public int Value;
   }
   ```
4. Add diagnostics to build process

#### 2. Static State vs ECS Components

**Current Pattern:**
```csharp
// Commands/Core/KillStreakCommands.cs
private static readonly ConcurrentDictionary<ulong, int> _playerStreaks = new();
```

**ECS-Compliant Pattern:**
```csharp
// Core/Components/KillStreakComponents.cs (currently commented out)
public struct KillStreakState : IComponentData
{
    public int CurrentStreak;
    public long LastKillTimestamp;
}

// System processes this
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class KillStreakSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithAll<PlayerCharacter>()
            .ForEach((Entity entity, ref KillStreakState streak) => {
                // ECS-based processing
            }).Run();
    }
}
```

**Bottleneck:** Cannot migrate to ECS pattern due to component compilation failures.

#### 3. Harmony Patches -> Static Methods -> Services

**Current Flow:**
```
DeathEventHook (Harmony)
  ↓
TrapSpawnRules.OnPlayerDeath() (static)
  ↓
ChestSpawnService (static)
```

**ECS Flow (blocked):**
```
DeathEventHook (Harmony)
  ↓
ECB.AddComponent<PlayerDeathEvent>(entity)
  ↓
DeathProcessingSystem (ECS)
  ↓
Queries and processes entities with PlayerDeathEvent
```

#### 4. Query Performance (Potential Future Bottleneck)

**Not Currently an Issue** but could become one:

```
// If implemented without optimization
Entities.ForEach((Entity player) => {
    foreach (var zone in allZones) {  // O(players × zones)
        if (IsInZone(player, zone)) {
            // Problem: nested iteration
        }
    }
}).Run();
```

**Optimized Pattern:**
```
// Spatial partitioning with broad-phase culling
var spatialHash = new NativeMultiHashMap<int2, Entity>(zoneCount, Allocator.Temp);
// Hash zones by grid cell
// Query only nearby zones per player
```

---

## V Rising Mod Ecosystem Integration

### Popular Mods Analysis

#### 1. KindredCommands Pattern (from KINDRED_EXTRACT_INTEGRATION_PLAN.md)

**Integration Status:** ✅ Partially implemented

**Adopted Patterns:**
- `PlayerData` struct (if implemented in commented-out files)
- Player service with caching
- Command framework usage

**Recommended Adoption:**
```csharp
// KindredCommands uses this for player lookups
public struct PlayerData
{
    public FixedString64Bytes CharacterName;
    public ulong SteamID;
    public Entity UserEntity;
    public Entity CharEntity;
}

// Implement helper
public static class PlayerHelper
{
    public static bool TryGetPlayerData(ulong steamId, out PlayerData data)
    {
        // Query ECS for player
    }
}
```

**Cross-Mod Compatibility:**
- Share `PlayerData` struct definition
- Use same query patterns for player lookups
- Coordinate on component namespacing

#### 2. KindredArena (Hypothetical Integration)

**Potential Conflicts:**
- Arena zone definitions
- PvP lifecycle management
- Equipment management (EndGameKit)

**Resolution Strategy:**
```csharp
// Check if KindredArena is loaded
public static bool IsKindredArenaLoaded()
{
    return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("KindredArena.GUID");
}

// Conditional system registration
if (!IsKindredArenaLoaded())
{
    World.GetOrCreateSystem<VAutoArenaSystem>();
}
else
{
    Plugin.Log.LogInfo("[VAuto] KindredArena detected, disabling arena systems");
}
```

#### 3. VMods Framework Integration

**Pattern:** VMods provides base framework utilities.

**Integration Points:**
- Database persistence
- Configuration management
- Command registration

**Recommended Approach:**
```csharp
// Soft dependency - check at runtime
#if VMODS_AVAILABLE
using VMods.Shared;
#endif

public class Plugin : BasePlugin
{
    public override void Load()
    {
        #if VMODS_AVAILABLE
        VM odsDatabase.Initialize();
        #else
        // Fallback to JSON persistence
        JsonDatabase.Initialize();
        #endif
    }
}
```

### Cross-Mod Compatibility Checklist

- [ ] **Namespace Isolation:** Use `VAuto.*` for all types
- [ ] **Mod Detection:** Check for other mods before loading conflicting systems
- [ ] **Shared Components:** Coordinate with popular mods on component definitions
- [ ] **Config Namespacing:** Prefix all config keys with mod name
- [ ] **Event Broadcasting:** Use centralized event system for inter-mod communication
- [ ] **Soft Dependencies:** Make all mod integrations optional with fallbacks

---

## Search Patterns for Troubleshooting

### 1. ECS Entity Lifecycle Issues

**Pattern:** Entity exists but query doesn't find it

**Search Strategy:**
```powershell
# Find entity creation
Select-String -Path "*.cs" -Pattern "EntityManager\.CreateEntity.*PlayerCharacter"

# Find component additions
Select-String -Path "*.cs" -Pattern "AddComponent.*PlayerCharacter"

# Find query definitions
Select-String -Path "*.cs" -Pattern "GetEntityQuery.*PlayerCharacter"
```

**Debugging Steps:**
1. Log entity creation with all components
2. Log query filters (required/excluded components)
3. Verify component presence before query execution

### 2. IL2CPP Interop Failures

**Pattern:** `NullReferenceException` or `InvalidCastException`

**Search Strategy:**
```powershell
# Find all IL2CPP casts
Select-String -Path "*.cs" -Pattern "\.Cast<.*>"

# Find potential null access
Select-String -Path "*.cs" -Pattern "ProjectM\..*\." -Context 1

# Check for missingExists checks
Select-String -Path "*.cs" -Pattern "EntityManager\.GetComponent" | 
    Select-String -NotMatch "EntityManager\.Exists"
```

**Fix Pattern:**
```csharp
// Always check existence
if (EntityManager.Exists(entity) && 
    EntityManager.HasComponent<T>(entity))
{
    var comp = EntityManager.GetComponentData<T>(entity);
}
```

### 3. Native Collection Leaks

**Pattern:** Memory usage grows over time

**Search Strategy:**
```powershell
# Find NativeArray allocations without try/finally
Select-String -Path "*.cs" -Pattern "ToEntityArray\(Allocator" -Context 5 | 
    Select-String -NotMatch "finally"

# Find Allocator.TempJob usage (unsafe in ProjectM)
Select-String -Path "*.cs" -Pattern "Allocator\.TempJob"
```

**Audit Pattern:**
```csharp
// Safe pattern
var entities = _query.ToEntityArray(Allocator.Temp);
try
{
    // Process
}
finally
{
    entities.Dispose();
}
```

### 4. Harmony Patch Conflicts

**Pattern:** Expected behavior not occurring

**Search Strategy:**
```powershell
# Find all Harmony patches
Select-String -Path "*.cs" -Pattern "\[HarmonyPatch"

# Check for duplicate patches (target same method)
Select-String -Path "*.cs" -Pattern 'HarmonyPatch\(typeof\((.*?)\)' | 
    Group-Object -Property Line | 
    Where-Object { $_.Count -gt 1 }
```

### 5. ProjectM API Changes

**Pattern:** Code breaks after game update

**Search Strategy:**
```csharp
// Version-gated features
if (VersionGate.IsAtLeast("1.1.0"))
{
    // Use new API
}
else
{
    // Fallback to old API
}
```

**Update Workflow:**
1. Check `MIGRATION_1.1.md` for documented changes
2. Search for `[Obsolete]` attributes in ProjectM assemblies
3. Compare method signatures between versions

### 6. Performance Regression

**Pattern:** Frame drops after changes

**Search Strategy:**
```powershell
# Find systems without frame budget checks
Select-String -Path "Services\Systems\*.cs" -Pattern "OnUpdate" -Context 10 |
Select-String -NotMatch "Stopwatch|ElapsedMilliseconds"

# Find O(n²) loops
Select-String -Path "*.cs" -Pattern "foreach.*foreach" -Context 3
```

**Profiling Pattern:**
```csharp
protected override void OnUpdate()
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    // System logic
    
    sw.Stop();
    if (sw.ElapsedMilliseconds > 16) // 60 FPS = 16ms budget
    {
        Plugin.Log.LogWarning($"[{GetType().Name}] Slow frame: {sw.ElapsedMilliseconds}ms");
    }
}
```

---

## Actionable Recommendations

### Priority 1: Fix ECS Component Compilation

**Blockers:** 15+ systems commented out due to Unity.Entities errors

**Action Plan:**

1. **Isolate Compilation Issue**
   ```powershell
   # Test minimal component
   # Create Core/Components/Test/MinimalComponent.cs
   ```
   ```csharp
   using Unity.Entities;
   
   namespace VAuto.Core.Components.Test
   {
      public struct MinimalTestComponent : IComponentData
      {
         public int Value;
      }
   }
   ```
   - Compile
   - If fails: Unity.Entities.dll version issue
   - If succeeds: Existing component has syntax error

2. **Check Unity.Entities Version**
   ```powershell
   # In libs/ folder
   Get-Item Unity.Entities.dll | Select-Object -ExpandProperty VersionInfo
   ```
   - Compare with ProjectM's Unity version (likely 2022.3.x LTS)
   - Verify IL2CPP compatibility

3. **Incremental Uncomment Strategy**
   - Start with `Core/Components/Lifecycle/LifecycleComponents.cs`
   - Uncomment ONE component at a time
   - Compile after each one
   - Identify problematic component

4. **IL2CPP Interop Fix**
   ```csharp
   // If using FixedString in component:
   public struct PlayerLifecycleState : IComponentData
   {
      // May need Unity.Collections import
      public FixedString64Bytes StateName; // Ensure Unity.Collections is referenced
      public long StateEnterTimestamp;
   }
   ```

**Implemented (2026-02-02):**
- Added `Core/Components/Test/MinimalTestComponent.cs` for compile diagnostics.
- Documented requirements in `docs/COMPILE_REQUIREMENTS.md`.

### Priority 2: Establish Debugging Infrastructure

**Goal:** Make debugging efficient in IL2CPP environment

**Tasks:**

1. **Structured Logging Helper**
   ```csharp
   // Core/Logging/StructuredLog.cs
   public static class StructuredLog
   {
       public static void EntityQuery(string system, EntityQuery query)
       {
           var count = query.CalculateEntityCount();
           Plugin.Log.LogDebug($"{PREFIX}[QUERY][{system}] {count} entities matched");
       }
       
       public static void ComponentMutation(Entity entity, string component, string operation)
       {
           Plugin.Log.LogDebug($"[{operation}] {component} on {entity}");
       }
   }
   ```

2. **Debug Commands**
   ```csharp
   [Command("debugecs", "Dump ECS state", adminOnly: true)]
   public static void DebugECS(ChatCommandContext ctx, Entity? entity = null)
   {
       if (entity.HasValue)
       {
           DumpEntityComponents(entity.Value);
       }
       else
       {
           DumpSystemStats();
       }
   }
   ```

3. **Performance Monitoring**
   - Add frame budget logging to all systems
   - Create performance report command
   - Log system execution order

**Implemented (2026-02-02):**
- Added `Core/Logging/StructuredLog.cs` helper.
- Added `Commands/Core/DebugCommands.cs` with `.debugecs` and `.debugentity`.

### Priority 3: Document V Rising Mod Integration Patterns

**Goal:** Enable seamless integration with popular mods

**Deliverables:**

1. **Mod Compatibility Matrix**
   | Mod | Conflicts | Integration Required | Status |
   |-----|-----------|---------------------|--------|
   | KindredCommands | None | Player data sharing | ✅ Implemented |
   | KindredArena | Zone management | Conditional loading | ⚠️ TODO |
   | VMods | None | Optional config | ⚠️ TODO |

2. **Integration Guide** (separate doc)
   - How to detect other mods
   - Shared component conventions
   - Event system for inter-mod communication

3. **Testing with Popular Mods**
   - Install top 5 V Rising mods
   - Test VAuto with each combination
   - Document conflicts and resolutions

###Priority 4: Migrate Static State to ECS

**Dependency:** Priority 1 must be complete

**Migration Path:**

1. **Kill Streak System**
   ```csharp
   // Replace Commands/Core/KillStreakCommands.cs static dict
   public struct KillStreakState : IComponentData
   {
       public int CurrentStreak;
       public long LastKillTimestamp;
   }
   
   [UpdateInGroup(typeof(SimulationSystemGroup))]
   public partial class KillStreakSystem : SystemBase
   {
       protected override void OnUpdate()
       {
           var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds executes once per frame with no player iterations);
           
           Entities
               .WithAll<PlayerCharacter>()
               .ForEach((Entity entity, ref KillStreakState streak) => {
                   // Check timeout
                   if (currentTime - streak.LastKillTimestamp > 120)
                   {
                       streak.CurrentStreak = 0;
                   }
               }).Run();
       }
   }
   ```

2. **Trap Spawn State**
   ```csharp
   public struct TrapOwnershipState : IComponentData
   {
       public ulong OwnerID;
       public float3 TrapPosition;
       public int TrapType; // enum as int
   }
   ```

3. **Commands Use ECS Queries**
   ```csharp
   [Command("streak", "Show kill streak")]
   public static void StreakCommand(ChatCommandContext ctx)
   {
       var entity = ctx.Event.SenderCharacterEntity;
       var em = VWorld.Server.EntityManager;
       
       if (em.HasComponent<KillStreakState>(entity))
       {
           var streak = em.GetComponentData<KillStreakState>(entity);
           ctx.Reply($"Streak: {streak.CurrentStreak}");
       }
   }
   ```

### Priority 5: Cross-Mod API Documentation

**Goal:** Enable other mod developers to integrate with VAuto

**Deliverables:**

1. **Public API Surface**
   ```csharp
   namespace VAuto.API
   {
       public static class ZoneAPI
       {
           public static bool IsPlayerInZone(Entity player, string zoneName);
           public static IEnumerable<string> GetPlayerZones(Entity player);
       }
       
       public static class LifecycleAPI
       {
           public static PlayerLifecycleState GetState(Entity player);
           public static void EnterArena(Entity player, string arenaName);
       }
   }
   ```

2. **Event System**
   ```csharp
   public static class VAutoEvents
   {
       public static event Action<Entity, string> OnPlayerEnteredZone;
       public static event Action<Entity, string> OnPlayerExitedZone;
       public static event Action<Entity, int> OnKillStreakMilestone;
   }
   ```

3. **Integration Examples**
   - Sample mod that uses VAuto zones
   - Sample mod that listens to VAuto events
   - Logging best practices

---

## Summary

### Current State
- **Architecture:** Hybrid (services + commented-out ECS)
- **Compilation:** 50+ files active, 18 ECS files blocked
- **Root Issue:** Unity.Entities compilation failures
- **Workaround:** Static dictionaries and services (violates design principles)

### Critical Path
1. ✅ Fix component compilation (Priority 1)
2. ✅ Establish debugging infrastructure (Priority 2)
3. ⚠️ Document mod ecosystem (Priority 3)
4. ⚠️ Migrate to ECS (requires #1 complete)
5. ⚠️ Stabilize cross-mod API (Priority 5)

### Risk Assessment
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Unity.Entities version incompatible | High | Critical | Test with multiple Unity versions |
| Performance regression after ECS migration | Medium | High | Profile before/after, staged rollout |
| Conflicts with popular mods | Medium | Medium | Test matrix, conditional loading |
| IL2CPP memory leaks | Low | High | Audit all native collections |

### Next Steps
1. Create `Core/Components/Test/MinimalComponent.cs` and test compilation
2. Identify exact Unity.Entities version requirement
3. Document component compilation requirements in `docs/COMPILE_REQUIREMENTS.md`
4. Incrementally uncomment lifecycle components
5. Add structured logging to all active systems
6. Create mod compatibility testing plan

---
