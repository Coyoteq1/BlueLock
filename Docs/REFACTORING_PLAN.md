# VAutomationEvents Refactoring Plan: ECS Compliance

**Created:** 2026-01-30

**Based On:** [ECS System Integration Plan](ECS_SYSTEM_INTEGRATION_PLAN.md) + [Code Review Report](CODE_REVIEW_REPORT.md)

**Objective:** Achieve full ECS compliance by refactoring static services into proper ECS systems

---

## Executive Summary

This plan outlines the systematic refactoring required to align the VAutomationEvents codebase with the ECS System Integration Plan. The current architecture uses static service classes and manual state management, which violates core project principles. The refactoring will convert all functionality to proper ECS systems with appropriate attributes, components, and Harmony patches.

**Estimated Effort:** Multi-phase implementation (see timeline below)

**Risk Level:** Medium (breaking changes to core architecture)

**Dependencies:** V Rising ECS dump, HarmonyLib, Unity.Entities

---

## Phase 0: Preparation (Before Refactoring)

### 0.1 Backup Current Codebase

- Create branch `refactor/ecs-compliance`
- Document current test coverage
- Identify integration test points

### 0.2 Update Dependencies

- Verify Unity.Entities version compatibility
- Update HarmonyLib if needed
- Ensure BepInEx configuration is compatible

### 0.3 Create Component Definitions Directory

```text
Core/Components/
├── ZoneComponents.cs        # All zone types: Arena, PvP, Glow, Safe, Portal
├── CombatComponents.cs
├── SpawnComponents.cs
└── PersistenceComponents.cs
```

---

## Phase 1: Core ECS Component Definitions

### 1.1 Zone Components Overview

Create the data components that will replace in-memory state management.

**File:** `Core/Components/ZoneComponents.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Enum for different zone types
    /// </summary>
    public enum ZoneType : byte
    {
        None = 0,
        GlowZone = 1,
        PvPArena = 2,
        SafeZone = 3,
        MainArena = 4,
        Portal = 5
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
    /// Singleton component for zone configuration
    /// </summary>
    public struct ZoneConfig : IComponentData
    {
        public float3 MainZoneCenter;
        public float MainZoneRadius;
        public float3 PvPZoneCenter;
        public float PvPZoneRadius;
        public bool BuildingAllowed;
        public bool CraftingAllowed;
        public bool TeleportersAllowed;
    }

    /// <summary>
    /// Tag for entities inside a zone
    /// </summary>
    public struct ZoneStatus : IComponentData
    {
        public int ZoneId;
        public ZoneType ZoneType;
        public double EntryTime;
        public bool IsPvPEnabled;
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
    /// Temporary component for zone detection results.
    /// Created by ZoneDetectionSystem, consumed by ZoneTransitionSystem.
    /// </summary>
    public struct ZoneDetectionResult : IComponentData
    {
        public int DetectedZoneId;
        public ZoneType DetectedZoneType;
        public double DetectionTime;
    }
}
```

### 1.2 Combat Components Overview

**File:** `Core/Components/CombatComponents.cs`

```csharp
using Unity.Entities;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Marks entity as protected from damage (safe zone)
    /// </summary>
    public struct SafeZoneProtection : IComponentData
    {
        public double ProtectionEndTime;
        public int DamagePrevented;
    }

    /// <summary>
    /// Tracks combat state for lifecycle events
    /// </summary>
    public struct CombatLifecycleState : IComponentData
    {
        public bool InCombat;
        public double LastCombatTime;
        public Entity LastAttacker;
    }

    /// <summary>
    /// Buff application for custom automation rules
    /// </summary>
    public struct AutomationBuff : IComponentData
    {
        public int BuffType; // 0=Repair, 1=Buff, 2=Debuff
        public double Duration;
        public double AppliedTime;
    }
}
```

### 1.3 Spawn Components Overview

**File:** `Core/Components/SpawnComponents.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Custom spawn data for VAutomationEvents
    /// </summary>
    public struct CustomSpawnData : IComponentData
    {
        public int SpawnType; // 0=MainZone, 1=PvPZone, 2=Portal
        public float3 SpawnPosition;
        public bool ApplyGear;
        public int BloodTypeGuid;
        public float BloodQuality;
    }

    /// <summary>
    /// Tag for entities spawned by VAutomationEvents
    /// </summary>
    public struct VAutoSpawnedTag : IComponentData
    {
        public double SpawnTime;
    }
}
```

### 1.4 Persistence Components Overview

**File:** `Core/Components/PersistenceComponents.cs`

```csharp
using Unity.Entities;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Track when entity was last saved
    /// </summary>
    public struct LastPersistenceSave : IComponentData
    {
        public double SaveTime;
    }

    /// <summary>
    /// Marks entity as requiring persistence save
    /// </summary>
    public struct PendingPersistenceSave : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Singleton for tracking save state
    /// </summary>
    public struct PersistenceTracking : IComponentData
    {
        public bool IsSaving;
        public int PendingEntities;
        public double LastSaveTime;
    }
}
```

