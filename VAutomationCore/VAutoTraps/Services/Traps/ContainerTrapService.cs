using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VAutoTraps;
using VAuto.Core.Logging;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Simple container trap system using dictionaries (no ECS dependencies).
    /// This tracks traps on containers and their state.
    /// </summary>
    public static class ContainerTrapService
    {
        private static readonly Dictionary<float3, ContainerTrapData> _traps = new();
        private static readonly object _lock = new object();
        private static bool _initialized;
        private static CoreLogger _log;
        
        public static void Initialize(CoreLogger log)
        {
            if (_initialized) return;
            _initialized = true;
            _log = log;
            _log.LogInfo("[ContainerTrapService] Initialized");
        }
        
        /// <summary>
        /// Set a trap on a container at the given position.
        /// </summary>
        public static void SetTrap(float3 position, ulong ownerId, string trapType = "container")
        {
            lock (_lock)
            {
                var trap = new ContainerTrapData
                {
                    Position = position,
                    OwnerPlatformId = ownerId,
                    GlowColor = trapType == "container" 
                        ? TrapSpawnRules.Config.ContainerGlowColor 
                        : TrapSpawnRules.Config.WaypointTrapGlowColor,
                    GlowRadius = trapType == "container"
                        ? TrapSpawnRules.Config.ContainerGlowRadius
                        : TrapSpawnRules.Config.WaypointTrapGlowRadius,
                    DamageAmount = TrapSpawnRules.Config.TrapDamageAmount,
                    Duration = TrapSpawnRules.Config.TrapDuration,
                    IsArmed = true,
                    Triggered = false,
                    TrapType = trapType,
                    Name = trapType == "container" ? "Container Trap" : "Waypoint Trap"
                };
                
                _traps[position] = trap;
                _log.LogInfo($"[ContainerTrapService] {trap.Name} set at {position} for owner {ownerId}");
            }
        }
        
        /// <summary>
        /// Remove a trap from a container position.
        /// </summary>
        public static bool RemoveTrap(float3 position)
        {
            lock (_lock)
            {
                if (_traps.Remove(position))
                {
                    _log.LogInfo($"[ContainerTrapService] Trap removed at {position}");
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Arm or disarm a trap.
        /// </summary>
        public static bool SetArmed(float3 position, bool armed)
        {
            lock (_lock)
            {
                if (_traps.TryGetValue(position, out var trap))
                {
                    trap.IsArmed = armed;
                    _traps[position] = trap;
                    _log.LogInfo($"[ContainerTrapService] Trap at {position} {(armed ? "ARMED" : "DISARMED")}");
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Trigger a trap (called when someone opens it).
        /// </summary>
        public static bool TriggerTrap(float3 position, ulong intruderId)
        {
            lock (_lock)
            {
                if (_traps.TryGetValue(position, out var trap))
                {
                    if (!trap.IsArmed)
                    {
                        _log.LogInfo($"[ContainerTrapService] Trap at {position} is disarmed");
                        return false;
                    }
                    
                    trap.Triggered = true;
                    trap.LastTriggeredBy = intruderId;
                    trap.LastTriggeredTime = DateTime.UtcNow;
                    _traps[position] = trap;
                    
                    Plugin.Log.LogInfo($"[ContainerTrapService] TRIGGERED: {trap.Name} at {position} by {intruderId}");
                    
                    // Notify owner if possible
                    if (trap.OwnerPlatformId != 0)
                    {
                        Plugin.Log.LogInfo($"[ContainerTrapService] Owner {trap.OwnerPlatformId} should be notified");
                    }
                    
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Find the nearest trap to a position.
        /// </summary>
        public static (float3 Position, ContainerTrapData Trap)? FindNearestTrap(float3 position, float maxDistance = 10f)
        {
            lock (_lock)
            {
                float3 nearestPos = default;
                ContainerTrapData nearestTrap = default;
                float minDist = maxDistance;
                
                foreach (var kvp in _traps)
                {
                    var dist = math.distance(position, kvp.Key);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestPos = kvp.Key;
                        nearestTrap = kvp.Value;
                    }
                }
                
                if (minDist <= maxDistance)
                {
                    return (nearestPos, nearestTrap);
                }
                return null;
            }
        }
        
        /// <summary>
        /// Get all traps.
        /// </summary>
        public static Dictionary<float3, ContainerTrapData> GetAllTraps()
        {
            lock (_lock)
            {
                return new Dictionary<float3, ContainerTrapData>(_traps);
            }
        }
        
        /// <summary>
        /// Get trap count.
        /// </summary>
        public static int GetTrapCount()
        {
            lock (_lock)
            {
                return _traps.Count;
            }
        }
        
        /// <summary>
        /// Clear all traps.
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                _traps.Clear();
                Plugin.Log.LogInfo("[ContainerTrapService] All traps cleared");
            }
        }
    }
    
    /// <summary>
    /// Container trap data.
    /// </summary>
    public struct ContainerTrapData
    {
        public float3 Position;
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
    }
}
