using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using ProjectM;
using ProjectM.Network;
using VAuto.Zone.Commands;
using VAuto.Zone.Services;
using VAuto.Zone.Core;
using VAutomationCore.Core;
using VAutomationCore.Core.Services;
using VAutomationCore.Core.Logging;

namespace VAuto.Zone
{
    [BepInPlugin("gg.coyote.VAutomationZone", "VAutoZone", "1.0.0")]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.0")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInDependency("gg.coyote.lifecycle", "1.0.0")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        #region Logging
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource("VAutoZone");
        public static ManualLogSource Logger => _staticLog;
        public static CoreLogger CoreLog { get; private set; }
        #endregion

        public static Plugin Instance { get; private set; }
        
        private Harmony _harmony;

        #region CFG Configuration Entries
        // General
        public static ConfigEntry<bool> GeneralEnabled;
        public static ConfigEntry<string> LogLevel;
        
        // Zone Detection
        public static ConfigEntry<int> ZoneDetectionCheckIntervalMs;
        public static ConfigEntry<float> ZoneDetectionPositionThreshold;
        public static ConfigEntry<float> MapIconSpawnRefreshIntervalSeconds;
        public static ConfigEntry<bool> ZoneDetectionDebugMode;
        
        // Glow System
        public static ConfigEntry<bool> GlowSystemEnabled;
        public static ConfigEntry<float> GlowSystemUpdateInterval;
        public static ConfigEntry<bool> GlowSystemShowDebugInfo;
        
        // Arena Territory
        public static ConfigEntry<bool> ArenaTerritoryEnabled;
        public static ConfigEntry<bool> ArenaTerritoryShowGrid;
        public static ConfigEntry<float> ArenaTerritoryGridCellSize;
        
        // Integration
        public static ConfigEntry<bool> IntegrationLifecycleEnabled;
        public static ConfigEntry<bool> IntegrationSendZoneEvents;
        public static ConfigEntry<bool> IntegrationAllowTrapOverrides;
        
        // Debug
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<bool> HotReloadEnabled;
        #endregion

        #region JSON Configuration
        private static string _configPath;
        private static ZoneJsonConfig _jsonConfig;
        private static DateTime _lastConfigCheck;
        private static System.Timers.Timer _hotReloadTimer;
        #endregion

        public Plugin()
        {
            Instance = this;
        }

        public override void Load()
        {
            try
            {
                // Initialize configuration path
                _configPath = Path.Combine(Paths.ConfigPath, "VAuto.ZoneLifecycle.json");

                // Bind CFG configuration
                BindConfiguration();

                // Load JSON configuration
                LoadJsonConfiguration();

                // Check if enabled
                if (GeneralEnabled != null && !GeneralEnabled.Value)
                {
                    Logger.LogInfo("[VAutoZone] Disabled via config.");
                    return;
                }

                _harmony = new Harmony("gg.coyote.VAutomationZone");
                _harmony.PatchAll(typeof(Patches));
                
                // Initialize CoreLogger and services
                CoreLog = new CoreLogger("VAutoZone");
                
                // Initialize all services using ServiceInitializer
                var servicesInitialized = ServiceInitializer.InitializeAll(CoreLog);
                if (!servicesInitialized)
                {
                    Logger.LogWarning("[VAutoZone] Some services failed to initialize");
                }

                // Initialize arena territory
                try
                {
                    ArenaTerritory.InitializeArenaGrid();
                    Logger.LogInfo("Arena territory initialized");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Territory init failed: {ex.Message}");
                }

                // Initialize ZoneEventBridge (now static)
                try
                {
                    ZoneEventBridge.Initialize();
                    Logger.LogInfo("ZoneEventBridge initialized");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"ZoneEventBridge init failed: {ex.Message}");
                }

                // Register commands with assembly for VCF auto-discovery
                CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());

                // Start hot-reload monitoring if enabled
                if (HotReloadEnabled?.Value == true)
                {
                    StartHotReloadMonitoring();
                }
                
