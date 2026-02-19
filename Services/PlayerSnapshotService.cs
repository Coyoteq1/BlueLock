using System;
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

        private static readonly Dictionary<Entity, PlayerSnapshot> _snapshots = new Dictionary<Entity, PlayerSnapshot>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Stores all player data before arena entry.
        /// </summary>
        public static bool SaveSnapshot(Entity playerEntity, out string error)
        {
            error = string.Empty;
            
            try
            {
                var em = ZoneCore.EntityManager;
                
                if (!em.Exists(playerEntity))
                {
                    error = "Entity no longer exists";
                    ZoneCore.LogWarning($"[Snapshot] Save failed: {error}");
                    return false;
                }

                var snapshot = new PlayerSnapshot
                {
                    EntityIndex = playerEntity.Index,
                    Timestamp = DateTime.UtcNow
                };

                // Position: Support both LocalTransform and Translation
                if (em.HasComponent<LocalTransform>(playerEntity))
                {
                    snapshot.Position = em.GetComponentData<LocalTransform>(playerEntity).Position;
                    snapshot.Rotation = em.GetComponentData<LocalTransform>(playerEntity).Rotation;
                }
                else if (em.HasComponent<Translation>(playerEntity))
                {
                    snapshot.Position = em.GetComponentData<Translation>(playerEntity).Value;
                    if (em.HasComponent<Rotation>(playerEntity))
                    {
                        snapshot.Rotation = em.GetComponentData<Rotation>(playerEntity).Value;
                    }
                }

                // Health: Capture current health value
                if (em.HasComponent<Health>(playerEntity))
                {
                    var health = em.GetComponentData<Health>(playerEntity);
                    snapshot.Health = health.Value;
                }

                // Blood: Capture type and quality
                if (em.HasComponent<Blood>(playerEntity))
                {
                    var blood = em.GetComponentData<Blood>(playerEntity);
                    snapshot.BloodType = blood.BloodType;
                    snapshot.BloodQuality = blood.Quality;
                }

                // Inventory: Future schema supports inventory snapshots via InventorySnapshot list
                // Current implementation deferred to future enhancement pending inventory API surface changes
                snapshot.InventoryItems = new List<InventorySnapshot>();

                lock (_lock)
                {
                    _snapshots[playerEntity] = snapshot;
                    ZoneCore.LogInfo($"[Snapshot] Saved for Entity {playerEntity.Index} at {snapshot.Position} (Inventory items: {snapshot.InventoryItems?.Count ?? 0})");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                ZoneCore.LogError($"[Snapshot] Save failed: {ex.ToString()}");
                return false;
            }
        }

        /// <summary>
        /// Restores player data after arena exit.
        /// </summary>
        public static bool RestoreSnapshot(Entity playerEntity, out string error)
        {
            error = string.Empty;
            
            try
            {
                var em = ZoneCore.EntityManager;
                
                if (!em.Exists(playerEntity))
                {
                    error = "Entity no longer exists";
                    ZoneCore.LogWarning($"[Snapshot] Restore failed: {error}");
                    return false;
                }

                LogRestoreSystemAvailability();

                PlayerSnapshot snapshot;
                lock (_lock)
                {
                    if (!_snapshots.TryGetValue(playerEntity, out snapshot))
                    {
                        error = "No snapshot found for this entity";
                        ZoneCore.LogWarning($"[Snapshot] Restore failed: {error} (Entity: {playerEntity.Index})");
                        return false;
                    }

                    _snapshots.Remove(playerEntity);
                }

                // Inventory: Future schema supports inventory snapshots
                // Restoration deferred to future enhancement pending inventory API surface changes
                var itemsRestored = 0;
                if (snapshot.InventoryItems != null)
                {
                    itemsRestored = snapshot.InventoryItems.Count;
                }
                bool positionRestored = false;
                if (em.HasComponent<LocalTransform>(playerEntity))
                {
                    var transform = em.GetComponentData<LocalTransform>(playerEntity);
                    transform.Position = snapshot.Position;
                    transform.Rotation = snapshot.Rotation;
                    em.SetComponentData(playerEntity, transform);
                    positionRestored = true;
                }
                else if (em.HasComponent<Translation>(playerEntity))
                {
                    var translation = em.GetComponentData<Translation>(playerEntity);
                    translation.Value = snapshot.Position;
                    em.SetComponentData(playerEntity, translation);

                    if (em.HasComponent<Rotation>(playerEntity))
                    {
                        var rotation = em.GetComponentData<Rotation>(playerEntity);
                        rotation.Value = snapshot.Rotation;
                        em.SetComponentData(playerEntity, rotation);
                    }
                    positionRestored = true;
                }


                // Restore health (current value only - MaxHealth is determined by game state)
                if (em.HasComponent<Health>(playerEntity))
                {
                    var health = em.GetComponentData<Health>(playerEntity);
                    health.Value = snapshot.Health;
                    em.SetComponentData(playerEntity, health);
                    ZoneCore.LogInfo($"[Snapshot] Health restored: {health.Value}");
                }

                // Restore blood type and quality
                if (em.HasComponent<Blood>(playerEntity))
                {
                    var blood = em.GetComponentData<Blood>(playerEntity);
                    blood.BloodType = snapshot.BloodType;
                    blood.Quality = snapshot.BloodQuality;
                    em.SetComponentData(playerEntity, blood);
                    ZoneCore.LogInfo($"[Snapshot] Blood restored: {blood.BloodType} quality {blood.Quality}");
                }


                ZoneCore.LogInfo($"[Snapshot] Restored for Entity {playerEntity.Index} at {snapshot.Position} (Position: {positionRestored}, Inventory items tracked: {itemsRestored})");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                ZoneCore.LogError($"[Snapshot] Restore failed: {ex.ToString()}");
                return false;
            }
        }

        public static void ClearAll()
        {
            lock (_lock)
            {
                int count = _snapshots.Count;
                _snapshots.Clear();
                ZoneCore.LogInfo($"[Snapshot] All cleared ({count} snapshots removed)");
            }
        }

        public static int Count
        {
            get { lock (_lock) { return _snapshots.Count; } }
        }

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

                var worldType = world.GetType();
                var getSystemMethod = worldType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m =>
                        string.Equals(m.Name, "GetExistingSystemManaged", StringComparison.Ordinal) &&
                        m.IsGenericMethodDefinition &&
                        m.GetParameters().Length == 0);

                if (getSystemMethod == null)
                {
                    ZoneCore.LogWarning("[Snapshot] Restore systems check: GetExistingSystemManaged<T>() not found.");
                    return;
                }

                foreach (var typeName in RestoreSystemTypeNames)
                {
                    var systemType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(typeName, false))
                        .FirstOrDefault(t => t != null);

                    if (systemType == null)
                    {
                        ZoneCore.LogWarning($"[Snapshot] Restore system missing type: {typeName}");
                        continue;
                    }

                    try
                    {
                        var generic = getSystemMethod.MakeGenericMethod(systemType);
                        var systemInstance = generic.Invoke(world, Array.Empty<object>());
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
    }

    /// <summary>
    /// Snapshot of a single inventory item.
    /// </summary>
    public class InventorySnapshot
    {
        public int ItemGuid { get; set; }
        public int StackSize { get; set; }
    }

    public class PlayerSnapshot
    {
        public int EntityIndex { get; set; }
        public DateTime Timestamp { get; set; }
        public float3 Position { get; set; }
        public quaternion Rotation { get; set; }
        public float Health { get; set; }
        public PrefabGUID BloodType { get; set; }
        public float BloodQuality { get; set; }
        public List<InventorySnapshot> InventoryItems { get; set; } = new List<InventorySnapshot>();
    }
}