---

## Phase 2: Zone Enforcement System

### 2.1 Zone Detection Overview

Implement ECS system for detecting and enforcing zone rules (Arena, PvP, Glow, Safe zones).

> **Warning:** V Rising ECS Requirement: Zone detection must be split into two systems:
>
> - `ZoneDetectionSystem` - Detect current zone per player (single result)
> - `ZoneTransitionSystem` - Compare previous vs current zone and apply effects
>
> O(N×M) spatial checks (player × zone) will not scale in V Rising.

**Target Integration Point:**

- Group: `ProjectM.UpdateGroup`
- Before: `ProjectM.PlayerCombatBuffSystem_InitialApplication_Aggro`

### 2.2 Zone Detection System Implementation

**File:** `Services/Systems/ZoneDetectionSystem.cs`

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
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
                // Process each player
                foreach (var (transform, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<ProjectM.PlayerCharacter>()
                         .WithEntityAccess())
                {
                    float3 position = transform.ValueRO.Position;
                    int detectedZoneId = -1;
                    ZoneType detectedZoneType = ZoneType.None;

                    // Efficient zone lookup - single pass through zones
                    foreach (var zone in zones)
                    {
                        if (math.distance(position, zone.Center) <= zone.Radius)
                        {
                            // First matching zone wins (priority order)
                            if (detectedZoneId == -1)
                            {
                                detectedZoneId = zone.ZoneId;
                                detectedZoneType = zone.ZoneType;
                            }
                        }
                    }

                    // Store detection result for transition system
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

    /// <summary>
    /// Temporary component storing zone detection result for this frame.
    /// </summary>
    public struct ZoneDetectionResult : IComponentData
    {
        public int DetectedZoneId;
        public ZoneType DetectedZoneType;
        public double DetectionTime;
    }
}
```

### 2.3 Zone Transition System Implementation

**File:** `Services/Systems/ZoneTransitionSystem.cs`

```csharp
using Unity.Entities;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Compares current zone detection with previous zone membership.
    /// Applies/removes effects only on zone transitions.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    [UpdateAfter(typeof(ZoneDetectionSystem))]
    [UpdateBefore(typeof(ProjectM.PlayerCombatBuffSystem_InitialApplication_Aggro))]
    public partial struct ZoneTransitionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ZoneDetectionResult>();
        }

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

                // Get previous zone membership
                if (SystemAPI.HasBuffer<ZoneMembershipElement>(entity))
                {
                    var buffer = SystemAPI.GetBuffer<ZoneMembershipElement>(entity);
                    int previousZoneId = -1;

                    foreach (var membership in buffer)
                    {
                        if (membership.ZoneType == currentZoneType)
                        {
                            previousZoneId = membership.ZoneId;
                            break;
                        }
                    }

                    // Detect transition
                    if (currentZoneId != previousZoneId)
                    {
                        if (currentZoneId != -1)
                        {
                            // Entered zone
                            HandleZoneEntry(ref state, ecb, entity, currentZoneId, currentZoneType);
                        }
                        else if (previousZoneId != -1)
                        {
                            // Exited zone
                            HandleZoneExit(ref state, ecb, entity, previousZoneId, currentZoneType);
                        }
                    }
                }
                else if (currentZoneId != -1)
                {
                    // First time detection - entering zone
                    HandleZoneEntry(ref state, ecb, entity, currentZoneId, currentZoneType);
                }
            }

            // Remove temporary detection result
            var cleanupECB = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<ZoneDetectionResult>>().WithEntityAccess())
            {
                cleanupECB.RemoveComponent<ZoneDetectionResult>(entity);
            }
        }

        private void HandleZoneEntry(ref SystemState state, EntityCommandBuffer ecb,
            Entity player, int zoneId, ZoneType zoneType)
        {
            // Add zone membership
            if (!SystemAPI.HasBuffer<ZoneMembershipElement>(player))
            {
                ecb.AddComponent<ZoneMembershipElement>(player);
            }

            var buffer = SystemAPI.GetBuffer<ZoneMembershipElement>(player);
            buffer.Add(new ZoneMembershipElement
            {
                ZoneId = zoneId,
                ZoneType = zoneType,
                EntryTime = SystemAPI.Time.ElapsedTime
            });

            // Apply zone-specific effects
            if (zoneType == ZoneType.SafeZone || zoneType == ZoneType.GlowZone)
            {
                ecb.AddComponent(player, new SafeZoneProtection
                {
                    ProtectionEndTime = double.MaxValue,
                    DamagePrevented = 0
                });
            }

            Plugin.LogInstance.LogInfo($"[Zone] Entity {player.Index} entered {zoneType} (Zone {zoneId})");
        }

        private void HandleZoneExit(ref SystemState state, EntityCommandBuffer ecb,
            Entity player, int zoneId, ZoneType zoneType)
        {
            if (!SystemAPI.HasBuffer<ZoneMembershipElement>(player))
                return;

            var buffer = SystemAPI.GetBuffer<ZoneMembershipElement>(player);
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].ZoneId == zoneId && buffer[i].ZoneType == zoneType)
                {
                    buffer.RemoveAt(i);

                    // Remove zone-specific effects
                    if (zoneType == ZoneType.SafeZone || zoneType == ZoneType.GlowZone)
                    {
                        ecb.RemoveComponent<SafeZoneProtection>(player);
                    }

                    Plugin.LogInstance.LogInfo($"[Zone] Entity {player.Index} exited {zoneType} (Zone {zoneId})");
                    break;
                }
            }
        }
    }
}
```

### 2.4 Zone Configuration System Implementation

**File:** `Services/Systems/ZoneConfigSystem.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Initializes zone configuration from plugin config.
    /// Creates ZoneBoundary entities at startup.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.InitializationSystemGroup))]
    public partial class ZoneConfigSystem : SystemBase
    {
        private bool _initialized = false;

        protected override void OnUpdate()
        {
            // Run once at startup only
            if (_initialized || SystemAPI.Time.ElapsedTime > 0.1f)
                return;

            _initialized = true;

            // Get config values
            var zoneEnabled = Plugin.ZoneEnable?.Value ?? false;
            var glowZonesEnabled = Plugin.GlowZonesEnabled?.Value ?? true;

            if (!glowZonesEnabled)
                return;

            var entityManager = EntityManager;

            // Create zone entities from configuration
            // Example: Main Arena Zone
            CreateZoneBoundary(entityManager, 1, ZoneType.MainArena,
                new float3(-1000f, 5f, -500f), 50f, false, false);

            // Example: PvP Arena Zone
            CreateZoneBoundary(entityManager, 2, ZoneType.PvPArena,
                new float3(-1500f, 5f, -1000f), 75f, true, false);

            // Example: Glow/Safe Zone
            CreateZoneBoundary(entityManager, 3, ZoneType.GlowZone,
                new float3(0f, 0f, 0f), 30f, false, true);
        }

        private void CreateZoneBoundary(EntityManager em, int zoneId, ZoneType zoneType,
            float3 center, float radius, bool pvpEnabled, bool safeZone)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new ZoneBoundary
            {
                ZoneId = zoneId,
                ZoneType = zoneType,
                Center = center,
                Radius = radius,
                PvPEnabled = pvpEnabled,
                SafeZone = safeZone
            });

            Plugin.LogInstance.LogInfo(
                $"[ZoneConfig] Created {zoneType} (ID: {zoneId}) at {center} with radius {radius}");
        }
    }
}
```

---

## Phase 3: Portal Intercept System

### 3.1 Portal Intercept Overview

Intercept and validate teleport/portal events before they execute.

**Target Integration Point:**

- Group: `ProjectM.UpdateGroup`
- Before: `ProjectM.Gameplay.Systems.TeleportSystem`

### 3.2 Portal Intercept System Implementation

**File:** `Services/Systems/PortalInterceptSystem.cs`

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Intercepts teleport requests and applies custom validation rules.
    /// Properly cleans up all teleport-related components when cancelling.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    [UpdateBefore(typeof(ProjectM.Gameplay.Systems.TeleportSystem))]
    public partial struct PortalInterceptSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectM.TeleportRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (request, entity) in
                     SystemAPI.Query<RefRW<ProjectM.TeleportRequest>>()
                     .WithEntityAccess())
            {
                var sourceEntity = request.ValueRO.SourceEntity;
                var destination = request.ValueRO.Destination;

                // Validate teleport request
                if (ValidateTeleport(ref state, sourceEntity, destination))
                {
                    // Log for tracking
                    Plugin.LogInstance.LogInfo(
                        $"[Portal] Teleport approved: Entity {sourceEntity.Index} -> {destination}");
                }
                else
                {
                    // Cancel teleport with proper cleanup
                    CancelTeleport(ref state, ecb, sourceEntity, entity);

                    Plugin.LogInstance.LogInfo(
                        $"[Portal] Teleport cancelled for entity {sourceEntity.Index}");
                }
            }
        }

        private bool ValidateTeleport(ref SystemState state, Entity entity, float3 destination)
        {
            // Check if destination is valid
            if (math.all(destination == float3.zero))
                return false;

            // Check if entity is in restricted area
            if (IsInRestrictedArea(ref state, entity))
                return false;

            // Check cooldown
            if (HasTeleportCooldown(entity))
                return false;

            return true;
        }

        private bool IsInRestrictedArea(ref SystemState state, Entity entity)
        {
            // Check zone restrictions
            if (SystemAPI.HasBuffer<ZoneMembershipElement>(entity))
            {
                var buffer = SystemAPI.GetBuffer<ZoneMembershipElement>(entity);
                foreach (var membership in buffer)
                {
                    if (membership.ZoneType == ZoneType.MainArena ||
                        membership.ZoneType == ZoneType.PvPArena)
                    {
                        // Arena zones may restrict teleporting
                        return !Plugin.AllowTeleporters.Value;
                    }
                }
            }
            return false;
        }

        private bool HasTeleportCooldown(Entity entity)
        {
            // Implement cooldown tracking if needed
            return false;
        }

        private void CancelTeleport(ref SystemState state, EntityCommandBuffer ecb,
            Entity sourceEntity, Entity requestEntity)
        {
            // Proper cleanup of all teleport-related state
            // This prevents ghost teleports and client/server mismatch

            // Remove the request entity
            ecb.DestroyEntity(requestEntity);

            // Remove TeleportRequest from source (if present as component)
            if (SystemAPI.HasComponent<ProjectM.TeleportRequest>(sourceEntity))
            {
                ecb.RemoveComponent<ProjectM.TeleportRequest>(sourceEntity);
            }

            // Remove TeleportTarget if present
            if (SystemAPI.HasComponent<ProjectM.TeleportTarget>(sourceEntity))
            {
                ecb.RemoveComponent<ProjectM.TeleportTarget>(sourceEntity);
            }

            // Optional: Add teleport failed event for client notification
            ecb.AddComponent(sourceEntity, new TeleportFailedEvent
            {
                Reason = "Zone restriction",
                Timestamp = SystemAPI.Time.ElapsedTime
            });
        }
    }

    /// <summary>
    /// Event added when teleport is cancelled (for client notification).
    /// </summary>
    public struct TeleportFailedEvent : IComponentData
    {
        public string Reason;
        public double Timestamp;
    }
}
```

