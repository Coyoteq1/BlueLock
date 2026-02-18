using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using ProjectM.Shared;
using ProjectM.Gameplay.Systems;
using Unity.Collections;
using Stunlock.Core;
using Stunlock.Network;
using VAuto.Zone.Services;
using VAuto.Zone.Core;
using VAuto.Zone.Core.Components;
using VAuto.Zone.Models;
using VAutomationCore.Core;
using VAutomationCore.Core.Arena;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.Lifecycle;
using VAutomationCore.Core.Services;
using VAutomationCore.Core.Logging;

namespace VAuto.Zone
{
    [BepInPlugin("gg.coyote.BlueLock", "BlueLock", "1.0.0")]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.0")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInDependency("gg.coyote.lifecycle", "1.0.0")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        #region Logging
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource("BlueLock");
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
        public static ConfigEntry<double> ZoneDetectionEnterConfirmSeconds;
        public static ConfigEntry<double> ZoneDetectionExitConfirmSeconds;
        public static ConfigEntry<double> ZoneDetectionTransitionCooldownSeconds;
        
        // Glow System
        public static ConfigEntry<bool> GlowSystemEnabled;
        public static ConfigEntry<float> GlowSystemUpdateInterval;
        public static ConfigEntry<bool> GlowSystemShowDebugInfo;
        public static ConfigEntry<bool> GlowSystemAutoRotateEnabled;
        public static ConfigEntry<int> GlowSystemAutoRotateIntervalMinutes;
        
        // Arena Territory
        public static ConfigEntry<bool> ArenaTerritoryEnabled;
        public static ConfigEntry<bool> ArenaTerritoryShowGrid;
        public static ConfigEntry<float> ArenaTerritoryGridCellSize;
        
        // Integration
        public static ConfigEntry<bool> IntegrationLifecycleEnabled;
        public static ConfigEntry<bool> IntegrationSendZoneEvents;
        public static ConfigEntry<bool> IntegrationAllowTrapOverrides;
        public static ConfigEntry<bool> BuildingPlacementRestrictionsDisabled;
        
        // Kit Settings
        public static ConfigEntry<bool> KitAutoEquipEnabled;
        public static ConfigEntry<bool> KitRestoreOnExit;
        public static ConfigEntry<bool> KitBroadcastEquips;
        public static ConfigEntry<string> KitDefaultName;
        public static ConfigEntry<string> KitDefinitionsPath;

        // Sandbox Progression
        public static ConfigEntry<bool> SandboxProgressionEnabled;
        public static ConfigEntry<bool> SandboxProgressionDefaultZoneUnlockEnabled;
        public static ConfigEntry<bool> SandboxProgressionPersistSnapshots;
        public static ConfigEntry<string> SandboxProgressionSnapshotFilePath;
        public static ConfigEntry<bool> SandboxProgressionVerboseLogs;

        // Debug
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<bool> HotReloadEnabled;
        #endregion

        #region JSON Configuration
        private static string _configPath;
        private static string _zonesConfigPath;
        private static string _kitsConfigPath;
        private static ZoneJsonConfig _jsonConfig;
        private static DateTime _lastConfigCheck;
        private static DateTime _lastZonesConfigCheck;
        private static DateTime _lastKitsConfigCheck;
        private static DateTime _lastAbilityConfigCheck;
        private static DateTime _lastPrefabsRefCheck;
        private static DateTime _lastAbilityAliasCheck;
        private static System.Timers.Timer _hotReloadTimer;
        #endregion

        // Auto zone detection state (main-thread only, updated from Harmony OnUpdate patch)
        private static readonly Dictionary<Entity, string> _playerZoneStates = new();
        private static readonly Dictionary<Entity, PendingZoneTransition> _pendingZoneTransitions = new();
        private static readonly Dictionary<Entity, DateTime> _lastCommittedZoneTransitions = new();
        private static readonly Dictionary<string, List<Entity>> _zoneBorderEntities = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _zoneGlowAutoSpawnDisabled = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ulong, float3> _zoneReturnPositions = new();
        private static readonly Dictionary<ulong, PendingZoneTeleport> _pendingZoneEnterTeleports = new();
        private static readonly Dictionary<string, Dictionary<string, List<Entity>>> _zoneTemplateEntities = new(StringComparer.OrdinalIgnoreCase);
        private static readonly PluginZoneLifecycleStepRegistry _zoneLifecycleStepRegistry = new();

        private struct PendingZoneTeleport
        {
            public Entity Player;
            public string ZoneId;
            public float3 TargetPos;
            public int Attempts;
        }

        private sealed class PluginZoneLifecycleContext : IZoneLifecycleContext
        {
            public Entity Player { get; }
            public string ZoneId { get; }
            public EntityManager EntityManager { get; }

            public PluginZoneLifecycleContext(Entity player, string zoneId, EntityManager entityManager)
            {
                Player = player;
                ZoneId = zoneId ?? string.Empty;
                EntityManager = entityManager;
            }
        }

        private sealed class ActionTokenEnterStep : IZoneEnterStep
        {
            private readonly string _actionToken;

            public string Name => _actionToken;
            public int Order { get; }

            public ActionTokenEnterStep(string actionToken, int order)
            {
                _actionToken = actionToken ?? string.Empty;
                Order = order;
            }

            public void Execute(IZoneLifecycleContext context)
            {
                if (context is PluginZoneLifecycleContext pluginContext)
                {
                    ExecuteEnterLifecycleAction(_actionToken, pluginContext.Player, pluginContext.ZoneId, pluginContext.EntityManager);
                }
            }
        }

        private sealed class ActionTokenExitStep : IZoneExitStep
        {
            private readonly string _actionToken;

            public string Name => _actionToken;
            public int Order { get; }

            public ActionTokenExitStep(string actionToken, int order)
            {
                _actionToken = actionToken ?? string.Empty;
                Order = order;
            }

            public void Execute(IZoneLifecycleContext context)
            {
                if (context is PluginZoneLifecycleContext pluginContext)
                {
                    ExecuteExitLifecycleAction(_actionToken, pluginContext.Player, pluginContext.ZoneId, pluginContext.EntityManager);
                }
            }
        }

        private sealed class PluginZoneLifecycleStepRegistry : IZoneLifecycleStepRegistry
        {
            public IReadOnlyList<IZoneEnterStep> GetEnterSteps()
            {
                return BuildEnterSteps(DefaultEnterLifecycleActions);
            }

            public IReadOnlyList<IZoneExitStep> GetExitSteps()
            {
                return BuildExitSteps(DefaultExitLifecycleActions);
            }

            public IReadOnlyList<IZoneEnterStep> BuildEnterSteps(IEnumerable<string> actionTokens)
            {
                var steps = new List<IZoneEnterStep>();
                var order = 0;
                foreach (var token in actionTokens ?? Array.Empty<string>())
                {
                    steps.Add(new ActionTokenEnterStep(token, order++));
                }

                return steps;
            }

            public IReadOnlyList<IZoneExitStep> BuildExitSteps(IEnumerable<string> actionTokens)
            {
                var steps = new List<IZoneExitStep>();
                var order = 0;
                foreach (var token in actionTokens ?? Array.Empty<string>())
                {
                    steps.Add(new ActionTokenExitStep(token, order++));
                }

                return steps;
            }
        }

        private sealed class PendingZoneTransition
        {
            public string PreviousZoneId { get; set; } = string.Empty;
            public string CandidateZoneId { get; set; } = string.Empty;
            public DateTime FirstSeenUtc { get; set; }
        }

        private const double DefaultZoneEnterTransitionConfirmSeconds = 0.35d;
        private const double DefaultZoneExitTransitionConfirmSeconds = 0.75d;
        private const double DefaultZoneTransitionCooldownSeconds = 1.25d;
        private static int _glowRotationOffset;
        private static DateTime _nextGlowRotationUtc = DateTime.MinValue;
        private static Entity _lastKnownUserEntity = Entity.Null;
        private static bool _needsGlowBuffRebuildOnFirstUser;
        private static DateTime _lastAutoGlowRebuildUtc = DateTime.MinValue;
        private static DateTime _nextAllowedRebuildRequestUtc = DateTime.MinValue;
        private static DateTime _nextBorderReadinessLogUtc = DateTime.MinValue;
        private const int RebuildRequestCooldownSeconds = 5;
        private static readonly string[] LifecycleAssemblyNames = { "Cycleborn", "Vlifecycle" };
        private static readonly Dictionary<string, Type> LifecycleTypeCache = new(StringComparer.Ordinal);
        private static readonly string[] DefaultEnterLifecycleActions =
        {
            "capture_return_position",
            "snapshot_save",
            "zone_enter_message",
            "apply_kit",
            "teleport_enter",
            "apply_templates",
            "apply_abilities",
            "glow_spawn",
            "boss_enter",
            "integration_events_enter",
            "announce_enter"
        };
        private static readonly string[] DefaultExitLifecycleActions =
        {
            "zone_exit_message",
            "restore_kit_snapshot",
            "restore_abilities",
            "boss_exit",
            "teleport_return",
            "glow_reset",
            "integration_events_exit"
        };
        private static volatile bool _pendingZoneBorderRebuild;
        private static float _lastZoneDetectionUpdateTime;
        private static ArenaLifecycleManager _arenaLifecycleManager;
        private static EntityQuery _autoZonePlayerQuery;
        private static bool _autoZonePlayerQueryInitialized;

        public Plugin()
        {
            Instance = this;
        }

