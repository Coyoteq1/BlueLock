using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectM;
using ProjectM.Shared;
using ProjectM.Gameplay.Systems;

namespace GlobalCombatMarker
{
    /// <summary>
    /// Configuration for combat map icons.
    /// </summary>
    public struct CombatMapIconConfig
    {
        public bool Enabled;
        public float MaxTtlSeconds;
        public float SampleIntervalSeconds;
        public float PairDistanceMeters;
        public int MarkerPrefabGuid;
        public int InCombatBuffGuid;
    }

    public partial class GlobalCombatMarkerSystem : SystemBase
    {
        private EntityQuery _players;
        private Entity _spawnEntity;
        private float _nextUpdateTime;
        
        // Active Entity _spawnEntity markers tracking: key = pair hash, value = expiry time
        private NativeHashMap<int, double> _activeMarkers;
        private bool _markersInitialized;
        
        // Configuration
        private CombatMapIconConfig _config;

        public override void OnCreate()
        {
            _players = GetEntityQuery(
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<PlayerCharacter>());

            // Create spawn entity for map icon requests
            _spawnEntity = EntityManager.CreateEntity();
            
            // Initialize config defaults
            _config = new CombatMapIconConfig
            {
                Enabled = true,
                MaxTtlSeconds = 5.0f,
                SampleIntervalSeconds = 0.5f,
                PairDistanceMeters = 35.0f,
                MarkerPrefabGuid = 1716771727, // MapIcon_PlayerCustomMarker
                InCombatBuffGuid = 697095869
            };
            
            _activeMarkers = new NativeHashMap<int, double>(100, Allocator.Persistent);
            _markersInitialized = true;
        }

        public override void OnDestroy()
        {
            if (_markersInitialized && _activeMarkers.IsCreated)
            {
                _activeMarkers.Dispose();
            }
        }

        public override void OnUpdate()
        {
            // Check if MapIconSpawnSystem is ready (deferred initialization)
            if (!SystemAPI.TryGetSingleton<MapIconSpawnSystem>(out var mapIconSystem))
            {
                // Retry next frame - system not ready yet
                return;
            }

            if (!_config.Enabled)
                return;

            // Throttle updates
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextUpdateTime)
            {
                return;
            }
            _nextUpdateTime = now + _config.SampleIntervalSeconds;

            var em = EntityManager;

            var players = _players.ToEntityArray(Allocator.Temp);
            var transforms = _players.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            
            var currentTime = now;

            try
            {
                // Clean up expired markers
                CleanupExpiredMarkers(currentTime);

                for (int i = 0; i < players.Length; i++)
                {
                    for (int j = i + 1; j < players.Length; j++)
                    {
                        // Check if either player is in combat
                        bool inCombatA = em.HasComponent<InCombatBuff>(players[i]);
                        bool inCombatB = em.HasComponent<InCombatBuff>(players[j]);

                        if (!inCombatA && !inCombatB)
                            continue;

                        float3 posA = transforms[i].Position;
                        float3 posB = transforms[j].Position;

                        float dist = math.distance(posA, posB);
                        if (dist > _config.PairDistanceMeters)
                            continue;

                        // Generate unique key for this player pair
                        int pairKey = GeneratePairKey(players[i].Index, players[j].Index);
                        
                        // Check if marker already exists and not expired
                        if (_activeMarkers.TryGetValue(pairKey, out double expiryTime))
                        {
                            if (currentTime < expiryTime)
                                continue; // Still valid
                        }

                        float3 midpoint = (posA + posB) * 0.5f;
                        
                        // Spawn global marker
                        SpawnGlobalMarker(em, midpoint, currentTime);
                        
                        // Track this marker
                        _activeMarkers[pairKey] = currentTime + _config.MaxTtlSeconds;
                    }
                }
            }
            finally
            {
                players.Dispose();
                transforms.Dispose();
            }
        }

        private void CleanupExpiredMarkers(double currentTime)
        {
            var keysToRemove = new NativeList<int>(Allocator.Temp);
            
            foreach (var kvp in _activeMarkers)
            {
                if (currentTime >= kvp.Value)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            for (int i = 0; i < keysToRemove.Length; i++)
            {
                _activeMarkers.Remove(keysToRemove[i]);
            }
            
            keysToRemove.Dispose();
        }

        private int GeneratePairKey(int entityA, int entityB)
        {
            // Ensure consistent ordering regardless of which player is A or B
            if (entityA > entityB)
            {
                (entityA, entityB) = (entityB, entityA);
            }
            
            // Simple hash combine
            return (entityA * 397) ^ entityB;
        }

        private void SpawnGlobalMarker(EntityManager em, float3 pos, double currentTime)
        {
            // Add buffer for map icon spawn requests
            if (!em.HasBuffer<MapIconSpawnRequest>(_spawnEntity))
            {
                em.AddBuffer<MapIconSpawnRequest>(_spawnEntity);
            }
            
            var buffer = em.GetBuffer<MapIconSpawnRequest>(_spawnEntity);

            buffer.Add(new MapIconSpawnRequest
            {
                PrefabGuid = new PrefabGUID(_config.MarkerPrefabGuid),
                Position = pos,
                Owner = Entity.Null,
                TeamId = 0,
                FactionMask = FactionMask.All,
                Lifetime = _config.MaxTtlSeconds,
                IconType = MapIconType.Ping,
                IsStatic = true
            });
        }

        /// <summary>
        /// Update the configuration at runtime.
        /// </summary>
        public void UpdateConfig(CombatMapIconConfig newConfig)
        {
            _config = newConfig;
        }
    }
}