---

## Phase 4: Command Parsing System

### 4.1 Command Parsing Overview

Parse commands before chat system processes them.

**Target Integration Point:**

- Group: `ProjectM.UpdateGroup`
- Before: `ProjectM.ChatMessageSystem`

### 4.2 Command Parsing System Implementation

**File:** `Services/Systems/CommandParsingSystem.cs`

```csharp
using Unity.Entities;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Processes chat commands before they reach the chat system.
    /// Commands are identified by prefix and consumed if valid.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    [UpdateBefore(typeof(ProjectM.ChatMessageSystem))]
    public partial struct CommandParsingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectM.ChatMessageEvent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (chatEvent, entity) in
                     SystemAPI.Query<RefRO<ProjectM.ChatMessageEvent>>()
                     .WithEntityAccess())
            {
                string message = chatEvent.ValueRO.Message;
                Entity sender = chatEvent.ValueRO.UserEntity;

                // Check if message is a command
                if (message.StartsWith(".") || message.StartsWith("/"))
                {
                    // Parse and execute command
                    bool commandExecuted = ExecuteCommand(ref state, sender, message);

                    if (commandExecuted)
                    {
                        // Remove from chat buffer to prevent global display
                        ecb.RemoveComponent<ProjectM.ChatMessageEvent>(entity);

                        Plugin.LogInstance.LogInfo(
                            $"[Command] Executed command from entity {sender.Index}: {message}");
                    }
                }
            }
        }

        private bool ExecuteCommand(ref SystemState state, Entity sender, string message)
        {
            var parts = message.Split(' ', 2);
            var commandName = parts[0].TrimStart('.', '/').ToLowerInvariant();
            var args = parts.Length > 1 ? parts[1] : "";

            // Route to command handler (existing CommandExecutor logic)
            // This would integrate with the existing command system

            return true; // Command was handled
        }
    }
}
```

