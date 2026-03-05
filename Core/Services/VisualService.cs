using System;
using System.Collections.Generic;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAutomationCore.Core.Logging;

namespace VAuto.Core.Services
{
    /// <summary>
    /// ScarletCore-style visual service for particle effects and visual elements.
    /// Direct entity manipulation with minimal abstraction.
    /// </summary>
    public static class VisualService
    {
        private static readonly Dictionary<string, List<Entity>> _zoneEffects = new();
        private static readonly CoreLogger _logger = new("VisualService");

        /// <summary>
        /// Spawn visual effect from prefab - ScarletCore direct approach
        /// </summary>
        public static Entity SpawnEffect(PrefabGUID effectPrefab, float3 position, float duration = 10f)
        {
            try
            {
                var effectEntity = GameSystems.ServerGameManager.InstantiateEntityImmediate(Entity.Null, effectPrefab);

                effectEntity.AddWith((ref LocalTransform lt) => 
                {
                    lt.Position = position;
                });

                // Add lifetime for auto-cleanup
                effectEntity.AddWith((ref LifeTime lt) => 
                {
                    lt.Duration = duration;
                    lt.EndAction = LifeTimeEndAction.Destroy;
                });

                _logger.LogInfo($"Spawned visual effect at {position}");
                return effectEntity;
            }
            catch (Exception ex)
            {
                _logger.LogException("Error spawning visual effect", ex);
                return Entity.Null;
            }
        }

        /// <summary>
        /// Spawn particle effect by name - ScarletCore simplicity
        /// </summary>
        public static Entity SpawnParticleEffect(string effectName, float3 position, float3 direction)
        {
            try
            {
                // Get prefab from name (ScarletCore pattern)
                if (GameSystems.PrefabCollectionSystem._PrefabLookupMap.TryGetGUID(effectName, out var prefabGuid))
                {
                    return SpawnEffect(prefabGuid, position);
                }

                _logger.LogWarning($"Effect prefab not found: {effectName}");
                return Entity.Null;
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error spawning particle effect: {effectName}", ex);
                return Entity.Null;
            }
        }

        /// <summary>
        /// Create lightning effect between two points - ScarletCore visual focus
        /// </summary>
        public static void SpawnLightning(float3 start, float3 end, float3 color, float duration = 2f)
        {
            try
            {
                // Use known lightning prefab
                var lightningPrefab = new PrefabGUID(-987654321); // Lightning effect prefab
                var lightningEntity = GameSystems.ServerGameManager.InstantiateEntityImmediate(Entity.Null, lightningPrefab);

                // Position at start point
                lightningEntity.AddWith((ref LocalTransform lt) => 
                {
                    lt.Position = start;
                    lt.Rotation = quaternion.LookRotationSafe(end - start, math.up());
                });

                // Add lightning component with target
                lightningEntity.Add(new LightningComponent
                {
                    StartPoint = start,
                    EndPoint = end,
                    Color = color,
                    Duration = duration
                });

                // Auto-cleanup
                lightningEntity.AddWith((ref LifeTime lt) => 
                {
                    lt.Duration = duration;
                    lt.EndAction = LifeTimeEndAction.Destroy;
                });

                _logger.LogInfo($"Spawned lightning from {start} to {end}");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error spawning lightning", ex);
            }
        }

        /// <summary>
        /// Create explosion effect - ScarletCore visual impact
        /// </summary>
        public static void SpawnExplosion(float3 position, float radius, float3 color)
        {
            try
            {
                // Use known explosion prefab
                var explosionPrefab = new PrefabGUID(-555666777); // Explosion effect prefab
                var explosionEntity = GameSystems.ServerGameManager.InstantiateEntityImmediate(Entity.Null, explosionPrefab);

                explosionEntity.AddWith((ref LocalTransform lt) => 
                {
                    lt.Position = position;
                });

                // Add explosion component
                explosionEntity.Add(new ExplosionComponent
                {
                    Position = position,
                    Radius = radius,
                    Color = color,
                    Intensity = 1f
                });

                // Short duration for explosion
                explosionEntity.AddWith((ref LifeTime lt) => 
                {
                    lt.Duration = 3f;
                    lt.EndAction = LifeTimeEndAction.Destroy;
                });

                _logger.LogInfo($"Spawned explosion at {position}");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error spawning explosion", ex);
            }
        }

        /// <summary>
        /// Create visual line from points - ScarletCore border effects
        /// </summary>
        public static void CreateVisualLine(float3[] points, float3 color, float duration = 5f)
        {
            try
            {
                if (points.Length < 2)
                {
                    _logger.LogWarning("Visual line needs at least 2 points");
                    return;
                }

                // Create small visual entities along the line
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var start = points[i];
                    var end = points[i + 1];
                    var distance = math.distance(start, end);
                    var steps = (int)(distance / 2f); // Place effect every 2 units

                    for (int step = 0; step <= steps; step++)
                    {
                        var t = (float)step / steps;
                        var position = math.lerp(start, end, t);

                        // Create small particle for line segment
                        var particle = SpawnEffect(new PrefabGUID(-111222333), position, duration);
                        if (particle != Entity.Null)
                        {
                            // Add color override
                            particle.Add(new ColorOverrideComponent
                            {
                                Color = color
                            });
                        }
                    }
                }

                _logger.LogInfo($"Created visual line with {points.Length} points");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error creating visual line", ex);
            }
        }

