using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using Unity.Mathematics;
using VAuto;
using VAuto.Core.Lifecycle;
using VLifecycle.Services.Lifecycle;

namespace VLifecycle
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
        public static Plugin Instance { get; private set; }
        #endregion

        #region CFG Configuration Entries
        // General
        public static ConfigEntry<bool> GeneralEnabled;
        public static ConfigEntry<string> LogLevel;
        
        // Arena Lifecycle
        public static ConfigEntry<bool> ArenaSaveInventory;
        public static ConfigEntry<bool> ArenaRestoreInventory;
        public static ConfigEntry<bool> ArenaSaveBuffs;
        public static ConfigEntry<bool> ArenaRestoreBuffs;
        public static ConfigEntry<bool> ArenaClearBuffsOnExit;
        public static ConfigEntry<bool> ArenaResetAbilityCooldowns;
        public static ConfigEntry<bool> ArenaResetCooldownsOnExit;
        
        // Player State
        public static ConfigEntry<bool> PlayerSaveEquipment;
        public static ConfigEntry<bool> PlayerSaveBlood;
        public static ConfigEntry<bool> PlayerSaveSpells;
        public static ConfigEntry<bool> PlayerSaveHealth;
        public static ConfigEntry<bool> PlayerRestoreHealth;
        

        // Respawn
        public static ConfigEntry<bool> RespawnForceArenaRespawn;
        public static ConfigEntry<bool> RespawnTeleportToSpawn;
        public static ConfigEntry<bool> RespawnClearDebuffs;
        public static ConfigEntry<int> RespawnTeleportDelayMs;
        
        // Transitions
        public static ConfigEntry<int> TransitionsEnterDelayMs;
        public static ConfigEntry<int> TransitionsExitDelayMs;
        public static ConfigEntry<bool> TransitionsLockMovement;
        public static ConfigEntry<bool> TransitionsShowMessages;
        
        // Safety
        public static ConfigEntry<bool> SafetyRestoreOnError;
        public static ConfigEntry<bool> SafetyBlockEntryOnSaveFailure;
        public static ConfigEntry<bool> SafetyVerboseLogging;
        
        // Integration
        public static ConfigEntry<bool> IntegrationZoneTriggersLifecycle;
        public static ConfigEntry<bool> IntegrationAllowTrapOverrides;
        public static ConfigEntry<bool> IntegrationSendEvents;
        
        // Debug
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<bool> HotReloadEnabled;
        #endregion

        #region JSON Configuration
        private static string _configPath;
        private static LifecycleJsonConfig _jsonConfig;
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
                _configPath = Path.Combine(Paths.ConfigPath, "VAuto.Lifecycle.json");

                // Bind CFG configuration
                BindConfiguration();

                // Load JSON configuration
                LoadJsonConfiguration();

                // Check if enabled
                if (GeneralEnabled != null && !GeneralEnabled.Value)
                {
                    Log.LogInfo("[VLifecycle] Disabled via config.");
                    return;
                }

                // Initialize ArenaLifecycleManager
                ArenaLifecycleManager.Instance.Initialize();

                // Initialize connection event patches for spellbook management
                Services.Lifecycle.ConnectionEventPatches.Initialize();

                // Initialize ZUI input blocker for arena menus
                Services.Lifecycle.ZUIInputBlocker.Initialize();
                
                // Apply ZUI input blocking patches
                var harmony = new Harmony("gg.coyote.Vlifecycle.ZUI");
                harmony.PatchAll(typeof(Services.Lifecycle.InputSystemUpdatePatch));

                // Commands auto-register via Vampire Command Framework
                CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());

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
            var configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VLifecycle.cfg"), true);

            // General
            GeneralEnabled = configFile.Bind("General", "Enabled", true, "Enable or disable VLifecycle plugin");
            LogLevel = configFile.Bind("General", "LogLevel", "Info", "Log level (Debug, Info, Warning, Error)");

            // Arena Lifecycle
            ArenaSaveInventory = configFile.Bind("Arena", "SaveInventory", true, "Save player inventory when entering arena");
            ArenaRestoreInventory = configFile.Bind("Arena", "RestoreInventory", true, "Restore inventory when exiting arena");
            ArenaSaveBuffs = configFile.Bind("Arena", "SaveBuffs", true, "Save buffs before arena entry");
            ArenaRestoreBuffs = configFile.Bind("Arena", "RestoreBuffs", true, "Restore buffs after exit");
            ArenaClearBuffsOnExit = configFile.Bind("Arena", "ClearArenaBuffsOnExit", true, "Remove temporary arena buffs on exit");
            ArenaResetAbilityCooldowns = configFile.Bind("Arena", "ResetAbilityCooldowns", true, "Reset cooldowns when entering arena");
            ArenaResetCooldownsOnExit = configFile.Bind("Arena", "ResetCooldownsOnExit", true, "Reset cooldowns when exiting arena");

            // Player State
            PlayerSaveEquipment = configFile.Bind("PlayerState", "SaveEquipment", true, "Save equipped gear");
            PlayerSaveBlood = configFile.Bind("PlayerState", "SaveBlood", true, "Save blood type & quality");
            PlayerSaveSpells = configFile.Bind("PlayerState", "SaveSpells", true, "Save spell loadout");
            PlayerSaveHealth = configFile.Bind("PlayerState", "SaveHealth", true, "Save player health state");
            PlayerRestoreHealth = configFile.Bind("PlayerState", "RestoreHealth", true, "Restore health after arena exit");

            // Respawn
            RespawnForceArenaRespawn = configFile.Bind("Respawn", "ForceArenaRespawn", false, "Force arena respawn instead of normal game respawn");
            RespawnTeleportToSpawn = configFile.Bind("Respawn", "TeleportToArenaSpawn", true, "Teleport player to arena spawn after death");
            RespawnClearDebuffs = configFile.Bind("Respawn", "ClearTemporaryDebuffs", true, "Clear temporary combat debuffs after respawn");
            RespawnTeleportDelayMs = configFile.Bind("Respawn", "RespawnTeleportDelayMs", 1000, "Delay before respawn teleport (milliseconds)");

            // Transitions
            TransitionsEnterDelayMs = configFile.Bind("Transitions", "EnterDelayMs", 0, "Delay before lifecycle actions when entering arena");
            TransitionsExitDelayMs = configFile.Bind("Transitions", "ExitDelayMs", 0, "Delay before lifecycle actions when leaving arena");
            TransitionsLockMovement = configFile.Bind("Transitions", "LockMovementDuringTransition", false, "Lock player movement during transition");
            TransitionsShowMessages = configFile.Bind("Transitions", "ShowTransitionMessages", true, "Show transition system messages");

            // Safety
            SafetyRestoreOnError = configFile.Bind("Safety", "RestoreOnError", true, "Restore player state if lifecycle fails");
            SafetyBlockEntryOnSaveFailure = configFile.Bind("Safety", "BlockEntryOnSaveFailure", true, "Prevent arena entry if player state cannot be saved");
            SafetyVerboseLogging = configFile.Bind("Safety", "VerboseLogging", false, "Log detailed lifecycle state changes");

            // Integration
            IntegrationZoneTriggersLifecycle = configFile.Bind("Integration", "ZoneTriggersLifecycle", true, "Allow zone system to trigger lifecycle automatically");
            IntegrationAllowTrapOverrides = configFile.Bind("Integration", "AllowTrapOverrides", true, "Allow trap system to temporarily override lifecycle");
            IntegrationSendEvents = configFile.Bind("Integration", "SendLifecycleEvents", true, "Allow announcement module to broadcast lifecycle events");

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
                    _jsonConfig = JsonSerializer.Deserialize<LifecycleJsonConfig>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                    Log.LogInfo($"[VLifecycle] Loaded JSON configuration from {_configPath}");
                }
                else
                {
                    _jsonConfig = new LifecycleJsonConfig();
                    SaveJsonConfiguration();
                    Log.LogInfo($"[VLifecycle] Created new JSON configuration at {_configPath}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[VLifecycle] Failed to load JSON configuration: {ex.Message}");
                _jsonConfig = new LifecycleJsonConfig();
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
                Log.LogInfo($"[VLifecycle] Saved JSON configuration to {_configPath}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[VLifecycle] Failed to save JSON configuration: {ex.Message}");
            }
        }

        private void StartHotReloadMonitoring()
        {
            _lastConfigCheck = DateTime.UtcNow;
            _hotReloadTimer = new System.Timers.Timer(5000);
            _hotReloadTimer.Elapsed += (_, _) => CheckForConfigChanges();
            _hotReloadTimer.Start();
            Log.LogInfo("[VLifecycle] Hot-reload monitoring started.");
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
                        Log.LogInfo("[VLifecycle] Configuration hot-reloaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[VLifecycle] Error checking configuration changes: {ex.Message}");
            }
        }

        #region Public Configuration Accessors
        public static bool IsEnabled => GeneralEnabled?.Value ?? true;
        public static bool SaveInventory => ArenaSaveInventory?.Value ?? true;
        public static bool RestoreInventory => ArenaRestoreInventory?.Value ?? true;
        public static bool SaveBuffs => ArenaSaveBuffs?.Value ?? true;
        public static bool RestoreBuffs => ArenaRestoreBuffs?.Value ?? true;
        public static bool ClearBuffsOnExit => ArenaClearBuffsOnExit?.Value ?? true;
        public static bool ResetCooldownsOnEnter => ArenaResetAbilityCooldowns?.Value ?? true;
        public static bool ResetCooldownsOnExit => ArenaResetCooldownsOnExit?.Value ?? true;
        public static bool SaveEquipment => PlayerSaveEquipment?.Value ?? true;
        public static bool SaveBlood => PlayerSaveBlood?.Value ?? true;
        public static bool SaveSpells => PlayerSaveSpells?.Value ?? true;
        public static bool SaveHealth => PlayerSaveHealth?.Value ?? true;
        public static bool RestoreHealth => PlayerRestoreHealth?.Value ?? true;
        public static bool ForceArenaRespawn => RespawnForceArenaRespawn?.Value ?? false;
        public static bool TeleportToSpawnOnRespawn => RespawnTeleportToSpawn?.Value ?? true;
        public static bool ClearDebuffsOnRespawn => RespawnClearDebuffs?.Value ?? true;
        public static int RespawnDelayMs => RespawnTeleportDelayMs?.Value ?? 1000;
        public static int EnterDelayMs => TransitionsEnterDelayMs?.Value ?? 0;
        public static int ExitDelayMs => TransitionsExitDelayMs?.Value ?? 0;
        public static bool LockMovementDuringTransition => TransitionsLockMovement?.Value ?? false;
        public static bool ShowTransitionMessages => TransitionsShowMessages?.Value ?? true;
        public static bool RestoreOnError => SafetyRestoreOnError?.Value ?? true;
        public static bool BlockEntryOnSaveFailure => SafetyBlockEntryOnSaveFailure?.Value ?? true;
        public static bool VerboseLogging => SafetyVerboseLogging?.Value ?? false;
        public static bool ZoneTriggersLifecycle => IntegrationZoneTriggersLifecycle?.Value ?? true;
        public static bool AllowTrapOverrides => IntegrationAllowTrapOverrides?.Value ?? true;
        public static bool SendLifecycleEvents => IntegrationSendEvents?.Value ?? true;
        public static bool DebugModeEnabled => DebugMode?.Value ?? false;
        #endregion

        public override bool Unload()
        {
            try
            {
                _hotReloadTimer?.Dispose();
                _hotReloadTimer = null;
                ArenaLifecycleManager.Instance.Shutdown();
                Log.LogInfo("[VLifecycle] Unloaded.");
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
    public class LifecycleJsonConfig
    {
        public LifecycleConfigSection Lifecycle { get; set; } = new();
    }

    public class LifecycleConfigSection
    {
        public bool Enabled { get; set; } = true;
        public ArenaLifecycleConfig Arena { get; set; } = new();
        public PlayerStateConfig PlayerState { get; set; } = new();
        public RespawnConfig Respawn { get; set; } = new();
        public TransitionsConfig Transitions { get; set; } = new();
        public SafetyConfig Safety { get; set; } = new();
        public IntegrationConfig Integration { get; set; } = new();
    }

    public class ArenaLifecycleConfig
    {
        public bool SaveInventory { get; set; } = true;
        public bool RestoreInventory { get; set; } = true;
        public bool SaveBuffs { get; set; } = true;
        public bool RestoreBuffs { get; set; } = true;
        public bool ClearArenaBuffsOnExit { get; set; } = true;
        public bool ResetAbilityCooldowns { get; set; } = true;
        public bool ResetCooldownsOnExit { get; set; } = false;
    }

    public class PlayerStateConfig
    {
        public bool SaveEquipment { get; set; } = true;
        public bool SaveBlood { get; set; } = true;
        public bool SaveSpells { get; set; } = true;
        public bool SaveHealth { get; set; } = true;
        public bool RestoreHealth { get; set; } = true;
    }

    public class RespawnConfig
    {
        public bool ForceArenaRespawn { get; set; } = false;
        public bool TeleportToArenaSpawn { get; set; } = true;
        public bool ClearTemporaryDebuffs { get; set; } = true;
        public int RespawnTeleportDelayMs { get; set; } = 1000;
    }

    public class TransitionsConfig
    {
        public int EnterDelayMs { get; set; } = 0;
        public int ExitDelayMs { get; set; } = 0;
        public bool LockMovementDuringTransition { get; set; } = false;
        public bool ShowTransitionMessages { get; set; } = true;
    }

    public class SafetyConfig
    {
        public bool RestoreOnError { get; set; } = true;
        public bool BlockEntryOnSaveFailure { get; set; } = true;
        public bool VerboseLogging { get; set; } = false;
    }

    public class IntegrationConfig
    {
        public bool ZoneTriggersLifecycle { get; set; } = true;
        public bool AllowTrapOverrides { get; set; } = true;
        public bool SendLifecycleEvents { get; set; } = true;
    }
    #endregion
}
