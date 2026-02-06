# VAutomationEvents Refactoring Plan: Complete ECS Compliance

**Created:** 2026-01-31

**Based On:**
- [ECS System Integration Plan](../docs/ECS_SYSTEM_INTEGRATION_PLAN.md)
- [Code Review Report](../docs/CODE_REVIEW_REPORT.md)
- Existing codebase analysis

**Objective:** Achieve full ECS compliance with proper folder structure organization.

---

## Codebase Status Summary

### ✅ Already Done (ECS-Compliant)

These files are already properly implemented and ready to use:

| File | Status | Notes |
|------|--------|-------|
| [`Core/Components/EndGameKitComponents.cs`](../Core/Components/EndGameKitComponents.cs) | ✅ Done | Proper IComponentData components |
| [`Extensions/ECSExtensions.cs`](../Extensions/ECSExtensions.cs) | ✅ Done | IL2CPP-compatible extensions |
| [`EndGameKit/Helpers/PlayerHelper.cs`](../EndGameKit/Helpers/PlayerHelper.cs) | ✅ Done | Player entity operations |
| [`EndGameKit/Helpers/EquipmentSlotConverter.cs`](../EndGameKit/Helpers/EquipmentSlotConverter.cs) | ✅ Done | Equipment slot conversion |
| [`EndGameKit/Helpers/GuidHelper.cs`](../EndGameKit/Helpers/GuidHelper.cs) | ✅ Done | GUID utility functions |
| [`EndGameKit/Services/EquipmentService.cs`](../EndGameKit/Services/EquipmentService.cs) | ✅ Done | Equipment management |
| [`EndGameKit/Services/ConsumableService.cs`](../EndGameKit/Services/ConsumableService.cs) | ✅ Done | Consumable application |
| [`EndGameKit/Services/JewelService.cs`](../EndGameKit/Services/JewelService.cs) | ✅ Done | Jewel attachment |
| [`EndGameKit/Services/StatExtensionService.cs`](../EndGameKit/Services/StatExtensionService.cs) | ✅ Done | Stat modifications |
| [`EndGameKit/Configuration/EndGameKitProfile.cs`](../EndGameKit/Configuration/EndGameKitProfile.cs) | ✅ Done | Kit profile definition |
| [`Core/UserExtensions.cs`](../Core/UserExtensions.cs) | ✅ Done | User entity extensions |
| [`Core/PrefabGuidExtensions.cs`](../Core/PrefabGuidExtensions.cs) | ✅ Done | PrefabGUID extensions |
| [`Core/Logging/VAutoLogger.cs`](../Core/Logging/VAutoLogger.cs) | ✅ Done | Logging infrastructure |
| [`Core/Patterns/ServiceManager.cs`](../Core/Patterns/ServiceManager.cs) | ✅ Done | Service management |
| [`Core/Patterns/Singleton.cs`](../Core/Patterns/Singleton.cs) | ✅ Done | Singleton pattern |

### 📋 Data Classes Ready (Need ECS Components)

These are data models used for JSON deserialization - need IComponentData wrappers:

| File | Purpose | Action |
|------|---------|--------|
| [`Core/Components/ZoneComponents.cs`](../Core/Components/ZoneComponents.cs) | Zone configuration | Add IComponentData components |
| [`Core/Configuration/ECSSystemConfiguration.cs`](../Core/Configuration/ECSSystemConfiguration.cs) | System config | Already has singleton |
| [`Core/Configuration/PVPLifecycleConfigLoader.cs`](../Core/Configuration/PVPLifecycleConfigLoader.cs) | PvP config | Already has singleton |

### 🔄 Needs ECS Conversion (Static → ECS)

These files are static implementations that need to be converted to ECS systems:

