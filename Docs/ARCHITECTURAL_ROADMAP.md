# VAutomation Mod Suite - Architectural Roadmap

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Complete File Inventory](#2-complete-file-inventory)
3. [Class and Interface Structure](#3-class-and-interface-structure)
4. [Method Inventory](#4-method-inventory)
5. [Dependency Mapping](#5-dependency-mapping)
6. [Data Flow Analysis](#6-data-flow-analysis)
7. [Control Flow Patterns](#7-control-flow-patterns)
8. [Architecture Overview](#8-architecture-overview)

---

## 1. Project Overview

### 1.1 Multi-Plugin Architecture

The VAutomation Mod Suite is a comprehensive collection of **5 BepInEx plugins** designed to extend V Rising with automation features, arena management, lifecycle systems, and trap mechanics.

| Plugin | GUID | Purpose |
|--------|-----|---------|
| **VAutomationCore** | `VAutomationCore` | Core framework, services, interfaces, and shared utilities |
| **VAutoannounce** | `VAuto.Announcement` | Announcement and notification broadcasting system |
| **VAutoArena** | `VAuto.Arena` | Arena territory management and glow border systems |
| **VAutoTraps** | `VAuto.Traps` | Trap spawning, container traps, and kill streak tracking |
| **Vlifecycle** | `VAuto.Lifecycle` | Player lifecycle management, kit systems, and snapshots |

### 1.2 Technology Stack

- **Framework**: .NET 6.0 / C# 10
- **Plugin Infrastructure**: BepInEx 6 (Unity IL2CPP)
- **Command Framework**: VampireCommandFramework
- **Patch System**: HarmonyLib
- **ECS**: Unity Entities (DOTS) for high-performance game systems
- **Serialization**: System.Text.Json with custom converters
- **Configuration**: TOML/JSON formats

### 1.3 Core Design Patterns

| Pattern | Implementation | Purpose |
|---------|----------------|---------|
| **Singleton** | [`Singleton<T>`](Vlifecycle/Core/Patterns/Singleton.cs) | Thread-safe singleton for services |
| **Service Manager** | [`ServiceManager`](ServiceManager.cs) | Centralized service registry |
| **Queue Service** | [`QueueService`](QueueService.cs) | Task execution queue with threading |
| **ECS Components** | `IComponentData` structs | Data containers for entities |
| **Harmony Patching** | `[HarmonyPatch]` attributes | Game method interception |

---

## 2. Complete File Inventory

### 2.1 Core Plugin Projects

#### VAutomationCore
```
VAutomationCore/
├── Core_VRCore.cs          # V Rising core access (World, EntityManager, ServerGameManager)
├── IService.cs             # Service interfaces (IService, IEntityService, IArenaService, IPlayerService)
├── Configuration/
│   ├── JsonConfigManager.cs
│   ├── PluginGuidRegistry.cs
│   └── PluginManifest.cs
└── Json/
    ├── PrefabGuidJsonConverter.cs
    └── VAutoJsonOptions.cs
```

#### VAutoannounce
```
VAutoannounce/
├── Plugin.cs               # BepInEx entry point
├── Commands/
│   └── Core/
│       └── AnnounceCommands.cs
├── Core/
│   ├── PrefabGuidConverter.cs
│   └── VRCore.cs
└── Services/
    └── AnnouncementService.cs
```

#### VAutoArena
```
VAutoArena/
├── Plugin.cs               # BepInEx entry point
├── Commands/
│   ├── Arena/
│   │   ├── ArenaCommands.cs
│   │   └── ZoneGlowCommands.cs
│   ├── Core/
│   │   └── Arena/
│   └── Zone/
│       └── ZoneSetCommands.cs
├── Core/
│   ├── Constants.cs
│   ├── PrefabGuidConverter.cs
│   └── VRCore.cs
├── Models/
│   └── GlowZonesConfig.cs
└── Services/
    ├── ArenaGlowBorderService.cs
    ├── ArenaPlayerService.cs
    ├── ArenaTerritory.cs
    ├── ArenaZoneConfigLoader.cs
    ├── SimpleToml.cs
    ├── ZoneGlowBorderService.cs
    └── ZoneGlowRotationService.cs
```

#### VAutoTraps
```
VAutoTraps/
├── Plugin.cs               # BepInEx entry point
├── Commands/
│   └── Core/
│       └── TrapCommands.cs
├── Configuration/
│   └── killstreak_trap_config.toml
├── Convertor/
│   └── JSONConverters.cs
├── Core/
│   ├── PrefabGuidConverter.cs
│   └── VRCore.cs
├── Data/
│   └── ChestRewardTypes.cs
└── Services/
    ├── SimpleToml.cs
    ├── Rules/
    │   └── TrapSpawnRules.cs
    └── Traps/
        ├── ChestSpawnService.cs
        ├── ContainerTrapService.cs
        └── TrapZoneService.cs
```

#### Vlifecycle
```
Vlifecycle/
├── Plugin.cs               # BepInEx entry point
├── Commands/
│   └── Lifecycle/
│       └── LifecycleCommands.cs
├── Configuration/
│   └── pvp_item.json/toml
├── Core/
│   ├── PrefabGuidConverter.cs
│   ├── VRCore.cs
│   ├── Components/
│   ├── Configuration/
│   │   ├── PVPLifecycleConfigLoader.cs
│   │   └── SimpleToml.cs
│   ├── Lifecycle/
│   │   ├── ArenaLifecycleManager.cs
│   │   ├── AutoEnterService.cs
│   │   ├── BuildingService.cs
│   │   ├── IArenaLifecycleService.cs
│   │   ├── KitConfigService.cs
│   │   ├── KitLifecycleService.cs
│   │   ├── KitRecordsService.cs
│   │   ├── LifecycleActionHandlers.cs
│   │   ├── LifecycleStepsConfig.cs
│   │   ├── LocationTracker.cs
│   │   ├── PVPItemLifecycle.cs
│   │   ├── ServiceManager.cs
│   │   ├── SnapshotLifecycleService.cs
│   │   └── Snapshots/
│   │       ├── CharacterSnapshotModels.cs
│   │       ├── SnapshotStore.cs
│   │       └── Sections/
│   │           ├── BuffGlowSectionSaver.cs
│   │           ├── EquipmentSectionSaver.cs
│   │           ├── InventorySectionSaver.cs
│   │           ├── ISnapshotSectionSaver.cs
│   │           ├── JewelSocketSectionSaver.cs
│   │           ├── SpellbookSectionSaver.cs
│   │           └── VBloodSectionSaver.cs
│   └── Patterns/
│       └── Singleton.cs
├── EndGameKit/
│   ├── Configuration/
│   ├── Helpers/
│   └── Services/
├── Services/
│   └── Interfaces/
│       └── IService.cs
```

### 2.2 Shared Projects Features

```
Projects_features/
├── ContainerInteractionPatch.cs
├── ContainerInteractPatch.cs
├── DeathEventHook.cs
├── KillStreakTrackingSystem.cs
├── RepairVBloodProgressionSystemPatch.cs
├── ServerShutdownInterceptor.cs
├── Announcement/
│   └── AnnouncementService.cs
├── Arena/
├── Automation/
│   ├── AutomationService.cs
│   └── AutomationTasks.cs
├── Components/
│   ├── ChestSpawnComponents.cs
│   ├── ContainerTrapComponents.cs
│   ├── EndGameKitComponents.cs
│   ├── KillStreakComponents.cs
│   ├── NotificationComponents.cs
│   ├── RegionComponents.cs
│   ├── TrapSystemComponents.cs
│   ├── WaypointMapComponents.cs
│   ├── WaypointTrapComponents.cs
│   ├── ZoneComponents.cs
│   ├── Lifecycle/
│   │   ├── LifecycleComponents.cs
│   │   ├── RepairComponents.cs
│   │   ├── SpellbookComponents.cs
│   │   ├── VBloodComponents.cs
│   │   └── VBloodSnapshotComponents.cs
│   └── Test/
│       └── MinimalTestComponent.cs
├── EndGameKit/
│   ├── ConsumableService.cs
│   ├── EndGameEquipmentRegistry.cs
│   ├── EndGameKitConfigService.cs
│   ├── EndGameKitSystem.cs
│   ├── EquipmentService.cs
│   ├── JewelService.cs
│   ├── StatExtensionService.cs
│   ├── Configuration/
│   │   └── EndGameKitProfile.cs
│   └── KitHelpers/
│       ├── EquipmentSlotConverter.cs
│       ├── GuidHelper.cs
│       └── PlayerHelper.cs
├── Glow/
├── Lifecycle/
│   ├── LifecycleActionHandlers.cs
│   └── PVPItemLifecycle.cs
├── PvP/
└── Traps/
    ├── ChestSpawnService.cs
    ├── ContainerTrapService.cs
    ├── KillStreakTrapService.cs
    ├── TrapSystemService.cs
    └── TrapZoneService.cs
```

### 2.3 Shared Utilities

```
Helpers/
├── ECSServiceManager.cs
├── EntityHelper.cs
├── EntityQueryHelper.cs
└── ZoneHelper.cs

Logging/
├── LogComponents.cs
├── PerformanceBudget.cs
├── StructuredLog.cs
└── VAutoLogger.cs

Models/
└── PlayerData.cs

API/
├── LifecycleAPI.cs
├── VAutoEvents.cs
└── ZoneAPI.cs

Commands/
├── ArenaCommands.cs
├── KitCommands.cs
├── LifecycleCommands.cs
├── PlayerCommands.cs
├── PortalCommands.cs
├── PvPCommands.cs
├── SpawnCommands.cs
├── ZoneCommands.cs
├── Arena/
│   └── ArenaEnterExitCommands.cs
├── Core/
│   ├── AnnounceCommands.cs
│   ├── AutomationCommands.cs
│   ├── ContainerTrapCommands.cs
│   ├── DebugCommands.cs
│   ├── GlowCommands.cs
│   ├── KillStreakCommands.cs
│   ├── PlayerCommands.cs
│   ├── SpawnChestCommands.cs
│   ├── TrapSystemCommands.cs
│   ├── VBloodRepairCommands.cs
│   ├── ZoneCommands.cs
│   └── ZoneTrapCommands.cs

Networking/
├── StateSerializer.cs
├── WireProtocol.cs
└── WireService.cs

Data/
├── ChestRewardTypes.cs
├── GameDataModels.cs
└── Prefabs.cs

Convertor/
├── JSONConverters.cs
├── Vector2Converter.cs
└── Vector3Converter.cs

Core/
└── Constants.cs
```

---

## 3. Class and Interface Structure

### 3.1 Inheritance Hierarchies

#### Service Layer
```
IService (Interface)
├── IEntityService (Interface)
│   └── IArenaService (Interface)
└── Concrete Services
    ├── AnnouncementService (Static)
    ├── ArenaGlowBorderService
    ├── ArenaPlayerService
    ├── ArenaTerritory
    ├── AutoEnterService
    ├── BuildingService
    ├── ChestSpawnService
    ├── ContainerTrapService
    ├── GlowZonesService
    ├── KitConfigService
    ├── KitLifecycleService
    ├── KitRecordsService
    ├── LocationTracker
    ├── PVPItemLifecycle (Singleton)
    ├── SnapshotLifecycleService
    ├── TrapSpawnRules (Static)
    ├── TrapZoneService
    └── ZoneGlowBorderService
```

#### ECS Components (IComponentData)
```
IComponentData
├── KillStreak
├── KillStreakConfig
├── ActiveKillStreak
├── KillStreakAnnouncement
├── KillFeedEntry (BufferElementData)
├── ChatNotification
├── ChestSpawnComponents
├── ContainerTrapComponents
├── EndGameKitComponents
├── NotificationComponents
├── RegionComponents
├── TrapSystemComponents
├── WaypointMapComponents
├── WaypointTrapComponents
├── ZoneComponents
└── Lifecycle Components
    ├── LifecycleComponents
    ├── RepairComponents
    ├── SpellbookComponents
    ├── VBloodComponents
    └── VBloodSnapshotComponents
```

#### ECS Systems (SystemBase)
```
SystemBase
├── KillStreakTrackingSystem
├── AutomationService
├── AutomationTasks
├── ConsumableService
├── EndGameEquipmentRegistry
├── EndGameKitConfigService
├── EndGameKitSystem
├── EquipmentService
├── JewelService
├── StatExtensionService
├── ChestSpawnService
├── ContainerTrapService
├── KillStreakTrapService
├── TrapSystemService
└── TrapZoneService
```

#### Singleton Pattern
```
Singleton<T> (Abstract)
└── Instances
    ├── ServiceManager
    ├── ArenaLifecycleManager
    └── PVPItemLifecycle
```

### 3.2 Interface Relationships

```csharp
// IService - Base service contract
public interface IService
{
    bool IsInitialized { get; }
    ManualLogSource Log { get; }
    void Initialize();
    void Cleanup();
}

// IEntityService - Entity management
public interface IEntityService : IService
{
    bool RegisterEntity(Entity entity);
    bool UnregisterEntity(Entity entity);
    int GetEntityCount();
}

// IArenaService - Arena-specific operations
public interface IArenaService : IEntityService
{
    bool CreateArena(string arenaId, float3 center, float radius);
    bool DeleteArena(string arenaId);
    List<string> GetArenaIds();
    int GetArenaCount();
}

// IPlayerService - Player operations
public interface IPlayerService : IService
{
    // Player-specific methods
}
```

---

## 4. Method Inventory

### 4.1 Core Services

#### [`VRCore`](VAutomationCore/Core_VRCore.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| ServerWorld | `static World ServerWorld` | Gets the server world instance |
| EM | `static EntityManager EM` | Gets the entity manager |
| ServerGameManager | `static ServerGameManager ServerGameManager` | Gets the server game manager |
| ServerScriptMapper | `static ServerScriptMapper ServerScriptMapper` | Gets the server script mapper |
| Initialize | `static void Initialize()` | Initializes core services |
| ResetInitialization | `static void ResetInitialization()` | Resets initialization state |

#### [`Singleton<T>`](Vlifecycle/Core/Patterns/Singleton.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Instance | `static T Instance` | Gets the singleton instance |
| Reset | `static void Reset()` | Resets the singleton instance |
| IsInitialized | `static bool IsInitialized` | Checks if instance exists |

#### [`ServiceManager`](ServiceManager.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `void Initialize()` | Initializes all registered services |
| RegisterService<T> | `void RegisterService<T>()` | Registers a service |
| GetService<T> | `T GetService<T>()` | Gets a service by type |
| GetAllServices | `IEnumerable<IService> GetAllServices()` | Gets all registered services |

#### [`QueueService`](QueueService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Start | `void Start()` | Starts the queue service |
| EnqueueTask | `void EnqueueTask(ArenaTask task)` | Adds a task to the queue |
| ExecuteImmediate | `void ExecuteImmediate(ArenaTask task)` | Executes a task immediately |
| Stop | `void Stop()` | Stops the queue service |

### 4.2 Announcement Service

#### [`AnnouncementService`](VAutoannounce/Services/AnnouncementService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `static void Initialize()` | Initializes the announcement service |
| Broadcast | `static void Broadcast(string message, NotifyType type)` | Broadcasts to all players |
| SendTo | `static void SendTo(ulong platformId, string message, NotifyType type)` | Sends to specific player |
| BroadcastTrapTrigger | `static void BroadcastTrapTrigger(string playerName, string trapOwnerName, bool isContainerTrap)` | Announces trap triggers |
| NotifyTrapOwner | `static void NotifyTrapOwner(string ownerName, ulong ownerPlatformId, string intruderName, string location)` | Notifies trap owner |
| AnnounceKillStreak | `static void AnnounceKillStreak(string playerName, int streak)` | Announces kill streak milestones |
| AnnounceChestSpawn | `static void AnnounceChestSpawn(string playerName, int waypointIndex, string waypointName)` | Announces chest spawns |

### 4.3 Arena Services

#### [`ArenaTerritory`](VAutoArena/Services/ArenaTerritory.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| InitializeArenaGrid | `static void InitializeArenaGrid()` | Initializes the arena grid |
| InitializeFromTerritory | `static void InitializeFromTerritory()` | Loads territory config |
| CreateArena | `static bool CreateArena(string arenaId, float3 center, float radius)` | Creates a new arena |
| DeleteArena | `static bool DeleteArena(string arenaId)` | Deletes an arena |

#### [`ArenaGlowBorderService`](VAutoArena/Services/ArenaGlowBorderService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| SpawnBorderGlows | `static bool SpawnBorderGlows(string configPath, string prefab, float spacing, out string error)` | Spawns glow borders |
| GetDefaultPrefabName | `static string GetDefaultPrefabName()` | Gets the default glow prefab |

#### [`ZoneGlowBorderService`](VAutoArena/Services/ZoneGlowBorderService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| SpawnZoneGlow | `static bool SpawnZoneGlow(string zoneId, float3 center, float radius)` | Spawns zone glow |
| RemoveZoneGlow | `static bool RemoveZoneGlow(string zoneId)` | Removes zone glow |

### 4.4 Trap Services

#### [`KillStreakTrapRules`](Projects_features/Traps/KillStreakTrapService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `static void Initialize()` | Initializes trap rules |
| OnPlayerDeath | `static void OnPlayerDeath(ulong killerPlatformId, ulong victimPlatformId)` | Handles player death |
| ResetStreak | `static void ResetStreak(ulong platformId)` | Resets player streak |
| SpawnTrap | `static Entity SpawnTrap(ulong ownerId, float3 position)` | Spawns a trap |

#### [`ContainerTrapService`](VAutoTraps/Services/Traps/ContainerTrapService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `static void Initialize()` | Initializes container traps |
| RegisterTrap | `static void RegisterTrap(Entity trapEntity, ulong ownerId)` | Registers a trap |
| OnTriggered | `static void OnTriggered(Entity trapEntity, Entity triggerEntity)` | Handles trap trigger |

#### [`ChestSpawnService`](VAutoTraps/Services/Traps/ChestSpawnService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `static void Initialize()` | Initializes chest spawning |
| SpawnChest | `static Entity SpawnChest(float3 position, int waypointIndex)` | Spawns a chest |
| OnPlayerKill | `static void OnPlayerKill(ulong playerPlatformId)` | Handles player kills |

### 4.5 Lifecycle Services

#### [`PVPItemLifecycle`](Vlifecycle/Core/Lifecycle/PVPItemLifecycle.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `void Initialize()` | Initializes the lifecycle system |
| Shutdown | `void Shutdown()` | Shuts down the system |
| Cleanup | `void Cleanup()` | Cleans up resources |
| TriggerLifecycleStage | `bool TriggerLifecycleStage(string stageName, LifecycleContext context)` | Triggers a lifecycle stage |
| RegisterActionHandler | `void RegisterActionHandler(string actionType, LifecycleActionHandler handler)` | Registers action handler |

#### [`SnapshotLifecycleService`](Vlifecycle/Core/Lifecycle/SnapshotLifecycleService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| TakeSnapshot | `Entity TakeSnapshot(Entity player)` | Takes player snapshot |
| RestoreSnapshot | `bool RestoreSnapshot(Entity player, Entity snapshot)` | Restores player snapshot |
| DeleteSnapshot | `void DeleteSnapshot(Entity player)` | Deletes player snapshot |

#### [`KitLifecycleService`](Vlifecycle/Core/Lifecycle/KitLifecycleService.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| ApplyKit | `bool ApplyKit(Entity player, string kitName)` | Applies kit to player |
| RemoveKit | `bool RemoveKit(Entity player, string kitName)` | Removes kit from player |
| SetKitService | `void SetKitService(IKitService service)` | Sets the kit service |

### 4.6 ECS Systems

#### [`KillStreakTrackingSystem`](Projects_features/KillStreakTrackingSystem.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| OnCreate | `void OnCreate()` | Creates entity queries |
| OnUpdate | `void OnUpdate()` | Processes kill/death events |
| ProcessKill | `void ProcessKill(Entity killer, Entity victim, double currentTime, KillStreakConfig config, EntityCommandBuffer ecb)` | Processes a kill event |
| ProcessDeath | `void ProcessDeath(Entity victim, double currentTime, KillStreakConfig config, EntityCommandBuffer ecb)` | Processes a death event |

#### [`EndGameKitSystem`](Projects_features/EndGameKit/EndGameKitSystem.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `void Initialize()` | Initializes the system |
| ApplyKit | `bool ApplyKit(Entity player, string kitName)` | Applies a kit |
| RemoveKit | `bool RemoveKit(Entity player, string kitName)` | Removes a kit |
| ReloadConfig | `void ReloadConfig()` | Reloads configuration |

### 4.7 Hooks and Patches

#### [`DeathEventHook`](Projects_features/DeathEventHook.cs)
| Method | Signature | Purpose |
|--------|-----------|---------|
| Initialize | `static void Initialize()` | Initializes the hook |
| RegisterKill | `static void RegisterKill(ulong killerPlatformId, ulong victimPlatformId)` | Registers a kill |
| RegisterDeath | `static void RegisterDeath(ulong victimPlatformId)` | Registers a death |

#### Harmony Patches
| Class | Patch Type | Target |
|-------|------------|--------|
| `ContainerInteractionPatch` | Prefix/Postfix | Container interactions |
| `ContainerInteractPatch` | Prefix/Postfix | Container use |
| `RepairVBloodProgressionSystemPatch` | Prefix/Postfix | VBlood progression |
| `ServerShutdownInterceptor` | Prefix | Server shutdown |

---

## 5. Dependency Mapping

### 5.1 Plugin Dependencies

```
VAutomationCore (No dependencies)
    ↓
    ├── VAutoannounce (Depends on Core)
    ├── VAutoArena (Depends on Core)
    ├── VAutoTraps (Depends on Core)
    └── Vlifecycle (Depends on Core)
```

### 5.2 External Dependencies

| Plugin | External Dependency | Purpose |
|--------|-------------------|---------|
| All | `BepInEx.Core` | Plugin infrastructure |
| All | `HarmonyLib` | Method patching |
| All | `VampireCommandFramework` | Command registration |
| All | `Unity.Entities` | ECS framework |
| All | `ProjectM.*` | V Rising game APIs |
| All | `Stunlock.*` | V Rising networking |

### 5.3 Internal Service Dependencies

```csharp
// ServiceManager initializes services in order:
ServiceManager
├── PVPItemLifecycle (Singleton)
├── GlowZonesService
├── ArenaLifecycleManager
│   ├── SnapshotLifecycleService
│   ├── AutoEnterService
│   ├── BuildingService
│   ├── LocationTracker
│   └── KitLifecycleService
└── Other registered services
```

### 5.4 Component Dependencies

```csharp
// KillStreakComponents require:
KillStreak → PlayerCharacter (for entity lookup)
KillStreakConfig → Singleton entity
DeathEvent → DamageEvent (for kill confirmation)

// TrapComponents require:
ContainerTrapComponents → Owner component
TrapSystemComponents → Position component
```

---

## 6. Data Flow Analysis

### 6.1 Kill Streak Data Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Player Death   │────▶│ DeathEventHook  │────▶│ KillStreakTrack │
│   (ECS Event)   │     │ (Harmony Patch) │     │   ingSystem     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                       │
                                                       ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Announcement   │◀────│  Announcement   │◀────│ Kill Streak     │
│    Service      │     │    Component    │     │ Config          │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │
        ▼
┌─────────────────┐
│   Player Chat   │
│   (Broadcast)   │
└─────────────────┘
```

### 6.2 Arena Territory Data Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Config Load   │────▶│ ArenaTerritory  │────▶│ Territory Grid  │
│   (JSON/TOML)   │     │   Initialize    │     │   (ECS Query)   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                       │
        ┌──────────────────────────────────────────────┘
        ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Glow Border    │◀────│ ArenaGlowBorder │◀────│ Glow Zones      │
│    Service      │     │    Service      │     │   Config        │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

### 6.3 Lifecycle Data Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Event Trigger │────▶│ PVPItemLifecycle│────▶│ LifecycleStage  │
│   (Enter/Exit)  │     │   TriggerStage  │     │    Execution    │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                                               │
        │                                               ▼
        │                                       ┌─────────────────┐
        │                                       │ Action Handlers │
        │                                       │ (ApplyKit, etc) │
        │                                       └─────────────────┘
        │                                               │
        ▼                                               ▼
┌─────────────────┐                             ┌─────────────────┐
│  Player State   │                             │  Service Calls  │
│   (Snapshot)    │                             │ (Equip, Apply)  │
└─────────────────┘                             └─────────────────┘
```

### 6.4 Trap System Data Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Player Enter   │────▶│  Trap Zone      │────▶│ Trap Trigger    │
│   Zone/Container│     │   Detection     │     │   Check         │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                       │
                               ┌─────────────────────────┤
                               │                         ▼
                               │               ┌─────────────────┐
                               │               │ Kill Streak     │
                               │               │   Increment     │
                               │               └─────────────────┘
                               │                         │
                               ▼                         ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Container     │◀────│ Trap Spawning   │◀────│ Spawn Rules     │
│    Spawn        │     │   Service       │     │   (Config)      │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

### 6.5 Configuration Data Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Config File   │────▶│ JsonConfigManager│───▶│ Service Config  │
│   (JSON/TOML)   │     │   / TomlLoader  │     │   (In-Memory)   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                                                       │
        ┌──────────────────────────────────────────────┘
        ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Hot Reload    │────▶│ Config Change   │────▶│ Service Update  │
│   (File Watch)  │     │   Detection     │     │   (Dynamic)     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

---

## 7. Control Flow Patterns

### 7.1 Plugin Loading Sequence

```csharp
// All plugins follow this pattern:
1. BasePlugin.Load()
   ├── Read plugin manifest (MyPluginInfo)
   ├── Initialize Harmony (if enabled)
   ├── Apply Harmony patches
   ├── Initialize core services
   └── Register commands

// Vlifecycle has additional complexity:
├── Initialize PVPItemLifecycle (Singleton)
├── Load PVPLifecycleConfigLoader
├── Create service instances
│   ├── SnapshotLifecycleService
│   ├── AutoEnterService
│   ├── BuildingService
│   ├── LocationTracker
│   └── KitLifecycleService
├── Register services with ServiceManager
├── Initialize all services
└── Register arena lifecycle manager
```

### 7.2 Command Execution Pattern

```
User Input (RCON/Chat)
        │
        ▼
VampireCommandFramework (CommandRegistry)
        │
        ▼
Command Handler Method
        │
        ├── Validate input
        ├── Check permissions
        ├── Call service method
        └── Return result
```

### 7.3 ECS System Execution

```csharp
// Standard ECS SystemBase pattern:
public partial class MySystem : SystemBase
{
    protected override void OnCreate()
    {
        // Initialize entity queries
        // Create required components
    }
    
    protected override void OnUpdate()
    {
        // Get EntityCommandBuffer
        // Query entities
        // Process data
        // Write changes
    }
    
    protected override void OnDestroy()
    {
        // Cleanup
    }
}
```

### 7.4 Async/Task Patterns

#### QueueService Pattern
```csharp
// Background thread processing:
public void ProcessQueue()
{
    while (!_cts.Token.IsCancellationRequested)
    {
        ArenaTask task = null;
        
        lock (_lock)
        {
            while (_taskQueue.Count == 0)
            {
                Monitor.Wait(_lock, timeout);
            }
            task = _taskQueue.Dequeue();
        }
        
        if (task != null)
        {
            task.Execute();
        }
    }
}
```

### 7.5 Event/Hook Patterns

#### Static Event System
```csharp
// VAutoEvents (Cross-mod API)
public static class VAutoEvents
{
    public static event Action<Entity, string>? OnPlayerEnteredZone;
    public static event Action<Entity, string>? OnPlayerExitedZone;
    public static event Action<Entity, int>? OnKillStreakMilestone;
    
    public static void RaisePlayerEnteredZone(Entity player, string zoneName)
    {
        OnPlayerEnteredZone?.Invoke(player, zoneName);
    }
}
```

#### Harmony Patch Pattern
```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
[HarmonyPostfix]
static void TargetMethod_Postfix(/* parameters */)
{
    // Execute after original method
    // Can modify return value (via ref) or side effects
}
```

---

## 8. Architecture Overview

### 8.1 Layer Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Presentation Layer                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │  Commands   │  │   Events    │  │   Cross-Mod API         │  │
│  │ (VCF-based) │  │  (Static)   │  │   (VAutoEvents)         │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                      Application Layer                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │  Services   │  │   Systems   │  │   Lifecycle Managers    │  │
│  │ (Business)  │  │   (ECS)     │  │   (State Machines)      │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                       Domain Layer                               │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │ Components  │  │  Hooks/     │  │   Configuration         │  │
│  │  (Data)     │  │  Patches    │  │   (Schemas)             │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                     Infrastructure Layer                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │   VRCore    │  │  Helpers    │  │   Logging/Serialization │  │
│  │ (Game API)  │  │ (Utility)   │  │   (Json/Toml)          │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                       External Layer                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │  BepInEx    │  │  Harmony    │  │   Unity Entities        │  │
│  │  Framework  │  │  Patching   │  │   (ECS/DOTS)            │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 8.2 Design Patterns Used

| Pattern | Location | Purpose |
|---------|----------|---------|
| **Singleton** | `Singleton<T>` | Service instances |
| **Service Locator** | `ServiceManager` | Dependency resolution |
| **Facade** | `VRCore` | Simplified game API access |
| **Strategy** | `LifecycleActionHandler` | Configurable actions |
| **Observer** | `VAutoEvents` | Event-driven communication |
| **Command** | `ArenaTask` | Queue task execution |
| **Repository** | `SnapshotStore` | Player state persistence |
| **Factory** | `*Service.Create*()` | Entity/component creation |

### 8.3 Key Architectural Decisions

#### Decision 1: Hybrid ECS/GameObject Approach
**Status**: Implemented
**Rationale**: V Rising uses both Unity DOTS/ECS and traditional GameObjects
**Implementation**: ECS for high-performance systems, GameObjects for visual elements

#### Decision 2: Plugin Architecture
**Status**: Implemented
**Rationale**: Modular design for separate features
**Implementation**: 5 independent BepInEx plugins with shared core

#### Decision 3: Static Service Pattern
**Status**: Implemented
**Rationale**: Simplifies access in Harmony patches
**Implementation**: Static service classes (AnnouncementService, etc.)

#### Decision 4: Configuration Hot-Reload
**Status**: Partial
**Rationale**: Support configuration changes without server restart
**Implementation**: File watchers and dynamic config updates

### 8.4 Performance Considerations

| Area | Approach | Details |
|------|----------|---------|
| **ECS Queries** | EntityQuery caching | Pre-built queries in OnCreate |
| **Memory** | Allocator.TempJob | Temporary allocations with proper disposal |
| **Threading** | QueueService | Background thread for long-running tasks |
| **Batching** | EntityCommandBuffer | Deferred entity modifications |
| **Serialization** | System.Text.Json | Efficient JSON parsing |

### 8.5 Scalability Considerations

- **Multiplayer**: Stateless services for cluster support
- **Large Worlds**: Configurable zone/arena limits
- **High Load**: QueueService for task throttling
- **Extensibility**: Cross-mod API (VAutoEvents) for integrations

### 8.6 Future Roadmap

| Phase | Features | Status |
|-------|----------|--------|
| Phase 1 | Core framework, announcements, basic arena | ✅ Complete |
| Phase 2 | Trap systems, kill streak tracking | ✅ Complete |
| Phase 3 | Lifecycle management, kit system | ✅ Complete |
| Phase 4 | Cross-mod API, event system | ✅ Complete |
| Phase 5 | Advanced automation, performance optimization | 📋 Planned |

---

## Appendix

### A. Glossary

| Term | Definition |
|------|------------|
| **ECS** | Entity Component System (Unity DOTS) |
| **BepInEx** | Unity/IL2CPP plugin framework |
| **Harmony** | Runtime patching library |
| **VCF** | VampireCommandFramework |
| **PrefabGUID** | V Rising asset identifier |
| **EntityQuery** | ECS query for entity filtering |

### B. File Extension Reference

| Extension | Description |
|-----------|-------------|
| `.cs` | C# source files |
| `.csproj` | MSBuild project files |
| `.json` | JSON configuration |
| `.toml` | TOML configuration |
| `.md` | Markdown documentation |

### C. Related Documentation

- [REFACTORING_PLAN.md](REFACTORING_PLAN.md) - Current refactoring roadmap
- [MOD_COMPATIBILITY_MATRIX.md](MOD_COMPATIBILITY_MATRIX.md) - Cross-mod compatibility
- [ECS_SYSTEM_INTEGRATION_PLAN.md](ECS_SYSTEM_INTEGRATION_PLAN.md) - ECS integration details
- [VRISING_MOD_ARCHITECTURE.md](VRISING_MOD_ARCHITECTURE.md) - V Rising modding guide

---

*Document generated for VAutomation Mod Suite v1.0+*
*Last updated: 2024*