---

## Phase 5: Server Shutdown Hook (Harmony)

### 5.1 Server Shutdown Hook Overview

Intercept server shutdown to inject custom logic.

**Integration:** Harmony Patch on `ProjectM.ServerShutdownSystem.OnUpdate`

### 5.2 Harmony Patch Implementation

**File:** `Core/Harmony/ServerShutdownInterceptor.cs`

```csharp
using HarmonyLib;
using ProjectM;
using Unity.Entities;

namespace VAuto.Core.Harmony
{
    /// <summary>
    /// Intercepts server shutdown events to inject custom logic.
    /// </summary>
    [HarmonyPatch(typeof(ServerShutdownSystem))]
    [HarmonyPatch("OnUpdate")]
    public class ServerShutdownInterceptor
    {
        static bool Prefix(ServerShutdownSystem __instance)
        {
            // Check if shutdown should be cancelled
            if (ShouldCancelShutdown())
            {
                Plugin.LogInstance.LogInfo("[ServerShutdown] Shutdown cancelled by VAutomationEvents");
                return false; // Skip original method
            }

            // Inject custom countdown messages
            InjectShutdownMessages(__instance);

            return true; // Continue with original method
        }

        private static bool ShouldCancelShutdown()
        {
            // Check for pending operations or player vote
            // This would integrate with custom shutdown prevention logic
            return false;
        }

        private static void InjectShutdownMessages(ServerShutdownSystem system)
        {
            // Inject custom messages before shutdown
            Plugin.LogInstance.LogInfo("[ServerShutdown] Custom shutdown message injected");
        }
    }
}
```

