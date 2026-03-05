using System;
using System.Collections.Generic;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAutomationCore.Core.Logging;
using Blueluck.Data;

namespace VAuto.Core.Services
{
    /// <summary>
    /// ScarletCore-style tile service for tile-based world manipulation.
    /// Direct entity manipulation with minimal abstraction.
    /// </summary>
    public static class TileService
    {
        private static readonly Dictionary<string, List<Entity>> _zoneTiles = new();
        private static readonly CoreLogger _logger = new("TileService");

        /// <summary>
        /// Spawn single tile - ScarletCore direct approach
        /// </summary>
        public static Entity SpawnTile(string tileName, float3 position, quaternion rotation = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tileName))
                {
                    _logger.LogWarning("Tile name cannot be empty");
                    return Entity.Null;
                }

                // Get tile prefab from datatype (ScarletCore pattern)
                if (!Tiles.ByShortName.TryGetValue(tileName, out var fullTileName))
                {
                    _logger.LogWarning($"Tile not found: {tileName}");
                    return Entity.Null;
                }

                // Get prefab GUID
                if (!GameSystems.PrefabCollectionSystem._PrefabLookupMap.TryGetGUID(fullTileName, out var prefabGuid))
                {
                    _logger.LogWarning($"Tile prefab not found: {fullTileName}");
                    return Entity.Null;
                }

                // Spawn tile directly (ScarletCore approach)
                var tileEntity = GameSystems.ServerGameManager.InstantiateEntityImmediate(Entity.Null, prefabGuid);

                // Set position and rotation
                tileEntity.AddWith((ref LocalTransform lt) => 
                {
                    lt.Position = position;
                    lt.Rotation = rotation == default ? quaternion.identity : rotation;
                });

