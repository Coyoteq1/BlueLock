#if true
    using System;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using Unity.Entities;
    
    namespace VAuto.Helpers
    {
        /// <summary>
        /// Helper class for arena zone management
        /// Bridges between arena data and game API
        /// </summary>
        public static class ZoneHelper
        {
            /// <summary>
            /// Get spawn location for a zone by name
            /// </summary>
            public static float3? GetZoneSpawnLocation(string zoneName)
            {
                // TODO: Implement with actual zone data source
                // For now, return a default location based on zone name
                return zoneName switch
                {
                    "MainArena" => new float3(-1000f, 0f, -500f),
                    "PVPZone" => new float3(-1500f, 0f, -600f),
                    "TrainingArea" => new float3(-800f, 0f, -400f),
                    _ => new float3(-1000f, 0f, -500f)
                };
            }
    
            /// <summary>
            /// Get default arena spawn location
            /// </summary>
            public static float3 GetDefaultSpawnLocation()
            {
                return new float3(-1000f, 0f, -500f);
            }
    
            /// <summary>
            /// Get zone radius by name
            /// </summary>
            public static float GetZoneRadius(string zoneName)
            {
                return zoneName switch
                {
                    "MainArena" => 250f,
                    "PVPZone" => 150f,
                    "TrainingArea" => 100f,
                    _ => 200f
                };
            }
    
            /// <summary>
            /// Check if a position is within a zone
            /// </summary>
            public static bool IsInZone(float3 position, string zoneName)
            {
                var zoneCenter = GetZoneSpawnLocation(zoneName);
                if (!zoneCenter.HasValue)
                {
                    return false;
                }
    
                var distance = math.distance(position, zoneCenter.Value);
                return distance <= GetZoneRadius(zoneName);
            }
    
            /// <summary>
            /// Check if a position is within the default arena zone
            /// </summary>
            public static bool IsInDefaultArena(float3 position)
            {
                var zoneCenter = GetDefaultSpawnLocation();
                var distance = math.distance(position, zoneCenter);
                return distance <= GetZoneRadius("MainArena");
            }
    
            /// <summary>
            /// Get all enabled zone names
            /// </summary>
            public static List<string> GetEnabledZoneNames()
            {
                return new List<string>
                {
                    "MainArena",
                    "PVPZone",
                    "TrainingArea"
                };
            }
    
            /// <summary>
            /// Get zone info by name
            /// </summary>
            public static (string Name, float3 Position, float Radius, bool Enabled)? GetZoneInfo(string zoneName)
            {
                var position = GetZoneSpawnLocation(zoneName);
                if (!position.HasValue)
                {
                    return null;
                }
    
                return (zoneName, position.Value, GetZoneRadius(zoneName), true);
            }
    
            /// <summary>
            /// Teleport player to zone spawn
            /// </summary>
            public static bool TeleportToZone(Entity characterEntity, string zoneName)
            {
                try
                {
                    var spawnLocation = GetZoneSpawnLocation(zoneName);
                    if (!spawnLocation.HasValue)
                    {
                        Plugin.Log.LogError($"Zone '{zoneName}' not found");
                        return false;
                    }
    
                    Plugin.Log.LogInfo($"Teleporting to zone '{zoneName}' at {spawnLocation.Value}");
                    
                    // TODO: Use TeleportService or PlayerService to teleport
                    // PlayerService.SetPlayerPosition(characterEntity, spawnLocation.Value);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Error teleporting to zone: {ex.Message}");
                    return false;
                }
            }
    
            /// <summary>
            /// Get nearest zone to a position
            /// </summary>
            public static string? GetNearestZone(float3 position)
            {
                var enabledZones = GetEnabledZoneNames();
                string? nearestZone = null;
                float nearestDistance = float.MaxValue;
    
                foreach (var zone in enabledZones)
                {
                    var zoneCenter = GetZoneSpawnLocation(zone);
                    if (!zoneCenter.HasValue) continue;
                    
                    var distance = math.distance(position, zoneCenter.Value);
                    
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestZone = zone;
                    }
                }
    
                return nearestZone;
            }
    
            /// <summary>
            /// Format zone info for display
            /// </summary>
            public static string FormatZoneInfo(string zoneName)
            {
                var zoneInfo = GetZoneInfo(zoneName);
                if (!zoneInfo.HasValue)
                {
                    return $"Zone '{zoneName}' not found";
                }
    
                var (name, pos, radius, enabled) = zoneInfo.Value;
                return $"{name}:\n" +
                       $"  Location: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})\n" +
                       $"  Radius: {radius:F0}m\n" +
                       $"  Status: {(enabled ? "Enabled" : "Disabled")}";
            }
        }
    }
    
    
#endif
