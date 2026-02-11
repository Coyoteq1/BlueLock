using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAuto.Zone.Core.Lifecycle;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Zone tracking service using NativeHashMap for high-performance ECS-based player tracking.
    /// Provides autonomous zone detection without relying on VAutoZone callbacks.
    /// </summary>
    public class ZoneTrackingService : IDisposable
    {
        private readonly EntityManager _entityManager;
        private readonly ManualLogSource _log;
        
        // Native collections for player tracking
        private NativeHashMap<Entity, float3> _playerPositions;
        private NativeHashMap<Entity, Entity> _playerCurrentZone;
        private NativeHashMap<Entity, float> _playerLastTransitionTime;
        
        // Zone definitions
        private NativeList<ZoneDefinition> _zoneDefinitions;
        
        private bool _isInitialized;
        private readonly object _lock = new object();

        /// <summary>
        /// Event fired when a player transitions between zones.
        /// </summary>
        public event Action<Entity, Entity, Entity> OnPlayerZoneTransition;

        /// <summary>
        /// Event fired when a player enters a lifecycle zone.
        /// </summary>
        public event Action<Entity, Entity> OnPlayerEnterLifecycleZone;

        /// <summary>
        /// Event fired when a player exits a lifecycle zone.
        /// </summary>
        public event Action<Entity, Entity> OnPlayerExitLifecycleZone;

        public ZoneTrackingService(EntityManager entityManager, ManualLogSource log)
        {
            _entityManager = entityManager;
            _log = log;
        }

        /// <summary>
        /// Initialize the zone tracking service.
        /// </summary>
        public void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                _playerPositions = new NativeHashMap<Entity, float3>(100, Allocator.Persistent);
                _playerCurrentZone = new NativeHashMap<Entity, Entity>(100, Allocator.Persistent);
                _playerLastTransitionTime = new NativeHashMap<Entity, float>(100, Allocator.Persistent);
                _zoneDefinitions = new NativeList<ZoneDefinition>(10, Allocator.Persistent);

                _isInitialized = true;
                _log.LogInfo("[ZoneTrackingService] Initialized with NativeHashMap tracking");
            }
        }

        /// <summary>
        /// Register a zone definition for tracking.
        /// </summary>
        public void RegisterZone(Entity zoneEntity, float3 center, float radius, string zoneId, bool isLifecycleZone = false)
        {
            if (!_isInitialized)
            {
                _log.LogWarning("[ZoneTrackingService] Not initialized, cannot register zone");
                return;
            }

            var definition = new ZoneDefinition
            {
                ZoneEntity = zoneEntity,
                Center = center,
                Radius = radius,
                ZoneId = zoneId ?? "Unknown",
                IsLifecycleZone = isLifecycleZone
            };

            lock (_lock)
            {
                _zoneDefinitions.Add(definition);
            }

            _log.LogInfo($"[ZoneTrackingService] Registered zone: {definition.ZoneId} at ({definition.Center.x:F0}, {definition.Center.y:F0}, {definition.Center.z:F0}) with radius {definition.Radius:F0}");
        }

        /// <summary>
        /// Unregister a zone from tracking.
        /// </summary>
        public void UnregisterZone(string zoneId)
        {
            if (!_isInitialized) return;

            lock (_lock)
            {
                for (int i = _zoneDefinitions.Length - 1; i >= 0; i--)
                {
                    if (_zoneDefinitions[i].ZoneId == zoneId)
                    {
                        _zoneDefinitions.RemoveAt(i);
                        _log.LogInfo($"[ZoneTrackingService] Unregistered zone: {zoneId}");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Update zone tracking for all players.
        /// Should be called every frame or on a regular interval.
        /// </summary>
        public void UpdateTracking()
        {
            if (!_isInitialized) return;

            var playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerCharacter>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            var players = playerQuery.ToEntityArray(Allocator.Temp);

            foreach (var player in players)
            {
                if (!_entityManager.HasComponent<LocalTransform>(player)) continue;

                var position = _entityManager.GetComponentData<LocalTransform>(player).Position;
                UpdatePlayerZone(player, position);
            }

            players.Dispose();
        }

        /// <summary>
        /// Update zone tracking for a specific player.
        /// </summary>
        private void UpdatePlayerZone(Entity player, float3 position)
        {
            lock (_lock)
            {
                // Update player position
                if (_playerPositions.ContainsKey(player))
                {
                    _playerPositions[player] = position;
                }
                else
                {
                    _playerPositions.TryAdd(player, position);
                }

                // Find which zone the player is in
                Entity newZone = Entity.Null;
                Entity previousZone = Entity.Null;

                if (_playerCurrentZone.TryGetValue(player, out previousZone))
                {
                    // Check if player is still in previous zone
                    if (previousZone != Entity.Null && IsInZone(previousZone, position))
                    {
                        newZone = previousZone;
                    }
                }

                // Find new zone if needed
                if (newZone == Entity.Null)
                {
                    newZone = FindPlayerZone(position);
                }

                // Handle zone transition
                if (newZone != previousZone)
                {
                    _playerCurrentZone[player] = newZone;
                    var transitionTime = (float)SystemAPI.Time.ElapsedTime;
                    
                    if (_playerLastTransitionTime.ContainsKey(player))
                    {
                        _playerLastTransitionTime[player] = transitionTime;
                    }
                    else
                    {
                        _playerLastTransitionTime.TryAdd(player, transitionTime);
                    }

                    _log.LogDebug($"[ZoneTrackingService] Player {player} transitioned from zone {previousZone} to zone {newZone}");

                    // Fire transition event
                    OnPlayerZoneTransition?.Invoke(player, previousZone, newZone);

                    // Check if entering/exiting lifecycle zone
                    if (IsLifecycleZone(newZone))
                    {
                        OnPlayerEnterLifecycleZone?.Invoke(player, newZone);
                    }

                    if (IsLifecycleZone(previousZone))
                    {
                        OnPlayerExitLifecycleZone?.Invoke(player, previousZone);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a position is within a zone.
        /// </summary>
        public bool IsInZone(Entity zoneEntity, float3 position)
        {
            lock (_lock)
            {
                foreach (var zone in _zoneDefinitions)
                {
                    if (zone.ZoneEntity == zoneEntity)
                    {
                        return CheckDistance(zone.Center, position) <= zone.Radius;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Find the zone containing a position.
        /// Returns Entity.Null if no zone found.
        /// </summary>
        public Entity FindPlayerZone(float3 position)
        {
            lock (_lock)
            {
                foreach (var zone in _zoneDefinitions)
                {
                    if (CheckDistance(zone.Center, position) <= zone.Radius)
                    {
                        return zone.ZoneEntity;
                    }
                }
            }
            return Entity.Null;
        }

        /// <summary>
        /// Check if a zone entity is a lifecycle zone.
        /// </summary>
        public bool IsLifecycleZone(Entity zoneEntity)
        {
            if (zoneEntity == Entity.Null) return false;

            lock (_lock)
            {
                foreach (var zone in _zoneDefinitions)
                {
                    if (zone.ZoneEntity == zoneEntity)
                    {
                        return zone.IsLifecycleZone;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Get the current zone for a player.
        /// Returns Entity.Null if player not tracked.
        /// </summary>
        public Entity GetPlayerZone(Entity player)
        {
            if (_playerCurrentZone.TryGetValue(player, out var zone))
            {
                return zone;
            }
            return Entity.Null;
        }

        /// <summary>
        /// Get the last known position for a player.
        /// Returns default(float3) if player not tracked.
        /// </summary>
        public float3 GetPlayerPosition(Entity player)
        {
            if (_playerPositions.TryGetValue(player, out var position))
            {
                return position;
            }
            return default;
        }

        /// <summary>
        /// Check if a player has recently transitioned zones.
        /// Useful for cooldown management.
        /// </summary>
        public bool IsPlayerInCooldown(Entity player, float cooldownSeconds)
        {
            if (_playerLastTransitionTime.TryGetValue(player, out var lastTransition))
            {
                return (float)SystemAPI.Time.ElapsedTime - lastTransition < cooldownSeconds;
            }
            return false;
        }

        /// <summary>
        /// Get all tracked players.
        /// </summary>
        public NativeArray<Entity> GetTrackedPlayers(Allocator allocator)
        {
            return _playerPositions.GetKeyArray(allocator);
        }

        /// <summary>
        /// Calculate distance between two positions.
        /// </summary>
        private float CheckDistance(float3 a, float3 b)
        {
            return math.distance(a, b);
        }

        /// <summary>
        /// Dispose native collections.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_playerPositions.IsCreated) _playerPositions.Dispose();
                if (_playerCurrentZone.IsCreated) _playerCurrentZone.Dispose();
                if (_playerLastTransitionTime.IsCreated) _playerLastTransitionTime.Dispose();
                if (_zoneDefinitions.IsCreated) _zoneDefinitions.Dispose();
                _isInitialized = false;
                _log.LogInfo("[ZoneTrackingService] Disposed");
            }
        }
    }
}