### 5.3 Plugin Registration Implementation

**File:** `Plugin.cs` - Update `Load()` method:

```csharp
// In Load() method, after _harmony creation:
_harmony.PatchAll(typeof(VAuto.Core.Harmony.ServerShutdownInterceptor).Assembly);
```

---

## Phase 6: Persistence Save Hook

### 6.1 Persistence Save Hook Overview

Trigger custom data dumps when server saves.

**Target Integration Point:**

- Group: `ProjectM.StartSimulationGroup`
- After: `ProjectM.AfterDestroyGroup_Server.TriggerPersistenceSaveSystem`

### 6.2 Persistence Save Hook System Implementation

**File:** `Services/Systems/PersistenceSaveHookSystem.cs`

```csharp
using Unity.Entities;
using Unity.Collections;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Hooks into persistence save system to trigger custom data exports.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.StartSimulationGroup))]
    [UpdateAfter(typeof(ProjectM.TriggerPersistenceSaveSystem))]
    public partial struct PersistenceSaveHookSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Detect save is in progress
            if (!SystemAPI.HasSingleton<PersistenceTracking>())
                return;

            var tracking = SystemAPI.GetSingleton<PersistenceTracking>();

            if (!tracking.IsSaving)
            {
                // Check if save just started (trigger from TriggerPersistenceSaveSystem)
                bool saveStarting = DetectSaveStart(ref state);

                if (saveStarting)
                {
                    StartCustomSave(ref state);
                }
            }
            else
            {
                // Check if save completed
                bool saveComplete = DetectSaveComplete(ref state);

                if (saveComplete)
                {
                    CompleteCustomSave(ref state);
                }
            }
        }

        private bool DetectSaveStart(ref SystemState state)
        {
            // Check for TriggerPersistenceSaveSystem completion
            return false;
        }

        private void StartCustomSave(ref SystemState state)
        {
            Plugin.LogInstance.LogInfo("[Persistence] Custom save started");

            // Update tracking
            var trackingEntity = SystemAPI.GetSingletonEntity<PersistenceTracking>();
            var tracking = SystemAPI.GetComponent<PersistenceTracking>(trackingEntity);
            tracking.IsSaving = true;
            tracking.PendingEntities = CountTrackedEntities(ref state);
            SystemAPI.SetComponent(trackingEntity, tracking);

            // Mark all tracked entities for custom save
            MarkEntitiesForSave(ref state);

            // Log zone status before save
            LogZoneStatus(ref state);
        }

        private void CompleteCustomSave(ref SystemState state)
        {
            Plugin.LogInstance.LogInfo("[Persistence] Custom save completed");

            // Export data
            ExportCustomData(ref state);

            // Reset tracking
            var trackingEntity = SystemAPI.GetSingletonEntity<PersistenceTracking>();
            var tracking = SystemAPI.GetComponent<PersistenceTracking>(trackingEntity);
            tracking.IsSaving = false;
            tracking.LastSaveTime = SystemAPI.Time.ElapsedTime;
            SystemAPI.SetComponent(trackingEntity, tracking);
        }

        private int CountTrackedEntities(ref SystemState state)
        {
            // Count entities with pending save
            var query = SystemAPI.QueryBuilder()
                .WithAll<PendingPersistenceSave>()
                .Build();
            return query.CalculateEntityCount();
        }

        private void MarkEntitiesForSave(ref SystemState state)
        {
            // Enable PendingPersistenceSave component on tracked entities
        }

        private void ExportCustomData(ref SystemState state)
        {
            // Export zone data, arena status, etc. to JSON
            // This would integrate with existing zone configuration
            ExportZoneData(ref state);
        }

        private void ExportZoneData(ref SystemState state)
        {
            // Export all zone membership data
            var zoneQuery = SystemAPI.QueryBuilder()
                .WithAll<ZonePlayerTag, ZoneEntryTimestamp>()
                .Build();

            // Process and export zone data
        }
    }
}
```

---

## Phase 7: Spawn Customization System

### 7.1 Spawn Customization Overview

Apply custom gear/stats when entities spawn.

**Target Integration Point:**

- Group: `ProjectM.SpawnGroup`
- After: `ProjectM.Gameplay.Systems.SpawnCharacterSystem`

### 7.2 Spawn Customization System Implementation