| File | Purpose | Convert To |
|------|---------|------------|
| [`Core/Helpers/ZoneHelper.cs`](../Core/Helpers/ZoneHelper.cs) | Zone helpers | ECS integration in ZoneSystem |
| [`Core/EntityHelper.cs`](../Core/EntityHelper.cs) | Entity helpers | Merge into Core.cs |
| [`EndGameKit/EndGameKitSystem.cs`](../EndGameKit/EndGameKitSystem.cs) | Kit orchestrator | EndGameKitApplicationSystem |
| [`Services/PlayerService.cs`](../Services/PlayerService.cs) | Player caching | PlayerTrackingSystem |
| [`Services/World/GlowZoneService.cs`](../Services/World/GlowZoneService.cs) | Glow zones | GlowZoneSystem |
| [`Core/Lifecycle/PVPItemLifecycle.cs`](../Core/Lifecycle/PVPItemLifecycle.cs) | PvP items | PVPLifecycleSystem |
| [`Core/Lifecycle/LifecycleActionHandlers.cs`](../Core/Lifecycle/LifecycleActionHandlers.cs) | Lifecycle actions | PVPLifecycleActionSystem |
| [`Core/ECSServiceManager.cs`](../Core/ECSServiceManager.cs) | ECS services | Update for new structure |

---

## Proposed Folder Structure

```text
VAuto/
├── Core/
│   ├── Components/
│   │   ├── ZoneComponents.cs          # Data + Add IComponentData
│   │   ├── EndGameKitComponents.cs    # ✅ Done
│   │   └── PVPLifecycleComponents.cs  # 🆕 New
│   │
│   ├── Harmony/
│   │   └── ServerShutdownInterceptor.cs
│   │
│   ├── Configuration/
│   │   ├── ECSSystemConfiguration.cs  # ✅ Done
│   │   └── PVPLifecycleConfigLoader.cs # ✅ Done
│   │
│   ├── Logging/
│   │   ├── VAutoLogger.cs             # ✅ Done
│   │   └── LogComponents.cs
│   │
│   ├── Lifecycle/
│   │   ├── PVPItemLifecycle.cs        # 🔄 Convert to ECS
│   │   └── LifecycleActionHandlers.cs # 🔄 Convert to ECS
│   │
│   ├── Patterns/
│   │   ├── ServiceManager.cs          # ✅ Done
│   │   └── Singleton.cs               # ✅ Done
│   │
│   ├── Networking/
│   │   ├── StateSerializer.cs
│   │   ├── WireProtocol.cs
│   │   └── WireService.cs
│   │
│   └── Core.cs                        # 🆕 Merge EntityHelper here
│
├── Extensions/
│   ├── ECSExtensions.cs               # ✅ Done
│   ├── UserExtensions.cs              # ✅ Done
│   └── PrefabGuidExtensions.cs        # ✅ Done
│
├── Helpers/
│   ├── ZoneHelper.cs                  # 🔄 Integrate into ZoneSystem
│   └── [Future helpers]
│
├── Services/
│   ├── Systems/                       # 🆕 New ECS systems
│   │   ├── Zone/
│   │   │   ├── ZoneDetectionSystem.cs
│   │   │   ├── ZoneTransitionSystem.cs
│   │   │   └── ZoneConfigSystem.cs
│   │   │
│   │   ├── EndGameKit/
│   │   │   ├── EndGameKitApplicationSystem.cs
│   │   │   ├── EndGameKitRestoreSystem.cs
│   │   │   └── EndGameKitValidationSystem.cs
│   │   │
│   │   ├── PVPLifecycle/
│   │   │   ├── PVPLifecycleStateSystem.cs
│   │   │   ├── PVPLifecycleActionSystem.cs
│   │   │   └── PVPLifecycleEventSystem.cs
│   │   │
│   │   └── Player/
│   │       └── PlayerTrackingSystem.cs
│   │
│   └── World/
│       ├── GlowZoneService.cs         # 🔄 Integrate into ZoneSystem
│       ├── TeleportService.cs
│       └── WorldSpawnService.cs
│
├── EndGameKit/                        # ✅ Mostly done
│   ├── EndGameKitSystem.cs            # 🔄 Convert to ECS systems
│   ├── Configuration/
│   │   ├── EndGameKitProfile.cs       # ✅ Done
│   │   └── EndGameKitConfigService.cs
│   ├── Services/
│   │   ├── EquipmentService.cs        # ✅ Done
│   │   ├── ConsumableService.cs       # ✅ Done
│   │   ├── JewelService.cs            # ✅ Done
│   │   └── StatExtensionService.cs    # ✅ Done
│   └── Helpers/
│       ├── PlayerHelper.cs            # ✅ Done
│       ├── EquipmentSlotConverter.cs  # ✅ Done
│       └── GuidHelper.cs              # ✅ Done
│
├── Commands/
│   ├── ArenaCommands.cs
│   ├── KitCommands.cs
│   ├── LifecycleCommands.cs
│   ├── PlayerCommands.cs
│   ├── PortalCommands.cs
│   ├── PvPCommands.cs
│   ├── SpawnCommands.cs
│   └── ZoneCommands.cs
│
├── Data/
│   └── Prefabs.cs
│
├── config/
│   ├── EndGameKit.json
│   └── VAuto.Arena/
│       ├── arena_zones.json
│       └── builds.json
│
└── Plugin.cs
```

