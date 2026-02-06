# V Rising ECS System Integration Plan

## Executive Summary

This document outlines the comprehensive integration strategy for injecting custom automation logic into the V Rising Server ECS (Entity Component System) loop. The plan leverages the specific execution order identified in the system dump to ensure proper system placement, data integrity, and execution reliability.

**Project Reference:** VAutomationEvents (VAutomationevents.csproj)

---

## 1. Hierarchy Analysis & Execution Flow

Understanding the precise order of operations for the Server World is critical for injecting logic that depends on specific data being present (e.g., Input) or processed (e.g., Physics).

### 1.1 Initialization Phase

**System Group:** `Unity.Entities.InitializationSystemGroup`

**Role:** Sets up the frame, time, and loads scenes/data.

**Usage Guidelines:**
- Place logic here that needs to reset per-frame counters
- Handle data that must exist before the simulation starts
- Reference system: `ProjectM.ServerGameSettingsSystem`

**Integration Opportunities:**
- Configuration loading and validation
- Global state initialization
- Asset preloading checks

### 1.2 Simulation Phase

**System Group:** `Unity.Entities.SimulationSystemGroup`

**Role:** The core game loop containing the majority of gameplay logic.

#### 1.2.1 StartSimulationGroup

**Purpose:** Handles networking inputs and server time.

**Critical Note:** Logic reacting to raw player input should happen **after** `ProjectM.Network.DeserializeUserInputSystem`.

**Integration Targets:**
- Player input processing hooks
- Network state synchronization
- Time-based scheduling initialization

#### 1.2.2 UpdateGroup

**Purpose:** The bulk of gameplay logic including Movement, AI, and Interactions.

**Strategy:** Most VAutomationEvents systems should reside here.

**Key Reference Systems:**
- `ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem`
- `ProjectM.Gameplay.Systems.MovePlayerSystem`
- `ProjectM.Gameplay.Systems.InteractionSystem`

#### 1.2.3 LateUpdateGroup

**Purpose:** Post-processing logic that reacts to changes made during the Update group.

**Strategy:** Use this to check if entities died this frame or other state changes.

**Integration Targets:**
- Death event processing
- Score/stat updates after combat
- Cleanup operations

### 1.3 Fixed Step Phase

**System Group:** `Unity.Entities.FixedStepSimulationSystemGroup`

**Role:** Physics and deterministic updates.

**Usage Guidelines:**
- Only place logic here if it strictly interacts with Unity Physics (Collision/Raycasts)
- Not recommended for general automation logic

**Integration Targets:**
- Physics-based triggers
- Collision detection callbacks
- Raycast-based proximity systems

---

## 2. Integration Strategy: System Placement

To ensure stability and data integrity, we will use ECS Attributes to inject custom systems relative to the native systems identified in the dump.

### 2.1 General Gameplay Automation

**Target Group:** `ProjectM.UpdateGroup`

**Reference System:** `ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem`

**Implementation Strategy:**

```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
public class VAutomationEventProcessingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Custom automation logic
    }
}
```

**Timer-Dependent Logic:**

```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateAfter(typeof(ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem))]
public class VAutomationCooldownSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Cooldown processing after tick events
    }
}
```

### 2.2 Combat & PVP Lifecycle

**Target Group:** `ProjectM.StatChangeGroup`

**Reference System:** `ProjectM.Gameplay.Systems.DealDamageSystem`

#### 2.2.1 Damage Prevention (Safe Zones)

```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateBefore(typeof(ProjectM.Gameplay.Systems.DealDamageSystem))]
public class SafeZoneDamagePreventionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Check if attacker/target is in safe zone
        // Add "Invulnerable" buff or remove damage components
    }
}
```

#### 2.2.2 Damage Analytics (Post-Processing)

```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateAfter(typeof(ProjectM.Gameplay.Systems.DealDamageSystem))]
public class DamageAnalyticsSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Read damage events buffer
        // Process for analytics/tracking
    }
}
```

### 2.3 Spawning & World Population

