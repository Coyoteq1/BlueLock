using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Extensions;
using VAutomationCore.Core.Services;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Batch entity spawning system for V Rising with glow effects and buff capabilities.
    /// Provides efficient one-step spawning of multiple entities with visual and buff components.
    /// </summary>
    public static class EntitySpawner
    {
        private static EntityManager _entityManager;
        private static bool _initialized;
        private static readonly object _initLock = new object();

        #region Configuration

        /// <summary>
        /// Configuration for glow appearance.
        /// </summary>
        public struct GlowConfig
        {
            public float3 Color;
            public float Intensity;
            public float Radius;
            public float Duration;

            public static readonly GlowConfig Default = new GlowConfig
            {
                Color = new float3(0f, 0.5f, 1f), // Cyan glow
                Intensity = 1f,
                Radius = 5f,
                Duration = 30f
            };
        }

        /// <summary>
        /// Configuration for batch spawning parameters.
        /// </summary>
        public struct SpawnConfig
        {
            public int Count;
            public float3 CenterPosition;
            public float SpawnRadius;
            public bool RandomRotation;
            public GlowConfig Glow;
            public PrefabGUID? BuffPrefab;
            public int? BuffId;

            public static readonly SpawnConfig Default = new SpawnConfig
            {
                Count = 5,
                CenterPosition = float3.zero,
                SpawnRadius = 10f,
                RandomRotation = true,
                Glow = GlowConfig.Default,
                BuffPrefab = null,
                BuffId = 561176
            };
        }

        /// <summary>
        /// Result of a batch spawn operation.
        /// </summary>
        public struct SpawnResult
        {
            public NativeList<Entity> SpawnedEntities;
            public NativeList<float3> SpawnedPositions;
            public int SuccessCount;
            public int FailCount;
            public double TotalTimeMs;

            public void Dispose()
            {
                if (SpawnedEntities.IsCreated) SpawnedEntities.Dispose();
                if (SpawnedPositions.IsCreated) SpawnedPositions.Dispose();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the entity spawner system.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;

                try
                {
                    var world = World.DefaultGameObjectInjectionWorld;
                    if (world == null)
                    {
                        Plugin.Log.LogWarning("[EntitySpawner] World not available, deferring initialization");
                        return;
                    }

                    _entityManager = world.EntityManager;
                    _initialized = true;
                    Plugin.Log.LogInfo("[EntitySpawner] Initialized successfully");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[EntitySpawner] Initialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if spawner is ready.
        /// </summary>
        public static bool IsReady()
        {
            return _initialized && _entityManager != null;
        }

        #endregion

        #region Batch Spawning API

        /// <summary>
        /// Spawn a batch of glowing buff entities with randomized positions.
        /// </summary>
        /// <param name="config">Spawn configuration (uses defaults if not specified)</param>
        /// <returns>SpawnResult with spawned entities and statistics</returns>
        public static SpawnResult SpawnGlowingBuffEntities(SpawnConfig? config = null)
        {
            var cfg = config ?? SpawnConfig.Default;
            var result = new SpawnResult
            {
                SpawnedEntities = new NativeList<Entity>(cfg.Count, Allocator.Temp),
                SpawnedPositions = new NativeList<float3>(cfg.Count, Allocator.Temp),
                SuccessCount = 0,
                FailCount = 0,
                TotalTimeMs = 0
            };

            if (!IsReady())
            {
                Plugin.Log.LogWarning("[EntitySpawner] Not initialized, cannot spawn");
                return result;
            }

            var startTime = DateTime.UtcNow;

            try
            {
                // Generate random positions
                var random = new Random((uint)DateTime.UtcNow.Ticks);
                var positions = GenerateRandomPositions(cfg.CenterPosition, cfg.SpawnRadius, cfg.Count, ref random);

                // Create entities in batch
                for (int i = 0; i < positions.Length; i++)
                {
                    var entity = CreateGlowingBuffEntity(positions[i], cfg.Glow, cfg.BuffId, cfg.RandomRotation, ref random);
                    if (entity != Entity.Null)
                    {
                        result.SpawnedEntities.Add(entity);
                        result.SpawnedPositions.Add(positions[i]);
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailCount++;
                    }
                }

                result.TotalTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Plugin.Log.LogInfo($"[EntitySpawner] Batch spawn completed: {result.SuccessCount} succeeded, {result.FailCount} failed in {result.TotalTimeMs:F2}ms");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EntitySpawner] Batch spawn failed: {ex.Message}");
                result.FailCount = cfg.Count;
            }

            return result;
        }

        /// <summary>
        /// Spawn a single glowing buff entity at a specific position.
        /// </summary>
        /// <param name="position">World position for the entity</param>
        /// <param name="glowConfig">Glow appearance configuration</param>
        /// <param name="buffId">Buff ID to apply (default: 561176)</param>
        /// <param name="randomRotation">Whether to apply random rotation</param>
        /// <returns>The spawned entity or Entity.Null on failure</returns>
        public static Entity SpawnSingleGlowingBuffEntity(
            float3 position,
            GlowConfig? glowConfig = null,
            int? buffId = null,
            bool randomRotation = true)
        {
            if (!IsReady()) return Entity.Null;

            var glow = glowConfig ?? GlowConfig.Default;
            var random = new Random((uint)DateTime.UtcNow.Ticks);
            return CreateGlowingBuffEntity(position, glow, buffId, randomRotation, ref random);
        }

        /// <summary>
        /// Spawn entities at predefined positions.
        /// </summary>
        /// <param name="positions">Array of world positions</param>
        /// <param name="glowConfig">Glow appearance configuration</param>
        /// <param name="buffId">Buff ID to apply</param>
        /// <returns>SpawnResult with spawned entities</returns>
        public static SpawnResult SpawnAtPositions(
            NativeArray<float3> positions,
            GlowConfig? glowConfig = null,
            int? buffId = null)
        {
            var result = new SpawnResult
            {
                SpawnedEntities = new NativeList<Entity>(positions.Length, Allocator.Temp),
                SpawnedPositions = new NativeList<float3>(positions.Length, Allocator.Temp),
                SuccessCount = 0,
                FailCount = 0
            };

            if (!IsReady()) return result;

            var glow = glowConfig ?? GlowConfig.Default;
            var random = new Random((uint)DateTime.UtcNow.Ticks);

            for (int i = 0; i < positions.Length; i++)
            {
                var entity = CreateGlowingBuffEntity(positions[i], glow, buffId, false, ref random);
                if (entity != Entity.Null)
                {
                    result.SpawnedEntities.Add(entity);
                    result.SpawnedPositions.Add(positions[i]);
                    result.SuccessCount++;
                }
                else
                {
                    result.FailCount++;
                }
            }

            return result;
        }

        #endregion

        #region Single Entity Creation

        /// <summary>
        /// Create a single entity with glow and buff components.
        /// </summary>
        private static Entity CreateGlowingBuffEntity(
            float3 position,
            GlowConfig glow,
            int? buffId,
            bool randomRotation,
            ref Random random)
        {
            try
            {
                // Create a new entity using the archetype
                var archetype = _entityManager.CreateArchetype(
                    typeof(Translation),
                    typeof(LocalToWorld),
                    typeof(LocalTransform),
                    typeof(Buff),
                    typeof(GlowComponent)
                );

                var entity = _entityManager.CreateEntity(archetype);

                // Set position components
                var rotation = randomRotation ? quaternion.Euler(
                    random.NextFloat(0, math.PI * 2),
                    random.NextFloat(0, math.PI * 2),
                    random.NextFloat(0, math.PI * 2)
                ) : quaternion.identity;

                var transform = LocalTransform.FromPositionRotation(position, rotation);
                _entityManager.SetComponentData(entity, transform);
                _entityManager.SetComponentData(entity, new Translation { Value = position });

                // Initialize glow component
                _entityManager.SetComponentData(entity, new GlowComponent
                {
                    Color = glow.Color,
                    Intensity = glow.Intensity,
                    Radius = glow.Radius,
                    Duration = glow.Duration,
                    IsActive = true
                });

                // Apply buff if specified
                if (buffId.HasValue)
                {
                    ApplyBuffToEntity(entity, buffId.Value);
                }

                Plugin.Log.LogDebug($"[EntitySpawner] Created glowing buff entity at {position}");
                return entity;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EntitySpawner] Failed to create entity at {position}: {ex.Message}");
                return Entity.Null;
            }
        }

        /// <summary>
        /// Apply a buff to an entity using BuffSystem.
        /// </summary>
        private static void ApplyBuffToEntity(Entity entity, int buffId)
        {
            try
            {
                var buffGuid = new PrefabGUID(buffId);
                if (GameActionService.TryApplyBuff(entity, buffGuid, 30f))
                {
                    Plugin.Log.LogDebug($"[EntitySpawner] Applied buff {buffId} to entity");
                    return;
                }

                Plugin.Log.LogWarning($"[EntitySpawner] Failed applying buff {buffId} via GameActionService");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[EntitySpawner] Failed to apply buff {buffId}: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Generate random positions within a radius.
        /// </summary>
        private static NativeArray<float3> GenerateRandomPositions(
            float3 center,
            float radius,
            int count,
            ref Random random)
        {
            var positions = new NativeArray<float3>(count, Allocator.Temp);
            
            for (int i = 0; i < count; i++)
            {
                // Generate random position within sphere
                var randomDir = random.NextFloat3Direction();
                var randomDist = random.NextFloat(0, radius);
                positions[i] = center + (randomDir * randomDist);
            }

            return positions;
        }

        /// <summary>
        /// Spawn a glow border around a position (multiple entities in a circle).
        /// </summary>
        public static SpawnResult SpawnGlowBorder(
            float3 center,
            float radius,
            int entityCount,
            GlowConfig? glowConfig = null,
            int? buffId = null)
        {
            if (entityCount < 3) entityCount = 8; // Minimum for a circle
            
            var result = new SpawnResult
            {
                SpawnedEntities = new NativeList<Entity>(entityCount, Allocator.Temp),
                SpawnedPositions = new NativeList<float3>(entityCount, Allocator.Temp),
                SuccessCount = 0,
                FailCount = 0
            };

            if (!IsReady()) return result;

            var glow = glowConfig ?? GlowConfig.Default;
            var angleStep = (math.PI * 2) / entityCount;
            var random = new Random((uint)DateTime.UtcNow.Ticks);

            for (int i = 0; i < entityCount; i++)
            {
                var angle = angleStep * i;
                var position = new float3(
                    center.x + math.cos(angle) * radius,
                    center.y,
                    center.z + math.sin(angle) * radius
                );

                var entity = CreateGlowingBuffEntity(position, glow, buffId, false, ref random);
                if (entity != Entity.Null)
                {
                    result.SpawnedEntities.Add(entity);
                    result.SpawnedPositions.Add(position);
                    result.SuccessCount++;
                }
                else
                {
                    result.FailCount++;
                }
            }

            Plugin.Log.LogInfo($"[EntitySpawner] Spawned glow border: {result.SuccessCount} entities at radius {radius}");
            return result;
        }

        /// <summary>
        /// Spawn a grid of glowing entities.
        /// </summary>
        public static SpawnResult SpawnGlowGrid(
            float3 origin,
            int rows,
            int columns,
            float spacing,
            GlowConfig? glowConfig = null,
            int? buffId = null)
        {
            var totalCount = rows * columns;
            var result = new SpawnResult
            {
                SpawnedEntities = new NativeList<Entity>(totalCount, Allocator.Temp),
                SpawnedPositions = new NativeList<float3>(totalCount, Allocator.Temp),
                SuccessCount = 0,
                FailCount = 0
            };

            if (!IsReady()) return result;

            var glow = glowConfig ?? GlowConfig.Default;
            var random = new Random((uint)DateTime.UtcNow.Ticks);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    var position = origin + new float3(row * spacing, 0, col * spacing);
                    var entity = CreateGlowingBuffEntity(position, glow, buffId, false, ref random);
                    
                    if (entity != Entity.Null)
                    {
                        result.SpawnedEntities.Add(entity);
                        result.SpawnedPositions.Add(position);
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailCount++;
                    }
                }
            }

            Plugin.Log.LogInfo($"[EntitySpawner] Spawned glow grid: {result.SuccessCount} entities ({rows}x{columns})");
            return result;
        }

        /// <summary>
        /// Despawn all entities created by the spawner.
        /// </summary>
        public static void DespawnAll(NativeList<Entity> entities)
        {
            if (!IsReady() || !entities.IsCreated) return;

            try
            {
                _entityManager.DestroyEntity(entities.AsArray());
                Plugin.Log.LogInfo($"[EntitySpawner] Despawned {entities.Length} entities");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EntitySpawner] Failed to despawn entities: {ex.Message}");
            }
        }

        /// <summary>
        /// Update glow configuration on existing entities.
        /// </summary>
        public static void UpdateGlowConfig(NativeList<Entity> entities, GlowConfig newConfig)
        {
            if (!IsReady() || !entities.IsCreated) return;

            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    if (_entityManager.Exists(entity) && _entityManager.HasComponent<GlowComponent>(entity))
                    {
                        _entityManager.SetComponentData(entity, new GlowComponent
                        {
                            Color = newConfig.Color,
                            Intensity = newConfig.Intensity,
                            Radius = newConfig.Radius,
                            Duration = newConfig.Duration,
                            IsActive = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EntitySpawner] Failed to update glow config: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Shutdown the spawner and cleanup resources.
        /// </summary>
        public static void Shutdown()
        {
            if (_entityManager != null)
            {
                _entityManager = null;
                _initialized = false;
                Plugin.Log.LogInfo("[EntitySpawner] Shutdown complete");
            }
        }

        #endregion
    }

    /// <summary>
    /// Component data for glow effect configuration.
    /// </summary>
    public struct GlowComponent : IComponentData
    {
        public float3 Color;
        public float Intensity;
        public float Radius;
        public float Duration;
        public bool IsActive;
        public float CreatedTime;
    }

    /// <summary>
    /// Buffer element for storing multiple buffs on an entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct BuffBufferElement : IBufferElementData
    {
        public PrefabGUID BuffGuid;
        public float Duration;
        public float RemainingTime;
        public int StackCount;
        public bool IsActive;
    }
}