---

## Phase 1: Add ECS Components to ZoneComponents

**File:** `Core/Components/ZoneComponents.cs`

The existing file has data classes for JSON config. Add IComponentData components:

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    // === Existing data classes (keep as-is) ===
    [Serializable]
    public class ZoneConfig { ... }
    [Serializable]
    public class ZoneDefinition { ... }
    // ... other data classes

    // === Add ECS Components ===
    public enum ZoneType : byte
    {
        None = 0,
        MainArena = 1,
        PvPArena = 2,
        GlowZone = 3,
        SafeZone = 4,
        Custom = 5
    }

    /// <summary>
    /// Tag component for players currently inside a zone
    /// </summary>
    public struct ZonePlayerTag : IComponentData
    {
        public int ZoneId;
        public ZoneType ZoneType;
    }

    /// <summary>
    /// Stores zone entry timestamp for duration tracking
    /// </summary>
    public struct ZoneEntryTimestamp : IComponentData
    {
        public int ZoneId;
        public double EntryTime;
    }

    /// <summary>
    /// Component for zone boundaries
    /// </summary>
    public struct ZoneBoundary : IComponentData
    {
        public int ZoneId;
        public ZoneType ZoneType;
        public float3 Center;
        public float Radius;
        public bool PvPEnabled;
        public bool SafeZone;
    }

    /// <summary>
    /// Buffer element for tracking multiple zone memberships
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ZoneMembershipElement : IBufferElementData
    {
        public int ZoneId;
        public ZoneType ZoneType;
        public double EntryTime;
    }
}
```

---

## Phase 2: Create PvP Lifecycle Components

**File:** `Core/Components/PVPLifecycleComponents.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Tracks PvP lifecycle state for players
    /// </summary>
    public struct PVPLifecycleState : IComponentData
    {
        public bool InCombat;
        public double LastCombatTime;
        public Entity LastAttacker;
        public PVPLifecycleStatus Status;
    }

    public enum PVPLifecycleStatus
    {
        Peaceful = 0,
        InCombat = 1,
        RecentlyEngaged = 2
    }

    /// <summary>
    /// Tag for players in PvP-enabled zone
    /// </summary>
    public struct PvPZoneTag : IComponentData
    {
        public FixedString64Bytes ZoneName;
        public double EntryTime;
    }

    /// <summary>
    /// Tracks PvP item set applied to player
    /// </summary>
    public struct PVPLoadoutState : IComponentData
    {
        public bool LoadoutApplied;
        public FixedString64Bytes LoadoutName;
        public double ApplicationTime;
    }
}
```

---

## Phase 3: Create ECS Systems

### 3.1 Zone Detection System

**File:** `Services/Systems/Zone/ZoneDetectionSystem.cs`

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using VAuto.Core.Components;

namespace VAuto.Services.Systems.Zone
{
    /// <summary>
    /// Detects which zone each player is currently in.
    /// Uses efficient lookup - players belong to at most ONE zone per ZoneType.
    /// Runs BEFORE ZoneTransitionSystem.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    [UpdateBefore(typeof(ZoneTransitionSystem))]
    public partial struct ZoneDetectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ZoneBoundary>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Get all active zones
            var zoneQuery = SystemAPI.QueryBuilder()
                .WithAll<ZoneBoundary>()
                .Build();

            var zones = zoneQuery.ToComponentDataArray<ZoneBoundary>(Allocator.TempJob);

            try
            {
                foreach (var (transform, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<ProjectM.PlayerCharacter>()
                         .WithEntityAccess())
                {
                    float3 position = transform.ValueRO.Position;
                    int detectedZoneId = -1;
                    ZoneType detectedZoneType = ZoneType.None;

                    foreach (var zone in zones)
                    {
                        if (math.distance(position, zone.Center) <= zone.Radius)
                        {
                            if (detectedZoneId == -1)
                            {
                                detectedZoneId = zone.ZoneId;
                                detectedZoneType = zone.ZoneType;
                            }
                        }
                    }

                    ecb.AddComponent(entity, new ZoneDetectionResult
                    {
                        DetectedZoneId = detectedZoneId,
                        DetectedZoneType = detectedZoneType,
                        DetectionTime = SystemAPI.Time.ElapsedTime
                    });
                }
            }
            finally
            {
                zones.Dispose();
            }
        }
    }

    public struct ZoneDetectionResult : IComponentData
    {
        public int DetectedZoneId;
        public ZoneType DetectedZoneType;
        public double DetectionTime;
    }
}
```