**Target Group:** `ProjectM.SpawnGroup`

**Reference System:** `ProjectM.Gameplay.Systems.SpawnCharacterSystem`

**Implementation:**

```csharp
[UpdateInGroup(typeof(ProjectM.SpawnGroup))]
[UpdateAfter(typeof(ProjectM.Gameplay.Systems.SpawnCharacterSystem))]
public class SpawnCustomizationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Modify entities immediately upon spawn
        // Apply custom gear, stats, or behaviors
    }
}
```

### 2.4 Castle & Territory Management

**Target Group:** `ProjectM.ReactToTilePositionGroup` or `ProjectM.CastleBuilding.CastleBlockSystem`

**Reference System:** `ProjectM.CastleBuilding.CastleHeartStateUpdateSystem`

**Implementation:**

```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
public class CastleManagementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Castle decay processing
        // Territory checks
        // Heart state monitoring
    }
}
```

---

## 3. Specific Hook Points for VAutomationEvents

Based on the VAutomationEvents project context, the following systems provide optimal integration points:

### 3.1 Feature: PVP/Glow Zones

**Target System:** `ProjectM.Gameplay.Systems.PlayerCombatBuffSystem_InitialApplication_Aggro`

**Integration Goal:** Detect when combat starts to enforce zone rules.

**Implementation Approach:**
```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateBefore(typeof(ProjectM.Gameplay.Systems.PlayerCombatBuffSystem_InitialApplication_Aggro))]
public class GlowZoneEnforcementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Check combat initiation against glow zone boundaries
        // Apply zone-specific rules (pvp enable/disable, visual effects)
    }
}
```

### 3.2 Feature: Portals/Teleport

**Target System:** `ProjectM.Gameplay.Systems.TeleportSystem`

**Integration Goal:** Intercept or log teleport events; ensure custom teleports happen before physics checks.

**Implementation Approach:**
```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateBefore(typeof(ProjectM.Gameplay.Systems.TeleportSystem))]
public class PortalInterceptSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Process pending portal requests
        // Validate destination
        // Apply custom teleport logic
    }
}
```

### 3.3 Feature: Server Management

**Target System:** `ProjectM.ServerShutdownSystem`

**Integration Goal:** Hook OnUpdate via Harmony to inject "Cancel Shutdown" logic or custom countdown messages.

**Harmony Patch Approach:**
```csharp
[HarmonyPatch(typeof(ProjectM.ServerShutdownSystem))]
[HarmonyPatch("OnUpdate")]
public class ServerShutdownHook
{
    static void Prefix(ServerShutdownSystem __instance)
    {
        // Check for shutdown cancellation
        // Inject custom countdown messages
        // Apply shutdown prevention logic
    }
}
```

### 3.4 Feature: Chat/Commands

**Target System:** `ProjectM.ChatMessageSystem`

**Integration Goal:** Ensure command parsing happens before this system processes chat to hide commands from global chat.

**Implementation Approach:**
```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateBefore(typeof(ProjectM.ChatMessageSystem))]
public class CommandParsingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Process player commands
        // Mark as handled to prevent global chat output
    }
}
```

### 3.5 Feature: Persistence

**Target System:** `ProjectM.TriggerPersistenceSaveSystem`

**Integration Goal:** Detect when the server is saving to trigger custom data dumps (JSONs).

**Implementation Approach:**
```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateAfter(typeof(ProjectM.TriggerPersistenceSaveSystem))]
public class PersistenceHookSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Trigger custom data serialization
        // Export JSON dumps
        // Validate data integrity
    }
}
```

---

## 4. Implementation Guidelines

### 4.1 Core Principles

#### 4.1.1 Do Not Edit Native Systems

**Rule:** Never attempt to modify the code of the systems listed in the dump directly.

**Rationale:**
- Updates to the base game will overwrite changes
- Breaking native systems can cause server crashes
- Violates modding best practices

**Alternative:** Use Harmony patches or ECS ordering attributes.

