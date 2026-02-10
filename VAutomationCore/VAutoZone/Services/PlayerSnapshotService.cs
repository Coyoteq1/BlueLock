using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore;
using VAutomationCore.Core.ECS;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Stores and restores player data when entering/exiting the arena zone.
    /// All snapshot data is stored as pure data types - Entity access only during save/restore.
    /// </summary>
    public static class PlayerSnapshotService
    {
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

                // Health: Health.Value is a float in V Rising
                if (em.HasComponent<Health>(playerEntity))
                {
                    var health = em.GetComponentData<Health>(playerEntity);
                    snapshot.Health = health.Value;
                }

                // Blood
                if (em.HasComponent<Blood>(playerEntity))
                {
                    snapshot.Blood = em.GetComponentData<Blood>(playerEntity).Value;
                }

                lock (_lock)
                {
                    _snapshots[playerEntity] = snapshot;
                    ZoneCore.LogInfo($"[Snapshot] Saved for Entity {playerEntity.Index} at {snapshot.Position}");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                ZoneCore.LogError($"[Snapshot] Save failed: {error}");
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

                // Restore position
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

                // Restore health
                if (snapshot.Health > 0 && em.HasComponent<Health>(playerEntity))
                {
                    var health = em.GetComponentData<Health>(playerEntity);
                    health.Value = snapshot.Health;
                    em.SetComponentData(playerEntity, health);
                }

                // Restore blood
                if (snapshot.Blood > 0 && em.HasComponent<Blood>(playerEntity))
                {
                    var blood = em.GetComponentData<Blood>(playerEntity);
                    blood.Value = snapshot.Blood;
                    em.SetComponentData(playerEntity, blood);
                }

                ZoneCore.LogInfo($"[Snapshot] Restored for Entity {playerEntity.Index} at {snapshot.Position} (Position: {positionRestored})");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                ZoneCore.LogError($"[Snapshot] Restore failed: {error}");
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
    }

    public class PlayerSnapshot
    {
        public int EntityIndex { get; set; }
        public DateTime Timestamp { get; set; }
        public float3 Position { get; set; }
        public quaternion Rotation { get; set; }
        public float Health { get; set; }
        public float Blood { get; set; }
    }
}
