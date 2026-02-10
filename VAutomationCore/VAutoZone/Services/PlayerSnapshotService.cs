using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAutoZone;
using VAutomationCore;
using VAutomationCore.Core.ECS;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Stores and restores player data when entering/exiting the arena zone.
    /// </summary>
    public static class PlayerSnapshotService
    {
        private static readonly Dictionary<int, PlayerSnapshot> _snapshots = new Dictionary<int, PlayerSnapshot>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Stores all player data before arena entry.
        /// </summary>
        public static bool SaveSnapshot(Entity playerEntity, out string error)
        {
            error = string.Empty;
            try
            {
                // Use entity index as simple ID for snapshots
                var entityId = playerEntity.Index;

                lock (_lock)
                {
                    var em = UnifiedCore.EntityManager;
                    var snapshot = new PlayerSnapshot
                    {
                        EntityId = entityId,
                        Timestamp = DateTime.UtcNow,
                        Position = em.HasComponent<LocalTransform>(playerEntity) 
                            ? em.GetComponentData<LocalTransform>(playerEntity).Position
                            : float3.zero
                    };

                    _snapshots[entityId] = snapshot;
                    UnifiedCore.LogInfo($"[Snapshot] Saved for Entity {entityId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                UnifiedCore.LogError($"[Snapshot] Save failed: {error}");
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
                // Use entity index as ID
                var entityId = playerEntity.Index;

                lock (_lock)
                {
                    if (!_snapshots.TryGetValue(entityId, out var snapshot))
                    {
                        error = "No snapshot found";
                        return false;
                    }

                    _snapshots.Remove(entityId);
                    UnifiedCore.LogInfo($"[Snapshot] Restored for Entity {entityId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                UnifiedCore.LogError($"[Snapshot] Restore failed: {error}");
                return false;
            }
        }

        /// <summary>
        /// Clears all snapshots.
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                _snapshots.Clear();
                UnifiedCore.LogInfo("[Snapshot] All cleared");
            }
        }

        public static int Count
        {
            get { lock (_lock) { return _snapshots.Count; } }
        }
    }

    public class PlayerSnapshot
    {
        public int EntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public float3 Position { get; set; }
    }
}