#### 4.1.2 Use Harmony for Interception

When you need to:
- Stop a system from running
- Change internal logic
- Access private fields/methods

```csharp
[HarmonyPatch(typeof(TargetSystem))]
[HarmonyPatch("MethodName")]
public class HarmonyInterceptor
{
    static bool Prefix(TargetSystem __instance, /* params */)
    {
        // Your pre-processing logic
        
        // Return false to skip original method
        // Return true to run original method
        return true;
    }
}
```

#### 4.1.3 Use ECS Attributes for Ordering

For new systems, strictly define attribute relationships:

```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateAfter(typeof(ProjectM.Gameplay.Systems.ReferenceSystem))]
[UpdateBefore(typeof(ProjectM.Gameplay.Systems.NextSystem))]
public class MyCustomSystem : SystemBase
{
    // System implementation
}
```

**Critical:** Use exact type names from the dump to prevent race conditions.

### 4.2 Data Access Patterns

#### 4.2.1 Entity Queries

Use predefined queries when available:
```csharp
// Preferred: Use existing query
var entities = SystemAPI.QueryBuilder()
    .WithAll<PlayerData>()
    .Build()
    .ToEntityArray(Allocator.Temp);

// Alternative: Create new query if needed
var query = GetEntityQuery(typeof(PlayerData), typeof(Health));
```

#### 4.2.2 Component Access

Always verify component presence:
```csharp
if (EntityManager.HasComponent<Health>(entity))
{
    var health = EntityManager.GetComponentData<Health>(entity);
    // Process health data
}
```

#### 4.2.3 Entity Existence Checks

For components referencing other entities:
```csharp
if (EntityManager.Exists(targetEntity))
{
    // Safe to access target entity
}
```

### 4.3 Memory Management

#### 4.3.1 NativeArray Disposal Pattern

**Required:** Always use try-finally blocks for NativeArray disposal.

```csharp
var entities = query.ToEntityArray(Allocator.TempJob);
try
{
    // Process entities
    foreach (var entity in entities)
    {
        // Your logic
    }
}
finally
{
    entities.Dispose();
}
```

**Note:** Avoid `using` statements with native collections in V Rising's ECS environment.

#### 4.3.2 Allocator Selection

| Allocator | Use Case |
|-----------|----------|
| `Allocator.Temp` | Single-frame operations, within try-finally |
| `Allocator.TempJob` | Processing over multiple frames |
| `Allocator.Persistent` | Long-lived data structures |

### 4.4 Logging Standards

#### 4.4.1 Centralized Logging

Use `Plugin.LogInstance.LogInfo()` for consistency:

```csharp
Plugin.LogInstance.LogInfo($"[{SystemName}] Processed {count} entities");
```

#### 4.4.2 Context-Rich Logging

Include system name, entity ID, and relevant component data:

```csharp
Plugin.LogInstance.LogInfo(
    $"[SafeZoneSystem] Entity {entity.Index} entering safe zone at {position}"
);
```

### 4.5 Performance Considerations

#### 4.5.1 Job System Integration

For heavy processing, use C# Job System:

```csharp
[BurstCompile]
public partial struct ProcessEntitiesJob : IJobEntity
{
    public float DeltaTime;
    
    void Execute(ref Health health, in Position position)
    {
        // Burst-compiled logic
        health.Value -= DeltaTime * 5f;
    }
}
```

#### 4.5.2 Entity Access Optimization

- Use `SystemAPI.Time.DeltaTime` for frame-independent calculations
- Prefer `ref readonly` for read-only component access
- Batch entity operations when possible

---

## 5. System Reference Table

