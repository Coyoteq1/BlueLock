using System;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto.Core.Services;
using VAutoTraps;

namespace VAutoTraps
{
    [BepInPlugin(MyPluginInfo.GUID, MyPluginInfo.NAME, MyPluginInfo.VERSION)]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.0")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        #region Logging
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.NAME);
        public new static ManualLogSource Log => _staticLog;
        public static ManualLogSource Logger => _staticLog;
        #endregion

        #region Harmony
        private Harmony? _harmony;
        public static Plugin Instance { get; private set; }
        #endregion

        #region CFG Configuration Entries
        // General
        public static ConfigEntry<bool> GeneralEnabled;
        public static ConfigEntry<string> LogLevel;

        // Trap System
        public static ConfigEntry<bool> TrapSystemEnabled;
        public static ConfigEntry<bool> TrapDebugMode;
        public static ConfigEntry<int> TrapUpdateInterval;

        // Chest Spawns
        public static ConfigEntry<bool> ChestSpawnsEnabled;
        public static ConfigEntry<float> ChestSpawnRadius;
        public static ConfigEntry<int> ChestMaxCount;
        public static ConfigEntry<int> ChestSpawnInterval;
        public static ConfigEntry<string> ChestRewards;

        // Container Traps
        public static ConfigEntry<bool> ContainerTrapsEnabled;
        public static ConfigEntry<float> ContainerRadius;
        public static ConfigEntry<int> ContainerMaxCount;

        // Kill Streak Rewards
        public static ConfigEntry<bool> KillStreakEnabled;
        public static ConfigEntry<int> KillStreakThreshold;
        public static ConfigEntry<string> KillStreakRewardPrefab;

        // Zone Rules
        public static ConfigEntry<bool> AllowInsideZones;
        public static ConfigEntry<bool> AllowOutsideZones;

        // Debug
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<bool> HotReloadEnabled;
        #endregion

        #region JSON Configuration
        private static string _configPath;
        private static TrapsJsonConfig _jsonConfig;
        private static DateTime _lastConfigCheck;
        private static System.Timers.Timer _hotReloadTimer;
        #endregion

        public override void Load()
        {
            Instance = this;
            Log.LogInfo($"[{MyPluginInfo.NAME}] Loading v{MyPluginInfo.VERSION}...");

            try
            {
                // Initialize configuration path
                _configPath = Path.Combine(Paths.ConfigPath, "VAuto.Traps.json");

                // Bind CFG configuration
                BindConfiguration();

                // Load JSON configuration
                LoadJsonConfiguration();

                // Check if enabled
                if (GeneralEnabled != null && !GeneralEnabled.Value)
                {
                    Log.LogInfo("[VAutoTraps] Disabled via config.");
                    return;
                }

                // Initialize services
                TrapSpawnRules.Initialize();
                ContainerTrapService.Initialize();
                ChestSpawnService.Initialize();
                TrapZoneService.Initialize();

                // Register commands
                CommandRegistry.RegisterAll();

                // Start hot-reload monitoring if enabled
                if (HotReloadEnabled?.Value == true)
                {
                    StartHotReloadMonitoring();
                }

                Log.LogInfo($"[{MyPluginInfo.NAME}] Loaded successfully.");
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
            }
        }

        private void BindConfiguration()
        {
            var configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAuto.Traps.cfg"), true);

            // General
            GeneralEnabled = configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Traps plugin");
            LogLevel = configFile.Bind("General", "LogLevel", "Info", "Log level (Debug, Info, Warning, Error)");

            // Trap System
            TrapSystemEnabled = configFile.Bind("TrapSystem", "Enabled", true, "Enable trap system");
            TrapDebugMode = configFile.Bind("TrapSystem", "DebugMode", false, "Enable trap debug mode");
            TrapUpdateInterval = configFile.Bind("TrapSystem", "UpdateInterval", 5, "Trap update interval in seconds");

            // Chest Spawns
            ChestSpawnsEnabled = configFile.Bind("ChestSpawns", "Enabled", true, "Enable chest spawns");
            ChestSpawnRadius = configFile.Bind("ChestSpawns", "Radius", 30f, "Chest spawn radius");
            ChestMaxCount = configFile.Bind("ChestSpawns", "MaxCount", 10, "Maximum chest count");
            ChestSpawnInterval = configFile.Bind("ChestSpawns", "SpawnInterval", 60, "Spawn interval in seconds");
            ChestRewards = configFile.Bind("ChestSpawns", "Rewards", "relic,shard,gem", "Comma-separated reward types");

            // Container Traps
            ContainerTrapsEnabled = configFile.Bind("ContainerTraps", "Enabled", true, "Enable container traps");
            ContainerRadius = configFile.Bind("ContainerTraps", "Radius", 20f, "Container trap radius");
            ContainerMaxCount = configFile.Bind("ContainerTraps", "MaxCount", 5, "Maximum container trap count");

            // Kill Streak Rewards
            KillStreakEnabled = configFile.Bind("KillStreak", "Enabled", true, "Enable kill streak rewards");
            KillStreakThreshold = configFile.Bind("KillStreak", "Threshold", 5, "Kill streak threshold");
            KillStreakRewardPrefab = configFile.Bind("KillStreak", "RewardPrefab", "Inventory_Kill_Count_Ticket_01", "Reward prefab name");

            // Zone Rules
            AllowInsideZones = configFile.Bind("ZoneRules", "AllowInsideZones", true, "Allow traps inside zones");
            AllowOutsideZones = configFile.Bind("ZoneRules", "AllowOutsideZones", true, "Allow traps outside zones");

            // Debug
            DebugMode = configFile.Bind("Debug", "DebugMode", false, "Enable debug mode");
            HotReloadEnabled = configFile.Bind("Debug", "HotReload", true, "Enable hot-reload of configuration");
        }

        private void LoadJsonConfiguration()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (File.Exists(_configPath))
                {
                    var jsonContent = File.ReadAllText(_configPath);
                    _jsonConfig = JsonSerializer.Deserialize<TrapsJsonConfig>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    }) ?? new TrapsJsonConfig();
                    Log.LogInfo($"[VAutoTraps] Loaded JSON configuration from {_configPath}");
                }
                else
                {
                    _jsonConfig = new TrapsJsonConfig();
                    SaveJsonConfiguration();
                    Log.LogInfo($"[VAutoTraps] Created new JSON configuration at {_configPath}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[VAutoTraps] Failed to load JSON configuration: {ex.Message}");
                _jsonConfig = new TrapsJsonConfig();
            }
        }

        private void SaveJsonConfiguration()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var jsonContent = JsonSerializer.Serialize(_jsonConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                // Atomic write using temp file
                var tmpPath = _configPath + ".tmp";
                File.WriteAllText(tmpPath, jsonContent);
                File.Copy(tmpPath, _configPath, overwrite: true);
                File.Delete(tmpPath);
                
                Log.LogInfo($"[VAutoTraps] Saved JSON configuration to {_configPath}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[VAutoTraps] Failed to save JSON configuration: {ex.Message}");
            }
        }

        private void StartHotReloadMonitoring()
        {
            _lastConfigCheck = DateTime.UtcNow;
            _hotReloadTimer = new System.Timers.Timer(5000);
            _hotReloadTimer.Elapsed += (_, _) => CheckForConfigChanges();
            _hotReloadTimer.Start();
            Log.LogInfo("[VAutoTraps] Hot-reload monitoring started.");
        }

        private void CheckForConfigChanges()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var lastModified = File.GetLastWriteTime(_configPath);
                    if (lastModified > _lastConfigCheck)
                    {
                        _lastConfigCheck = lastModified;
                        LoadJsonConfiguration();
                        Log.LogInfo("[VAutoTraps] Configuration hot-reloaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[VAutoTraps] Error checking configuration changes: {ex.Message}");
            }
        }

        #region Public Configuration Accessors
        public static bool IsEnabled => GeneralEnabled?.Value ?? true;
        public static bool TrapSystemActive => TrapSystemEnabled?.Value ?? true;
        public static bool TrapDebugActive => TrapDebugMode?.Value ?? false;
        public static int TrapUpdateMs => (TrapUpdateInterval?.Value ?? 5) * 1000;
        public static bool ChestSpawnsActive => ChestSpawnsEnabled?.Value ?? true;
        public static float ChestSpawnRadiusMeters => ChestSpawnRadius?.Value ?? 30f;
        public static int ChestMaxActive => ChestMaxCount?.Value ?? 10;
        public static int ChestSpawnIntervalSeconds => ChestSpawnInterval?.Value ?? 60;
        public static bool ContainerTrapsActive => ContainerTrapsEnabled?.Value ?? true;
        public static float ContainerTrapRadius => ContainerRadius?.Value ?? 20f;
        public static int ContainerMaxActive => ContainerMaxCount?.Value ?? 5;
        public static bool KillStreakActive => KillStreakEnabled?.Value ?? true;
        public static int KillStreakMinKills => KillStreakThreshold?.Value ?? 5;
        public static string KillStreakReward => KillStreakRewardPrefab?.Value ?? "Inventory_Kill_Count_Ticket_01";
        public static bool AllowTrapsInsideZones => AllowInsideZones?.Value ?? true;
        public static bool AllowTrapsOutsideZones => AllowOutsideZones?.Value ?? true;
        public static bool DebugModeEnabled => DebugMode?.Value ?? false;
        #endregion

        public override bool Unload()
        {
            try
            {
                _harmony?.UnpatchSelf();
                _hotReloadTimer?.Dispose();
                _hotReloadTimer = null;
                Log.LogInfo("[VAutoTraps] Unloaded.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
                return false;
            }
        }
    }

    #region JSON Configuration Classes
    public class TrapsJsonConfig
    {
        public TrapsConfigSection Traps { get; set; } = new();
    }

    public class TrapsConfigSection
    {
        public bool Enabled { get; set; } = true;
        public TrapSystemConfig TrapSystem { get; set; } = new();
        public ChestSpawnsConfig ChestSpawns { get; set; } = new();
        public ContainerTrapsConfig ContainerTraps { get; set; } = new();
        public KillStreakConfig KillStreak { get; set; } = new();
        public ZoneRulesConfig ZoneRules { get; set; } = new();
    }

    public class TrapSystemConfig
    {
        public bool Enabled { get; set; } = true;
        public bool DebugMode { get; set; } = false;
        public int UpdateInterval { get; set; } = 5;
    }

    public class ChestSpawnsConfig
    {
        public bool Enabled { get; set; } = true;
        public float Radius { get; set; } = 30f;
        public int MaxCount { get; set; } = 10;
        public int SpawnInterval { get; set; } = 60;
        public string Rewards { get; set; } = "relic,shard,gem";
    }

    public class ContainerTrapsConfig
    {
        public bool Enabled { get; set; } = true;
        public float Radius { get; set; } = 20f;
        public int MaxCount { get; set; } = 5;
    }

    public class KillStreakConfig
    {
        public bool Enabled { get; set; } = true;
        public int Threshold { get; set; } = 5;
        public string RewardPrefab { get; set; } = "Inventory_Kill_Count_Ticket_01";
    }

    public class ZoneRulesConfig
    {
        public bool AllowInsideZones { get; set; } = true;
        public bool AllowOutsideZones { get; set; } = true;
    }
    #endregion
}