        public override void Load()
        {
            try
            {
                // Initialize configuration paths under the Bluelock root config folder
                _configPath = ResolveBluelockConfigPath("VAuto.ZoneLifecycle.json");
                _zonesConfigPath = ResolveBluelockConfigPath("VAuto.Zones.json");
                _kitsConfigPath = ResolveBluelockConfigPath("VAuto.Kits.json");

                // Bind CFG configuration
                BindConfiguration();

                // Load JSON configuration
                LoadJsonConfiguration();

                // Validate all configurations
                var configValidation = ProcessConfigService.ValidateAllConfigs(Path.GetDirectoryName(_zonesConfigPath) ?? Paths.ConfigPath);
                if (!configValidation.Success)
                {
                    Logger.LogWarning("[BlueLock] Configuration validation completed with errors - review log above");
                }

                // Check if enabled
                if (GeneralEnabled != null && !GeneralEnabled.Value)
                {
                    Logger.LogInfo("[BlueLock] Disabled via config.");
                    return;
                }

                ConfigureSandboxProgressionBridge();

                _harmony = new Harmony("gg.coyote.BlueLock");
                _harmony.PatchAll(typeof(Patches));
                // Patch additional arena loot suppression
                try
                {
                    var arenaPatchesType = typeof(Plugin).Assembly.GetType("VAuto.Zone.ArenaPatches.DropInventorySystemPatch");
                    if (arenaPatchesType != null)
                    {
                        _harmony.PatchAll(arenaPatchesType);
                    }
                }
                catch { }
                
                // Initialize CoreLogger and services
                CoreLog = new CoreLogger("BlueLock");
                _arenaLifecycleManager = new ArenaLifecycleManager(CoreLog);
                VAutomationCore.Services.ZoneEventBridge.Initialize();
                _arenaLifecycleManager.Initialize();
                
                // Initialize all services using ServiceInitializer
                var servicesInitialized = ServiceInitializer.InitializeAll(CoreLog);
                if (!servicesInitialized)
                {
                    Logger.LogWarning("[BlueLock] Some services failed to initialize");
                }

                // Initialize arena territory
                try
                {
                    // ArenaTerritory.InitializeArenaGrid(); // Excluded from headless builds
                    Logger.LogInfo("Arena territory (headless stub)");
                    
                    // Initialize KitService
                    KitService.Initialize();
                    Logger.LogInfo("KitService initialized");
                    
                    // Initialize ZoneConfigService
                    ZoneConfigService.Initialize();
                    Logger.LogInfo("ZoneConfigService initialized");

                    // Initialize Zone boss spawner service
                    ZoneBossSpawnerService.Initialize();
                    Logger.LogInfo("ZoneBossSpawnerService initialized");

                    // Initialize ability UI enter/exit binding service
                    ConfigureAbilityUi();
                    AbilityUi.Initialize();
                    Logger.LogInfo("AbilityUi initialized");

                    // Queue border build to run on server update thread when world is ready.
                    ZoneGlowBorderService.QueueRebuild("startup");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Territory init failed: {ex.Message}");
                }

                // NOTE: ZoneEventBridge, ZoneLifecycleObserver, ArenaTerritory, ArenaGlowBorderService
                // are excluded from headless builds. These services require Unity runtime.
                // For in-game runtime, ensure these files are not excluded.

                // Register commands with VCF
                CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());
                Logger.LogInfo("[BlueLock] Commands registered");
                LogStartupSummary();

                // Start hot-reload monitoring if enabled
                if (HotReloadEnabled?.Value == true)
                {
                    StartHotReloadMonitoring();
                }
                
                Logger.LogInfo("BlueLock loaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static void LogStartupSummary()
        {
            Logger.LogInfo("[BlueLock] Startup Summary:");
            Logger.LogInfo($"[BlueLock]   Config(CFG): {ResolveBluelockConfigPath("VAuto.Zone.cfg")}");
            Logger.LogInfo($"[BlueLock]   Config(JSON Lifecycle): {_configPath}");
            Logger.LogInfo($"[BlueLock]   Config(JSON Zones): {_zonesConfigPath}");
            if (KitService.IsLegacyKitsDisabled)
            {
                Logger.LogInfo("[BlueLock]   Config(JSON Kits): disabled (legacy kits ignored)");
            }
            else
            {
                Logger.LogInfo($"[BlueLock]   Config(JSON Kits): {KitsConfigPathValue}");
            }
            Logger.LogInfo("[BlueLock]   Command Roots: zone, arena, enter, exit");
        }

        private static void ConfigureAbilityUi()
        {
            AbilityUi.GetEntityManager = () => UnifiedCore.EntityManager;
            AbilityUi.GetSteamId = ResolvePlatformId;
            AbilityUi.ResolveAbilityGuid = ResolveAbilityGuid;
            AbilityUi.LogInfo = message => Logger.LogInfo($"[AbilityUi] {message}");
            AbilityUi.LogWarn = message => Logger.LogWarning($"[AbilityUi] {message}");
            AbilityUi.LogError = message => Logger.LogError($"[AbilityUi] {message}");
        }

        private static PrefabGUID? ResolveAbilityGuid(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var value = raw.Trim();
            if (int.TryParse(value, out var hash))
            {
                return new PrefabGUID(hash);
            }

            try
            {
                if (PrefabReferenceCatalog.TryResolve(value, out var catalogGuid,
                        PrefabCatalogDomain.Ability,
                        PrefabCatalogDomain.Spell,
                        PrefabCatalogDomain.Weapon))
                {
                    return catalogGuid;
                }

                if (ZoneCore.TryResolvePrefabEntity(value, out var guid, out _))
                {
                    return guid;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Ability GUID resolve failed for '{value}': {ex.Message}");
            }

            return null;
        }

        private void BindConfiguration()
        {
            var cfgPath = ResolveBluelockConfigPath("VAuto.Zone.cfg");
            var cfgDirectory = Path.GetDirectoryName(cfgPath);
            if (!string.IsNullOrEmpty(cfgDirectory))
            {
                Directory.CreateDirectory(cfgDirectory);
            }

            var configFile = new ConfigFile(cfgPath, true);

            // General
            GeneralEnabled = configFile.Bind("General", "Enabled", true, "Enable or disable VAutoZone plugin");
            LogLevel = configFile.Bind("General", "LogLevel", "Info", "Log level (Debug, Info, Warning, Error)");

            // Zone Detection
            ZoneDetectionCheckIntervalMs = configFile.Bind("ZoneDetection", "CheckIntervalMs", 100, "Interval for checking zone transitions (milliseconds)");
            ZoneDetectionPositionThreshold = configFile.Bind("ZoneDetection", "PositionChangeThreshold", 1.0f, "Minimum position change to trigger zone check (units)");
            MapIconSpawnRefreshIntervalSeconds = configFile.Bind("ZoneDetection", "MapIconSpawnRefreshIntervalSeconds", 10.0f, "Interval for refreshing map icon spawns (seconds)");
            ZoneDetectionDebugMode = configFile.Bind("ZoneDetection", "DebugMode", false, "Enable zone detection debug logging");
            ZoneDetectionEnterConfirmSeconds = configFile.Bind("ZoneDetection", "EnterConfirmSeconds", DefaultZoneEnterTransitionConfirmSeconds, "Seconds a player must remain in a candidate zone before enter is committed.");
            ZoneDetectionExitConfirmSeconds = configFile.Bind("ZoneDetection", "ExitConfirmSeconds", DefaultZoneExitTransitionConfirmSeconds, "Seconds a player must remain outside a zone before exit is committed.");
            ZoneDetectionTransitionCooldownSeconds = configFile.Bind("ZoneDetection", "TransitionCooldownSeconds", DefaultZoneTransitionCooldownSeconds, "Minimum seconds between committed zone transitions for the same player.");

            // Glow System
            GlowSystemEnabled = configFile.Bind("GlowSystem", "Enabled", true, "Enable the zone glow system");
            GlowSystemUpdateInterval = configFile.Bind("GlowSystem", "UpdateInterval", 0.5f, "Update interval for glow effects (seconds)");
            GlowSystemShowDebugInfo = configFile.Bind("GlowSystem", "ShowDebugInfo", false, "Show debug information for glow zones");
            GlowSystemAutoRotateEnabled = configFile.Bind("GlowSystem", "AutoRotateEnabled", true, "Auto-rotate border glow buffs (main-thread only)");
            GlowSystemAutoRotateIntervalMinutes = configFile.Bind("GlowSystem", "AutoRotateIntervalMinutes", 5, "Minutes between border glow rotations");

            // Arena Territory
            ArenaTerritoryEnabled = configFile.Bind("ArenaTerritory", "Enabled", true, "Enable arena territory management");
            ArenaTerritoryShowGrid = configFile.Bind("ArenaTerritory", "ShowGrid", false, "Show arena grid debug visualization");
            ArenaTerritoryGridCellSize = configFile.Bind("ArenaTerritory", "GridCellSize", 100.0f, "Size of arena grid cells (units)");

            // Integration
            IntegrationLifecycleEnabled = configFile.Bind("Integration", "LifecycleEnabled", true, "Allow zone system to trigger lifecycle events");
            IntegrationSendZoneEvents = configFile.Bind("Integration", "SendZoneEvents", true, "Allow zone system to send events to other modules");
            IntegrationAllowTrapOverrides = configFile.Bind("Integration", "AllowTrapOverrides", true, "Allow trap system to override zone behaviors");
            BuildingPlacementRestrictionsDisabled = configFile.Bind("Integration", "BuildingPlacementRestrictionsDisabled", false, "Disable building placement restrictions. Castle Heart placement is blocked while this is enabled.");

            // Kit Settings
            KitAutoEquipEnabled = configFile.Bind("Kit Settings", "AutoEquipEnabled", true, "Enable automatic kit equipping on zone enter");
            KitRestoreOnExit = configFile.Bind("Kit Settings", "RestoreOnExit", true, "Enable gear restoration on zone exit");
            KitBroadcastEquips = configFile.Bind("Kit Settings", "BroadcastEquips", false, "Broadcast kit equip messages to all players");
            KitDefaultName = configFile.Bind("Kit Settings", "DefaultKit", "startkit", "Default kit name to use when zone has no specific kit");
            KitDefinitionsPath = configFile.Bind("Kit Settings", "DefinitionsPath", _kitsConfigPath, "Path to kit definitions JSON file");

            // Sandbox Progression
            SandboxProgressionEnabled = configFile.Bind("Sandbox Progression", "Enabled", true, "Enable sandbox progression backup/unlock/restore flow.");
            SandboxProgressionDefaultZoneUnlockEnabled = configFile.Bind("Sandbox Progression", "DefaultZoneUnlockEnabled", true, "Default sandbox progression unlock behavior when a zone does not define SandboxUnlockEnabled.");
            SandboxProgressionPersistSnapshots = configFile.Bind("Sandbox Progression", "PersistSnapshots", true, "Persist sandbox progression snapshots to disk for restore after restart.");
            SandboxProgressionSnapshotFilePath = configFile.Bind("Sandbox Progression", "SnapshotFilePath", Path.Combine(Paths.ConfigPath, "Bluelock", "state", "sandbox_progression_snapshots.json"), "Path to sandbox progression snapshot state file.");
            SandboxProgressionVerboseLogs = configFile.Bind("Sandbox Progression", "VerboseLogs", false, "Enable verbose sandbox progression logs.");

            // Debug
            DebugMode = configFile.Bind("Debug", "DebugMode", false, "Enable debug mode");
            HotReloadEnabled = configFile.Bind("Debug", "HotReload", true, "Enable hot-reload of configuration");
        }

        private static string ResolveBluelockConfigPath(string fileName)
        {
            var rootDir = Path.Combine(Paths.ConfigPath, "Bluelock");
            Directory.CreateDirectory(rootDir);

            var rootPath = Path.Combine(rootDir, fileName);
            var legacyPath = Path.Combine(rootDir, "config", fileName);

            try
            {
                if (!File.Exists(rootPath) && File.Exists(legacyPath))
                {
                    File.Copy(legacyPath, rootPath, overwrite: false);
                    Logger.LogInfo($"[BlueLock] Migrated config '{fileName}' from legacy folder to '{rootPath}'.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Config migration check failed for '{fileName}': {ex.Message}");
            }

            return rootPath;
        }

        private static void ConfigureSandboxProgressionBridge()
        {
            try
            {
                VAuto.Core.Services.DebugEventBridge.ConfigureSandboxProgression(
                    SandboxProgressionEnabledValue,
                    SandboxProgressionPersistSnapshotsValue,
                    SandboxProgressionSnapshotFilePathValue,
                    SandboxProgressionVerboseLogsValue);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Failed to configure DebugEventBridge sandbox progression: {ex.Message}");
            }
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
                    Logger.LogInfo($"[BlueLock] Loaded JSON configuration from {_configPath}");
                }
                else
                {
                    _jsonConfig = new ZoneJsonConfig();
                    SaveJsonConfiguration();
                    Logger.LogInfo($"[BlueLock] Created new JSON configuration at {_configPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] Failed to load JSON configuration: {ex.Message}");
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
                Logger.LogInfo($"[BlueLock] Saved JSON configuration to {_configPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] Failed to save JSON configuration: {ex.Message}");
            }
        }

        private void StartHotReloadMonitoring()
        {
            _lastConfigCheck = File.Exists(_configPath) ? File.GetLastWriteTime(_configPath) : DateTime.MinValue;
            _lastZonesConfigCheck = File.Exists(_zonesConfigPath) ? File.GetLastWriteTime(_zonesConfigPath) : DateTime.MinValue;
            _lastKitsConfigCheck = !KitService.IsLegacyKitsDisabled && File.Exists(KitsConfigPathValue)
                ? File.GetLastWriteTime(KitsConfigPathValue)
                : DateTime.MinValue;
            _lastAbilityConfigCheck = File.Exists(AbilityUi.ConfigPath) ? File.GetLastWriteTime(AbilityUi.ConfigPath) : DateTime.MinValue;
            _lastPrefabsRefCheck = File.Exists(PrefabResolver.PrefabsRefConfigPath) ? File.GetLastWriteTime(PrefabResolver.PrefabsRefConfigPath) : DateTime.MinValue;
            _lastAbilityAliasCheck = File.Exists(PrefabResolver.AbilityAliasConfigPath) ? File.GetLastWriteTime(PrefabResolver.AbilityAliasConfigPath) : DateTime.MinValue;
            _hotReloadTimer = new System.Timers.Timer(5000);
            _hotReloadTimer.Elapsed += (_, _) => CheckForConfigChanges();
            _hotReloadTimer.Start();
            Logger.LogInfo("[BlueLock] Hot-reload monitoring started.");
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
                        
                        // Validate reloaded configurations
                        var configValidation = ProcessConfigService.ValidateAllConfigs(Path.GetDirectoryName(_zonesConfigPath) ?? Paths.ConfigPath);
                        
                        Logger.LogInfo("[BlueLock] Configuration hot-reloaded successfully");
                    }
                }

                if (File.Exists(_zonesConfigPath))
                {
                    var zonesLastModified = File.GetLastWriteTime(_zonesConfigPath);
                    if (zonesLastModified > _lastZonesConfigCheck)
                    {
                        _lastZonesConfigCheck = zonesLastModified;
                        ZoneConfigService.Reload();
                        _zoneGlowAutoSpawnDisabled.Clear();
                        ZoneGlowBorderService.QueueRebuild("zones-config-hot-reload");
                        Logger.LogInfo("[BlueLock] Zones config hot-reloaded and border rebuild queued");
                    }
                }

                var kitsPath = KitsConfigPathValue;
                if (!KitService.IsLegacyKitsDisabled && File.Exists(kitsPath))
                {
                    var kitsLastModified = File.GetLastWriteTime(kitsPath);
                    if (kitsLastModified > _lastKitsConfigCheck)
                    {
                        _lastKitsConfigCheck = kitsLastModified;
                        KitService.Reload();
                        Logger.LogInfo("[BlueLock] Kits config hot-reloaded");
                    }
                }

                if (File.Exists(AbilityUi.ConfigPath))
                {
                    var abilityLastModified = File.GetLastWriteTime(AbilityUi.ConfigPath);
                    if (abilityLastModified > _lastAbilityConfigCheck)
                    {
                        _lastAbilityConfigCheck = abilityLastModified;
                        AbilityUi.Reload();
                        Logger.LogInfo("[BlueLock] Ability zone config hot-reloaded");
                    }
                }

                var prefabsRefPath = PrefabResolver.PrefabsRefConfigPath;
                if (File.Exists(prefabsRefPath))
                {
                    var prefabsRefLast = File.GetLastWriteTime(prefabsRefPath);
                    if (prefabsRefLast > _lastPrefabsRefCheck)
                    {
                        _lastPrefabsRefCheck = prefabsRefLast;
                        PrefabResolver.Reload();
                        ZoneConfigService.Reload();
                        _zoneGlowAutoSpawnDisabled.Clear();
                        if (!KitService.IsLegacyKitsDisabled)
                        {
                            KitService.Reload();
                        }
                        ZoneGlowBorderService.ReloadConfigAndRebuild();
                        Logger.LogInfo(KitService.IsLegacyKitsDisabled
                            ? "[BlueLock] Prefabsref catalog hot-reloaded; zones/glow rebuilt."
                            : "[BlueLock] Prefabsref catalog hot-reloaded; zones/kits/glow rebuilt.");
                    }
                }

                var abilityAliasPath = PrefabResolver.AbilityAliasConfigPath;
                if (File.Exists(abilityAliasPath))
                {
                    var abilityAliasLast = File.GetLastWriteTime(abilityAliasPath);
                    if (abilityAliasLast > _lastAbilityAliasCheck)
                    {
                        _lastAbilityAliasCheck = abilityAliasLast;
                        PrefabResolver.Reload();
                        AbilityUi.Reload();
                        Logger.LogInfo("[BlueLock] Ability alias catalog hot-reloaded.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] Error checking configuration changes: {ex.Message}");
            }
        }

        #region Public Configuration Accessors
        public static bool IsEnabled => GeneralEnabled?.Value ?? true;
        public static int CheckIntervalMs => ZoneDetectionCheckIntervalMs?.Value ?? 100;
        public static float PositionChangeThreshold => ZoneDetectionPositionThreshold?.Value ?? 1.0f;
        public static double ZoneEnterTransitionConfirmSecondsValue => Math.Max(0.05d, ZoneDetectionEnterConfirmSeconds?.Value ?? DefaultZoneEnterTransitionConfirmSeconds);
        public static double ZoneExitTransitionConfirmSecondsValue => Math.Max(0.05d, ZoneDetectionExitConfirmSeconds?.Value ?? DefaultZoneExitTransitionConfirmSeconds);
        public static double ZoneTransitionCooldownSecondsValue => Math.Max(0.0d, ZoneDetectionTransitionCooldownSeconds?.Value ?? DefaultZoneTransitionCooldownSeconds);
        public static float MapIconSpawnRefreshIntervalSecondsValue => MapIconSpawnRefreshIntervalSeconds?.Value ?? 10.0f;
        public static bool ZoneDetectionDebug => ZoneDetectionDebugMode?.Value ?? false;
        public static bool GlowSystemEnabledValue => GlowSystemEnabled?.Value ?? true;
        public static float GlowSystemUpdateIntervalValue => GlowSystemUpdateInterval?.Value ?? 0.5f;
        public static bool GlowSystemShowDebugInfoValue => GlowSystemShowDebugInfo?.Value ?? false;
        public static bool GlowSystemAutoRotateEnabledValue => GlowSystemAutoRotateEnabled?.Value ?? true;
        public static int GlowSystemAutoRotateIntervalMinutesValue => GlowSystemAutoRotateIntervalMinutes?.Value ?? 5;
        public static bool ArenaTerritoryEnabledValue => ArenaTerritoryEnabled?.Value ?? true;
        public static bool ArenaTerritoryShowGridValue => ArenaTerritoryShowGrid?.Value ?? false;
        public static float ArenaTerritoryGridCellSizeValue => ArenaTerritoryGridCellSize?.Value ?? 100.0f;
        public static bool IntegrationLifecycleEnabledValue => IntegrationLifecycleEnabled?.Value ?? true;
        public static bool IntegrationSendZoneEventsValue => IntegrationSendZoneEvents?.Value ?? true;
        public static bool IntegrationAllowTrapOverridesValue => IntegrationAllowTrapOverrides?.Value ?? true;
        public static bool BuildingPlacementRestrictionsDisabledValue => BuildingPlacementRestrictionsDisabled?.Value ?? false;
        public static bool KitAutoEquipEnabledValue => KitAutoEquipEnabled?.Value ?? true;
        public static bool KitRestoreOnExitValue => KitRestoreOnExit?.Value ?? true;
        public static bool KitBroadcastEquipsValue => KitBroadcastEquips?.Value ?? false;
        public static string KitDefaultNameValue => string.IsNullOrWhiteSpace(KitDefaultName?.Value) ? "startkit" : KitDefaultName.Value;
        public static string KitsConfigPathValue => string.IsNullOrWhiteSpace(KitDefinitionsPath?.Value) ? _kitsConfigPath : KitDefinitionsPath.Value;
        public static bool SandboxProgressionEnabledValue => SandboxProgressionEnabled?.Value ?? true;
        public static bool SandboxProgressionDefaultZoneUnlockEnabledValue => SandboxProgressionDefaultZoneUnlockEnabled?.Value ?? true;
        public static bool SandboxProgressionPersistSnapshotsValue => SandboxProgressionPersistSnapshots?.Value ?? true;
        public static string SandboxProgressionSnapshotFilePathValue
            => string.IsNullOrWhiteSpace(SandboxProgressionSnapshotFilePath?.Value)
                ? Path.Combine(Paths.ConfigPath, "Bluelock", "state", "sandbox_progression_snapshots.json")
                : SandboxProgressionSnapshotFilePath.Value;
        public static bool SandboxProgressionVerboseLogsValue => SandboxProgressionVerboseLogs?.Value ?? false;
        public static bool DebugModeEnabled => DebugMode?.Value ?? false;
        public static int ActiveGlowEntityCount
        {
            get
            {
                var em = UnifiedCore.EntityManager;
                var total = 0;
                foreach (var kvp in _zoneBorderEntities)
                {
                    foreach (var entity in kvp.Value)
                    {
                        if (em.Exists(entity))
                        {
                            total++;
                        }
                    }
                }
                return total;
            }
        }

        public static bool HasStoredReturnPosition(Entity characterEntity)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                var platformId = ResolvePlatformId(characterEntity, em);
                return platformId != 0 && _zoneReturnPositions.ContainsKey(platformId);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryFindClosestMarkerInZone(string zoneId, float3 position, out Entity closestMarker)
        {
            closestMarker = Entity.Null;
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            if (!_zoneBorderEntities.TryGetValue(zoneId, out var entities) || entities == null || entities.Count == 0)
            {
                return false;
            }

            var em = UnifiedCore.EntityManager;
            var bestDistSq = float.MaxValue;
            foreach (var entity in entities)
            {
                if (entity == Entity.Null || !em.Exists(entity))
                {
                    continue;
                }

                // Zone marker list also contains glow buff entities; skip those.
                if (em.HasComponent<Buff>(entity))
                {
                    continue;
                }

                if (!TryGetBestPosition(em, entity, out var markerPos))
                {
                    continue;
                }

                var distSq = math.lengthsq(markerPos - position);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    closestMarker = entity;
                }
            }

            return closestMarker != Entity.Null;
        }
        #endregion

        public static void SetGlowSystemEnabled(bool enabled)
        {
            if (GlowSystemEnabled != null)
            {
                GlowSystemEnabled.Value = enabled;
            }
        }

        public static void SetGlowAutoRotateEnabled(bool enabled)
        {
            if (GlowSystemAutoRotateEnabled != null)
            {
                GlowSystemAutoRotateEnabled.Value = enabled;
            }
        }

        public static void RebuildZoneBordersNow(string reason = "manual")
        {
            RequestZoneBorderRebuild(reason, bypassCooldown: true);
            ProcessPendingZoneBorderRebuild();
        }

        public static void RotateZoneBorderGlowNow()
        {
            _glowRotationOffset++;
            _nextGlowRotationUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, GlowSystemAutoRotateIntervalMinutesValue));
            RequestZoneBorderRebuild("manual-rotate", bypassCooldown: true);
            ProcessPendingZoneBorderRebuild();
        }

        public static void OnApprovedUserConnected(Entity userEntity)
        {
            _lastKnownUserEntity = userEntity;

            if (_needsGlowBuffRebuildOnFirstUser)
            {
                _needsGlowBuffRebuildOnFirstUser = false;
                RequestZoneBorderRebuild("first-user-connect", bypassCooldown: true);
            }

            // Auto rebuild on user spawn/connect (covers admin spawns as requested).
            RequestZoneBorderRebuild("approved-user-connect");
        }

        public static void ClearZoneBordersNow()
        {
            ClearAllZoneBorders();
        }

        public static void QueueZoneBorderRebuild(string reason = "service", bool bypassCooldown = false)
        {
            RequestZoneBorderRebuild(reason, bypassCooldown);
        }

        public static int GetPlayersInZoneCount(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return 0;
            }

            var count = 0;
            foreach (var state in _playerZoneStates.Values)
            {
                if (string.Equals(state, zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        public void OnDestroy()
        {
            _hotReloadTimer?.Dispose();
            _hotReloadTimer = null;
            try
            {
                _arenaLifecycleManager?.Shutdown();
            }
            catch
            {
            }
            try
            {
                _autoZonePlayerQuery.Dispose();
            }
            catch
            {
            }
            _autoZonePlayerQueryInitialized = false;
            _harmony?.UnpatchSelf();
            Logger.LogInfo("VAutoZone unloaded");
        }

        private static void RebuildAllZoneBorders()
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default || em.World == null || !em.World.IsCreated)
                {
                    Logger.LogWarning("[BlueLock] RebuildAllZoneBorders skipped: world not ready");
                    return;
                }

                ClearAllZoneBorders();

                if (!GlowSystemEnabledValue)
                {
                    Logger.LogInfo("[BlueLock] Glow system disabled; skipped zone border rebuild");
                    return;
                }

                var zones = ZoneConfigService.GetAllZones();
                if (zones == null || zones.Count == 0)
                {
                    Logger.LogInfo("[BlueLock] No zones found for border build");
                    return;
                }

                var builtZones = 0;
                var builtEntities = 0;
                var failedZones = 0;
                var glowBuffs = Array.Empty<PrefabGUID>();
                var hasGlowUser = false;
                var glowUserEntity = Entity.Null;

                foreach (var zone in zones)
                {
                    // Zone-level fail isolation: wrap entire zone in try/catch
                    try
                    {
                        if (zone == null || string.IsNullOrWhiteSpace(zone.Id))
                        {
                            continue;
                        }

                        var borderCfg = ZoneConfigService.GetEffectiveBorderConfig(zone);
                        if (borderCfg == null || !borderCfg.Enabled)
                        {
                            continue;
                        }

                        var spacing = Math.Max(1f, borderCfg.Spacing);
                        var heightOffset = borderCfg.HeightOffset;

                        var markerName = borderCfg.PrefabName;
                        var markerId = borderCfg.PrefabGuid;

                        // Log prefab resolution attempt
                        var markerResolved = TryResolveZonePrefab(markerName, markerId, out var markerGuid, out var markerPrefabEntity, out var markerSource) &&
                                             markerPrefabEntity != Entity.Null &&
                                             !IsBuffPrefab(em, markerPrefabEntity);

                        if (!markerResolved)
                        {
                            Logger.LogDebug($"[BlueLock] Zone '{zone.Id}': Initial marker resolution failed (name='{markerName}', id={markerId})");
                        }
                        else
                        {
                            Logger.LogDebug($"[BlueLock] Zone '{zone.Id}': Marker resolved via {markerSource} to {markerGuid.GuidHash}, entity exists={em.Exists(markerPrefabEntity)}");
                        }

                        if (!markerResolved && !string.IsNullOrWhiteSpace(markerName) &&
                            PrefabRemapService.TryRemap(markerName, out var remappedName))
                        {
                            Logger.LogDebug($"[BlueLock] Zone '{zone.Id}': Attempting remap '{markerName}' -> '{remappedName}'");
                            markerResolved = TryResolveZonePrefab(remappedName, 0, out markerGuid, out markerPrefabEntity, out markerSource) &&
                                             markerPrefabEntity != Entity.Null &&
                                             !IsBuffPrefab(em, markerPrefabEntity);

                            if (markerResolved)
                            {
                                markerName = remappedName;
                                markerId = markerGuid.GuidHash;
                                Logger.LogInfo($"[BlueLock] Zone '{zone.Id}': Marker remap successful ('{remappedName}' -> {markerGuid.GuidHash})");
                            }
                        }

                        // Fallback: if primary marker fails, try a known-good spawnable marker
                        if (!markerResolved)
                        {
                            Logger.LogDebug($"[BlueLock] Zone '{zone.Id}': Primary marker failed, attempting fallback markers");
                            
                            // Try known-good fallback markers that are spawnable
                            var fallbackMarkers = new[]
                            {
                                ("TM_Castle_ObjectDecor_TargetDummy_Vampire01", 230163020),
                                ("TM_TargetDummy_01", 10513858),
                                ("ZoneIcon_Default", 0),
                                ("PurpleCarpetsBuildMenuGroup01", 1144832236),
                                ("Item_Consumable_Salve_Vermin", -1661017425)
                            };
                            
                            foreach (var (fallbackName, fallbackId) in fallbackMarkers)
                            {
                                if (TryResolveZonePrefab(fallbackName, fallbackId, out markerGuid, out markerPrefabEntity, out markerSource) &&
                                    markerPrefabEntity != Entity.Null &&
                                    !IsBuffPrefab(em, markerPrefabEntity) &&
                                    em.Exists(markerPrefabEntity))
                                {
                                    markerName = fallbackName;
                                    markerId = markerGuid.GuidHash;
                                    markerResolved = true;
                                    Logger.LogWarning($"[BlueLock] Zone '{zone.Id}': Using fallback marker '{fallbackName}' (Guid={markerId}, source={markerSource})");
                                    break;
                                }
                            }
                        }

                        if (!markerResolved)
                        {
                            Logger.LogWarning($"[BlueLock] Zone '{zone.Id}': Updated behavior active - unresolved marker (name='{markerName}', id={markerId}); skipping border with no fallback.");
                            failedZones++;
                            continue;
                        }

                        if (!em.Exists(markerPrefabEntity))
                        {
                            Logger.LogWarning($"[BlueLock] Zone '{zone.Id}': Border marker prefab entity does not exist (Guid={markerId}, Name='{markerName}'); skipping border");
                            failedZones++;
                            continue;
                        }

                        var borderPoints = GetZoneBorderPoints(zone, spacing);
                        if (borderPoints.Count == 0)
                        {
                            Logger.LogWarning($"[BlueLock] Zone '{zone.Id}': No border points generated (shape={zone.Shape}, radius={zone.Radius}, spacing={spacing})");
                            failedZones++;
                            continue;
                        }

                        var zoneEntities = new List<Entity>(borderPoints.Count * 2);
                        var borderBaseY = (zone.GlowSpawnHeight > 0f ? zone.GlowSpawnHeight : 0.3f) + heightOffset;
                        var applyZoneGlow = zone.AutoGlowWithZone;
                        var configuredGlowSpecified = zone.GlowPrefabId != 0 || !string.IsNullOrWhiteSpace(zone.GlowPrefab);
                        var hasConfiguredZoneGlow = false;
                        var configuredZoneGlowGuid = PrefabGUID.Empty;
                        if (applyZoneGlow)
                        {
                            hasConfiguredZoneGlow = TryResolveConfiguredZoneGlowBuff(zone, em, out configuredZoneGlowGuid);
                            if (configuredGlowSpecified && !hasConfiguredZoneGlow)
                            {
                                Logger.LogWarning($"[BlueLock] Zone '{zone.Id}': configured glow invalid (GlowPrefabId={zone.GlowPrefabId}, GlowPrefab='{zone.GlowPrefab ?? string.Empty}'). Using fallback glow list.");
                            }
                        }

                        if (applyZoneGlow)
                        {
                            if (glowBuffs.Length == 0)
                            {
                        glowBuffs = GlowService.GetValidatedGlowBuffHashes()
                            .Where(hash => hash != 0)
                            .Select(hash => new PrefabGUID(hash))
                            .ToArray();
                                Logger.LogDebug($"[BlueLock] Loaded {glowBuffs.Length} glow buffs");
                            }

                            if (glowBuffs.Length > 0 && !hasGlowUser)
                            {
                                hasGlowUser = TryGetGlowBuffUserEntity(em, out glowUserEntity);
                                if (hasGlowUser)
                                {
                                    Logger.LogDebug($"[BlueLock] Glow user entity resolved: {glowUserEntity.Index}:{glowUserEntity.Version}");
                                }
                            }

                            if (glowBuffs.Length > 0 && !hasGlowUser)
                            {
                                // Server is empty; we'll rebuild once a real user connects so DebugEventsSystem can apply buffs.
                                _needsGlowBuffRebuildOnFirstUser = true;
                                Logger.LogDebug($"[BlueLock] Zone '{zone.Id}': No glow user available; will rebuild on first user connect");
                            }
                        }

                        var zoneSeed = ArenaMatchUtilities.StableHash(zone.Id);
                        var selectedFallbackGlowGuid = PrefabGUID.Empty;
                        if (applyZoneGlow && !hasConfiguredZoneGlow && glowBuffs.Length > 0)
                        {
                            selectedFallbackGlowGuid = glowBuffs[Mod(zoneSeed + _glowRotationOffset, glowBuffs.Length)];
                        }
                        var pointsSpawned = 0;
                        var buffsApplied = 0;

                        for (var pointIndex = 0; pointIndex < borderPoints.Count; pointIndex++)
                        {
                            Entity marker = Entity.Null;
                            try
                            {
                                var point = borderPoints[pointIndex];
                                // Point-level fail isolation: wrap instantiate and position
                                try
                                {
                                    marker = em.Instantiate(markerPrefabEntity);
                                    if (marker == Entity.Null || !em.Exists(marker))
                                    {
                                        Logger.LogWarning($"[BlueLock] Zone '{zone.Id}' point {pointIndex}: Instantiate returned null/invalid entity");
                                        continue;
                                    }
                                }
                                catch (Exception instantEx)
                                {
                                    Logger.LogWarning($"[BlueLock] Zone '{zone.Id}' point {pointIndex}: Instantiate failed: {instantEx.ToString()}");
                                    continue;
                                }

                                var markerPos = new float3(point.x, borderBaseY, point.z);
                                try
                                {
                                    if (em.HasComponent<LocalTransform>(marker))
                                    {
                                        var t = em.GetComponentData<LocalTransform>(marker);
                                        t.Position = markerPos;
                                        em.SetComponentData(marker, t);
                                    }
                                    else if (em.HasComponent<Translation>(marker))
                                    {
                                        var t = em.GetComponentData<Translation>(marker);
                                        t.Value = markerPos;
                                        em.SetComponentData(marker, t);
                                    }
                                }
                                catch (Exception posEx)
                                {
                                    Logger.LogWarning($"[BlueLock] Zone '{zone.Id}' point {pointIndex}: Position set failed: {posEx.ToString()}");
                                    if (em.Exists(marker))
                                    {
                                        em.DestroyEntity(marker);
                                    }
                                    continue;
                                }

                                zoneEntities.Add(marker);
                                builtEntities++;
                                pointsSpawned++;

                                // Glow buff application: separate try/catch
                                if (applyZoneGlow && hasGlowUser && (hasConfiguredZoneGlow || glowBuffs.Length > 0))
                                {
                                    try
                                    {
                                        var buffGuid = hasConfiguredZoneGlow
                                            ? configuredZoneGlowGuid
                                            : selectedFallbackGlowGuid;
                                        if (TryApplyVisualGlowBuff(glowUserEntity, marker, buffGuid, out var buffEntity))
                                        {
                                            zoneEntities.Add(buffEntity);
                                            builtEntities++;
                                            buffsApplied++;
                                        }
                                    }
                                    catch (Exception buffEx)
                                    {
                                        Logger.LogWarning($"[BlueLock] Zone '{zone.Id}' point {pointIndex}: Glow buff apply failed: {buffEx.ToString()}");
                                    }
                                }
                            }
                            catch (Exception pointEx)
                            {
                                if (marker != Entity.Null && em.Exists(marker))
                                {
                                    try { em.DestroyEntity(marker); } catch { }
                                }
                                Logger.LogWarning($"[BlueLock] Zone '{zone.Id}' point {pointIndex}: {pointEx.ToString()}");
                            }
                        }

                        _zoneBorderEntities[zone.Id] = zoneEntities;
                        builtZones++;
                        Logger.LogInfo($"[BlueLock] Zone '{zone.Id}': Built border with {pointsSpawned} points ({buffsApplied} buffs applied)");
                    }
                    catch (Exception zoneEx)
                    {
                        failedZones++;
                        Logger.LogError($"[BlueLock] Zone '{zone?.Id ?? "?"}': {zoneEx.ToString()}");
                    }
                }

                Logger.LogInfo($"[BlueLock] Border rebuild complete: {builtZones} zones built, {builtEntities} entities, {failedZones} zones failed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] RebuildAllZoneBorders failed: {ex.ToString()}");
            }
        }

        private static void ClearAllZoneBorders()
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                foreach (var kvp in _zoneBorderEntities)
                {
                    foreach (var entity in kvp.Value)
                    {
                        if (em.Exists(entity))
                        {
                            em.DestroyEntity(entity);
                        }
                    }
                }
                _zoneBorderEntities.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] ClearAllZoneBorders failed: {ex.Message}");
            }
        }

        private static List<float3> GetZoneBorderPoints(ZoneDefinition zone, float spacing)
        {
            var points = new List<float3>();
            var nodes = GlowTileGeometry.GetZoneBorderNodes(zone, spacing);
            foreach (var node in nodes)
            {
                points.Add(new float3(node.Position.x, zone.GlowSpawnHeight, node.Position.y));
            }

            return points;
        }

        private static bool TryResolveZonePrefab(string prefabName, int prefabId, out PrefabGUID guid, out Entity prefabEntity, out string source)
        {
            guid = PrefabGUID.Empty;
            prefabEntity = Entity.Null;
            source = "none";

            // 1) GUID first (fast path).
            if (prefabId != 0)
            {
                guid = new PrefabGUID(prefabId);
                if (ZoneCore.TryGetPrefabEntity(guid, out prefabEntity))
                {
                    source = "guid";
                    return true;
                }
            }

            // 2) Name/alias -> GUID via PrefabResolver (works for non-spawnable and spawnable prefab names).
            if (!string.IsNullOrWhiteSpace(prefabName) &&
                PrefabResolver.TryResolve(prefabName, out var guidFromCatalog) &&
                ZoneCore.TryGetPrefabEntity(guidFromCatalog, out prefabEntity))
            {
                guid = guidFromCatalog;
                source = "prefab-resolver";
                return true;
            }

            // 3) Spawnable-name lookup (world-dependent).
            if (!string.IsNullOrWhiteSpace(prefabName) && ZoneCore.TryResolvePrefabEntity(prefabName, out guid, out prefabEntity))
            {
                source = "spawnable-runtime";
                return true;
            }

            // 4) Legacy name fallback: resolve via in-code catalog loaded from Bluelock/Data.
            if (TryResolveGlowChoiceEntry(prefabName, out guid, out prefabEntity))
            {
                source = "glow-choice";
                return true;
            }

            return false;
        }

        private static bool IsBuffPrefab(EntityManager em, Entity prefabEntity)
        {
            try
            {
                return prefabEntity != Entity.Null &&
                       em.Exists(prefabEntity) &&
                       em.HasComponent<Buff>(prefabEntity);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetGlowBuffUserEntity(EntityManager em, out Entity userEntity)
        {
            userEntity = Entity.Null;

            try
            {
                if (_lastKnownUserEntity != Entity.Null &&
                    em.Exists(_lastKnownUserEntity) &&
                    em.HasComponent<User>(_lastKnownUserEntity))
                {
                    userEntity = _lastKnownUserEntity;
                    return true;
                }

                var query = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
                var users = query.ToEntityArray(Allocator.Temp);
                try
                {
                    if (users.Length == 0)
                    {
                        return false;
                    }

                    userEntity = users[0];
                    _lastKnownUserEntity = userEntity;
                    return userEntity != Entity.Null;
                }
                finally
                {
                    users.Dispose();
                    query.Dispose();
                }
            }
            catch
            {
                return false;
            }
        }

        private static int Mod(int value, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }

            var m = value % modulo;
            return m < 0 ? m + modulo : m;
        }

        private static bool TryApplyVisualGlowBuff(Entity userEntity, Entity targetEntity, PrefabGUID buffGuid, out Entity buffEntity)
        {
            buffEntity = Entity.Null;
            var em = UnifiedCore.EntityManager;
            if (userEntity == Entity.Null ||
                targetEntity == Entity.Null ||
                buffGuid == PrefabGUID.Empty ||
                em == default ||
                !em.Exists(targetEntity) ||
                !em.Exists(userEntity) ||
                !em.HasComponent<User>(userEntity))
            {
                return false;
            }

            try
            {
                var server = UnifiedCore.Server;
                var debugEvents = server.GetExistingSystemManaged<DebugEventsSystem>();
                if (debugEvents == null)
                {
                    return false;
                }

                debugEvents.ApplyBuff(
                    new FromCharacter { User = userEntity, Character = targetEntity },
                    new ApplyBuffDebugEvent { BuffPrefabGUID = buffGuid });

                if (!BuffUtility.TryGetBuff(em, targetEntity, buffGuid, out buffEntity))
                {
                    return false;
                }

                SanitizeGlowBuffEntity(em, buffEntity, targetEntity);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"[BlueLock] Failed applying glow buff {buffGuid.GuidHash}: {ex.Message}");
                return false;
            }
        }

        private static void SanitizeGlowBuffEntity(EntityManager em, Entity buffEntity, Entity targetEntity)
        {
            try
            {
                if (!em.Exists(buffEntity))
                {
                    return;
                }

                if (em.HasComponent<EntityOwner>(buffEntity))
                {
                    em.SetComponentData(buffEntity, new EntityOwner { Owner = targetEntity });
                }

                // Keep the buff alive until we destroy the border markers.
                if (em.HasComponent<LifeTime>(buffEntity))
                {
                    var lifeTime = em.GetComponentData<LifeTime>(buffEntity);
                    lifeTime.EndAction = LifeTimeEndAction.None;
                    em.SetComponentData(buffEntity, lifeTime);
                }

                // Keep visuals, strip gameplay side effects.
                if (em.HasComponent<CreateGameplayEventsOnSpawn>(buffEntity)) em.RemoveComponent<CreateGameplayEventsOnSpawn>(buffEntity);
                if (em.HasComponent<GameplayEventListeners>(buffEntity)) em.RemoveComponent<GameplayEventListeners>(buffEntity);
                if (em.HasComponent<RemoveBuffOnGameplayEvent>(buffEntity)) em.RemoveComponent<RemoveBuffOnGameplayEvent>(buffEntity);
                if (em.HasComponent<DealDamageOnGameplayEvent>(buffEntity)) em.RemoveComponent<DealDamageOnGameplayEvent>(buffEntity);
                if (em.HasComponent<ModifyMovementSpeedBuff>(buffEntity)) em.RemoveComponent<ModifyMovementSpeedBuff>(buffEntity);
                if (em.HasComponent<HealOnGameplayEvent>(buffEntity)) em.RemoveComponent<HealOnGameplayEvent>(buffEntity);
                if (em.HasComponent<DestroyOnGameplayEvent>(buffEntity)) em.RemoveComponent<DestroyOnGameplayEvent>(buffEntity);
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"[BlueLock] Glow buff sanitize warning: {ex.Message}");
            }
        }

        private static bool TryResolveGlowChoiceEntry(string lookup, out PrefabGUID guid, out Entity prefabEntity)
        {
            guid = PrefabGUID.Empty;
            prefabEntity = Entity.Null;

            if (string.IsNullOrWhiteSpace(lookup))
            {
                return false;
            }

            if (GlowService.TryResolve(lookup, out var hash))
            {
                if (hash != 0)
                {
                    guid = new PrefabGUID(hash);
                    return ZoneCore.TryGetPrefabEntity(guid, out prefabEntity);
                }
            }

            if (PrefabReferenceCatalog.TryResolve(lookup, out guid,
                    PrefabCatalogDomain.Glow,
                    PrefabCatalogDomain.Ability,
                    PrefabCatalogDomain.Spell))
            {
                return ZoneCore.TryGetPrefabEntity(guid, out prefabEntity);
            }

            return false;
        }

        private static bool TryResolveConfiguredZoneGlowBuff(ZoneDefinition zone, EntityManager em, out PrefabGUID glowGuid)
        {
            glowGuid = PrefabGUID.Empty;
            if (zone == null)
            {
                return false;
            }

            try
            {
                // Prefer explicit numeric glow id if provided.
                if (zone.GlowPrefabId != 0)
                {
                    var candidate = new PrefabGUID(zone.GlowPrefabId);
                    if (ZoneCore.TryGetPrefabEntity(candidate, out var entity) && entity != Entity.Null && em.Exists(entity) && IsBuffPrefab(em, entity))
                    {
                        glowGuid = candidate;
                        return true;
                    }
                }

                // Fallback to token/name resolution from zone config.
                if (!string.IsNullOrWhiteSpace(zone.GlowPrefab) && GlowService.TryResolve(zone.GlowPrefab, out var resolvedHash) && resolvedHash != 0)
                {
                    var resolvedGuid = new PrefabGUID(resolvedHash);
                    if (ZoneCore.TryGetPrefabEntity(resolvedGuid, out var entity) && entity != Entity.Null && em.Exists(entity) && IsBuffPrefab(em, entity))
                    {
                        glowGuid = resolvedGuid;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"[BlueLock] Zone '{zone.Id}' configured glow resolve failed: {ex.Message}");
            }

            return false;
        }

        internal static void ProcessAutoZoneDetection()
        {
            try
            {
                TickAutoGlowRotation();
                ProcessPendingZoneBorderRebuild();

                if (!IsEnabled || !IntegrationSendZoneEventsValue)
                {
                    return;
                }

                var em = UnifiedCore.EntityManager;

                var now = (float)UnityEngine.Time.realtimeSinceStartup;
                var intervalSeconds = Math.Max(0.05f, CheckIntervalMs / 1000f);
                if (now - _lastZoneDetectionUpdateTime < intervalSeconds)
                {
                    return;
                }
                _lastZoneDetectionUpdateTime = now;

                var query = GetOrCreateAutoZonePlayerQuery(em);

                var players = query.ToEntityArray(Allocator.Temp);
                var stillSeen = new HashSet<Entity>();
                var nowUtc = DateTime.UtcNow;

                try
                {
                    foreach (var player in players)
                    {
                        if (!em.Exists(player))
                        {
                            continue;
                        }

                        if (!TryGetBestPosition(em, player, out var position))
                        {
                            continue;
                        }

                        stillSeen.Add(player);
                        var zone = ZoneConfigService.GetZoneAtPosition(position.x, position.z);
                        var newZoneId = zone?.Id ?? string.Empty;

                        _playerZoneStates.TryGetValue(player, out var previousZoneId);
                        previousZoneId ??= string.Empty;

                        if (string.Equals(previousZoneId, newZoneId, StringComparison.OrdinalIgnoreCase))
                        {
                            _pendingZoneTransitions.Remove(player);
                            continue;
                        }

                        if (!ShouldCommitZoneTransition(player, previousZoneId, newZoneId, nowUtc))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(previousZoneId))
                        {
                            HandleZoneExit(player, previousZoneId);
                        }

                        if (!string.IsNullOrEmpty(newZoneId))
                        {
                            HandleZoneEnter(player, newZoneId);
                        }

                        if (string.IsNullOrEmpty(newZoneId))
                        {
                            _playerZoneStates.Remove(player);
                        }
                        else
                        {
                            _playerZoneStates[player] = newZoneId;
                            if (IsSandboxZone(newZoneId))
                            {
                                TryInvokeDebugEventBridgeInZone(player, newZoneId);
                            }
                        }
                    }
                }
                finally
                {
                    players.Dispose();
                }

                // Cleanup stale tracked players that no longer exist in query
                var stalePlayers = new List<Entity>();
                foreach (var tracked in _playerZoneStates.Keys)
                {
                    if (!stillSeen.Contains(tracked))
                    {
                        stalePlayers.Add(tracked);
                    }
                }

                foreach (var stale in stalePlayers)
                {
                    if (_playerZoneStates.TryGetValue(stale, out var staleZoneId) &&
                        !string.IsNullOrWhiteSpace(staleZoneId) &&
                        em.Exists(stale))
                    {
                        TryRunZoneExitStep("HandleZoneExit(StalePlayer)", () => HandleZoneExit(stale, staleZoneId));
                    }

                    _playerZoneStates.Remove(stale);
                    _pendingZoneTransitions.Remove(stale);
                    _lastCommittedZoneTransitions.Remove(stale);
                    KitService.ClearPlayerTrackingForEntity(stale, em);
                    AbilityUi.ClearStateForDisconnectedPlayer(stale, em);
                    VAutomationCore.Services.ZoneEventBridge.RemovePlayerZoneState(stale);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] Auto zone detection failed: {ex.Message}");
            }
        }

        private static bool ShouldCommitZoneTransition(Entity player, string previousZoneId, string candidateZoneId, DateTime nowUtc)
        {
            var requiredSeconds = string.IsNullOrWhiteSpace(candidateZoneId)
                ? ZoneExitTransitionConfirmSecondsValue
                : ZoneEnterTransitionConfirmSecondsValue;

            if (!_pendingZoneTransitions.TryGetValue(player, out var pending) ||
                !string.Equals(pending.PreviousZoneId, previousZoneId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(pending.CandidateZoneId, candidateZoneId, StringComparison.OrdinalIgnoreCase))
            {
                _pendingZoneTransitions[player] = new PendingZoneTransition
                {
                    PreviousZoneId = previousZoneId ?? string.Empty,
                    CandidateZoneId = candidateZoneId ?? string.Empty,
                    FirstSeenUtc = nowUtc
                };

                return false;
            }

            if ((nowUtc - pending.FirstSeenUtc).TotalSeconds < requiredSeconds)
            {
                return false;
            }

            if (_lastCommittedZoneTransitions.TryGetValue(player, out var lastCommittedUtc) &&
                (nowUtc - lastCommittedUtc).TotalSeconds < ZoneTransitionCooldownSecondsValue)
            {
                return false;
            }

            _pendingZoneTransitions.Remove(player);
            _lastCommittedZoneTransitions[player] = nowUtc;
            return true;
        }

        private static void RequestZoneBorderRebuild(string reason = "unspecified", bool bypassCooldown = false)
        {
            var now = DateTime.UtcNow;
            if (!bypassCooldown && now < _nextAllowedRebuildRequestUtc)
            {
                Logger.LogDebug($"[BlueLock] Zone border rebuild request skipped due to cooldown (reason={reason}, next={_nextAllowedRebuildRequestUtc:O}).");
                return;
            }

            _lastAutoGlowRebuildUtc = now;
            _nextAllowedRebuildRequestUtc = now.AddSeconds(Math.Max(1, RebuildRequestCooldownSeconds));
            _pendingZoneBorderRebuild = true;
            Logger.LogDebug($"[BlueLock] Zone border rebuild queued (reason={reason}).");
        }

        private static void TickAutoGlowRotation()
        {
            try
            {
                if (!GlowSystemEnabledValue || !GlowSystemAutoRotateEnabledValue)
                {
                    return;
                }

                var intervalMinutes = Math.Max(1, GlowSystemAutoRotateIntervalMinutesValue);
                var now = DateTime.UtcNow;

                if (_nextGlowRotationUtc == DateTime.MinValue)
                {
                    _nextGlowRotationUtc = now.AddMinutes(intervalMinutes);
                    return;
                }

                if (now < _nextGlowRotationUtc)
                {
                    return;
                }

                _glowRotationOffset++;
                _nextGlowRotationUtc = now.AddMinutes(intervalMinutes);
                RequestZoneBorderRebuild();
            }
            catch
            {
                // Never break zone detection due to rotation scheduling.
            }
        }

        private static void ProcessPendingZoneBorderRebuild()
        {
            if (!_pendingZoneBorderRebuild)
            {
                return;
            }

            try
            {
                if (!IsZoneBorderRebuildReady(out var reason))
                {
                    var now = DateTime.UtcNow;
                    if (now >= _nextBorderReadinessLogUtc)
                    {
                        _nextBorderReadinessLogUtc = now.AddSeconds(10);
                        Logger.LogDebug($"[BlueLock] Pending zone border rebuild waiting: {reason}");
                    }

                    return;
                }

                RebuildAllZoneBorders();
                _pendingZoneBorderRebuild = false;
                Logger.LogInfo("[BlueLock] Pending zone border rebuild completed");
            }
            catch (Exception ex)
            {
                // Keep pending so it retries on next server update when world is available.
                Logger.LogWarning($"[BlueLock] Pending zone border rebuild deferred: {ex.Message}");
            }
        }

        private static bool IsZoneBorderRebuildReady(out string reason)
        {
            reason = "unknown";
            try
            {
                var server = UnifiedCore.Server;
                if (server == null || !server.IsCreated)
                {
                    reason = "server world not ready";
                    return false;
                }

                var prefabCollection = server.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabCollection == null)
                {
                    reason = "PrefabCollectionSystem unavailable";
                    return false;
                }

                if (HasRegisteredPrefabs(prefabCollection, out var registryReason))
                {
                    return true;
                }

                reason = registryReason;
                return false;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        private static bool HasRegisteredPrefabs(PrefabCollectionSystem prefabCollection, out string reason)
        {
            reason = "prefab registry empty";
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var type = prefabCollection.GetType();

                var mapField = type.GetField("_PrefabGuidToEntityMap", flags);
                if (TryReadCollectionCount(mapField?.GetValue(prefabCollection), out var mapCount) && mapCount > 0)
                {
                    reason = $"_PrefabGuidToEntityMap count={mapCount}";
                    return true;
                }

                var dictionaryField = type.GetField("_PrefabGuidToEntityDictionary", flags);
                if (TryReadCollectionCount(dictionaryField?.GetValue(prefabCollection), out var dictCount) && dictCount > 0)
                {
                    reason = $"_PrefabGuidToEntityDictionary count={dictCount}";
                    return true;
                }

                var lookupMapGetter = type.GetMethod("get_PrefabLookupMap", flags);
                if (lookupMapGetter != null &&
                    TryReadCollectionCount(lookupMapGetter.Invoke(prefabCollection, null), out var lookupCount) &&
                    lookupCount > 0)
                {
                    reason = $"PrefabLookupMap count={lookupCount}";
                    return true;
                }

                // Runtime probe: if a known prefab guid resolves, prefabs are ready for border build.
                if (ZoneCore.TryGetPrefabEntity(new PrefabGUID(1144832236), out var prefabEntity) &&
                    prefabEntity != Entity.Null)
                {
                    reason = "runtime prefab probe succeeded";
                    return true;
                }
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }

            return false;
        }

        private static bool TryReadCollectionCount(object value, out int count)
        {
            count = 0;
            if (value == null)
            {
                return false;
            }

            if (value is IDictionary dictionary)
            {
                count = dictionary.Count;
                return true;
            }

            if (value is ICollection collection)
            {
                count = collection.Count;
                return true;
            }

            try
            {
                var countProperty = value.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (countProperty == null)
                {
                    return false;
                }

                var raw = countProperty.GetValue(value);
                switch (raw)
                {
                    case int i:
                        count = i;
                        return true;
                    case long l:
                        count = l > int.MaxValue ? int.MaxValue : (int)l;
                        return true;
                    case uint u:
                        count = u > int.MaxValue ? int.MaxValue : (int)u;
                        return true;
                    case ulong ul:
                        count = ul > int.MaxValue ? int.MaxValue : (int)ul;
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static void ProcessPendingZoneTeleports()
        {
            if (_pendingZoneEnterTeleports.Count == 0)
            {
                return;
            }

            try
            {
                var em = UnifiedCore.EntityManager;
                var toRemove = new List<ulong>();

                foreach (var kv in _pendingZoneEnterTeleports)
                {
                    var platformId = kv.Key;
                    var pending = kv.Value;

                    if (!em.Exists(pending.Player))
                    {
                        toRemove.Add(platformId);
                        continue;
                    }

                    // Attempt to capture return position if needed and now available.
                    CaptureReturnPositionIfNeeded(pending.Player, pending.ZoneId, em);

                    // Apply the teleport once the world/transform has stabilized.
                    if (IsNearZeroXZ(pending.TargetPos))
                    {
                        Logger.LogWarning($"[BlueLock] Zone enter teleport skipped: invalid/zero target ({pending.TargetPos.x:F1},{pending.TargetPos.y:F1},{pending.TargetPos.z:F1}) for zone '{pending.ZoneId}'.");
                        toRemove.Add(platformId);
                        continue;
                    }

                    TryTeleportWithFallback(pending.Player, pending.TargetPos, em);
                    Logger.LogDebug($"[BlueLock] Zone enter teleport applied from deferred queue for platform {platformId} zone '{pending.ZoneId}'.");
                    toRemove.Add(platformId);
                }

                foreach (var platformId in toRemove)
                {
                    _pendingZoneEnterTeleports.Remove(platformId);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] ProcessPendingZoneTeleports failed: {ex.Message}");
            }
        }

        private static void HandleZoneEnter(Entity player, string zoneId)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(player))
                {
                    return;
                }

                if (IsSandboxZone(zoneId))
                {
                    TryRunZoneEnterStep("DebugEventBridge.OnZoneEnterStart", () => TryInvokeDebugEventBridgeZoneEnterStart(player, zoneId));
                }

                var actionOrder = ResolveLifecycleActionsForZone(zoneId, isEnter: true);
                Logger.LogDebug($"[BlueLock] Zone '{zoneId}' enter actions: {string.Join(", ", actionOrder)}");
                var context = new PluginZoneLifecycleContext(player, zoneId, em);
                foreach (var step in _zoneLifecycleStepRegistry.BuildEnterSteps(actionOrder))
                {
                    step.Execute(context);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] HandleZoneEnter failed: {ex.Message}");
            }
        }

        private static void CaptureReturnPositionIfNeeded(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                if (!ZoneConfigService.ShouldReturnOnExit(zoneId))
                {
                    return;
                }

                var platformId = ResolvePlatformId(player, em);
                if (platformId == 0)
                {
                    Logger.LogDebug($"[BlueLock] Return position capture skipped: could not resolve platform id for entity {player.Index}:{player.Version}");
                    return;
                }

                if (_zoneReturnPositions.ContainsKey(platformId))
                {
                    return;
                }

                if (!TryGetBestPosition(em, player, out var pos))
                {
                    return;
                }

                if (!IsValidReturnPosition(pos))
                {
                    return;
                }

                const int MaxStoredPositions = 1000;
                if (_zoneReturnPositions.Count >= MaxStoredPositions)
                {
                    var oldestPlatformId = _zoneReturnPositions.Keys.First();
                    _zoneReturnPositions.Remove(oldestPlatformId);
                    Logger.LogDebug($"[BlueLock] Evicted return position for platform {oldestPlatformId}");
                }

                _zoneReturnPositions[platformId] = pos;
                Logger.LogDebug($"[BlueLock] Captured return position for platform {platformId}: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] CaptureReturnPositionIfNeeded failed: {ex.Message}");
            }
        }

        private static bool IsValidReturnPosition(float3 pos)
        {
            // Reject near-zero so we never return players to (0,0,0).
            return !(Math.Abs(pos.x) < 0.5f && Math.Abs(pos.z) < 0.5f);
        }

        private static bool IsNearZeroXZ(float3 pos)
        {
            return Math.Abs(pos.x) < 0.5f && Math.Abs(pos.z) < 0.5f;
        }

        private static void TryTeleportWithFallback(Entity player, float3 targetPos, EntityManager em)
        {
            if (GameActionService.TryTeleport(player, targetPos))
            {
                return;
            }

            if (em.HasComponent<LocalTransform>(player))
            {
                var t = em.GetComponentData<LocalTransform>(player);
                t.Position = targetPos;
                em.SetComponentData(player, t);
            }
            else if (em.HasComponent<Translation>(player))
            {
                var t = em.GetComponentData<Translation>(player);
                t.Value = targetPos;
                em.SetComponentData(player, t);
            }
        }

        private static bool TryGetBestPosition(EntityManager em, Entity entity, out float3 pos)
        {
            pos = default;
            try
            {
                if (em.HasComponent<LocalTransform>(entity))
                {
                    pos = em.GetComponentData<LocalTransform>(entity).Position;
                    return true;
                }

                if (em.HasComponent<Translation>(entity))
                {
                    pos = em.GetComponentData<Translation>(entity).Value;
                    return true;
                }

                if (em.HasComponent<LastTranslation>(entity))
                {
                    pos = em.GetComponentData<LastTranslation>(entity).Value;
                    return true;
                }

                if (em.HasComponent<SpawnTransform>(entity))
                {
                    pos = em.GetComponentData<SpawnTransform>(entity).Position;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private static EntityQuery GetOrCreateAutoZonePlayerQuery(EntityManager em)
        {
            if (!_autoZonePlayerQueryInitialized)
            {
                _autoZonePlayerQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerCharacter>());
                _autoZonePlayerQueryInitialized = true;
            }

            return _autoZonePlayerQuery;
        }

        private static void TryRunZoneEnterStep(string stepName, Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] ZoneEnter step '{stepName}' failed: {ex}");
            }
        }

        private static void TryRunZoneExitStep(string stepName, Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] ZoneExit step '{stepName}' failed: {ex}");
            }
        }

        private static void SendZoneEnterSystemMessage(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                // Use try-catch instead of HasComponent to avoid Unity generic method caching issues
                PlayerCharacter playerChar;
                try
                {
                    playerChar = em.GetComponentData<PlayerCharacter>(player);
                }
                catch
                {
                    return; // Player doesn't have PlayerCharacter component
                }

                var userEntity = playerChar.UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity))
                {
                    return;
                }

                var configured = ZoneConfigService.GetEnterMessageForZone(zoneId);
                var messageText = string.IsNullOrWhiteSpace(configured) ? $"Welcome to zone {zoneId}" : configured;
                _ = GameActionService.TrySendSystemMessageToUserEntity(userEntity, messageText);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Failed to send zone enter message: {ex.Message}");
            }
        }

        private static void ApplyZoneTemplatesOnEnter(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                if (!em.HasComponent<PlayerCharacter>(player))
                {
                    return;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(player).UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity))
                {
                    return;
                }

                var templates = ZoneConfigService.GetBuildTemplatesForZone(zoneId);
                if (templates == null || templates.Count == 0)
                {
                    return;
                }

                foreach (var template in templates)
                {
                    var isSchematic = ZoneSchematicLoader.TryGetSchematicPath(template) != null ||
                                      template.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase);

                    if (isSchematic)
                    {
                        var schematicResult = SchematicZoneService.ApplySchematicOnEnter(player, zoneId, template, em);
                        if (!schematicResult.Success)
                        {
                            Logger.LogWarning($"[BlueLock] Zone '{zoneId}' schematic spawn failed: {schematicResult.Error}");
                        }
                        else
                        {
                            Logger.LogInfo($"[BlueLock] Zone '{zoneId}' applied schematic ({schematicResult.EntityCount} entities)");
                        }
                    }
                    else
                    {
                        // Template placement can trigger spawn-chain side effects; skip InitializeNewSpawnChainSystem once to reduce racey transitions.
                        // This mirrors common KindredSchematics mitigation patterns.
                        Patches.SkipInitializeNewSpawnChainOnce = true;
                        var zone = ZoneConfigService.GetZoneById(zoneId);
                        var origin = zone != null
                            ? new float3(zone.CenterX, zone.CenterY, zone.CenterZ)
                            : float3.zero;
                        var result = BuildingService.Instance.LoadTemplate(template, em, origin, 0f);
                        if (!result.Success)
                        {
                            Logger.LogWarning($"[BlueLock] Zone '{zoneId}' template '{template}' failed: {result.Error}");
                        }
                        else
                        {
                            Logger.LogInfo($"[BlueLock] Zone '{zoneId}' applied template '{template}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Failed to apply zone templates for '{zoneId}': {ex.Message}");
            }
        }

        private static void HandleZoneExit(Entity player, string zoneId)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(player))
                {
                    return;
                }

                var actionOrder = ResolveLifecycleActionsForZone(zoneId, isEnter: false);
                Logger.LogDebug($"[BlueLock] Zone '{zoneId}' exit actions: {string.Join(", ", actionOrder)}");
                var context = new PluginZoneLifecycleContext(player, zoneId, em);
                foreach (var step in _zoneLifecycleStepRegistry.BuildExitSteps(actionOrder))
                {
                    step.Execute(context);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] HandleZoneExit failed: {ex.Message}");
            }
        }

        private static IReadOnlyList<string> ResolveLifecycleActionsForZone(string zoneId, bool isEnter)
        {
            var defaults = isEnter ? DefaultEnterLifecycleActions : DefaultExitLifecycleActions;
            if (_jsonConfig == null || !_jsonConfig.Enabled || _jsonConfig.Mappings == null || _jsonConfig.Mappings.Count == 0)
            {
                return defaults;
            }

            if (!TryGetLifecycleMappingForZone(zoneId, out var mapping))
            {
                return defaults;
            }

            var configured = isEnter ? mapping.OnEnter : mapping.OnExit;
            if (configured == null || configured.Length == 0)
            {
                return mapping.UseGlobalDefaults ? defaults : defaults;
            }

            var resolved = new List<string>();
            if (mapping.UseGlobalDefaults)
            {
                resolved.AddRange(defaults);
            }

            foreach (var token in configured)
            {
                var normalized = NormalizeLifecycleActionToken(token, isEnter);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!resolved.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    resolved.Add(normalized);
                }
            }

            return resolved.Count > 0 ? resolved : defaults;
        }

        private static bool TryGetLifecycleMappingForZone(string zoneId, out ZoneMapping mapping)
        {
            mapping = null;
            if (_jsonConfig?.Mappings == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(zoneId) && _jsonConfig.Mappings.TryGetValue(zoneId, out mapping))
            {
                return true;
            }

            if (_jsonConfig.Mappings.TryGetValue("*", out mapping))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeLifecycleActionToken(string rawToken, bool isEnter)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return string.Empty;
            }

            var token = rawToken.Trim().ToLowerInvariant().Replace('-', '_');
            if (isEnter)
            {
                return token switch
                {
                    "store" => "snapshot_save",
                    "snapshot" => "snapshot_save",
                    "message" => "zone_enter_message",
                    "kit" => "apply_kit",
                    "abilities" => "apply_abilities",
                    "ability" => "apply_abilities",
                    "glow" => "glow_spawn",
                    "teleport" => "teleport_enter",
                    "templates" => "apply_templates",
                    "integration" => "integration_events_enter",
                    "announce" => "announce_enter",
                    _ => token
                };
            }

            return token switch
            {
                "restore" => "restore_kit_snapshot",
                "message" => "zone_exit_message",
                "abilities" => "restore_abilities",
                "ability" => "restore_abilities",
                "glow" => "glow_reset",
                "teleport" => "teleport_return",
                "integration" => "integration_events_exit",
                "announce" => "announce_exit",
                _ => token
            };
        }

        private static void ExecuteEnterLifecycleAction(string action, Entity player, string zoneId, EntityManager em)
        {
            switch (action)
            {
                case "capture_return_position":
                    TryRunZoneEnterStep("CaptureReturnPositionIfNeeded", () => CaptureReturnPositionIfNeeded(player, zoneId, em));
                    break;
                case "snapshot_save":
                    TryRunZoneEnterStep("PlayerSnapshotService.SaveSnapshot", () =>
                    {
                        if (!PlayerSnapshotService.SaveSnapshot(player, out var snapshotError))
                        {
                            Logger.LogDebug($"[BlueLock] Snapshot save skipped/failed on enter for zone '{zoneId}': {snapshotError}");
                        }
                    });
                    break;
                case "zone_enter_message":
                    TryRunZoneEnterStep("SendZoneEnterSystemMessage", () => SendZoneEnterSystemMessage(player, zoneId, em));
                    break;
                case "apply_kit":
                    if (KitAutoEquipEnabledValue)
                    {
                        TryRunZoneEnterStep("KitService.ApplyKitOnEnter", () => KitService.ApplyKitOnEnter(zoneId, player, em));
                    }
                    break;
                case "teleport_enter":
                    TryRunZoneEnterStep("HandleZoneTeleportEnter", () => HandleZoneTeleportEnter(player, zoneId, em));
                    break;
                case "apply_templates":
                    TryRunZoneEnterStep("ApplyZoneTemplatesOnEnter", () => ApplyZoneTemplatesOnEnter(player, zoneId, em));
                    break;
                case "apply_abilities":
                    TryRunZoneEnterStep("AbilityUi.OnZoneEnter", () => AbilityUi.OnZoneEnter(player, zoneId));
                    break;
                case "glow_spawn":
                    TryRunZoneEnterStep("GlowTileService.AutoSpawn", () =>
                    {
                        var playersInZone = GetPlayersInZoneCount(zoneId);
                        if (!ZoneConfigService.ShouldAutoSpawnGlowOnEnter(zoneId) || playersInZone != 0)
                        {
                            return;
                        }

                        var zone = ZoneConfigService.GetZoneById(zoneId);
                        if (zone == null)
                        {
                            return;
                        }

                        if (_zoneGlowAutoSpawnDisabled.Contains(zoneId))
                        {
                            Logger.LogDebug($"[BlueLock] Glow auto spawn skipped for zone '{zoneId}' due to unresolved prefab configuration (awaiting config reload).");
                            return;
                        }

                        GlowTileService.PrepareForZoneActivation(zoneId, em);
                        var spawn = GlowTileService.TryAutoSpawnGlowTiles(zone, em);
                        if (!spawn.Success)
                        {
                            if (!string.IsNullOrWhiteSpace(spawn.Error) &&
                                spawn.Error.IndexOf("not configured or unavailable", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _zoneGlowAutoSpawnDisabled.Add(zoneId);
                                Logger.LogWarning($"[BlueLock] Glow auto spawn disabled for zone '{zoneId}' until config reload: {spawn.Error}");
                                return;
                            }

                            Logger.LogWarning($"[BlueLock] Glow auto spawn failed for zone '{zoneId}': {spawn.Error}");
                            return;
                        }

                        _zoneGlowAutoSpawnDisabled.Remove(zoneId);
                    });
                    break;
                case "boss_enter":
                    TryRunZoneEnterStep("ZoneBossSpawnerService.TryHandlePlayerEnter", () =>
                    {
                        if (ZoneBossSpawnerService.TryHandlePlayerEnter(player, zoneId, out var bossSpawnMessage))
                        {
                            Logger.LogInfo($"[BlueLock] {bossSpawnMessage}");
                        }
                    });
                    break;
                case "integration_events_enter":
                    TryRunZoneEnterStep("LifecycleManager.OnPlayerEntered", () => _arenaLifecycleManager?.OnPlayerEntered(player, zoneId));
                    if (IntegrationLifecycleEnabledValue)
                    {
                        TryRunZoneEnterStep("LifecycleManager.OnPlayerEnter", () => TryInvokeLifecycleManager("OnPlayerEnter", player, zoneId));
                        TryRunZoneEnterStep("CoreLifecycle.PlayerEnter", () => TryTriggerCoreLifecycleEvent(GameActionService.EventPlayerEnter, player, zoneId, em));
                    }
                    if (IsSandboxZone(zoneId))
                    {
                        TryRunZoneEnterStep("DebugEventBridge.OnZoneEnterStart", () => VAuto.Core.Services.DebugEventBridge.OnZoneEnterStart(player, zoneId, ZoneConfigService.IsSandboxUnlockEnabled(zoneId, SandboxProgressionDefaultZoneUnlockEnabledValue)));
                        TryRunZoneEnterStep("DebugEventBridge.OnPlayerEnterZone", () => VAuto.Core.Services.DebugEventBridge.OnPlayerEnterZone(player, ZoneConfigService.IsSandboxUnlockEnabled(zoneId, SandboxProgressionDefaultZoneUnlockEnabledValue)));
                    }
                    break;
                case "announce_enter":
                    TryRunZoneEnterStep("TryInvokeAnnouncementZoneEnter", () => TryInvokeAnnouncementZoneEnter(player, zoneId, em));
                    break;
                default:
                    Logger.LogWarning($"[BlueLock] Unknown enter lifecycle action '{action}' for zone '{zoneId}'.");
                    break;
            }
        }

        private static void ExecuteExitLifecycleAction(string action, Entity player, string zoneId, EntityManager em)
        {
            switch (action)
            {
                case "zone_exit_message":
                    TryRunZoneExitStep("SendZoneExitSystemMessage", () => SendZoneExitSystemMessage(player, zoneId, em));
                    break;
                case "restore_kit_snapshot":
                    if (KitRestoreOnExitValue)
                    {
                        TryRunZoneExitStep("KitService.RestoreKitOnExit", () => KitService.RestoreKitOnExit(zoneId, player, em));
                    }
                    break;
                case "restore_abilities":
                    TryRunZoneExitStep("AbilityUi.OnZoneExit", () => AbilityUi.OnZoneExit(player, zoneId));
                    break;
                case "boss_exit":
                    TryRunZoneExitStep("ZoneBossSpawnerService.HandlePlayerExit", () => ZoneBossSpawnerService.HandlePlayerExit(player, zoneId));
                    break;
                case "teleport_return":
                    TryRunZoneExitStep("HandleZoneTeleportExit", () => HandleZoneTeleportExit(player, zoneId, em));
                    break;
                case "glow_reset":
                    TryRunZoneExitStep("GlowTileService.ClearZoneGlow", () =>
                    {
                        var playersInZone = GetPlayersInZoneCount(zoneId);
                        if (ZoneConfigService.ShouldAutoSpawnGlowOnReset(zoneId) && playersInZone <= 1)
                        {
                            GlowTileService.ClearZoneGlow(zoneId, em);
                        }
                    });
                    break;
                case "integration_events_exit":
                    TryRunZoneExitStep("LifecycleManager.OnPlayerExited", () => _arenaLifecycleManager?.OnPlayerExited(player, zoneId));
                    if (IntegrationLifecycleEnabledValue)
                    {
                        TryRunZoneExitStep("LifecycleManager.OnPlayerExit", () => TryInvokeLifecycleManager("OnPlayerExit", player, zoneId));
                        TryRunZoneExitStep("CoreLifecycle.PlayerExit", () => TryTriggerCoreLifecycleEvent(GameActionService.EventPlayerExit, player, zoneId, em));
                    }
                    if (IsSandboxZone(zoneId))
                    {
                        TryRunZoneExitStep("DebugEventBridge.OnPlayerExitZone", () => VAuto.Core.Services.DebugEventBridge.OnPlayerExitZone(player, ZoneConfigService.IsSandboxUnlockEnabled(zoneId, SandboxProgressionDefaultZoneUnlockEnabledValue)));
                    }
                    break;
                case "announce_exit":
                    // Reserved for explicit exit announcements (not currently implemented in AnnouncementService).
                    break;
                default:
                    Logger.LogWarning($"[BlueLock] Unknown exit lifecycle action '{action}' for zone '{zoneId}'.");
                    break;
            }
        }

        private static void SendZoneExitSystemMessage(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                // Use try-catch instead of HasComponent to avoid Unity generic method caching issues
                PlayerCharacter playerChar;
                try
                {
                    playerChar = em.GetComponentData<PlayerCharacter>(player);
                }
                catch
                {
                    return; // Player doesn't have PlayerCharacter component
                }

                var userEntity = playerChar.UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity))
                {
                    return;
                }

                var configured = ZoneConfigService.GetExitMessageForZone(zoneId);
                var messageText = string.IsNullOrWhiteSpace(configured) ? "You left event" : configured;
                _ = GameActionService.TrySendSystemMessageToUserEntity(userEntity, messageText);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Failed to send zone exit message: {ex.Message}");
            }
        }

        private static void TryInvokeLifecycleManager(string methodName, Entity characterEntity, string zoneId)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(characterEntity) || !em.HasComponent<PlayerCharacter>(characterEntity))
                {
                    return;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(characterEntity).UserEntity;
                if (userEntity == Entity.Null)
                {
                    return;
                }

                var position = GetEntityPosition(em, characterEntity);

                var lifecycleType = ResolveLifecycleType("VAuto.Core.Lifecycle.ArenaLifecycleManager");
                if (lifecycleType == null)
                {
                    Logger.LogDebug($"[BlueLock] Lifecycle manager type not found for method '{methodName}'. Checked assemblies: {string.Join(", ", LifecycleAssemblyNames)}");
                    return;
                }

                var instanceProp = lifecycleType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null)
                {
                    Logger.LogWarning($"[BlueLock] Lifecycle type '{lifecycleType.Name}' has no Instance property");
                    return;
                }

                var lifecycleInstance = instanceProp.GetValue(null);
                if (lifecycleInstance == null)
                {
                    Logger.LogWarning($"[BlueLock] Lifecycle Instance property returned null");
                    return;
                }

                if (!TryInvokeLifecycleMethod(lifecycleType, lifecycleInstance, methodName, userEntity, characterEntity, zoneId, position))
                {
                    Logger.LogWarning($"[BlueLock] Lifecycle method '{methodName}' not found or failed on type '{lifecycleType.Name}'");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Lifecycle reflection invoke failed ({methodName}): {ex.Message}");
            }
        }

        private static bool TryInvokeLifecycleMethod(Type lifecycleType, object lifecycleInstance, string methodName, Entity userEntity, Entity characterEntity, string zoneId, float3 position)
        {
            var candidates = new[]
            {
                new object[] { userEntity, characterEntity, zoneId, position },
                new object[] { userEntity, characterEntity, zoneId },
                new object[] { characterEntity, zoneId, position },
                new object[] { characterEntity, zoneId }
            };

            foreach (var args in candidates)
            {
                var argTypes = args.Select(a => a.GetType()).ToArray();
                var method = lifecycleType.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    argTypes,
                    null);

                if (method == null)
                {
                    continue;
                }

                try
                {
                    method.Invoke(lifecycleInstance, args);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"[BlueLock] Lifecycle method '{methodName}' invoke failed for signature ({string.Join(", ", argTypes.Select(t => t.Name))}): {ex.Message}");
                }
            }

            return false;
        }

        private static float3 GetEntityPosition(EntityManager em, Entity entity)
        {
            try
            {
                if (em.HasComponent<LocalTransform>(entity))
                {
                    return em.GetComponentData<LocalTransform>(entity).Position;
                }

                if (em.HasComponent<Translation>(entity))
                {
                    return em.GetComponentData<Translation>(entity).Value;
                }
            }
            catch
            {
                // Position is best-effort only for lifecycle context.
            }

            return float3.zero;
        }

        private static void TryTriggerCoreLifecycleEvent(string eventName, Entity characterEntity, string zoneId, EntityManager em)
        {
            try
            {
                var fired = GameActionService.TriggerEvent(eventName, characterEntity, zoneId);
                Logger.LogDebug($"[BlueLock] Core lifecycle event '{eventName}' fired {fired} action(s) for zone '{zoneId}'.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Core lifecycle event '{eventName}' failed for zone '{zoneId}': {ex.Message}");
            }
        }

        private static void TryInvokeAnnouncementZoneEnter(Entity characterEntity, string zoneId, EntityManager em)
        {
            try
            {
                var playerName = ResolvePlayerName(characterEntity, em);
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    return;
                }

                var announceType = ResolveLifecycleType("VLifecycle.Services.Lifecycle.AnnouncementService");
                if (announceType == null)
                {
                    return;
                }

                var method = announceType.GetMethod(
                    "ZoneEnter",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null);

                method?.Invoke(null, new object[] { zoneId, playerName });

                if (IsPveEventZone(zoneId))
                {
                    var coopMethod = announceType.GetMethod(
                        "PveBossCoopCall",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);
                    coopMethod?.Invoke(null, new object[] { zoneId, playerName });

                    var scoreType = ResolveLifecycleType("VLifecycle.Services.Lifecycle.ScoreService");
                    var platformId = ResolvePlatformId(characterEntity, em);
                    if (scoreType != null && platformId != 0)
                    {
                        var scoreMethod = scoreType.GetMethod(
                            "OnCoopEventJoin",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { typeof(ulong), typeof(string) },
                            null);
                        scoreMethod?.Invoke(null, new object[] { platformId, zoneId });
                    }
                }

                if (IsPvpZone(zoneId))
                {
                    var pvpMethod = announceType.GetMethod(
                        "PvpFightCall",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);
                    pvpMethod?.Invoke(null, new object[] { zoneId, playerName });
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Announcement reflection invoke failed (ZoneEnter): {ex.Message}");
            }
        }

        private static string ResolvePlayerName(Entity characterEntity, EntityManager em)
        {
            try
            {
                if (!em.Exists(characterEntity) || !em.HasComponent<PlayerCharacter>(characterEntity))
                {
                    return string.Empty;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(characterEntity).UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return string.Empty;
                }

                var user = em.GetComponentData<User>(userEntity);
                return user.CharacterName.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static ulong ResolvePlatformId(Entity characterEntity, EntityManager em)
        {
            try
            {
                if (!em.Exists(characterEntity) || !em.HasComponent<PlayerCharacter>(characterEntity))
                {
                    return 0;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(characterEntity).UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return 0;
                }

                return em.GetComponentData<User>(userEntity).PlatformId;
            }
            catch
            {
                return 0;
            }
        }

        private static ulong ResolvePlatformId(Entity characterEntity)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                return ResolvePlatformId(characterEntity, em);
            }
            catch
            {
                return 0;
            }
        }

        private static bool IsPveEventZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            var id = zoneId.ToLowerInvariant();
            return id.Contains("pve") || id.Contains("boss") || id.Contains("event");
        }

        private static bool IsPvpZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            return zoneId.ToLowerInvariant().Contains("pvp");
        }

        private static bool IsSandboxZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            return ZoneConfigService.HasTag(zoneId, "sandbox");
        }

        private static Type ResolveLifecycleType(string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName))
            {
                return null;
            }

            if (LifecycleTypeCache.TryGetValue(fullTypeName, out var cached))
            {
                return cached;
            }

            foreach (var assemblyName in LifecycleAssemblyNames)
            {
                var type = Type.GetType($"{fullTypeName}, {assemblyName}", throwOnError: false);
                if (type != null)
                {
                    LifecycleTypeCache[fullTypeName] = type;
                    return type;
                }
            }

            return null;
        }

        private static void TryInvokeDebugEventBridgeZoneEnterStart(Entity characterEntity, string zoneId)
        {
            // Disabled to prevent duplicate DebugEventBridge.OnZoneEnterStart execution.
            // The typed invocation is performed later in the integration enter step
            // (see: TryRunZoneEnterStep within the lifecycle integration pipeline).
            // Keeping this as a no-op preserves binary compatibility.
            return;

#if false
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(characterEntity))
                {
                    return;
                }

                var bridgeType = Type.GetType("VAuto.Core.Services.DebugEventBridge, VAutomationCore");
                if (bridgeType == null)
                {
                    Logger.LogDebug($"[BlueLock] DebugEventBridge type not found for OnZoneEnterStart (zone='{zoneId}')");
                    return;
                }

                var zoneUnlockEnabled = ZoneConfigService.IsSandboxUnlockEnabled(zoneId, SandboxProgressionDefaultZoneUnlockEnabledValue);

                var method = bridgeType.GetMethod(
                    "OnZoneEnterStart",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity), typeof(string), typeof(bool) },
                    null);

                if (method != null)
                {
                    Logger.LogDebug($"[BlueLock] Invoking DebugEventBridge.OnZoneEnterStart(entity={characterEntity.Index}:{characterEntity.Version}, zone='{zoneId}', enableUnlock={zoneUnlockEnabled})");
                    method.Invoke(null, new object[] { characterEntity, zoneId, zoneUnlockEnabled });
                    return;
                }

                method = bridgeType.GetMethod(
                    "OnZoneEnterStart",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity), typeof(string) },
                    null);

                if (method != null)
                {
                    Logger.LogDebug($"[BlueLock] Invoking DebugEventBridge.OnZoneEnterStart(entity={characterEntity.Index}:{characterEntity.Version}, zone='{zoneId}')");
                    method.Invoke(null, new object[] { characterEntity, zoneId });
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] DebugEventBridge reflection invoke failed (OnZoneEnterStart, zone='{zoneId}'): {ex}");
            }
