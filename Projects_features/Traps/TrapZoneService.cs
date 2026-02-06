using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Zone-based trap system - creates small trigger zones (1-2m radius) at locations.
    /// When a player enters the zone, the trap triggers.
    /// </summary>
    public static class TrapZoneService
    {
        private static readonly Dictionary<float3, TrapZoneData> _zones = new();
        private static readonly object _lock = new object();
        private static bool _initialized;
        private const float DefaultRadius = 2f; // 2 meter radius
        
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Plugin.Log.LogInfo("[TrapZoneService] Initialized - zone-based traps ready");
        }
        
        /// <summary>
        /// Create a trap zone at the specified position.
        /// </summary>
        /// <param name="position">Center of the zone</param>
        /// <param name="ownerId">Player who owns the trap</param>
        /// <param name="radius">Trigger radius (default 2m)</param>
        /// <param name="trapType">Type: "container", "waypoint", "border"</param>
        public static void CreateZone(float3 position, ulong ownerId, float radius = DefaultRadius, string trapType = "container")
        {
            lock (_lock)
            {
                var zone = new TrapZoneData
                {
                    Position = position,
                    Radius = radius,
                    OwnerPlatformId = ownerId,
                    GlowColor = GetColorForType(trapType),
                    GlowRadius = radius * 1.5f, // Glow slightly larger than trigger zone
                    DamageAmount = TrapSpawnRules.Config.TrapDamageAmount,
                    Duration = TrapSpawnRules.Config.TrapDuration,
                    IsArmed = true,
                    Triggered = false,
                    TrapType = trapType,
                    Name = $"{trapType} trap zone",
                    CreatedTime = DateTime.UtcNow
                };
                
                _zones[position] = zone;
                Plugin.Log.LogInfo($"[TrapZoneService] Created {trapType} zone at {position} (r={radius}m) for owner {ownerId}");
            }
        }
        
        /// <summary>
        /// Remove a trap zone.
        /// </summary>
        public static bool RemoveZone(float3 position)
        {
            lock (_lock)
            {
                if (_zones.Remove(position))
                {
                    Plugin.Log.LogInfo($"[TrapZoneService] Zone removed at {position}");
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Remove zone within radius of position.
        /// </summary>
        public static bool RemoveNearestZone(float3 position, float maxRadius = 5f)
        {
            lock (_lock)
            {
                foreach (var kvp in _zones)
                {
                    if (math.distance(position, kvp.Key) <= maxRadius)
                    {
                        _zones.Remove(kvp.Key);
                        Plugin.Log.LogInfo($"[TrapZoneService] Nearest zone removed at {kvp.Key}");
                        return true;
                    }
                }
                return false;
            }
        }
        
        /// <summary>
        /// Arm or disarm a zone.
        /// </summary>
        public static bool SetArmed(float3 position, bool armed)
        {
            lock (_lock)
            {
                if (_zones.TryGetValue(position, out var zone))
                {
                    zone.IsArmed = armed;
                    _zones[position] = zone;
                    Plugin.Log.LogInfo($"[TrapZoneService] Zone at {position} {(armed ? "ARMED" : "DISARMED")}");
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Check if a position is inside any armed trap zone.
        /// Returns the zone data if triggered, null otherwise.
        /// </summary>
        public static (float3 ZonePosition, TrapZoneData Zone)? CheckTrigger(float3 playerPosition, ulong playerId)
        {
            lock (_lock)
            {
                foreach (var kvp in _zones)
                {
                    var zonePos = kvp.Key;
                    var zone = kvp.Value;
                    
                    // Check if player is inside zone
                    var dist = math.distance(playerPosition, zonePos);
                    
                    if (dist <= zone.Radius && zone.IsArmed && !zone.Triggered)
                    {
                        // Trigger the trap!
                        zone.Triggered = true;
                        zone.LastTriggeredBy = playerId;
                        zone.LastTriggeredTime = DateTime.UtcNow;
                        _zones[zonePos] = zone;
                        
                        Plugin.Log.LogInfo($"[TrapZoneService] 🔥 TRIGGERED: {zone.Name} at {zonePos}");
                        Plugin.Log.LogInfo($"[TrapZoneService]   Player {playerId} entered zone (dist={dist:F1}m, radius={zone.Radius}m)");
                        
                        return (zonePos, zone);
                    }
                }
                return null;
            }
        }
        
        /// <summary>
        /// Check if position is in any zone (without triggering).
        /// </summary>
        public static bool IsInZone(float3 position)
        {
            lock (_lock)
            {
                foreach (var kvp in _zones)
                {
                    if (math.distance(position, kvp.Key) <= kvp.Value.Radius)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        /// <summary>
        /// Get all zones.
        /// </summary>
        public static Dictionary<float3, TrapZoneData> GetAllZones()
        {
            lock (_lock)
            {
                return new Dictionary<float3, TrapZoneData>(_zones);
            }
        }
        
        /// <summary>
        /// Get zone count.
        /// </summary>
        public static int GetZoneCount()
        {
            lock (_lock)
            {
                return _zones.Count;
            }
        }
        
        /// <summary>
        /// Clear all zones.
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                _zones.Clear();
                Plugin.Log.LogInfo("[TrapZoneService] All zones cleared");
            }
        }
        
        /// <summary>
        /// Get trigger color based on trap type.
        /// </summary>
        private static float3 GetColorForType(string trapType)
        {
            return trapType switch
            {
                "container" => TrapSpawnRules.Config.ContainerGlowColor,
                "waypoint" => TrapSpawnRules.Config.WaypointTrapGlowColor,
                "border" => new float3(1f, 0f, 1f), // Purple for border traps
                _ => new float3(1f, 1f, 0f) // Yellow default
            };
        }
    }
    
    /// <summary>
    /// Trap zone data.
    /// </summary>
    public struct TrapZoneData
    {
        public float3 Position;
        public float Radius;
        public ulong OwnerPlatformId;
        public float3 GlowColor;
        public float GlowRadius;
        public float DamageAmount;
        public float Duration;
        public bool IsArmed;
        public bool Triggered;
        public ulong? LastTriggeredBy;
        public DateTime LastTriggeredTime;
        public string TrapType;
        public string Name;
        public DateTime CreatedTime;
    }
}