                _logger.LogInfo($"Spawned tile {tileName} at {position}");
                return tileEntity;
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error spawning tile: {tileName}", ex);
                return Entity.Null;
            }
        }

        /// <summary>
        /// Spawn tile pattern - ScarletCore batch approach
        /// </summary>
        public static List<Entity> SpawnTilePattern(string[] tileNames, float3 center, float spacing)
        {
            var tiles = new List<Entity>();
            
            try
            {
                if (tileNames == null || tileNames.Length == 0)
                {
                    _logger.LogWarning("Tile names array cannot be empty");
                    return tiles;
                }

                // Calculate grid dimensions (ScarletCore simplicity)
                var gridSize = (int)Math.Ceiling(Math.Sqrt(tileNames.Length));
                var halfGrid = gridSize * 0.5f * spacing;

                for (int i = 0; i < tileNames.Length; i++)
                {
                    var row = i / gridSize;
                    var col = i % gridSize;
                    
                    var position = center + new float3(
                        (col - halfGrid) * spacing,
                        0,
                        (row - halfGrid) * spacing
                    );

                    var tile = SpawnTile(tileNames[i], position);
                    if (tile != Entity.Null)
                    {
                        tiles.Add(tile);
                    }
                }

                _logger.LogInfo($"Spawned tile pattern with {tiles.Count} tiles at {center}");
                return tiles;
            }
            catch (Exception ex)
            {
                _logger.LogException("Error spawning tile pattern", ex);
                return tiles;
            }
        }

        /// <summary>
        /// Spawn tiles in circle - ScarletCore border pattern
        /// </summary>
        public static void SpawnTileCircle(string tileName, float3 center, float radius, int count)
        {
            try
            {
                var tiles = new List<Entity>();

                for (int i = 0; i < count; i++)
                {
                    // Calculate position on circle (ScarletCore direct math)
                    var angle = (float)i / count * Mathf.PI * 2f;
                    var position = center + new float3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );

                    var tile = SpawnTile(tileName, position);
                    if (tile != Entity.Null)
                    {
                        tiles.Add(tile);
                    }
                }

                _logger.LogInfo($"Spawned tile circle with {tiles.Count} {tileName} tiles at {center}");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error spawning tile circle: {tileName}", ex);
            }
        }

        /// <summary>
        /// Spawn tiles in rectangle - ScarletCore border pattern
        /// </summary>
        public static void SpawnTileRectangle(string tileName, float3 center, float2 size, float spacing = 2f)
        {
            try
            {
                var tiles = new List<Entity>();
                var halfSize = size * 0.5f;
                var tilesX = (int)Math.Ceiling(size.x / spacing);
                var tilesZ = (int)Math.Ceiling(size.y / spacing);

                for (int x = 0; x < tilesX; x++)
                {
                    for (int z = 0; z < tilesZ; z++)
                    {
                        var position = center + new float3(
                            -halfSize.x + x * spacing,
                            0,
                            -halfSize.y + z * spacing
                        );

                        var tile = SpawnTile(tileName, position);
                        if (tile != Entity.Null)
                        {
                            tiles.Add(tile);
                        }
                    }
                }

                _logger.LogInfo($"Spawned tile rectangle with {tiles.Count} {tileName} tiles at {center}");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error spawning tile rectangle: {tileName}", ex);
            }
        }

        /// <summary>
        /// Spawn tile border circle - ScarletCore zone border
        /// </summary>
        public static void SpawnTileBorderCircle(string tileName, float3 center, float radius, int count, string zoneId = null)
        {
            try
            {
                var tiles = new List<Entity>();

                for (int i = 0; i < count; i++)
                {
                    var angle = (float)i / count * Mathf.PI * 2f;
                    var position = center + new float3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );

                    var tile = SpawnTile(tileName, position);
                    if (tile != Entity.Null)
                    {
                        tiles.Add(tile);
                    }
                }

                // Store zone reference for cleanup
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (_zoneTiles.ContainsKey(zoneId))
                    {
                        _zoneTiles[zoneId].AddRange(tiles);
                    }
                    else
                    {
                        _zoneTiles[zoneId] = tiles;
                    }
                }

                _logger.LogInfo($"Spawned tile border circle with {tiles.Count} {tileName} tiles for zone {zoneId}");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error spawning tile border circle: {tileName}", ex);
            }
        }

        /// <summary>
        /// Spawn tile border rectangle - ScarletCore zone border
        /// </summary>
        public static void SpawnTileBorderRectangle(string tileName, float3 center, float2 size, int pointsPerSide, string zoneId = null)
        {
            try
            {
                var tiles = new List<Entity>();
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

                    var tile = SpawnTile(tileName, position);
                    if (tile != Entity.Null)
                    {
                        tiles.Add(tile);
                    }
                }

                // Store zone reference for cleanup
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (_zoneTiles.ContainsKey(zoneId))
                    {
                        _zoneTiles[zoneId].AddRange(tiles);
                    }
                    else
                    {
                        _zoneTiles[zoneId] = tiles;
                    }
                }

                _logger.LogInfo($"Spawned tile border rectangle with {tiles.Count} {tileName} tiles for zone {zoneId}");
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error spawning tile border rectangle: {tileName}", ex);
            }
        }

        /// <summary>
        /// Remove single tile - ScarletCore direct cleanup
        /// </summary>
        public static void RemoveTile(Entity tileEntity)
        {
            try
            {
                if (tileEntity.IsAlive())
                {
                    tileEntity.Destroy();
                    _logger.LogInfo("Removed tile entity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException("Error removing tile", ex);
            }
        }

        /// <summary>
        /// Clear tile area - ScarletCore area cleanup
        /// </summary>
        public static void ClearTileArea(float3 center, float radius)
        {
            try
            {
                // Find all tile entities in area (ScarletCore direct query)
                var entityManager = GameSystems.EntityManager;
                var query = entityManager.CreateEntityQuery(typeof(TileComponent), typeof(LocalTransform));
                
                var entities = query.ToEntityArray(Allocator.Temp);
                var removedCount = 0;

                foreach (var entity in entities)
                {
                    if (entity.Has<LocalTransform>())
                    {
                        var transform = entity.Read<LocalTransform>();
                        var distance = math.distance(transform.Position, center);
                        
                        if (distance <= radius)
                        {
                            entity.Destroy();
                            removedCount++;
                        }
                    }
                }

                entities.Dispose();
                query.Dispose();

                _logger.LogInfo($"Cleared {removedCount} tiles in area around {center}");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error clearing tile area", ex);
            }
        }

        /// <summary>
        /// Clear zone tiles - ScarletCore zone cleanup
        /// </summary>
        public static void ClearZoneTiles(string zoneId)
        {
            try
            {
                if (_zoneTiles.TryGetValue(zoneId, out var tiles))
                {
                    foreach (var tile in tiles)
                    {
                        if (tile.IsAlive())
                        {
                            tile.Destroy();
                        }
                    }
                    _zoneTiles.Remove(zoneId);
                    _logger.LogInfo($"Cleared {tiles.Count} tiles for zone {zoneId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException($"Error clearing zone tiles for {zoneId}", ex);
            }
        }

        /// <summary>
        /// Clear all tiles - ScarletCore global cleanup
        /// </summary>
        public static void ClearAllTiles()
        {
            try
            {
                // Clear all zone tiles
                var zones = new List<string>(_zoneTiles.Keys);
                foreach (var zoneId in zones)
                {
                    ClearZoneTiles(zoneId);
                }

                // Also clear any remaining tile entities
                var entityManager = GameSystems.EntityManager;
                var query = entityManager.CreateEntityQuery(typeof(TileComponent));
                
                var entities = query.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities)
                {
                    entity.Destroy();
                }

                entities.Dispose();
                query.Dispose();

                _logger.LogInfo("Cleared all tiles");
            }
            catch (Exception ex)
            {
                _logger.LogException("Error clearing all tiles", ex);
            }
        }
    }

    // Helper component for tile identification
    public struct TileComponent : IComponentData
    {
        public string TileName;
        public string ZoneId;
    }
}