### 3.2 Zone Transition System

**File:** `Services/Systems/Zone/ZoneTransitionSystem.cs`

```csharp
using Unity.Entities;
using VAuto.Core.Components;

namespace VAuto.Services.Systems.Zone
{
    /// <summary>
    /// Compares current zone detection with previous zone membership.
    /// Applies/removes effects only on zone transitions.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    [UpdateAfter(typeof(ZoneDetectionSystem))]
    public partial struct ZoneTransitionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (detection, entity) in
                     SystemAPI.Query<RefRO<ZoneDetectionResult>>()
                     .WithEntityAccess())
            {
                var currentZoneId = detection.ValueRO.DetectedZoneId;
                var currentZoneType = detection.ValueRO.DetectedZoneType;

                // Handle zone transitions
                // Apply InEndGameZoneTag for EndGameKit trigger
                // Apply PvPZoneTag for PvP lifecycle trigger
            }

            // Cleanup
            foreach (var (_, entity) in SystemAPI.Query<RefRO<ZoneDetectionResult>>().WithEntityAccess())
            {
                ecb.RemoveComponent<ZoneDetectionResult>(entity);
            }
        }
    }
}
```

### 3.3 EndGameKit Application System

**File:** `Services/Systems/EndGameKit/EndGameKitApplicationSystem.cs`

```csharp
using Unity.Entities;
using VAuto.Core.Components;

namespace VAuto.Services.Systems.EndGameKit
{
    /// <summary>
    /// Applies end-game kits to players when they enter end-game zones.
    /// Uses existing EndGameKitSystem services for actual application.
    /// Execution Order: Equipment → Consumables → Jewels → Stats
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    [UpdateAfter(typeof(ZoneTransitionSystem))]
    public partial struct EndGameKitApplicationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (zoneTag, kitState, entity) in
                     SystemAPI.Query<RefRO<InEndGameZoneTag>, RefRW<PlayerEndGameKitState>>()
                     .WithEntityAccess())
            {
                if (!kitState.ValueRO.KitApplied)
                {
                    // Apply kit using EndGameKitSystem
                    // Preserves execution order: Equipment → Consumables → Jewels → Stats
                }
            }
        }
    }
}
```