**File:** `Services/Systems/SpawnCustomizationSystem.cs`

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Applies custom gear, stats, and effects when entities spawn.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.SpawnGroup))]
    [UpdateAfter(typeof(ProjectM.Gameplay.Systems.SpawnCharacterSystem))]
    public partial struct SpawnCustomizationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CustomSpawnData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (spawnData, entity) in
                     SystemAPI.Query<RefRO<CustomSpawnData>>()
                     .WithEntityAccess())
            {
                var originalEntity = spawnData.ValueRO.SpawnType == 0
                    ? entity
                    : Entity.Null;

                // Apply customization
                ApplyGear(ref state, ecb, originalEntity, spawnData.ValueRO);
                ApplyBlood(ref state, ecb, originalEntity, spawnData.ValueRO);
                ApplyBuffs(ref state, ecb, originalEntity, spawnData.ValueRO);

                // Remove spawn data after processing
                ecb.RemoveComponent<CustomSpawnData>(originalEntity);

                Plugin.LogInstance.LogInfo(
                    $"[Spawn] Applied customization to entity {originalEntity.Index}");
            }
        }

        private void ApplyGear(ref SystemState state, EntityCommandBuffer ecb,
            Entity entity, CustomSpawnData data)
        {
            if (!data.ApplyGear || entity == Entity.Null)
                return;

            // Get prefab for gear items
            // Equip items on entity
            // This would integrate with existing gear logic
        }

        private void ApplyBlood(ref SystemState state, EntityCommandBuffer ecb,
            Entity entity, CustomSpawnData data)
        {
            if (data.BloodTypeGuid == 0)
                return;

            // Apply blood type and quality
            // This would integrate with blood system
        }

        private void ApplyBuffs(ref SystemState state, EntityCommandBuffer ecb,
            Entity entity, CustomSpawnData data)
        {
            // Apply arena entry buffs, repair buffs, etc.
        }
    }
}
```

---

## Phase 8: Safe Zone Damage Prevention

### 8.1 Safe Zone Damage Prevention Overview

Prevent damage to entities in safe zones using proper V Rising event handling.

> **Note:** In V Rising ECS, `DamageEvent` is a **transient event entity**, NOT a component on the target. You must modify/destroy the event entity, NOT remove from the target.

**Target Integration Point:**

- Group: `ProjectM.UpdateGroup`
- Before: `ProjectM.Gameplay.Systems.DealDamageSystem`

### 8.2 Safe Zone Damage Prevention System Implementation

**File:** `Services/Systems/SafeZoneDamagePreventionSystem.cs`

```csharp
using Unity.Entities;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Intercepts damage events targeting protected entities.
    /// Uses proper V Rising event entity handling (not component removal).
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    [UpdateBefore(typeof(ProjectM.Gameplay.Systems.DealDamageSystem))]
    public partial struct SafeZoneDamagePreventionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectM.DamageEvent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Query damage events - these are event entities, not target components
            foreach (var (damage, eventEntity) in
                     SystemAPI.Query<RefRW<ProjectM.DamageEvent>>()
                     .WithEntityAccess())
            {
                var target = damage.ValueRO.Target;

                // Check if target has safe zone protection
                if (!SystemAPI.HasComponent<SafeZoneProtection>(target))
                    continue;

                var protection = SystemAPI.GetComponent<SafeZoneProtection>(target);

                // Check if protection is still active
                if (protection.ProtectionEndTime > SystemAPI.Time.ElapsedTime)
                {
                    // Cancel damage by zeroing the amount
                    damage.ValueRW.DamageAmount = 0;

                    // Track prevented damage
                    protection.DamagePrevented += (int)damage.ValueRO.DamageAmount;
                    SystemAPI.SetComponent(target, protection);

                    Plugin.LogInstance.LogInfo(
                        $"[SafeZone] Prevented {damage.ValueRO.DamageAmount} damage to entity {target.Index}");
                }
            }
        }
    }
}
```

> **Note:** In V Rising, the DealDamageSystem reads `DamageEvent` entities and processes them. Setting `DamageAmount = 0` on the event effectively cancels the damage without corrupting event flow.

---

## Phase 9: Static Service Migration

### 9.1 Static Service Migration Overview

Convert all static service classes to proper ECS architecture.

### 9.2 Migration Map

| Original Class | Replacement System | Target File |
|----------------|-------------------|-------------|
| `AutomationService` | `AutomationProcessingSystem` | `Services/Systems/AutomationProcessingSystem.cs` |
| `GameSystems` | `ArenaTrackingSystem` | `Services/Systems/ArenaTrackingSystem.cs` |
| `PortalService` | `PortalInterceptSystem` | `Services/Systems/PortalInterceptSystem.cs` |

### 9.3 Arena Tracking System Implementation

**File:** `Services/Systems/ArenaTrackingSystem.cs`

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Tracks player arena status using ECS components instead of static state.
    /// Replaces GameSystems.IsPlayerInArena() and related methods.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    public partial struct ArenaTrackingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Get arena configuration
            if (!SystemAPI.HasSingleton<ArenaConfig>())
                return;

            var config = SystemAPI.GetSingleton<ArenaConfig>();
            float dt = (float)SystemAPI.Time.DeltaTime;

            foreach (var (transform, player, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProjectM.PlayerCharacter>>()
                     .WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;

                // Check main arena
                CheckArenaEntry(ref state, entity, position, config.ArenaCenter,
                    config.ArenaRadius, typeof(ArenaPlayerTag));

                // Check PvP arena
                CheckArenaEntry(ref state, entity, position, config.PvPArenaCenter,
                    config.PvPArenaRadius, typeof(PvPArenaPlayerTag));
            }
        }

        private void CheckArenaEntry(ref SystemState state, Entity entity,
            float3 position, float3 center, float radius, ComponentType tagType)
        {
            float distance = math.distance(position, center);
            bool isInArena = distance <= radius;
            bool hasTag = SystemAPI.HasComponent(entity, tagType);

            if (isInArena && !hasTag)
            {
                // Enter arena
                state.EntityManager.AddComponent(entity, tagType);
                state.EntityManager.AddComponent(entity, new ArenaEntryTimestamp
                {
                    EntryTime = SystemAPI.Time.ElapsedTime
                });

                Plugin.LogInstance.LogInfo(
                    $"[Arena] Entity {entity.Index} entered arena at position {position}");
            }
            else if (!isInArena && hasTag)
            {
                // Exit arena
                state.EntityManager.RemoveComponent(entity, tagType);
                state.EntityManager.RemoveComponent<ArenaEntryTimestamp>(entity);

                Plugin.LogInstance.LogInfo(
                    $"[Arena] Entity {entity.Index} exited arena");
            }
        }
    }
}
```

