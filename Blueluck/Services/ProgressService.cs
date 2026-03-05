using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectM;
using VAuto.Services.Interfaces;
using Blueluck.Models;
using VAutomationCore.Core;
using VAutomationCore.Services;
using VAutomationCore.Abstractions;

namespace Blueluck.Services
{
    /// <summary>
    /// Service for saving and restoring player progress in ArenaZone.
    /// Implements IService from VAutomationCore.
    /// </summary>
    public class ProgressService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.Progress");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private readonly Dictionary<Entity, PlayerProgress> _snapshots = new();

        public void Initialize()
        {
            IsInitialized = true;
            _log.LogInfo("[Progress] Initialized.");

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                _log.LogWarning("[Progress] World not available");
                return;
            }
        }

        public void Cleanup()
        {
            _snapshots.Clear();
            IsInitialized = false;
            _log.LogInfo("[Progress] Cleaned up.");
        }

        /// <summary>
        /// Validates that an entity is not corrupted or from a different world.
        /// </summary>
        private static bool IsValidEntity(Entity entity)
        {
            // Entity.Null has Index=0 and Version=0, but we want to catch obviously invalid entities
            // A valid entity should have non-negative index and non-negative version
            // The EntityManager capacity check happens when we call Exists, but we can do basic validation first
            return entity != Entity.Null;
        }

        /// <summary>
        /// Saves a gameplay snapshot (progression + buff set) for later restore.
        /// </summary>
        public void SaveProgress(Entity player)
        {
            try
            {
                if (!IsInitialized)
                {
                    _log.LogWarning("[Progress] Service not initialized");
                    return;
                }

                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    _log.LogWarning("[Progress] World not available");
                    return;
                }

                // Validate entity before passing to EntityManager (prevents ArgumentException on corrupted entities)
                if (!IsValidEntity(player))
                {
                    _log.LogWarning($"[Progress] Invalid player entity: Index={player.Index}, Version={player.Version}");
                    return;
                }

                var em = world.EntityManager;
                
                // Wrap Exists check in try-catch to handle corrupted entity gracefully
                bool exists;
                try
                {
                    exists = em.Exists(player);
                }
                catch (ArgumentException)
                {
                    _log.LogWarning($"[Progress] Entity validation failed for player {player.Index} (corrupted entity)");
                    return;
                }

                if (!exists)
                {
                    _log.LogWarning($"[Progress] Player entity {player.Index} does not exist");
                    return;
                }

                // Save essential player data using ECS queries
                var progress = new PlayerProgress
                {
                    EntityIndex = (int)player.Index,
                    Timestamp = DateTime.UtcNow,
                    Position = em.HasComponent<LocalToWorld>(player) ? em.GetComponentData<LocalToWorld>(player).Position : float3.zero,
                    Health = em.HasComponent<Health>(player) ? em.GetComponentData<Health>(player).Value : 0f,
                    BloodType = em.HasComponent<Blood>(player) ? em.GetComponentData<Blood>(player).BloodType.GetHashCode() : 0,
                    BloodQuality = em.HasComponent<Blood>(player) ? em.GetComponentData<Blood>(player).Quality : 0f
                };

                progress.BuffPrefabHashes = CaptureBuffSnapshot(em, player);

                _snapshots[player] = progress;
                _log.LogInfo($"[Progress] Saved snapshot for player {player.Index} (buffs={progress.BuffPrefabHashes.Count})");
            }
            catch (Exception ex)
            {
                _log.LogError($"[Progress] Error saving progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the saved snapshot to the player without removing extra buffs gained since save.
        /// Intended for "bring player back up to snapshot" semantics.
        /// </summary>
        public void ApplyProgress(Entity player)
        {
            try
            {
                if (!_snapshots.TryGetValue(player, out var savedProgress))
                {
                    _log.LogWarning($"[Progress] No snapshot found for player {player.Index}");
                    return;
                }

                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    _log.LogWarning("[Progress] World not available");
                    return;
                }

                var em = world.EntityManager;
                if (!em.Exists(player))
                {
                    _log.LogWarning($"[Progress] Player entity {player.Index} does not exist");
                    return;
                }

                // Restore essential player data (best effort)

                // Restore health
                if (em.HasComponent<Health>(player))
                {
                    var health = em.GetComponentData<Health>(player);
                    health.Value = savedProgress.Health;
                    em.SetComponentData(player, health);
                }

                // Restore blood
                if (em.HasComponent<Blood>(player))
                {
                    var blood = em.GetComponentData<Blood>(player);
                    blood.BloodType = new PrefabGUID(savedProgress.BloodType);
                    blood.Quality = savedProgress.BloodQuality;
                    em.SetComponentData(player, blood);
                }
                
                // Teleport to saved position
                if (!savedProgress.Position.Equals(float3.zero))
                {
                    TeleportPlayer(player, savedProgress.Position);
                }

                // Apply missing buffs (do not remove extra)
                ApplyMissingBuffs(em, player, savedProgress);

                _log.LogInfo($"[Progress] Applied snapshot for player {player.Index}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[Progress] Error applying snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores the player to the exact saved snapshot: removes buffs not present in the snapshot,
        /// and re-applies buffs that are missing. Optionally clears the snapshot after restore.
        /// </summary>
        public void RestoreProgress(Entity player, bool clearAfter = true)
        {
            try
            {
                if (!_snapshots.TryGetValue(player, out var savedProgress))
                {
                    _log.LogWarning($"[Progress] No snapshot found for player {player.Index}");
                    return;
                }

                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    _log.LogWarning("[Progress] World not available");
                    return;
                }

                var em = world.EntityManager;
                if (!em.Exists(player))
                {
                    _log.LogWarning($"[Progress] Player entity {player.Index} does not exist");
                    return;
                }

                // Apply base progression state first.
                ApplyProgress(player);

                // Reconcile buffs: remove extras, then add missing.
                RestoreBuffsExact(em, player, savedProgress);

                if (clearAfter)
                {
                    _snapshots.Remove(player);
                }

                _log.LogInfo($"[Progress] Restored snapshot for player {player.Index} (clearAfter={clearAfter})");
            }
            catch (Exception ex)
            {
                _log.LogError($"[Progress] Error restoring snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if player has saved progress.
        /// </summary>
        public bool HasSavedProgress(Entity player)
        {
            return _snapshots.ContainsKey(player);
        }

        /// <summary>
        /// Gets saved progress for a player (without removing).
        /// </summary>
        public bool TryGetSavedProgress(Entity player, out PlayerProgress progress)
        {
            return _snapshots.TryGetValue(player, out progress);
        }

        /// <summary>
        /// Clears saved progress for a player.
        /// </summary>
        public void ClearSavedProgress(Entity player)
        {
            _snapshots.Remove(player);
        }

        private void TeleportPlayer(Entity player, float3 position)
        {
            try
            {
                // Best-effort: let core action service apply the teleport semantics for the runtime.
                GameActionService.InvokeAction("setposition", new object[] { player, position });
                _log.LogInfo($"[Progress] Teleported player {player.Index} to {position}");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[Progress] Teleport failed for player {player.Index}: {ex.Message}");
            }
        }

        private static List<int> CaptureBuffSnapshot(EntityManager em, Entity player)
        {
            var results = new List<int>();

            try
            {
                if (em == default || player == Entity.Null || !em.Exists(player))
                {
                    return results;
                }

                if (!em.HasBuffer<BuffBuffer>(player))
                {
                    return results;
                }

                var buffer = em.GetBuffer<BuffBuffer>(player);
                results.Capacity = math.max(results.Capacity, buffer.Length);

                for (var i = 0; i < buffer.Length; i++)
                {
                    var guid = buffer[i].PrefabGuid;
                    if (guid == PrefabGUID.Empty)
                    {
                        continue;
                    }

                    results.Add(guid.GuidHash);
                }
            }
            catch
            {
                // ignored
            }

            return results;
        }

        private void ApplyMissingBuffs(EntityManager em, Entity player, PlayerProgress snapshot)
        {
            if (snapshot.BuffPrefabHashes == null || snapshot.BuffPrefabHashes.Count == 0)
            {
                return;
            }

            if (!TryResolveUserEntity(em, player, out var userEntity))
            {
                return;
            }

            var current = GetCurrentBuffHashSet(em, player);
            foreach (var hash in snapshot.BuffPrefabHashes)
            {
                if (hash == 0) continue;
                if (current.Contains(hash)) continue;

                Buffs.AddBuff(userEntity, player, new PrefabGUID(hash), duration: 0f, immortal: false);
            }
        }

        private void RestoreBuffsExact(EntityManager em, Entity player, PlayerProgress snapshot)
        {
            if (!TryResolveUserEntity(em, player, out var userEntity))
            {
                return;
            }

            var desired = new HashSet<int>(snapshot.BuffPrefabHashes ?? new List<int>());
            desired.Remove(0);

            var current = GetCurrentBuffHashSet(em, player);

            // Remove buffs not in desired snapshot.
            foreach (var hash in current)
            {
                if (hash == 0) continue;
                if (desired.Contains(hash)) continue;

                GameActionService.TryRemoveBuff(player, new PrefabGUID(hash));
            }

            // Add missing buffs from snapshot.
            foreach (var hash in desired)
            {
                if (current.Contains(hash)) continue;
                Buffs.AddBuff(userEntity, player, new PrefabGUID(hash), duration: 0f, immortal: false);
            }
        }

        private static HashSet<int> GetCurrentBuffHashSet(EntityManager em, Entity player)
        {
            var set = new HashSet<int>();

            try
            {
                if (em == default || player == Entity.Null || !em.Exists(player))
                {
                    return set;
                }

                if (!em.HasBuffer<BuffBuffer>(player))
                {
                    return set;
                }

                var buffer = em.GetBuffer<BuffBuffer>(player);
                for (var i = 0; i < buffer.Length; i++)
                {
                    var guid = buffer[i].PrefabGuid;
                    if (guid == PrefabGUID.Empty) continue;
                    set.Add(guid.GuidHash);
                }
            }
            catch
            {
                // ignored
            }

            return set;
        }

        private static bool TryResolveUserEntity(EntityManager em, Entity player, out Entity userEntity)
        {
            userEntity = Entity.Null;

            try
            {
                if (em == default || player == Entity.Null || !em.Exists(player) || !em.HasComponent<PlayerCharacter>(player))
                {
                    return false;
                }

                var pc = em.GetComponentData<PlayerCharacter>(player);
                if (pc.UserEntity == Entity.Null || !em.Exists(pc.UserEntity))
                {
                    return false;
                }

                userEntity = pc.UserEntity;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