### 3.4 PvP Lifecycle State System

**File:** `Services/Systems/PVPLifecycle/PVPLifecycleStateSystem.cs`

```csharp
using Unity.Entities;
using VAuto.Core.Components;

namespace VAuto.Services.Systems.PVPLifecycle
{
    /// <summary>
    /// Tracks PvP combat state for players.
    /// Replaces static PvP lifecycle tracking.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    public partial struct PVPLifecycleStateSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Update combat state based on damage events
            // Handle PvP zone entry/exit via PvPZoneTag
        }
    }
}
```

### 3.5 Player Tracking System

**File:** `Services/Systems/Player/PlayerTrackingSystem.cs`

```csharp
using Unity.Entities;
using Unity.Collections;
using ProjectM;
using VAuto.Core.Components;

namespace VAuto.Services.Systems.Player
{
    /// <summary>
    /// Tracks all online players using ECS queries.
    /// Replaces static PlayerService caching.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    public partial struct PlayerTrackingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Query all players with UserComponent
            // Update player tracking state
        }
    }
}
```

---

## Phase 4: Integrate Existing Services

### 4.1 Integrate ZoneHelper into ZoneSystem

The static methods in ZoneHelper should be integrated into ZoneDetectionSystem:

| ZoneHelper Method | Integration |
|-------------------|-------------|
| `GetZoneSpawnLocation()` | Use ZoneDefinition data |
| `GetZoneRadius()` | Use ZoneBoundary.Radius |
| `IsInZone()` | Use ZoneDetectionSystem |
| `TeleportToZone()` | Use TeleportService |
| `GetNearestZone()` | Use ZoneDetectionSystem |

### 4.2 Integrate GlowZoneService into ZoneSystem

The existing GlowZoneService manages visual zone markers. Integrate with ZoneTransitionSystem:

```csharp
// In ZoneTransitionSystem.HandleZoneEntry()
if (zoneType == ZoneType.GlowZone)
{
    // Apply glow effect via GlowZoneService
    GlowZoneService.ApplyGlow(player, zone.GlowConfig);
}
```

### 4.3 Preserve EndGameKit Execution Order

The existing EndGameKitSystem has a non-negotiable execution order:

1. **Validate player**
2. **Equip gear** (via EquipmentService)
3. **Apply consumables** (via ConsumableService)
4. **Attach jewels** (via JewelService)
5. **Apply stat extensions** (via StatExtensionService)
6. **Mark applied** (via PlayerEndGameKitState component)

---

## Phase 5: Command Updates

Update commands to use new ECS systems:

| Command | Update |
|---------|--------|
| `KitCommands` | Use EndGameKitApplicationSystem via PlayerEndGameKitState |
| `ZoneCommands` | Use ZoneDetectionSystem via ZoneMembershipElement |
| `PvPCommands` | Use PVPLifecycleStateSystem via PVPLoadoutState |
| `PlayerCommands` | Use PlayerTrackingSystem via user queries |

---

## Execution Order

1. **Phase 1** – Add ECS Components to ZoneComponents
2. **Phase 2** – Create PvP Lifecycle Components
3. **Phase 3** – Create ECS Systems (Zone, EndGameKit, PvP, Player)
4. **Phase 4** – Integrate existing services
5. **Phase 5** – Update commands

---

## Success Criteria

- [ ] All data classes have corresponding IComponentData
- [ ] All static helpers integrated into ECS systems
- [ ] EndGameKit execution order preserved
- [ ] Zone detection uses ECS queries
- [ ] PvP lifecycle uses ECS state tracking
- [ ] Player tracking uses ECS queries
- [ ] Commands use new ECS systems
- [ ] All existing tests pass

---

*Plan Version: 2.0*

*Next Review: After Phase 3 completion*