                Logger.LogInfo("VAutoZone loaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private void BindConfiguration()
        {
            var configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAutoZone.cfg"), true);

            // General
            GeneralEnabled = configFile.Bind("General", "Enabled", true, "Enable or disable VAutoZone plugin");
            LogLevel = configFile.Bind("General", "LogLevel", "Info", "Log level (Debug, Info, Warning, Error)");

            // Zone Detection
            ZoneDetectionCheckIntervalMs = configFile.Bind("ZoneDetection", "CheckIntervalMs", 100, "Interval for checking zone transitions (milliseconds)");
            ZoneDetectionPositionThreshold = configFile.Bind("ZoneDetection", "PositionChangeThreshold", 1.0f, "Minimum position change to trigger zone check (units)");
            MapIconSpawnRefreshIntervalSeconds = configFile.Bind("ZoneDetection", "MapIconSpawnRefreshIntervalSeconds", 10.0f, "Interval for refreshing map icon spawns (seconds)");
            ZoneDetectionDebugMode = configFile.Bind("ZoneDetection", "DebugMode", false, "Enable zone detection debug logging");

            // Glow System
            GlowSystemEnabled = configFile.Bind("GlowSystem", "Enabled", true, "Enable the zone glow system");
            GlowSystemUpdateInterval = configFile.Bind("GlowSystem", "UpdateInterval", 0.5f, "Update interval for glow effects (seconds)");
            GlowSystemShowDebugInfo = configFile.Bind("GlowSystem", "ShowDebugInfo", false, "Show debug information for glow zones");

            // Arena Territory
            ArenaTerritoryEnabled = configFile.Bind("ArenaTerritory", "Enabled", true, "Enable arena territory management");
            ArenaTerritoryShowGrid = configFile.Bind("ArenaTerritory", "ShowGrid", false, "Show arena grid debug visualization");
            ArenaTerritoryGridCellSize = configFile.Bind("ArenaTerritory", "GridCellSize", 100.0f, "Size of arena grid cells (units)");

            // Integration
            IntegrationLifecycleEnabled = configFile.Bind("Integration", "LifecycleEnabled", true, "Allow zone system to trigger lifecycle events");
            IntegrationSendZoneEvents = configFile.Bind("Integration", "SendZoneEvents", true, "Allow zone system to send events to other modules");
            IntegrationAllowTrapOverrides = configFile.Bind("Integration", "AllowTrapOverrides", true, "Allow trap system to override zone behaviors");

            // Debug
            DebugMode = configFile.Bind("Debug", "DebugMode", false, "Enable debug mode");
            HotReloadEnabled = configFile.Bind("Debug", "HotReload", true, "Enable hot-reload of configuration");
        }

        private void LoadJsonConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var jsonContent = File.ReadAllText(_configPath);
                    _jsonConfig = JsonSerializer.Deserialize<ZoneJsonConfig>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                    Logger.LogInfo($"[VAutoZone] Loaded JSON configuration from {_configPath}");
                }
                else
                {
                    _jsonConfig = new ZoneJsonConfig();
                    SaveJsonConfiguration();
                    Logger.LogInfo($"[VAutoZone] Created new JSON configuration at {_configPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] Failed to load JSON configuration: {ex.Message}");
                _jsonConfig = new ZoneJsonConfig();
            }
        }

        private void SaveJsonConfiguration()
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(_jsonConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(_configPath, jsonContent);
                Logger.LogInfo($"[VAutoZone] Saved JSON configuration to {_configPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] Failed to save JSON configuration: {ex.Message}");
            }
        }

        private void StartHotReloadMonitoring()
        {
            _lastConfigCheck = DateTime.UtcNow;
            _hotReloadTimer = new System.Timers.Timer(5000);
            _hotReloadTimer.Elapsed += (_, _) => CheckForConfigChanges();
            _hotReloadTimer.Start();
            Logger.LogInfo("[VAutoZone] Hot-reload monitoring started.");
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
                        Logger.LogInfo("[VAutoZone] Configuration hot-reloaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] Error checking configuration changes: {ex.Message}");
            }
        }

        #region Public Configuration Accessors
        public static bool IsEnabled => GeneralEnabled?.Value ?? true;
        public static int CheckIntervalMs => ZoneDetectionCheckIntervalMs?.Value ?? 100;
        public static float PositionChangeThreshold => ZoneDetectionPositionThreshold?.Value ?? 1.0f;
        public static float MapIconSpawnRefreshIntervalSecondsValue => MapIconSpawnRefreshIntervalSeconds?.Value ?? 10.0f;
        public static bool ZoneDetectionDebug => ZoneDetectionDebugMode?.Value ?? false;
        public static bool GlowSystemEnabledValue => GlowSystemEnabled?.Value ?? true;
        public static float GlowSystemUpdateIntervalValue => GlowSystemUpdateInterval?.Value ?? 0.5f;
        public static bool GlowSystemShowDebugInfoValue => GlowSystemShowDebugInfo?.Value ?? false;
        public static bool ArenaTerritoryEnabledValue => ArenaTerritoryEnabled?.Value ?? true;
        public static bool ArenaTerritoryShowGridValue => ArenaTerritoryShowGrid?.Value ?? false;
        public static float ArenaTerritoryGridCellSizeValue => ArenaTerritoryGridCellSize?.Value ?? 100.0f;
        public static bool IntegrationLifecycleEnabledValue => IntegrationLifecycleEnabled?.Value ?? true;
        public static bool IntegrationSendZoneEventsValue => IntegrationSendZoneEvents?.Value ?? true;
        public static bool IntegrationAllowTrapOverridesValue => IntegrationAllowTrapOverrides?.Value ?? true;
        public static bool DebugModeEnabled => DebugMode?.Value ?? false;
        #endregion

        public void OnDestroy()
        {
            _hotReloadTimer?.Dispose();
            _hotReloadTimer = null;
            _harmony?.UnpatchSelf();
            Logger.LogInfo("VAutoZone unloaded");
        }
    }

    #region JSON Configuration Classes
    public class ZoneJsonConfig
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalMs { get; set; } = 100;
        public float PositionChangeThreshold { get; set; } = 1.0f;
        public ZoneMappings Mappings { get; set; } = new();
    }

    public class ZoneMappings
    {
        public ZoneMapping ArenaMain { get; set; } = new();
        public ZoneMapping ArenaPvp1 { get; set; } = new();
        public ZoneMapping Default { get; set; } = new();
    }

    public class ZoneMapping
    {
        public string[] OnEnter { get; set; } = new string[] { "store" };
        public string[] OnExit { get; set; } = new string[] { "message" };
        public bool UseGlobalDefaults { get; set; } = false;
        public string MapIconChangePrefab { get; set; } = "";
    }
    #endregion

    public static class Patches
    {
    }
}