        /// <summary>
        /// Create visual border circle - ScarletCore zone effects
        /// </summary>
        public static void CreateVisualBorderCircle(float3 center, float radius, float3 color, int pointCount = 16, float duration = 60f)
        {
            try
            {
                var points = new float3[pointCount + 1];
                
                for (int i = 0; i <= pointCount; i++)
                {
                    var angle = (float)i / pointCount * Mathf.PI * 2f;
                    points[i] = center + new float3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );
                }

                CreateVisualLine(points, color, duration);
                _logger.LogInfo($"Created visual border circle at {center} with radius {radius}");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error creating visual border circle", ex);
            }
        }

        /// <summary>
        /// Create visual border rectangle - ScarletCore zone effects
        /// </summary>
        public static void CreateVisualBorderRect(float3 center, float2 size, float3 color, int pointsPerSide = 8, float duration = 60f)
        {
            try
            {
                var halfSize = size * 0.5f;
                var totalPoints = pointsPerSide * 4 + 1;
                var points = new float3[totalPoints];
                var index = 0;

                // Top edge
                for (int i = 0; i <= pointsPerSide; i++)
                {
                    var t = (float)i / pointsPerSide;
                    points[index++] = center + new float3(
                        -halfSize.x + t * size.x,
                        0,
                        -halfSize.y
                    );
                }

                // Right edge
                for (int i = 1; i <= pointsPerSide; i++)
                {
                    var t = (float)i / pointsPerSide;
                    points[index++] = center + new float3(
                        halfSize.x,
                        0,
                        -halfSize.y + t * size.y
                    );
                }

                // Bottom edge
                for (int i = 1; i <= pointsPerSide; i++)
                {
                    var t = (float)i / pointsPerSide;
                    points[index++] = center + new float3(
                        halfSize.x - t * size.x,
                        0,
                        halfSize.y
                    );
                }

                // Left edge
                for (int i = 1; i < pointsPerSide; i++)
                {
                    var t = (float)i / pointsPerSide;
                    points[index++] = center + new float3(
                        -halfSize.x,
                        0,
                        halfSize.y - t * size.y
                    );
                }

                CreateVisualLine(points, color, duration);
                _logger.LogInfo($"Created visual border rectangle at {center} with size {size}");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error creating visual border rectangle", ex);
            }
        }

        /// <summary>
        /// Set zone visual effects - ScarletCore zone management
        /// </summary>
        public static void SetZoneVisualBorder(string zoneId, float3 center, float radius, float3 color, int pointCount = 16)
        {
            try
            {
                // Clear existing zone effects
                ClearZoneVisualEffects(zoneId);

                // Create visual border
                CreateVisualBorderCircle(center, radius, color, pointCount, 300f); // 5 minutes

                // Store zone reference for cleanup
                _zoneEffects[zoneId] = new List<Entity>(); // Could store actual entities if needed

                _logger.LogInfo($"Set visual border for zone {zoneId}");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error setting zone visual border for {zoneId}", ex);
            }
        }

        /// <summary>
        /// Set rectangular zone visual border.
        /// </summary>
        public static void SetZoneVisualBorderRect(string zoneId, float3 center, float2 size, float3 color, int pointsPerSide = 8)
        {
            try
            {
                ClearZoneVisualEffects(zoneId);
                CreateVisualBorderRect(center, size, color, pointsPerSide, 300f); // 5 minutes
                _zoneEffects[zoneId] = new List<Entity>();
                _logger.LogInfo($"Set rectangular visual border for zone {zoneId}");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error setting rectangular visual border for {zoneId}", ex);
            }
        }

        /// <summary>
        /// Clear zone visual effects - ScarletCore cleanup
        /// </summary>
        public static void ClearZoneVisualEffects(string zoneId)
        {
            try
            {
                if (_zoneEffects.ContainsKey(zoneId))
                {
                    _zoneEffects.Remove(zoneId);
                    _logger.LogInfo($"Cleared visual effects for zone {zoneId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error clearing zone visual effects for {zoneId}", ex);
            }
        }

        /// <summary>
        /// Clear all visual effects - ScarletCore cleanup
        /// </summary>
        public static void ClearAllEffects()
        {
            try
            {
                var zones = new List<string>(_zoneEffects.Keys);
                foreach (var zoneId in zones)
                {
                    ClearZoneVisualEffects(zoneId);
                }

                _logger.LogInfo("Cleared all visual effects");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error clearing all visual effects", ex);
            }
        }
    }

    // Helper components for visual effects
    public struct LightningComponent : IComponentData
    {
        public float3 StartPoint;
        public float3 EndPoint;
        public float3 Color;
        public float Duration;
    }

    public struct ExplosionComponent : IComponentData
    {
        public float3 Position;
        public float Radius;
        public float3 Color;
        public float Intensity;
    }

    public struct ColorOverrideComponent : IComponentData
    {
        public float3 Color;
    }
}
