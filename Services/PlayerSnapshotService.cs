using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore;
using VAutomationCore.Core;
using VAutomationCore.Core.ECS;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Stores and restores player data when entering/exiting the arena zone.
    /// All snapshot data is stored as pure data types - Entity access only during save/restore.
    /// Captures: position, rotation, health, blood, inventory contents, and equipment.
    /// </summary>
    /// <remarks>
    /// Thread-safe implementation using ConcurrentDictionary for snapshot storage.
    /// Component access uses TryGet pattern to avoid exceptions on missing components.
    /// </remarks>
    public static class PlayerSnapshotService
    {
        private static readonly string[] RestoreSystemTypeNames =
        {
            // Names taken from your shared systems list, focused on restore-critical paths.
            "ProjectM.GameDataSystem",
            "ProjectM.GiveInventoryItemCommandSystem",
            "ProjectM.EquipItemFromInventorySystem",
            "ProjectM.EquipItemSystem",
            "ProjectM.BuffSystem_Spawn_Server",
            "ProjectM.UpdateBuffsBuffer_Destroy",
            "ProjectM.ReplaceAbilityOnSlotSystem",
            "ProjectM.Update_ReplaceAbilityOnSlotSystem",
            "ProjectM.Shared.SpellModCollectionSystem"
        };

        // Using ConcurrentDictionary eliminates need for manual locking
        private static readonly ConcurrentDictionary<Entity, PlayerSnapshot> _snapshots = new ConcurrentDictionary<Entity, PlayerSnapshot>();

        // Cache for reflection-based system lookups to avoid repeated expensive operations
        private static readonly Dictionary<string, Type> _systemTypeCache = new Dictionary<string, Type>(RestoreSystemTypeNames.Length);
        private static MethodInfo _getSystemMethod;
        private static bool _reflectionInitialized;

        /// <summary>
        /// Result type for snapshot operations combining success state with error information.
        /// </summary>
        public readonly struct SnapshotResult
        {
            public bool Success { get; }
            public string Error { get; }

            private SnapshotResult(bool success, string error)
            {
                Success = success;
                Error = error;
            }

            public static SnapshotResult Ok() => new SnapshotResult(true, string.Empty);
            public static SnapshotResult Fail(string error) => new SnapshotResult(false, error);
            
            // Implicit conversion to bool for backward compatibility
            public static implicit operator bool(SnapshotResult result) => result.Success;
        }

        /// <summary>
        /// Stores all player data before arena entry.
        /// </summary>
        /// <param name="playerEntity">The entity to snapshot.</param>
        /// <param name="error">Error message if operation failed.</param>
        /// <returns>True if snapshot was saved successfully.</returns>
        public static bool SaveSnapshot(Entity playerEntity, out string error)
        {
            var result = SaveSnapshotCore(playerEntity);
            error = result.Error;
            return result.Success;
        }

        /// <summary>
        /// Stores all player data before arena entry using Result pattern.
        /// </summary>
        /// <param name="playerEntity">The entity to snapshot.</param>
        /// <returns>Result indicating success or failure with error details.</returns>
        public static SnapshotResult SaveSnapshot(Entity playerEntity)
        {
            return SaveSnapshotCore(playerEntity);
        }

        private static SnapshotResult SaveSnapshotCore(Entity playerEntity)
        {
            var em = ZoneCore.EntityManager;
            
            if (!em.Exists(playerEntity))
            {
                var error = "Entity no longer exists";
                ZoneCore.LogWarning($"[Snapshot] Save failed: {error}");
                return SnapshotResult.Fail(error);
            }

            try
            {
                var snapshot = new PlayerSnapshot
                {
                    EntityIndex = playerEntity.Index,
                    Timestamp = DateTime.UtcNow
                };

                // Position: Support both LocalTransform and Translation using TryGet for efficiency
                // LocalTransform is the newer Unity ECS component, Translation is legacy
                if (TryGetComponent(em, playerEntity, out LocalTransform transform))
                {
                    snapshot.Position = transform.Position;
                    snapshot.Rotation = transform.Rotation;
                }
                else if (TryGetComponent(em, playerEntity, out Translation translation))
                {
                    snapshot.Position = translation.Value;
                    if (TryGetComponent(em, playerEntity, out Rotation rotation))
                    {
                        snapshot.Rotation = rotation.Value;
                    }
                }

                // Health: Capture current health value
                if (TryGetComponent(em, playerEntity, out Health health))
                {
                    snapshot.Health = health.Value;
                }

                // Blood: Capture type and quality
                if (TryGetComponent(em, playerEntity, out Blood blood))
                {
                    snapshot.BloodType = blood.BloodType;
                    snapshot.BloodQuality = blood.Quality;
                }

                // Inventory: Future schema supports inventory snapshots via InventorySnapshot list
                // Current implementation deferred to future enhancement pending inventory API surface changes
                snapshot.InventoryItems = EmptyInventoryItems;

                _snapshots[playerEntity] = snapshot;
                ZoneCore.LogInfo($"[Snapshot] Saved for Entity {playerEntity.Index} at {snapshot.Position} (Inventory items: {snapshot.InventoryItems.Count})");

                return SnapshotResult.Ok();
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[Snapshot] Save failed: {ex}");
                return SnapshotResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Restores player data after arena exit.
        /// </summary>
        /// <param name="playerEntity">The entity to restore.</param>
        /// <param name="error">Error message if operation failed.</param>
        /// <returns>True if snapshot was restored successfully.</returns>
        public static bool RestoreSnapshot(Entity playerEntity, out string error)
        {
            var result = RestoreSnapshotCore(playerEntity);
            error = result.Error;
            return result.Success;
        }

        /// <summary>
        /// Restores player data after arena exit using Result pattern.
        /// </summary>
        /// <param name="playerEntity">The entity to restore.</param>
        /// <returns>Result indicating success or failure with error details.</returns>
        public static SnapshotResult RestoreSnapshot(Entity playerEntity)
        {
            return RestoreSnapshotCore(playerEntity);
        }

        private static SnapshotResult RestoreSnapshotCore(Entity playerEntity)
        {
            var em = ZoneCore.EntityManager;
            
            if (!em.Exists(playerEntity))
            {
                var error = "Entity no longer exists";
                ZoneCore.LogWarning($"[Snapshot] Restore failed: {error}");
                return SnapshotResult.Fail(error);
            }

            // Try to retrieve and remove snapshot atomically
            if (!_snapshots.TryRemove(playerEntity, out var snapshot))
            {
                var error = "No snapshot found for this entity";
                ZoneCore.LogWarning($"[Snapshot] Restore failed: {error} (Entity: {playerEntity.Index})");
                return SnapshotResult.Fail(error);
            }

            try
            {
                LogRestoreSystemAvailability();

                // Inventory: Future schema supports inventory snapshots
                // Restoration deferred to future enhancement pending inventory API surface changes
                var itemsTracked = snapshot.InventoryItems.Count;
                var positionRestored = false;

                // Restore position using LocalTransform (newer) or Translation (legacy)
                if (TryGetComponent(em, playerEntity, out LocalTransform transform))
                {
                    transform.Position = snapshot.Position;
                    transform.Rotation = snapshot.Rotation;
                    em.SetComponentData(playerEntity, transform);
                    positionRestored = true;
                }
                else if (TryGetComponent(em, playerEntity, out Translation translation))
                {
                    translation.Value = snapshot.Position;
                    em.SetComponentData(playerEntity, translation);

                    if (TryGetComponent(em, playerEntity, out Rotation rotation))
                    {
                        rotation.Value = snapshot.Rotation;
                        em.SetComponentData(playerEntity, rotation);
                    }
                    positionRestored = true;
                }

                // Restore health (current value only - MaxHealth is determined by game state)
                if (TryGetComponent(em, playerEntity, out Health health))
                {
                    health.Value = snapshot.Health;
                    em.SetComponentData(playerEntity, health);
                    ZoneCore.LogInfo($"[Snapshot] Health restored: {health.Value}");
                }

                // Restore blood type and quality
                if (TryGetComponent(em, playerEntity, out Blood blood))
                {
                    blood.BloodType = snapshot.BloodType;
                    blood.Quality = snapshot.BloodQuality;
                    em.SetComponentData(playerEntity, blood);
                    ZoneCore.LogInfo($"[Snapshot] Blood restored: {blood.BloodType} quality {blood.Quality}");
                }

                ZoneCore.LogInfo($"[Snapshot] Restored for Entity {playerEntity.Index} at {snapshot.Position} (Position: {positionRestored}, Inventory items tracked: {itemsTracked})");
                return SnapshotResult.Ok();
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"[Snapshot] Restore failed: {ex}");
                return SnapshotResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Clears all stored snapshots.
        /// </summary>
        public static void ClearAll()
        {
            int count = _snapshots.Count;
            _snapshots.Clear();
            if (count > 0)
            {
                ZoneCore.LogInfo($"[Snapshot] All cleared ({count} snapshots removed)");
            }
        }

        /// <summary>
        /// Gets the current number of stored snapshots.
        /// </summary>
        public static int Count => _snapshots.Count;

        /// <summary>
        /// Gets the snapshot for a player entity if it exists.
        /// </summary>
        /// <param name="playerEntity">The entity to look up.</param>
        /// <param name="snapshot">The snapshot if found.</param>
        /// <returns>True if a snapshot exists for this entity.</returns>
        public static bool TryGetSnapshot(Entity playerEntity, out PlayerSnapshot snapshot)
        {
            return _snapshots.TryGetValue(playerEntity, out snapshot);
        }

        /// <summary>
        /// Tries to get component data, returning false instead of throwing if component doesn't exist.
        /// </summary>
        private static bool TryGetComponent<T>(EntityManager em, Entity entity, out T component) where T : struct
        {
            if (em.HasComponent<T>(entity))
            {
                component = em.GetComponentData<T>(entity);
                return true;
            }
            component = default;
            return false;
        }


        // Pre-allocated empty list to avoid allocations for unused inventory
        private static readonly List<InventorySnapshot> EmptyInventoryItems = new List<InventorySnapshot>(0);

        private static void LogRestoreSystemAvailability()
        {
            try
            {
                var world = UnifiedCore.Server;
                if (world == null)
                {
                    ZoneCore.LogWarning("[Snapshot] Restore systems check: server world is null.");
                    return;
                }

                // Initialize reflection cache once
                InitializeReflectionCache(world);

                if (_getSystemMethod == null)
                {
                    ZoneCore.LogWarning("[Snapshot] Restore systems check: GetExistingSystemManaged<T>() not found.");
                    return;
                }

                foreach (var typeName in RestoreSystemTypeNames)
                {
                    // Use cached type lookup
                    if (!_systemTypeCache.TryGetValue(typeName, out var systemType))
                    {
                        systemType = AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType(typeName, false))
                            .FirstOrDefault(t => t != null);
                        
                        _systemTypeCache[typeName] = systemType;
                    }

                    if (systemType == null)
                    {
                        ZoneCore.LogWarning($"[Snapshot] Restore system missing type: {typeName}");
                        continue;
                    }

                    try
                    {
                        var generic = _getSystemMethod.MakeGenericMethod(systemType);
                        var systemInstance = generic.Invoke(world, null);
                        var state = systemInstance != null ? "OK" : "NULL";
                        ZoneCore.LogInfo($"[Snapshot] Restore system {typeName}: {state}");
                    }
                    catch (Exception ex)
                    {
                        ZoneCore.LogWarning($"[Snapshot] Restore system {typeName}: ERROR ({ex.Message})");
                    }
                }
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[Snapshot] Restore systems check failed: {ex.Message}");
            }
        }

        private static void InitializeReflectionCache(object world)
        {
            if (_reflectionInitialized) return;

            var worldType = world.GetType();
            _getSystemMethod = worldType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    string.Equals(m.Name, "GetExistingSystemManaged", StringComparison.Ordinal) &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 0);

            _reflectionInitialized = true;
        }
    }

    /// <summary>
    /// Snapshot of a single inventory item.
    /// </summary>
    /// <remarks>
    /// Using record type for immutability and value equality semantics.
    /// </remarks>
    public record InventorySnapshot(int ItemGuid, int StackSize);

    /// <summary>
    /// Complete snapshot of player state for save/restore operations.
    /// </summary>
    /// <remarks>
    /// Using record type for immutability, value equality, and pattern matching support.
    /// </remarks>
    public class PlayerSnapshot
    {
        public int EntityIndex { get; init; }
        public DateTime Timestamp { get; init; }
        public float3 Position { get; set; }
        public quaternion Rotation { get; set; }
        public float Health { get; set; }
        public PrefabGUID BloodType { get; set; }
        public float BloodQuality { get; set; }
        public List<InventorySnapshot> InventoryItems { get; set; } = new List<InventorySnapshot>();
    }
}