---

## Phase 10: Castle/Territory Systems

### 10.1 Castle/Territory Systems Overview

Implement castle management and territory enforcement.

**Target Integration Point:**

- Group: `ProjectM.UpdateGroup`
- Reference: `ProjectM.CastleBuilding.CastleHeartStateUpdateSystem`

### 10.2 Castle Decay Monitoring System Implementation

**File:** `Services/Systems/CastleDecayMonitoringSystem.cs`

```csharp
using Unity.Entities;
using VAuto.Core.Components;

namespace VAuto.Services.Systems
{
    /// <summary>
    /// Monitors castle decay state and triggers automation events.
    /// </summary>
    [UpdateInGroup(typeof(ProjectM.UpdateGroup))]
    public partial struct CastleDecayMonitoringSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Query for castle heart entities
            foreach (var (heartState, entity) in
                     SystemAPI.Query<RefRO<ProjectM.CastleHeartState>>()
                     .WithEntityAccess())
            {
                // Monitor decay progress
                float decayLevel = heartState.ValueRO.DecayProgress;

                // Trigger events based on decay thresholds
                if (decayLevel > 0.8f)
                {
                    TriggerDecayWarning(ref state, entity, decayLevel);
                }
                else if (decayLevel > 0.5f)
                {
                    TriggerDecayAlert(ref state, entity, decayLevel);
                }
            }
        }

        private void TriggerDecayWarning(ref SystemState state, Entity heart, float decayLevel)
        {
            Plugin.LogInstance.LogWarning(
                $"[Castle] Castle {heart.Index} at critical decay level: {decayLevel:P0}");
        }

        private void TriggerDecayAlert(ref SystemState state, Entity heart, float decayLevel)
        {
            Plugin.LogInstance.LogInfo(
                $"[Castle] Castle {heart.Index} decay alert: {decayLevel:P0}");
        }
    }
}
```

---

## Phase 11: Testing and Validation

### 11.1 Testing Overview

Testing strategy for the refactored ECS components and systems.

### 11.2 Unit Tests

- Test component creation
- Test system query logic
- Test component attribute behavior

### 11.3 Integration Tests

- Test system execution order
- Test component data flow
- Test Harmony patches

### 11.4 Runtime Validation

- Verify systems execute in correct order
- Verify component data persistence
- Verify no memory leaks

---

## File Structure After Refactoring

```text
VAuto/
├── Core/
│   ├── Components/
│   │   ├── ZoneComponents.cs          # ZoneType, ZonePlayerTag, ZoneBoundary, ZoneDetectionResult
│   │   ├── CombatComponents.cs
│   │   ├── SpawnComponents.cs
│   │   └── PersistenceComponents.cs
│   ├── Harmony/
│   │   └── ServerShutdownInterceptor.cs
│   └── VAutoCore.cs
├── Services/
│   ├── Systems/
│   │   ├── ZoneConfigSystem.cs            # Zone bootstrap (runs once at startup)
│   │   ├── ZoneDetectionSystem.cs         # Detect current zone per player
│   │   ├── ZoneTransitionSystem.cs        # Apply/remove effects on zone changes
│   │   ├── PortalInterceptSystem.cs       # Teleport validation with proper cleanup
│   │   ├── CommandParsingSystem.cs        # Chat command processing
│   │   ├── PersistenceSaveHookSystem.cs   # Save hooks
│   │   ├── SpawnCustomizationSystem.cs    # Spawn-time customization
│   │   ├── SafeZoneDamagePreventionSystem.cs  # Damage event handling
│   │   ├── ZoneTrackingSystem.cs          # Unified zone tracking (replaces ArenaTracking)
│   │   ├── ZoneAutomationSystem.cs        # Zone automation logic
│   │   └── CastleDecayMonitoringSystem.cs
│   └── [Existing services - refactor or deprecate]
├── Plugin.cs
└── [Config files remain unchanged]
```