| Category | Target System | Purpose | Attribute |
|----------|---------------|---------|-----------|
| **Initialization** | `ServerGameSettingsSystem` | Config loading | `[UpdateInGroup(typeof(InitializationSystemGroup))]` |
| **Input Processing** | `DeserializeUserSystem` | Network input | `[UpdateInGroup(typeof(StartSimulationGroup))]` |
| **Gameplay Events** | `CreateGameplayEventOnTickSystem` | Event creation | `[UpdateInGroup(typeof(UpdateGroup))]` |
| **Lifecycle Abilities** | `LifecycleSpellbookSystem` | **Replaces VBlood & Kits**: Auto-scan spellbooks from Data.Prefabs, grant on enter, remove on exit | `[UpdateInGroup(typeof(UpdateGroup))][UpdateAfter(typeof(CreateGameplayEventOnTickSystem))]` |
| **Combat** | `DealDamageSystem` | Damage processing | `[UpdateInGroup(typeof(UpdateGroup))]` |
| **Safe Zone** | `SafeZoneDamagePreventionSystem` | Prevent damage in safe zones | `[UpdateInGroup(typeof(UpdateGroup))][UpdateBefore(typeof(DealDamageSystem))]` |
| **Damage Analytics** | `DamageAnalyticsSystem` | Post-damage processing | `[UpdateInGroup(typeof(UpdateGroup))][UpdateAfter(typeof(DealDamageSystem))]` |
| **Spawning** | `SpawnCharacterSystem` | Entity spawning | `[UpdateInGroup(typeof(SpawnGroup))]` |
| **Spawn Customization** | `SpawnCustomizationSystem` | Modify entities on spawn | `[UpdateInGroup(typeof(SpawnGroup))][UpdateAfter(typeof(SpawnCharacterSystem))]` |
| **Castle Management** | `CastleManagementSystem` | Castle decay & heart state | `[UpdateInGroup(typeof(UpdateGroup))]` |
| **PVP/Glow Zones** | `GlowZoneEnforcementSystem` | Combat zone enforcement | `[UpdateInGroup(typeof(UpdateGroup))][UpdateBefore(typeof(PlayerCombatBuffSystem_InitialApplication_Aggro))]` |
| **Portals/Teleport** | `PortalInterceptSystem` | Intercept teleport events | `[UpdateInGroup(typeof(UpdateGroup))][UpdateBefore(typeof(TeleportSystem))]` |
| **Chat/Commands** | `CommandParsingSystem` | Pre-process chat commands | `[UpdateInGroup(typeof(UpdateGroup))][UpdateBefore(typeof(ChatMessageSystem))]` |
| **Persistence** | `PersistenceHookSystem` | Trigger custom save | `[UpdateInGroup(typeof(UpdateGroup))][UpdateAfter(typeof(TriggerPersistenceSaveSystem))]` |
| **Server Shutdown** | `ServerShutdownHook` | Cancel / intercept shutdown | Harmony Patch |

---

## 5.1 Lifecycle Spellbook System (Replaces VBlood & Kits)

**Target Group:** `ProjectM.UpdateGroup`

**Reference System:** `ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem`

**Purpose:** Automatically scan all spellbooks from `Data.Prefabs`, grant them when a player enters a lifecycle/PvP zone, and remove them when leaving. This completely replaces manual VBlood and kit systems.

**Key Features:**
- **Auto-Discovery:** Scans `Data.Prefabs` for any prefab containing "Spellbook"
- **Legacy Support:** Manual GUID array for old spellbooks to preserve
- **Deduplication:** Uses `.Distinct()` to prevent duplicate entries
- **Thread-Safe:** Double-check locking for initialization
- **Runtime Extensible:** `AddLegacySpellbook()` for runtime registration

**Implementation Strategy:**

