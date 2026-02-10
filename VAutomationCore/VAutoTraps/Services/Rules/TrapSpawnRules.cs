using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;
using VAuto.Extensions;
using VAutoTraps;
using VAuto.Core.Logging;
using VAuto.Core.Config;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Trap spawn and kill streak rules (static global access).
    /// - Tracks player kills on death events
    /// - Spawns containers with glow at waypoints
    /// - Container/waypoint traps with glow and notifications
    /// </summary>
    public static class TrapSpawnRules
    {
        private static readonly Dictionary<ulong, int> _playerKills = new();
        private static readonly Dictionary<ulong, double> _lastKillTime = new();
        private static readonly Dictionary<int, TrapConfig> _waypoints = new();
        private static readonly object _initLock = new object();
        private static bool _initialized;
        private static CoreLogger _log;
        
        /// <summary>
        /// Configuration for trap system.
        /// </summary>
        public static TrapSystemConfig Config { get; private set; }
        
        #region Initialization
        
        /// <summary>
        /// Initialize the trap spawn rules.
        /// </summary>
        public static void Initialize(CoreLogger log)
        {
            lock (_initLock)
            {
                if (_initialized) return;
                _log = log;
                _log.LogInfo("[TrapSpawnRules] Initializing...");
                LoadConfig();
                CreateDefaultConfig();
                SetupWaypoints();
                _initialized = true;
                _log.LogInfo("[TrapSpawnRules] Initialized successfully");
            }
        }
        
        private static void LoadConfig()
        {
            var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto");
            var tomlPath = Path.Combine(configDir, "killstreak_trap_config.toml");
            var jsonPath = Path.Combine(configDir, "killstreak_trap_config.json");

            if (File.Exists(tomlPath))
            {
                if (TryLoadTomlConfig(tomlPath, out var cfgFromToml))
                {
                    Config = cfgFromToml;
                    _log.LogInfo("[TrapSpawnRules] TOML config loaded");
                    return;
                }
            }

            if (File.Exists(jsonPath))
            {
                try
                {
                    var json = File.ReadAllText(jsonPath);
                    var options = new JsonSerializerOptions { Converters = { new Float3Converter() } };
                    Config = JsonSerializer.Deserialize<TrapSystemConfig>(json, options) ?? new TrapSystemConfig();
                    _log.LogInfo("[TrapSpawnRules] Config loaded");
                    TryMigrateJsonToToml(jsonPath, tomlPath, Config);
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"[TrapSpawnRules] Config load failed: {ex.Message}");
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
            var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto");
            var tomlPath = Path.Combine(configDir, "killstreak_trap_config.toml");
            var jsonPath = Path.Combine(configDir, "killstreak_trap_config.json");
            var dir = configDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            var defaultConfig = new TrapSystemConfig
            {
                KillThreshold = 5,
                ChestsPerSpawn = 2,
                ContainerGlowColor = new float3(1f, 0.5f, 0f),
                ContainerGlowRadius = 5f,
                ContainerPrefabId = 45, // Level 15 chest - using int instead of PrefabGuid
                WaypointTrapGlowColor = new float3(1f, 0f, 0f),
                WaypointTrapGlowRadius = 8f,
                WaypointTrapThreshold = 10,
                NotificationEnabled = true,
                TrapDamageAmount = 50f,
                TrapDuration = 30f
            };
            
            try
            {
                if (!File.Exists(tomlPath))
                {
                    var toml = SimpleToml.SerializeTrapConfig(defaultConfig);
                    File.WriteAllText(tomlPath, toml);
                }

                if (!File.Exists(jsonPath))
                {
                    var options = new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Converters = { new Float3Converter() }
                    };
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(defaultConfig, options));
                }

                Config = defaultConfig;
                _log.LogInfo("[TrapSpawnRules] Default config created");
            }
            catch (Exception ex)
            {
                _log.LogError($"[TrapSpawnRules] Config write failed: {ex.Message}");
            }
        }

        private static bool TryLoadTomlConfig(string tomlPath, out TrapSystemConfig cfg)
        {
            cfg = null;
            try
            {
                var toml = File.ReadAllText(tomlPath);
                var parsed = SimpleToml.Parse(toml);
                var core = parsed.TryGetValue("core", out var coreObj) && coreObj is Dictionary<string, object> coreDict
                    ? coreDict
                    : parsed;

                var c = new TrapSystemConfig();
                if (core.TryGetValue("killThreshold", out var killThreshold)) c.KillThreshold = ToInt(killThreshold);
                if (core.TryGetValue("chestsPerSpawn", out var chestsPerSpawn)) c.ChestsPerSpawn = ToInt(chestsPerSpawn);
                if (core.TryGetValue("containerGlowRadius", out var containerGlowRadius)) c.ContainerGlowRadius = ToFloat(containerGlowRadius);
                if (core.TryGetValue("containerPrefabId", out var containerPrefabId)) c.ContainerPrefabId = ToInt(containerPrefabId);
                if (core.TryGetValue("waypointTrapThreshold", out var waypointTrapThreshold)) c.WaypointTrapThreshold = ToInt(waypointTrapThreshold);
                if (core.TryGetValue("waypointTrapGlowRadius", out var waypointTrapGlowRadius)) c.WaypointTrapGlowRadius = ToFloat(waypointTrapGlowRadius);
                if (core.TryGetValue("notificationEnabled", out var notificationEnabled)) c.NotificationEnabled = ToBool(notificationEnabled);
                if (core.TryGetValue("trapDamageAmount", out var trapDamageAmount)) c.TrapDamageAmount = ToFloat(trapDamageAmount);
                if (core.TryGetValue("trapDuration", out var trapDuration)) c.TrapDuration = ToFloat(trapDuration);

                if (core.TryGetValue("containerGlowColor", out var containerGlowColor) && containerGlowColor is object[] cArr && cArr.Length == 3)
                {
                    c.ContainerGlowColor = new float3(ToFloat(cArr[0]), ToFloat(cArr[1]), ToFloat(cArr[2]));
                }

                if (core.TryGetValue("waypointTrapGlowColor", out var waypointGlowColor) && waypointGlowColor is object[] wArr && wArr.Length == 3)
                {
                    c.WaypointTrapGlowColor = new float3(ToFloat(wArr[0]), ToFloat(wArr[1]), ToFloat(wArr[2]));
                }

                cfg = c;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryMigrateJsonToToml(string jsonPath, string tomlPath, TrapSystemConfig cfg)
        {
            try
            {
                if (File.Exists(tomlPath)) return;
                var dir = Path.GetDirectoryName(tomlPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var toml = SimpleToml.SerializeTrapConfig(cfg);
                File.WriteAllText(tomlPath, toml);
                _log.LogInfo("[TrapSpawnRules] Migrated JSON to TOML");
            }
            catch
            {
            }
        }

        private static int ToInt(object v)
        {
            return v switch
            {
                int i => i,
                long l => (int)l,
                float f => (int)f,
                double d => (int)d,
                _ => Convert.ToInt32(v)
            };
        }

        private static float ToFloat(object v)
        {
            return v switch
            {
                float f => f,
                double d => (float)d,
                int i => i,
                long l => l,
                _ => Convert.ToSingle(v)
            };
        }

        private static bool ToBool(object v)
        {
            return v switch
            {
                bool b => b,
                string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
                int i => i != 0,
                _ => Convert.ToBoolean(v)
            };
        }
        
        private static void SetupWaypoints()
        {
            _waypoints.Clear();
            _waypoints[0] = new TrapConfig { Position = new float3(0, 0, 0), Name = "Farbane Waypoint" };
            _waypoints[1] = new TrapConfig { Position = new float3(500, 0, 500), Name = "Dunley Farmlands" };
            _waypoints[2] = new TrapConfig { Position = new float3(-500, 0, 500), Name = "Silverlight Hills" };
            _waypoints[3] = new TrapConfig { Position = new float3(1000, 0, 0), Name = "The Iron Veins" };
            _waypoints[4] = new TrapConfig { Position = new float3(-1000, 0, -500), Name = "Cursed Forest" };
            
            _log.LogInfo($"[TrapSpawnRules] { _waypoints.Count} waypoints configured");
        }
        
        #endregion
        
        #region Kill Streak Tracking
        
        /// <summary>
        /// Process a player death event. Call this from DeathEvent.
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
            
            _log.LogInfo($"[TrapSpawnRules] Player {killerId} streak: {currentKills}");
            
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
            _log.LogInfo($"[TrapSpawnRules] Player {playerId} streak reset on death");
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
            
            _log.LogInfo($"[TrapSpawnRules] Spawning {chestsToSpawn} containers for player {playerId} (streak: {kills})");
            
            var waypointKeys = new List<int>(_waypoints.Keys);
            var random = new System.Random();
            
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
                _log.LogWarning($"[TrapSpawnRules] Waypoint {waypointIndex} not found");
                return;
            }
            
            _log.LogInfo($"[TrapSpawnRules] Container spawned for player {ownerId} at waypoint {waypointIndex} ({waypoint.Name})");
            _log.LogInfo($"[TrapSpawnRules] - Position: {waypoint.Position}");
            _log.LogInfo($"[TrapSpawnRules] - Glow Color: {Config.ContainerGlowColor}, Radius: {Config.ContainerGlowRadius}");
            _log.LogInfo($"[TrapSpawnRules] - Prefab ID: {Config.ContainerPrefabId}");
            
            // TODO: Spawn actual container entity using EntityManager
            // Use Config.ContainerPrefabId with EntityManager.CreateEntity(Archetype)
            
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
            _log.LogInfo($"[TrapSpawnRules] Waypoint {index} registered: {name} at {position}");
        }
        
        #endregion
        
        #region Trap Management
        
        /// <summary>
        /// Check if a player can open a container.
        /// </summary>
        public static bool CanOpenContainer(ulong playerId, ulong containerOwnerId)
        {
            if (playerId == containerOwnerId) return true;
            return GetKillStreak(playerId) >= Config.KillThreshold;
        }
        
        /// <summary>
        /// Trigger a trap at a location.
        /// </summary>
        public static void TriggerTrap(ulong ownerId, float3 position, string trapType)
        {
            _log.LogInfo($"[TrapSpawnRules] {trapType} trap triggered by owner {ownerId} at {position}");
            
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
            _log.LogInfo($"[TrapSpawnRules][To:{playerId}] {message}");
            // TODO: Use ProjectM.UserNameService or chat system for actual notifications
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
            _log.LogInfo("[TrapSpawnRules] All data cleared");
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
        public int ContainerPrefabId { get; set; } = 45; // Using int instead of PrefabGuid for simpler config
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
        public int? PrefabId { get; set; }
        public bool IsActive { get; set; } = true;
    }
    
    #endregion
}