---

## Execution Order Adjustments

Based on V Rising ECS requirements, the recommended rollout order is:

1. **Phase 1** – Components (ZoneComponents.cs with ZoneType enum)
2. **Phase 2** – Zone Systems (split into Detection + Transition)
3. **Phase 8** – SafeZoneDamagePrevention (validates zone detection logic)
4. **Phase 3** – PortalIntercept (requires proper cleanup pattern)
5. **Phase 4** – CommandParsing
6. **Phase 9** – Static service migration
7. **Phase 6** – Persistence hooks (last; hardest to debug)

---

## Risk Mitigation

### Risk 1: Breaking Existing Functionality

**Mitigation:**

- Maintain backward compatibility with existing commands
- Test each phase incrementally
- Keep fallback static implementations during transition

### Risk 2: Performance Impact

**Mitigation:**

- Use Burst-compiled jobs where possible
- Optimize entity queries
- Use buffer components efficiently

### Risk 3: ECS Version Compatibility

**Mitigation:**

- Use stable Unity.Entities APIs
- Avoid internal/private APIs
- Test with multiple V Rising versions

---

## Rollback Plan

If refactoring causes issues:

1. Revert to `refactor/ecs-compliance` branch
2. Hotfix production issues in original code
3. Create bug reports for ECS implementation issues
4. Iterate on problematic systems

---

## V Rising ECS Corrections Applied

This plan has been updated with V Rising-specific ECS best practices based on technical review.

### Zone Detection Pattern (O(N×M) → O(N))

**Before:** Brute-force player × zone distance checks every frame

**After:** Split into two systems:

- `ZoneDetectionSystem` - Detects current zone (single result per player)
- `ZoneTransitionSystem` - Applies effects only on zone changes

**Why:** V Rising can have 60+ players and dozens of zones. Brute-force scaling is fatal.

### Entity Command Buffer (ECB) for Structural Changes

**Before:** Direct `state.EntityManager.AddComponent()` inside queries

**After:** Always use ECB for all adds/removes:

```csharp
var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
    .CreateCommandBuffer(state.WorldUnmanaged);
ecb.AddComponent(entity, new Component { ... });
```

**Why:** Direct structural changes during iteration corrupt state in V Rising's ECS environment.

### DamageEvent Handling (Transient Event Entities)

**Before:** `ecb.RemoveComponent<DamageEvent>(target)`

**After:** Modify event entity, don't remove from target:

```csharp
foreach (var (damage, eventEntity) in SystemAPI.Query<RefRW<DamageEvent>>().WithEntityAccess())
{
    damage.ValueRW.DamageAmount = 0;  // Cancel damage
}
```

**Why:** `DamageEvent` is a transient event entity, not a component on the target. Removing from target leaks events and causes desyncs.

### Teleport Cleanup Pattern

**Before:** Just remove `TeleportRequest` component

**After:** Complete cleanup:

```csharp
ecb.DestroyEntity(requestEntity);           // Remove request
ecb.RemoveComponent<TeleportRequest>(source);   // Clean source
ecb.RemoveComponent<TeleportTarget>(source);   // Clean target
ecb.AddComponent(source, new TeleportFailedEvent { ... });  // Notify
```

**Why:** Incomplete cleanup causes ghost teleports and client/server mismatch.

### ZoneConfig Initialization

**Before:** `RequireForUpdate<ZoneBoundary>()` then create zones

**After:** `RequireForUpdate` removed, guard with `_initialized` flag

**Why:** System won't run if it requires a component it creates (circular dependency).

### Unified Zone Architecture

**Before:** Separate `ArenaTrackingSystem` + `ZoneEnforcementSystem`

**After:** All zone types unified via `ZoneType` enum:

- `MainArena`, `PvPArena`, `GlowZone`, `SafeZone`, `Portal`
- Single `ZoneBoundary` component with `ZoneType` field

**Why:** Eliminates code duplication and ensures consistent behavior.

---

## Success Criteria

- [ ] All static services converted to ECS systems
- [ ] No in-memory workarounds remain
- [ ] All systems have correct ECS attributes
- [ ] Harmony patches properly registered
- [ ] Command functionality preserved
- [ ] Performance maintained or improved
- [ ] All existing tests pass

---

*Plan Version: 1.0*

*Next Review: After Phase 3 completion*