```csharp
[UpdateInGroup(typeof(ProjectM.UpdateGroup))]
[UpdateAfter(typeof(ProjectM.Gameplay.Systems.CreateGameplayEventOnTickSystem))]
public partial class LifecycleSpellbookSystem : SystemBase
{
    // Legacy spellbooks to preserve
    static readonly PrefabGUID[] LegacySpellbooks = new PrefabGUID[]
    {
        // Add your custom spellbook GUIDs here
    };

    protected override void OnStartRunning()
    {
        // Automatically fetch all spellbooks from Data.Prefabs
        var prefabList = new List<PrefabGUID>();
        
        foreach (var field in typeof(Data.Prefabs).GetFields())
        {
            if (field.FieldType == typeof(PrefabGUID))
            {
                var guid = (PrefabGUID)field.GetValue(null);
                var name = guid.LookupName();
                if (!string.IsNullOrEmpty(name) && name.ToLower().Contains("spellbook"))
                {
                    prefabList.Add(guid);
                }
            }
        }
        
        prefabList.AddRange(LegacySpellbooks);
        _allSpellbooks = prefabList.Distinct().ToArray();
    }

    protected override void OnUpdate()
    {
        // Grant/Remove spellbooks based on lifecycle zone
    }
}
```

### 5.1.1 Lifecycle Spellbook Flow

```
[Server Start] ──> OnStartRunning ──> Scans Data.Prefabs for "Spellbook"
           │
           ▼
[Player enters lifecycle zone]
           │
           ▼
[LifecycleSpellbookSystem] ──> GrantSpellbooks() ──> AbilityGroupSlotModificationBuffer
           │
           ▼
[Other ECS Update Systems] execute normally
           │
           ▼
[Player exits lifecycle zone / dies]
           │
           ▼
[LifecycleSpellbookSystem] ──> RemoveSpellbooks() ──> Clean up buffer
```

### 5.1.2 Public API

| Method | Description |
|--------|-------------|
| `AddLegacySpellbook(PrefabGUID)` | Add legacy spellbook at runtime |
| `GetSpellbookCount()` | Get count of registered spellbooks |

### 5.1.3 Testing Checklist

1. **Auto-Discovery:** Verify spellbooks are found from Data.Prefabs
2. **Legacy Addition:** Test legacy spellbooks are included
3. **Zone Entry:** Player receives all spellbooks when entering
4. **Zone Exit:** Player loses all spellbooks when leaving/dying
5. **No Duplicates:** Verify Distinct() prevents duplicate spellbooks
6. **Multiplayer:** Multiple players handled correctly
7. **Runtime Extension:** AddLegacySpellbook() works correctly

---

## 6. Testing Strategy

### 6.1 Integration Testing

1. **Load Order Verification:** Ensure systems load without errors
2. **Execution Order Validation:** Confirm attribute-based ordering
3. **Data Integrity Checks:** Verify component data remains consistent
4. **Performance Profiling:** Monitor system execution time

### 6.2 Functional Testing

1. **Feature Activation:** Test each automation feature independently
2. **Edge Cases:** Boundary conditions and error states
3. **Multiplayer Simulation:** Test with multiple connected clients
4. **Server Stability:** Extended runtime testing

### 6.3 Regression Testing

1. **Native System Compatibility:** Verify no interference with core systems
2. **Save/Load Integrity:** Persistence across server restarts
3. **Version Compatibility:** Track changes in base game updates

---

## 7. Maintenance Guidelines

### 7.1 System Dump Updates

When V Rising updates:
1. Re-obtain system dump
2. Compare execution order changes
3. Update system attribute placements as needed
4. Test integration points

### 7.2 Dependency Management

- Track base game version compatibility
- Maintain changelog of system hooks
- Document breaking changes from updates

### 7.3 Performance Monitoring

- Regular profiling of system execution
- Memory usage tracking
- GC pressure optimization

---

## 8. Conclusion

This integration plan provides a comprehensive framework for extending V Rising's ECS infrastructure with custom automation logic. By following the established patterns for system placement, attribute usage, and Harmony interception, we can safely extend game functionality without disrupting core systems.

**Key Success Factors:**
- Strict adherence to ECS attribute ordering
- Proper memory management practices
- Comprehensive testing of integration points
- Ongoing maintenance with game updates

**Next Steps:**
1. Implement core systems following this plan
2. Create unit tests for system behavior
3. Establish CI/CD pipeline for mod updates
4. Document runtime configuration options

---

*Document Version: 1.0*
*Last Updated: 2024*
*Compatible with: V Rising Server ECS Architecture*