#endif
        }

        private static void TryInvokeDebugEventBridge(bool isEnter, Entity characterEntity, string zoneId)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(characterEntity))
                {
                    return;
                }

                var bridgeType = Type.GetType("VAuto.Core.Services.DebugEventBridge, VAutomationCore");
                if (bridgeType == null)
                {
                    Logger.LogDebug($"[BlueLock] DebugEventBridge type not found for zone '{zoneId}' ({(isEnter ? "enter" : "exit")})");
                    return;
                }

                var methodName = isEnter ? "OnPlayerEnterZone" : "OnPlayerExitZone";
                var zoneUnlockEnabled = ZoneConfigService.IsSandboxUnlockEnabled(zoneId, SandboxProgressionDefaultZoneUnlockEnabledValue);
                var method = isEnter
                    ? bridgeType.GetMethod("OnPlayerEnterZone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity), typeof(bool) }, null)
                    : bridgeType.GetMethod("OnPlayerExitZone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity), typeof(bool) }, null);

                if (method != null)
                {
                    Logger.LogDebug($"[BlueLock] Invoking DebugEventBridge.{methodName}(entity={characterEntity.Index}:{characterEntity.Version}, enableUnlock={zoneUnlockEnabled}) for zone '{zoneId}'");
                    method.Invoke(null, new object[] { characterEntity, zoneUnlockEnabled });
                    Logger.LogInfo($"[BlueLock] DebugEventBridge.{methodName} completed for zone '{zoneId}'");
                    return;
                }

                method = isEnter
                    ? bridgeType.GetMethod("OnPlayerEnterZone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null)
                    : bridgeType.GetMethod("OnPlayerExitZone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);

                if (method == null)
                {
                    Logger.LogDebug($"[BlueLock] DebugEventBridge.{methodName} method not found for zone '{zoneId}'");
                    return;
                }

                Logger.LogDebug($"[BlueLock] Invoking DebugEventBridge.{methodName}(entity={characterEntity.Index}:{characterEntity.Version}) for zone '{zoneId}'");
                method.Invoke(null, new object[] { characterEntity });
                Logger.LogInfo($"[BlueLock] DebugEventBridge.{methodName} completed for zone '{zoneId}'");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] DebugEventBridge reflection invoke failed for zone '{zoneId}': {ex.ToString()}");
            }
        }

        private static void TryInvokeDebugEventBridgeInZone(Entity characterEntity, string zoneId)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(characterEntity))
                {
                    return;
                }

                var bridgeType = Type.GetType("VAuto.Core.Services.DebugEventBridge, VAutomationCore");
                if (bridgeType == null)
                {
                    Logger.LogDebug($"[BlueLock] DebugEventBridge type not found for OnPlayerIsInZone");
                    return;
                }

                var zoneUnlockEnabled = ZoneConfigService.IsSandboxUnlockEnabled(zoneId, SandboxProgressionDefaultZoneUnlockEnabledValue);
                var method = bridgeType.GetMethod(
                    "OnPlayerIsInZone",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity), typeof(string), typeof(bool) },
                    null);

                if (method == null)
                {
                    Logger.LogDebug($"[BlueLock] DebugEventBridge.OnPlayerIsInZone method not found");
                    return;
                }

                Logger.LogDebug($"[BlueLock] Invoking DebugEventBridge.OnPlayerIsInZone(entity={characterEntity.Index}:{characterEntity.Version}, zone='{zoneId}', enableUnlock={zoneUnlockEnabled})");
                method.Invoke(null, new object[] { characterEntity, zoneId, zoneUnlockEnabled });
                Logger.LogInfo($"[BlueLock] DebugEventBridge.OnPlayerIsInZone completed");

                // Post-unlock ability apply: execute after DebugEventBridge unlock pipeline.
                TryRunZoneEnterStep("AbilityUi.TryApplyPresetForPlayer", () => AbilityUi.TryApplyPresetForPlayer(characterEntity));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] DebugEventBridge reflection invoke failed (OnPlayerIsInZone): {ex.ToString()}");
            }
        }

        private static void HandleZoneTeleportEnter(Entity player, string zoneId, EntityManager em)
        {
            if (!ZoneConfigService.TryGetTeleportPointForZone(zoneId, out var tx, out var ty, out var tz))
            {
                return;
            }

            try
            {
                var targetPos = new float3(tx, ty, tz);
                if (IsNearZeroXZ(targetPos))
                {
                    Logger.LogWarning($"[BlueLock] Zone enter teleport skipped: invalid/zero target ({targetPos.x:F1},{targetPos.y:F1},{targetPos.z:F1}) for zone '{zoneId}'.");
                    return;
                }

                // If return-on-exit is enabled and we couldn't capture a return position yet, defer by one tick.
                // This avoids timing races where the player's transform isn't readable at enter time.
                if (ZoneConfigService.ShouldReturnOnExit(zoneId))
                {
                    var platformId = ResolvePlatformId(player, em);
                    if (platformId != 0 && !_zoneReturnPositions.ContainsKey(platformId))
                    {
                        CaptureReturnPositionIfNeeded(player, zoneId, em);
                        if (!_zoneReturnPositions.ContainsKey(platformId))
                        {
                            _pendingZoneEnterTeleports[platformId] = new PendingZoneTeleport
                            {
                                Player = player,
                                ZoneId = zoneId,
                                TargetPos = targetPos,
                                Attempts = 1
                            };
                            Logger.LogDebug($"[BlueLock] Zone enter teleport deferred one tick (return position not captured yet) for platform {platformId} zone '{zoneId}'.");
                            return;
                        }
                    }
                }

                TryTeleportWithFallback(player, targetPos, em);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Zone enter teleport failed ({zoneId}): {ex.Message}");
            }
        }

        private static void HandleZoneTeleportExit(Entity player, string zoneId, EntityManager em)
        {
            if (!ZoneConfigService.ShouldReturnOnExit(zoneId))
            {
                return;
            }

            try
            {
                var platformId = ResolvePlatformId(player, em);
                if (platformId == 0)
                {
                    Logger.LogDebug($"[BlueLock] Return teleport skipped: could not resolve platform id for entity {player.Index}:{player.Version}");
                    return;
                }

                if (_zoneReturnPositions.TryGetValue(platformId, out var returnPos))
                {
                    if (!IsValidReturnPosition(returnPos))
                    {
                        Logger.LogWarning($"[BlueLock] Return teleport skipped: invalid/zero stored return position for platform {platformId} exiting zone '{zoneId}'.");
                        _zoneReturnPositions.Remove(platformId);
                        return;
                    }

                    TryTeleportWithFallback(player, returnPos, em);
                }
                else
                {
                    // No valid return position captured; skip instead of teleporting to (0,0,0).
                    Logger.LogDebug($"[BlueLock] No stored return position for platform {platformId} exiting zone '{zoneId}'; skipping return.");
                }

                _zoneReturnPositions.Remove(platformId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Zone exit return teleport failed ({zoneId}): {ex.Message}");
            }
        }

        public static bool ForcePlayerEnterZone(Entity player, string zoneId = "")
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(player))
                {
                    return false;
                }

                var resolvedZoneId = string.IsNullOrWhiteSpace(zoneId)
                    ? ZoneConfigService.GetDefaultZoneId()
                    : zoneId;
                if (string.IsNullOrWhiteSpace(resolvedZoneId))
                {
                    return false;
                }

                var zone = ZoneConfigService.GetZoneById(resolvedZoneId);
                if (zone == null)
                {
                    return false;
                }

                _pendingZoneTransitions.Remove(player);
                _lastCommittedZoneTransitions.Remove(player);
                CaptureReturnPositionIfNeeded(player, zone.Id, em);

                if (_playerZoneStates.TryGetValue(player, out var previousZoneId) &&
                    !string.IsNullOrWhiteSpace(previousZoneId) &&
                    !string.Equals(previousZoneId, zone.Id, StringComparison.OrdinalIgnoreCase))
                {
                    HandleZoneExit(player, previousZoneId);
                }

                if (!zone.TeleportOnEnter)
                {
                    TryTeleportPlayerToZoneCenter(player, zone.Id, em);
                }
                HandleZoneEnter(player, zone.Id);
                _playerZoneStates[player] = zone.Id;
                _lastCommittedZoneTransitions[player] = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] ForcePlayerEnterZone failed: {ex.Message}");
                return false;
            }
        }

        public static bool ForcePlayerExitZone(Entity player)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(player))
                {
                    return false;
                }

                string zoneId = string.Empty;
                if (_playerZoneStates.TryGetValue(player, out var tracked))
                {
                    zoneId = tracked ?? string.Empty;
                }
                else
                {
                    var state = VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(player);
                    zoneId = state?.CurrentZoneId ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    return false;
                }

                _pendingZoneTransitions.Remove(player);
                _lastCommittedZoneTransitions.Remove(player);
                HandleZoneExit(player, zoneId);
                _playerZoneStates.Remove(player);
                ZoneEventBridge.RemovePlayerState(player);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] ForcePlayerExitZone failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryTeleportPlayerToZoneCenter(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                var zone = ZoneConfigService.GetZoneById(zoneId);
                if (zone == null)
                {
                    return false;
                }

                var y = 0f;
                if (em.HasComponent<LocalTransform>(player))
                {
                    var transform = em.GetComponentData<LocalTransform>(player);
                    y = transform.Position.y;
                    var target = new float3(zone.CenterX, y, zone.CenterZ);
                    if (GameActionService.TryTeleport(player, target))
                    {
                        return true;
                    }

                    transform.Position = target;
                    em.SetComponentData(player, transform);
                    return true;
                }

                if (em.HasComponent<Translation>(player))
                {
                    var translation = em.GetComponentData<Translation>(player);
                    y = translation.Value.y;
                    var target = new float3(zone.CenterX, y, zone.CenterZ);
                    if (GameActionService.TrySetEntityPosition(player, target))
                    {
                        return true;
                    }

                    translation.Value = target;
                    em.SetComponentData(player, translation);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Teleport to zone center failed ({zoneId}): {ex.Message}");
            }

            return false;
        }

        private static DateTime _lastArenaCleanupTime;

        [HarmonyPatch(typeof(ProjectM.BacktraceSystem), nameof(ProjectM.BacktraceSystem.OnUpdate))]
        [HarmonyPostfix]
        public static void BacktraceSystem_OnUpdate_Postfix()
        {
            try
            {
                ApplyArenaDamageToPlayers();

                // Periodic cleanup of expired arena death records (every 5 seconds)
                var now = DateTime.UtcNow;
                if ((now - _lastArenaCleanupTime).TotalSeconds > 5.0)
                {
                    ArenaDeathTracker.CleanupExpired();
                    _lastArenaCleanupTime = now;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Arena damage postfix failed: {ex.Message}");
            }
        }

        private static void ApplyArenaDamageToPlayers()
        {
            try
            {
                var server = ZoneCore.Server;
                if (server == null || !server.IsCreated)
                    return;

                var em = server.EntityManager;
                if (em == default)
                    return;

                var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<User>()
                );

                try
                {
                    var playerEntities = query.ToEntityArray(Allocator.Temp);

                    var now = DateTime.UtcNow.Ticks / 10000000.0;  // Convert to seconds

                    foreach (var playerEntity in playerEntities)
                    {
                        try
                        {
                            if (!em.Exists(playerEntity))
                                continue;

                            // Get zone state
                            var zoneState = VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(playerEntity);
                            if (zoneState == null || string.IsNullOrWhiteSpace(zoneState.CurrentZoneId))
                                continue;

                            // Resolve zone config
                            var zoneConfig = ZoneConfigService.GetZoneById(zoneState.CurrentZoneId);
                            if (zoneConfig == null || !zoneConfig.IsArenaZone)
                                continue;

                            // Check for existing damage state
                            if (!em.HasComponent<ArenaDamageState>(playerEntity))
                            {
                                // Initialize damage state for this player (skip if holder)
                                if (!IsPlayerHolder(playerEntity, zoneConfig))
                                {
                                    em.AddComponentData(playerEntity, new ArenaDamageState
                                    {
                                        CurrentDamage = 20f,
                                        InitialDamage = 20f,
                                        LastTickTime = now,
                            ZoneIdHash = ArenaMatchUtilities.StableHash(zoneState.CurrentZoneId)
                                    });
                                }
                                continue;
                            }

                            var damageState = em.GetComponentData<ArenaDamageState>(playerEntity);

                            // Check zone hasn't changed
                            if (damageState.ZoneIdHash != ArenaMatchUtilities.StableHash(zoneState.CurrentZoneId))
                            {
                                em.RemoveComponent<ArenaDamageState>(playerEntity);
                                continue;
                            }

                            // Check if tick interval elapsed
                            if ((now - damageState.LastTickTime) < 1.0)
                                continue;

                            // Skip holder (has no damage state for them anyway)
                            if (IsPlayerHolder(playerEntity, zoneConfig))
                            {
                                em.RemoveComponent<ArenaDamageState>(playerEntity);
                                continue;
                            }

                            // Apply damage
                            if (em.HasComponent<Health>(playerEntity))
                            {
                                var health = em.GetComponentData<Health>(playerEntity);
                                health.Value -= (int)damageState.CurrentDamage;
                                em.SetComponentData(playerEntity, health);

                                // Register arena death if lethal
                                if (health.Value <= 0)
                                {
                                    ArenaDeathTracker.RegisterArenaDeath(playerEntity, zoneState.CurrentZoneId);
                                }
                            }

                            // Update damage (apply decay)
                            damageState.CurrentDamage *= 0.85f;
                            damageState.LastTickTime = now;
                            em.SetComponentData(playerEntity, damageState);
                        }
                        catch
                        {
                            // Skip individual player on error
                        }
                    }
                    
                    playerEntities.Dispose();
                }
                finally
                {
                    query.Dispose();
                }
            }
            catch
            {
                // Silently fail
            }
        }

        private static bool IsPlayerHolder(Entity playerEntity, ZoneDefinition zone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zone.HolderName))
                    return false;

                var em = ZoneCore.EntityManager;
                if (!em.HasComponent<User>(playerEntity))
                    return false;

                var user = em.GetComponentData<User>(playerEntity);
                var charName = user.CharacterName.ToString();

                return string.Equals(charName, zone.HolderName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    #region JSON Configuration Classes
    public class ZoneJsonConfig
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalMs { get; set; } = 100;
        public float PositionChangeThreshold { get; set; } = 1.0f;
        public Dictionary<string, ZoneMapping> Mappings { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["*"] = new ZoneMapping
            {
                OnEnter = new[]
                {
                    "capture_return_position",
                    "snapshot_save",
                    "zone_enter_message",
                    "apply_kit",
                    "teleport_enter",
                    "apply_templates",
                    "apply_abilities",
                    "glow_spawn",
                    "boss_enter",
                    "integration_events_enter",
                    "announce_enter"
                },
                OnExit = new[]
                {
                    "zone_exit_message",
                    "restore_kit_snapshot",
                    "restore_abilities",
                    "boss_exit",
                    "teleport_return",
                    "glow_reset",
                    "integration_events_exit"
                },
                UseGlobalDefaults = false
            }
        };
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
        private static readonly PrefabGUID CastleHeartPrefab = new PrefabGUID(-485210554); // TM_BloodFountain_CastleHeart
        private static readonly PrefabGUID CastleHeartRebuildPrefab = new PrefabGUID(-600018251); // TM_BloodFountain_CastleHeart_Rebuilding
        private static readonly PrefabGUID CarpetPrefab = new PrefabGUID(1144832236); // PurpleCarpetsBuildMenuGroup01
        private static readonly PrefabGUID DownedHorseBuff = new PrefabGUID(-266455478);
        private static readonly PrefabGUID SpecificMountPrefab = PrefabGUID.Empty; // Set if you want to restrict to one mount prefab.

        [HarmonyPatch(typeof(PlaceTileModelSystem), nameof(PlaceTileModelSystem.OnUpdate))]
        [HarmonyPrefix]
        public static void PlaceTileModelSystem_OnUpdate_Prefix(PlaceTileModelSystem __instance)
        {
            if (!Plugin.BuildingPlacementRestrictionsDisabledValue)
            {
                return;
            }

            try
            {
                var em = UnifiedCore.EntityManager;
                var buildEvents = __instance._BuildTileQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var buildEvent in buildEvents)
                    {
                        if (!em.Exists(buildEvent) || !em.HasComponent<BuildTileModelEvent>(buildEvent))
                        {
                            continue;
                        }

                        var btme = em.GetComponentData<BuildTileModelEvent>(buildEvent);
                        var isCastleHeart = btme.PrefabGuid == CastleHeartPrefab || btme.PrefabGuid == CastleHeartRebuildPrefab;
                        var isCarpet = btme.PrefabGuid == CarpetPrefab;
                        if (!isCastleHeart && !isCarpet)
                        {
                            continue;
                        }

                        if (em.HasComponent<FromCharacter>(buildEvent))
                        {
                            var fromCharacter = em.GetComponentData<FromCharacter>(buildEvent);
                            var message = isCarpet
                                ? "Can't place carpets while build restrictions are disabled."
                                : "Can't place Castle Hearts while build restrictions are disabled.";
                            _ = GameActionService.TrySendSystemMessageToUserEntity(fromCharacter.User, message);
                        }

                        if (em.Exists(buildEvent))
                        {
                            if (!em.HasComponent<Disabled>(buildEvent))
                            {
                                em.AddComponent<Disabled>(buildEvent);
                            }

                            em.DestroyEntity(buildEvent);
                        }
                    }
                }
                finally
                {
                    buildEvents.Dispose();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[BlueLock] PlaceTileModel patch failed: {ex.Message}");
            }
        }

        public static bool SkipInitializeNewSpawnChainOnce { get; set; }

        [HarmonyPatch(typeof(InitializeNewSpawnChainSystem), nameof(InitializeNewSpawnChainSystem.OnUpdate))]
        [HarmonyPrefix]
        public static bool InitializeNewSpawnChainSystem_OnUpdate_Prefix()
        {
            if (SkipInitializeNewSpawnChainOnce)
            {
                SkipInitializeNewSpawnChainOnce = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(AbilityRunScriptsSystem), nameof(AbilityRunScriptsSystem.OnUpdate))]
        [HarmonyPrefix]
        public static bool AbilityRunScriptsSystem_OnUpdate_Prefix(AbilityRunScriptsSystem __instance)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                var entities = __instance._OnCastStartedQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var entity in entities)
                    {
                        if (!em.Exists(entity) || !em.HasComponent<AbilityCastStartedEvent>(entity))
                        {
                            continue;
                        }

                        var started = em.GetComponentData<AbilityCastStartedEvent>(entity);
                        if (started.AbilityGroup == Entity.Null || !em.Exists(started.AbilityGroup) || !em.HasComponent<PrefabGUID>(started.AbilityGroup))
                        {
                            continue;
                        }

                        var abilityGroup = em.GetComponentData<PrefabGUID>(started.AbilityGroup);
                        if (AbilityUi.CheckAbilityUsage(started.Character, abilityGroup))
                        {
                            em.RemoveComponent<AbilityCastStartedEvent>(entity);
                        }
                    }
                }
                finally
                {
                    entities.Dispose();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[BlueLock] AbilityRunScripts patch failed: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
        [HarmonyPostfix]
        public static void ServerBootstrap_OnUpdate_Postfix()
        {
            Plugin.ProcessAutoZoneDetection();
            AbilityUi.ProcessPendingSlotApplies();
            Plugin.ProcessPendingZoneTeleports();
        }


        [HarmonyPatch(typeof(TriggerPersistenceSaveSystem), nameof(TriggerPersistenceSaveSystem.TriggerSave))]
        [HarmonyPostfix]
        public static void TriggerPersistenceSaveSystem_TriggerSave_Postfix(SaveReason reason, FixedString128Bytes saveName, ServerRuntimeSettings saveConfig)
        {
            try
            {
                KitService.SaveUsageData();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[BlueLock] Failed to persist kit usage data on save: {ex.Message}");
            }

            try
            {
                VAuto.Core.Services.DebugEventBridge.FlushSnapshotsToDisk();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[BlueLock] Failed to flush sandbox progression snapshots on save: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
        [HarmonyPostfix]
        public static void ServerBootstrapSystem_OnUserConnected_Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
        {
            try
            {
                var em = __instance.EntityManager;
                if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out var userIndex))
                {
                    return;
                }

                var serverClient = __instance._ApprovedUsersLookup[userIndex];
                var userEntity = serverClient.UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return;
                }

                Plugin.OnApprovedUserConnected(userEntity);

                // Register the user entity with KitService for tracking.
                KitService.EnsurePlayerRegistered(userEntity);

            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[BlueLock] OnUserConnected kit registration failed: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(DownedEventSystem), nameof(DownedEventSystem.OnUpdate))]
        [HarmonyPrefix]
        public static void DownedEventSystem_OnUpdate_Prefix(DownedEventSystem __instance)
        {
            NativeArray<Entity> query = default;
            try
            {
                var em = UnifiedCore.EntityManager;
                query = __instance._DownedEventQuery.ToEntityArray(Allocator.Temp);

                foreach (var eventEntity in query)
                {
                    if (!em.Exists(eventEntity) || !em.HasComponent<DownedEvent>(eventEntity))
                    {
                        continue;
                    }

                    var downedEntity = em.GetComponentData<DownedEvent>(eventEntity).Entity;
                    if (downedEntity == Entity.Null || !em.Exists(downedEntity))
                    {
                        continue;
                    }

                    if (!em.HasComponent<Mountable>(downedEntity))
                    {
                        continue;
                    }

                    if (!SpecificMountPrefab.IsEmpty())
                    {
                        if (!em.HasComponent<PrefabGUID>(downedEntity))
                        {
                            continue;
                        }

                        var downedPrefab = em.GetComponentData<PrefabGUID>(downedEntity);
                        if (downedPrefab != SpecificMountPrefab)
                        {
                            continue;
                        }
                    }

                    TryRemoveBuffViaExternalService(downedEntity, DownedHorseBuff);

                    if (!em.HasComponent<Health>(downedEntity))
                    {
                        continue;
                    }

                    var health = em.GetComponentData<Health>(downedEntity);
                    health.Value = health.MaxHealth;
                    health.MaxRecoveryHealth = health.MaxHealth;
                    em.SetComponentData(downedEntity, health);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[BlueLock] DownedEvent patch failed: {ex.Message}");
            }
            finally
            {
                if (query.IsCreated)
                {
                    query.Dispose();
                }
            }
        }

        private static void TryRemoveBuffViaExternalService(Entity target, PrefabGUID buffGuid)
        {
            _ = GameActionService.TryRemoveBuff(target, buffGuid);
        }

    }
}
