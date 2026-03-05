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
    /// ScarletCore-style glow service for real glow effects on entities and borders.
    /// Direct entity manipulation with minimal abstraction.
    /// </summary>
    public static class GlowService
    {
        private static readonly Dictionary<string, List<Entity>> _zoneBorders = new();
        private static readonly CoreLogger _logger = new("GlowService");

        /// <summary>
        /// Add glow effect to an existing entity - ScarletCore direct approach
        /// </summary>
        public static Entity AddGlow(Entity target, float3 color, float radius = 5f, float duration = 30f)
        {
            try
            {
                if (!target.IsAlive())
                {
                    _logger.LogWarning("Target entity is not alive");
                    return Entity.Null;
                }

                // Create glow entity directly (ScarletCore pattern)
                var glowEntity = GameSystems.ServerGameManager.InstantiateEntityImmediate(Entity.Null, 
                    new PrefabGUID(-123456789)); // Known glow prefab GUID

                // Position at target
                var targetPos = target.Read<LocalTransform>().Position;
                glowEntity.AddWith((ref LocalTransform lt) => 
                {
                    lt.Position = targetPos;
                });

                // Add glow component directly
                glowEntity.Add(new GlowComponent
                {
                    Color = color,
                    Intensity = 1f,
                    Radius = radius
                });

                // Add lifetime
                glowEntity.AddWith((ref LifeTime lt) => 
                {
                    lt.Duration = duration;
                    lt.EndAction = LifeTimeEndAction.Destroy;
                });

                _logger.LogInfo($"Added glow to entity at {targetPos}");
                return glowEntity;
            }
            catch (Exception ex)
            {
                _logger.LogException("Error adding glow to entity", ex);
                return Entity.Null;
            }
        }

        /// <summary>
        /// Spawn standalone glow entity - ScarletCore simplicity
        /// </summary>
        public static Entity SpawnGlowEntity(float3 position, float3 color, float radius = 5f, float duration = 30f)
        {
            try
            {
                var glowEntity = GameSystems.ServerGameManager.InstantiateEntityImmediate(Entity.Null, 
                    new PrefabGUID(-123456789));

                glowEntity.AddWith((ref LocalTransform lt) => 
                {
                    lt.Position = position;
                });

                glowEntity.Add(new GlowComponent
                {
                    Color = color,
                    Intensity = 1f,
                    Radius = radius
                });

                glowEntity.AddWith((ref LifeTime lt) => 
                {
                    lt.Duration = duration;
                    lt.EndAction = LifeTimeEndAction.Destroy;
                });

                _logger.LogInfo($"Spawned glow entity at {position}");
                return glowEntity;
            }
            catch (Exception ex)
            {
                _logger.LogException("Error spawning glow entity", ex);
                return Entity.Null;
            }
        }

        /// <summary>
        /// Create glow circle border - ScarletCore direct pattern
        /// </summary>
        public static List<Entity> SpawnGlowCircle(float3 center, float radius, int count, float3 color, float duration = 60f)
        {
            var glows = new List<Entity>();
            
            try
            {
                for (int i = 0; i < count; i++)
                {
                    // Calculate position on circle (ScarletCore simplicity)
                    var angle = (float)i / count * Mathf.PI * 2f;
                    var glowPos = center + new float3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );

                    var glow = SpawnGlowEntity(glowPos, color, 3f, duration);
                    if (glow != Entity.Null)
                    {
                        glows.Add(glow);
                    }
                }

                _logger.LogInfo($"Spawned glow circle with {glows.Count} entities at {center}");
                return glows;
            }
            catch (Exception ex)
            {
                _logger.LogException("Error spawning glow circle", ex);
                return glows;
            }
        }

        /// <summary>
        /// Create rectangular border glow - ScarletCore approach
        /// </summary>
        public static List<Entity> SpawnGlowBorder(float3 center, float2 size, float3 color, int pointsPerSide = 8, float duration = 60f)
        {
            var glows = new List<Entity>();
            
            try
            {
                var halfSize = size * 0.5f;
                var totalPoints = pointsPerSide * 4;

                for (int i = 0; i < totalPoints; i++)
                {
                    float3 position;
                    var side = i / pointsPerSide;
                    var sideProgress = (float)(i % pointsPerSide) / pointsPerSide;

                    // Calculate position on rectangle (ScarletCore direct math)
                    switch (side)
                    {
                        case 0: // Top
                            position = center + new float3(
                                -halfSize.x + sideProgress * size.x,
                                0,
                                -halfSize.y
                            );
                            break;
                        case 1: // Right
                            position = center + new float3(
                                halfSize.x,
                                0,
                                -halfSize.y + sideProgress * size.y
                            );
                            break;
                        case 2: // Bottom
                            position = center + new float3(
                                halfSize.x - sideProgress * size.x,
                                0,
                                halfSize.y
                            );
                            break;
                        default: // Left
                            position = center + new float3(
                                -halfSize.x,
                                0,
                                halfSize.y - sideProgress * size.y
                            );
                            break;
                    }

                    var glow = SpawnGlowEntity(position, color, 3f, duration);
                    if (glow != Entity.Null)
                    {
                        glows.Add(glow);
                    }
                }

                _logger.LogInfo($"Spawned rectangular border with {glows.Count} glows at {center}");
                return glows;
            }
            catch (Exception ex)
            {
                _logger.LogException("Error spawning glow border", ex);
                return glows;
            }
        }

        /// <summary>
        /// Set zone border glow - ScarletCore zone management
        /// </summary>
        public static void SetZoneBorder(string zoneId, float3 center, float radius, float3 color, int glowCount = 16)
        {
            try
            {
                // Clear existing zone border
                ClearZoneBorder(zoneId);

                // Create circular border
                var borderGlows = SpawnGlowCircle(center, radius, glowCount, color, 300f); // 5 minutes
                _zoneBorders[zoneId] = borderGlows;

                _logger.LogInfo($"Set border for zone {zoneId} with {borderGlows.Count} glows");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error setting zone border for {zoneId}", ex);
            }
        }

        /// <summary>
        /// Set rectangular zone border - ScarletCore approach
        /// </summary>
        public static void SetZoneBorderRect(string zoneId, float3 center, float2 size, float3 color, int pointsPerSide = 8)
        {
            try
            {
                // Clear existing zone border
                ClearZoneBorder(zoneId);

                // Create rectangular border
                var borderGlows = SpawnGlowBorder(center, size, color, pointsPerSide, 300f); // 5 minutes
                _zoneBorders[zoneId] = borderGlows;

                _logger.LogInfo($"Set rectangular border for zone {zoneId} with {borderGlows.Count} glows");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error setting rectangular zone border for {zoneId}", ex);
            }
        }

        /// <summary>
        /// Clear zone border glows - ScarletCore cleanup
        /// </summary>
        public static void ClearZoneBorder(string zoneId)
        {
            try
            {
                if (_zoneBorders.TryGetValue(zoneId, out var existingGlows))
                {
                    foreach (var glow in existingGlows)
                    {
                        if (glow.IsAlive())
                        {
                            glow.Destroy();
                        }
                    }
                    _zoneBorders.Remove(zoneId);
                    _logger.LogInfo($"Cleared border for zone {zoneId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error clearing zone border for {zoneId}", ex);
            }
        }

        /// <summary>
        /// Remove glow from entity - ScarletCore direct cleanup
        /// </summary>
        public static void RemoveGlow(Entity entity)
        {
            try
            {
                if (entity.IsAlive())
                {
                    entity.Destroy();
                    _logger.LogInfo("Removed glow entity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException("Error removing glow", ex);
            }
        }

        /// <summary>
        /// Clear all glow entities - ScarletCore cleanup
        /// </summary>
        public static void ClearAllGlows()
        {
            try
            {
                // Clear all zone borders
                var zones = new List<string>(_zoneBorders.Keys);
                foreach (var zoneId in zones)
                {
                    ClearZoneBorder(zoneId);
                }

                _logger.LogInfo("Cleared all glow entities");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error clearing all glows", ex);
            }
        }
    }
}
