using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Unity.Mathematics;
using VAuto.Core.Components;
using Stunlock.Core;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Kill streak and trap system rules (static global access).
    /// - Tracks player kills on death events
    /// - Spawns containers with glow at waypoints
    /// - Container/waypoint traps with glow and notifications
    /// </summary>
    public static class KillStreakTrapRules
    {
        private static readonly Dictionary<ulong, int> _playerKills = new();
        private static readonly Dictionary<ulong, double> _lastKillTime = new();
        private static readonly Dictionary<int, TrapConfig> _waypoints = new();
        private static readonly object _initLock = new object();
        private static bool _initialized;
        
        /// <summary>
        /// Configuration for trap system.
        /// </summary>
        public static TrapSystemConfig Config { get; private set; }
        
        #region Initialization
        
        /// <summary>
        /// Initialize the kill streak trap rules.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;
                
                Plugin.Log.LogInfo("[KillStreakTrapRules] Initializing...");
                LoadConfig();
                CreateDefaultConfig();
                SetupWaypoints();
                _initialized = true;
                Plugin.Log.LogInfo("[KillStreakTrapRules] Initialized successfully");
            }
        }
        
        private static void LoadConfig()
        {
            var configPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "killstreak_trap_config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    Config = JsonSerializer.Deserialize<TrapSystemConfig>(json) ?? new TrapSystemConfig();
                    Plugin.Log.LogInfo("[KillStreakTrapRules] Config loaded");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[KillStreakTrapRules] Config load failed: {ex.Message}");
                    Config = new TrapSystemConfig();
                }
            }
            else
            {
                Config = new TrapSystemConfig();
            }
        }
        
        private static void CreateDefaultConfig()
        {
            var configPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "killstreak_trap_config.json");
            var dir = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            var defaultConfig = new TrapSystemConfig
            {
                KillThreshold = 5,
                ChestsPerSpawn = 2,
                ContainerGlowColor = new float3(1f, 0.5f, 0f),
                ContainerGlowRadius = 5f,
                ContainerPrefabGuid = new PrefabGUID(45), // Level 15 chest
                WaypointTrapGlowColor = new float3(1f, 0f, 0f),
                WaypointTrapGlowRadius = 8f,
                WaypointTrapThreshold = 10,
                NotificationEnabled = true,
                TrapDamageAmount = 50f,
                TrapDuration = 30f
            };
            
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, options));
                Config = defaultConfig;
                Plugin.Log.LogInfo("[KillStreakTrapRules] Default config created");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[KillStreakTrapRules] Config write failed: {ex.Message}");
            }
        }
        
        private static void SetupWaypoints()
        {
            // Default waypoints at castle locations
            _waypoints.Clear();
            _waypoints[0] = new TrapConfig { Position = new float3(0, 0, 0), Name = "Farbane Waypoint" };
            _waypoints[1] = new TrapConfig { Position = new float3(500, 0, 500), Name = "Dunley Farmlands" };
            _waypoints[2] = new TrapConfig { Position = new float3(-500, 0, 500), Name = "Silverlight Hills" };
            _waypoints[3] = new TrapConfig { Position = new float3(1000, 0, 0), Name = "The Iron Veins" };
            _waypoints[4] = new TrapConfig { Position = new float3(-1000, 0, -500), Name = "Cursed Forest" };
            
            Plugin.Log.LogInfo($"[KillStreakTrapRules] { _waypoints.Count} waypoints configured");
        }
        
        #endregion
        
        #region Kill Streak Tracking
        
        /// <summary>
        /// Process a player death event. Call this from DeathEvent or DamageEvent.
        /// </summary>
        public static void OnPlayerDeath(ulong killerId, ulong victimId)
        {
            if (killerId == 0 || killerId == victimId) return;
            
            var now = DateTime.UtcNow.ToOADate();
            
            if (!_playerKills.TryGetValue(killerId, out var currentKills))
            {
                currentKills = 0;
            }
            currentKills++;
            _playerKills[killerId] = currentKills;
            _lastKillTime[killerId] = now;
            
            Plugin.Log.LogInfo($"[KillStreak] Player {killerId} streak: {currentKills}");
            
            // Check chest spawn threshold
            if (currentKills == Config.KillThreshold)
            {
                SpawnChestsForPlayer(killerId);
            }
            else if (currentKills > Config.KillThreshold && currentKills % Config.KillThreshold == 0)
            {
                SpawnChestsForPlayer(killerId);
            }
        }
        
        /// <summary>
        /// Get a player's current kill streak.
        /// </summary>
        public static int GetKillStreak(ulong playerId)
        {
            return _playerKills.TryGetValue(playerId, out var kills) ? kills : 0;
        }
        
        /// <summary>
        /// Reset a player's kill streak (when they die).
        /// </summary>
        public static void ResetStreak(ulong playerId)
        {
            _playerKills[playerId] = 0;
            Plugin.Log.LogInfo($"[KillStreak] Player {playerId} streak reset on death");
        }
        
        #endregion
        
        #region Chest/Container Spawning
        
        /// <summary>
        /// Spawn containers at random waypoints for a player.
        /// </summary>
        public static void SpawnChestsForPlayer(ulong playerId)
        {
            var kills = GetKillStreak(playerId);
            var chestsToSpawn = Config.ChestsPerSpawn;
            
            Plugin.Log.LogInfo($"[KillStreak] Spawning {chestsToSpawn} containers for player {playerId} (streak: {kills})");
            
            var waypointKeys = new List<int>(_waypoints.Keys);
            var random = new Random();
            
            for (int i = 0; i < chestsToSpawn && i < waypointKeys.Count; i++)
            {
                var waypointIndex = waypointKeys[random.Next(waypointKeys.Count)];
                SpawnContainerAtWaypoint(playerId, waypointIndex);
            }
        }
        
        /// <summary>
        /// Spawn a container at a specific waypoint.
        /// </summary>
        public static void SpawnContainerAtWaypoint(ulong ownerId, int waypointIndex)
        {
            if (!_waypoints.TryGetValue(waypointIndex, out var waypoint))
            {
                Plugin.Log.LogWarning($"[KillStreak] Waypoint {waypointIndex} not found");
                return;
            }
            
            Plugin.Log.LogInfo($"[KillStreak] Container spawned for player {ownerId} at waypoint {waypointIndex} ({waypoint.Name})");
            
            // TODO: Spawn actual container entity with:
            // - Container component with ownerId
            // - Glow zone component (Config.ContainerGlowColor, Config.ContainerGlowRadius)
            // - Interaction restriction (only owner or 5+ kills can open)
            
            // Log for now - actual spawning needs EntityManager access
            if (Config.NotificationEnabled)
            {
                NotifyPlayer(ownerId, $"Your containers are ready! Check waypoint {waypoint.Name}");
            }
        }
        
        /// <summary>
        /// Register a custom waypoint.
        /// </summary>
        public static void RegisterWaypoint(int index, float3 position, string name)
        {
            _waypoints[index] = new TrapConfig { Position = position, Name = name };
            Plugin.Log.LogInfo($"[KillStreak] Waypoint {index} registered: {name} at {position}");
        }
        
        #endregion
        
        #region Trap Management
        
        /// <summary>
        /// Check if a player can open a container.
        /// </summary>
        public static bool CanOpenContainer(ulong playerId, ulong containerOwnerId)
        {
            // Owner can always open
            if (playerId == containerOwnerId) return true;
            
            // Players with 5+ kill streak can open others' containers
            return GetKillStreak(playerId) >= Config.KillThreshold;
        }
        
        /// <summary>
        /// Trigger a trap at a location.
        /// </summary>
        public static void TriggerTrap(ulong ownerId, float3 position, string trapType)
        {
            Plugin.Log.LogInfo($"[KillStreak] {trapType} trap triggered by owner {ownerId} at {position}");
            
            if (Config.NotificationEnabled)
            {
                NotifyPlayer(ownerId, $"Your {trapType} trap was triggered!");
            }
        }
        
        /// <summary>
        /// Check waypoint trap threshold (10 kills).
        /// </summary>
        public static bool CanUseWaypointTrap(ulong playerId)
        {
            return GetKillStreak(playerId) >= Config.WaypointTrapThreshold;
        }
        
        #endregion
        
        #region Player Notifications
        
        private static void NotifyPlayer(ulong playerId, string message)
        {
            // TODO: Use V Rising chat/notification system
            Plugin.Log.LogInfo($"[KillStreak][To:{playerId}] {message}");
        }
        
        #endregion
        
        #region Stats and Debug
        
        /// <summary>
        /// Get all player kill streaks (for admin commands).
        /// </summary>
        public static Dictionary<ulong, int> GetAllStreaks()
        {
            return new Dictionary<ulong, int>(_playerKills);
        }
        
        /// <summary>
        /// Get all registered waypoints.
        /// </summary>
        public static Dictionary<int, TrapConfig> GetWaypoints()
        {
            return new Dictionary<int, TrapConfig>(_waypoints);
        }
        
        /// <summary>
        /// Clear all data (for testing/reset).
        /// </summary>
        public static void ClearAll()
        {
            _playerKills.Clear();
            _lastKillTime.Clear();
            Plugin.Log.LogInfo("[KillStreak] All data cleared");
        }
        
        #endregion
    }
    
    #region Configuration Classes
    
    /// <summary>
    /// Main trap system configuration.
    /// </summary>
    public class TrapSystemConfig
    {
        public int KillThreshold { get; set; } = 5;
        public int ChestsPerSpawn { get; set; } = 2;
        public float3 ContainerGlowColor { get; set; }
        public float ContainerGlowRadius { get; set; } = 5f;
        public PrefabGUID ContainerPrefabGuid { get; set; }
        public float3 WaypointTrapGlowColor { get; set; }
        public float WaypointTrapGlowRadius { get; set; } = 8f;
        public int WaypointTrapThreshold { get; set; } = 10;
        public bool NotificationEnabled { get; set; } = true;
        public float TrapDamageAmount { get; set; } = 50f;
        public float TrapDuration { get; set; } = 30f;
    }
    
    /// <summary>
    /// Waypoint/trap configuration.
    /// </summary>
    public class TrapConfig
    {
        public float3 Position { get; set; }
        public string Name { get; set; } = "";
        public PrefabGUID? PrefabGUID { get; set; }
        public bool IsActive { get; set; } = true;
    }
    
    #endregion
}
