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
    [BepInPlugin("gg.coyote.BlueLock", "VAutomation Zone", "1.0.1")]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.1")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInDependency("gg.coyote.lifecycle", "1.0.1")]
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
        public static ConfigEntry<string> GlowSystemDefaultBuffGuids;
        
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
        public static ConfigEntry<string> KitDefaultGlowBuffGuids;

        // Sandbox Progression
        public static ConfigEntry<bool> SandboxProgressionEnabled;
        public static ConfigEntry<bool> SandboxProgressionDefaultZoneUnlockEnabled;
        public static ConfigEntry<bool> SandboxProgressionPersistSnapshots;
        public static ConfigEntry<string> SandboxProgressionSnapshotFilePath;
        public static ConfigEntry<bool> SandboxProgressionVerboseLogs;

        // Debug
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<bool> HotReloadEnabled;
        public static ConfigEntry<bool> ConfigAutoParseFlowFolder;
        public static ConfigEntry<bool> ConfigAutoParseDatabaseFolder;
        public static ConfigEntry<bool> ConfigRequireZoneFlowFiles;
        public static ConfigEntry<bool> AutoSeedDatabaseFiles;
        public static ConfigEntry<bool> ForceSeedDatabaseFiles;
        public static ConfigEntry<bool> AutoSeedFlowFiles;
        public static ConfigEntry<bool> ForceSeedFlowFiles;
 
        // Runtime mode
        public static ConfigEntry<ZoneRuntimeMode> RuntimeMode;
public static ConfigEntry<float> EcsDetectionTickSeconds;
        public static ConfigEntry<int> ZoneDetectionOpsWarningThreshold;
        #endregion

        internal static ConfigSeedService.SeedSummary LastDatabaseSeedSummary { get; private set; }
        internal static ConfigSeedService.SeedSummary LastFlowSeedSummary { get; private set; }

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
        private static System.Timers.Timer _hotReloadTimer;
        #endregion

        // Auto zone detection state (main-thread only, updated from Harmony OnUpdate patch)
        private static readonly Dictionary<Entity, string> _playerZoneStates = new();
        private static readonly Dictionary<Entity, PendingZoneTransition> _pendingZoneTransitions = new();
        private static readonly Dictionary<Entity, DateTime> _lastCommittedZoneTransitions = new();
        private static readonly Dictionary<Entity, string> _playerZoneLocks = new();
        private static readonly Dictionary<Entity, string> _playerOriginalNames = new();
        private static readonly Dictionary<string, List<Entity>> _zoneBorderEntities = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _zoneGlowAutoSpawnDisabled = new(StringComparer.OrdinalIgnoreCase);
        private static readonly bool TempDisableAbilityUiDuringZoneTransitions = false;
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
            "set_blood",
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
        private static ZoneRuntimeMode _bootRuntimeMode = ZoneRuntimeMode.Legacy;
        private static bool _bootRuntimeModeLocked;
        private static bool _ecsInitializationDeferred;
        private static bool _runtimeServicesInitialized;

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
 
                var configDirectory = Path.GetDirectoryName(_zonesConfigPath) ?? Paths.ConfigPath;
 
                // Auto-seed required config directories before loading JSON
                var databaseSeedSummary = ConfigSeedService.EnsureDatabaseFiles(configDirectory, AutoSeedDatabaseFiles?.Value ?? true, ForceSeedDatabaseFiles?.Value ?? false);
                var flowSeedSummary = ConfigSeedService.EnsureFlowFiles(configDirectory, AutoSeedFlowFiles?.Value ?? true, ForceSeedFlowFiles?.Value ?? false);
                LastDatabaseSeedSummary = databaseSeedSummary;
                LastFlowSeedSummary = flowSeedSummary;
 
                // Load JSON configuration
                LoadJsonConfiguration();
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

                EnsureRuntimeServicesInitialized();

                // Validate configs after ZoneConfigService had a chance to create defaults.
                var configValidation = ProcessConfigService.ValidateAllConfigs(configDirectory);
                LogConfigValidationResult(configValidation, "startup");
                var requestedMode = RuntimeMode?.Value ?? ZoneRuntimeMode.Legacy;
                _bootRuntimeMode = configValidation.Success ? requestedMode : ZoneRuntimeMode.Legacy;
                if (!configValidation.Success && requestedMode != ZoneRuntimeMode.Legacy)
                {
                    Logger.LogWarning("[BlueLock] Config validation failed at startup; boot runtime mode forced to Legacy.");
                }
                _bootRuntimeModeLocked = true;
                if (requestedMode != _bootRuntimeMode)
                {
                    Logger.LogWarning($"[BlueLock] Requested runtime mode '{requestedMode}' differs from effective boot mode '{_bootRuntimeMode}'.");
                }

                InitializeZoneRuntimeSystems();

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

            if (LastDatabaseSeedSummary is { Disabled: false } dbSummary)
            {
                Logger.LogInfo($"[BlueLock][AutoSeed][database] Manifest v{DefaultConfigManifest.ManifestVersion} Created={dbSummary.Created} Upgraded={dbSummary.Upgraded} SkippedCustomized={dbSummary.SkippedCustomized} SkippedUpToDate={dbSummary.SkippedUpToDate} Failed={dbSummary.Failed} Force={dbSummary.ForceMode}");
            }
            if (LastFlowSeedSummary is { Disabled: false } flowSummary)
            {
                Logger.LogInfo($"[BlueLock][AutoSeed][flows] Manifest v{DefaultConfigManifest.ManifestVersion} Created={flowSummary.Created} Upgraded={flowSummary.Upgraded} SkippedCustomized={flowSummary.SkippedCustomized} SkippedUpToDate={flowSummary.SkippedUpToDate} Failed={flowSummary.Failed} Force={flowSummary.ForceMode}");
            }
            Logger.LogInfo("[BlueLock]   Command Roots: zone, match, spawn, template, tag, enter, exit, unlockprefab");
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
            GlowSystemDefaultBuffGuids = configFile.Bind("GlowSystem", "DefaultBuffGuids", "4345235,54252435,65245252", "Comma-separated fallback glow buff GUID hashes.");

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
            KitDefaultGlowBuffGuids = configFile.Bind("Kit Settings", "DefaultGlowBuffGuids", "4345235,54252435,65245252", "Comma-separated default glow buff GUID hashes associated with kit flow.");

            // Sandbox Progression
            SandboxProgressionEnabled = configFile.Bind("Sandbox Progression", "Enabled", true, "Enable sandbox progression backup/unlock/restore flow.");
            SandboxProgressionDefaultZoneUnlockEnabled = configFile.Bind("Sandbox Progression", "DefaultZoneUnlockEnabled", true, "Default sandbox progression unlock behavior when a zone does not define SandboxUnlockEnabled.");
            SandboxProgressionPersistSnapshots = configFile.Bind("Sandbox Progression", "PersistSnapshots", true, "Persist sandbox progression snapshots to disk for restore after restart.");
            SandboxProgressionSnapshotFilePath = configFile.Bind("Sandbox Progression", "SnapshotFilePath", Path.Combine(Paths.ConfigPath, "Bluelock", "state", "sandbox_progression"), "Path to sandbox progression snapshot state file.");
            SandboxProgressionVerboseLogs = configFile.Bind("Sandbox Progression", "VerboseLogs", false, "Enable verbose sandbox progression logs.");

            // Debug
            DebugMode = configFile.Bind("Debug", "DebugMode", false, "Enable debug mode");
            HotReloadEnabled = configFile.Bind("Debug", "HotReload", true, "Enable hot-reload of configuration");

            // Config Parsing
            ConfigAutoParseFlowFolder = configFile.Bind("Config Parsing", "AutoParseFlowFolder", true, "Auto-parse every JSON file in Bluelock/flows during validation.");
            ConfigAutoParseDatabaseFolder = configFile.Bind("Config Parsing", "AutoParseDatabaseFolder", true, "Auto-parse every JSON file in Bluelock/database during validation.");
            ConfigRequireZoneFlowFiles = configFile.Bind("Config Parsing", "RequireZoneFlowFiles", false, "When true, each zone FlowId must resolve to a file in Bluelock/flows.");
            AutoSeedDatabaseFiles = configFile.Bind("Config Parsing", "AutoSeedDatabaseFiles", true, "Automatically materialize default database JSON files when missing.");
            ForceSeedDatabaseFiles = configFile.Bind("Config Parsing", "ForceSeedDatabaseFiles", false, "When true, overwrite database files if they differ from embedded templates (backups saved).");
            AutoSeedFlowFiles = configFile.Bind("Config Parsing", "AutoSeedFlowFiles", true, "Automatically materialize default flow JSON files when missing.");
            ForceSeedFlowFiles = configFile.Bind("Config Parsing", "ForceSeedFlowFiles", false, "When true, overwrite flow files if they differ from embedded templates (backups saved).");
 
            // Runtime mode
            RuntimeMode = configFile.Bind("Runtime", "ZoneRuntimeMode", ZoneRuntimeMode.Legacy, "Zone execution mode: Legacy | Hybrid | EcsOnly");
EcsDetectionTickSeconds = configFile.Bind("Runtime", "EcsDetectionTickSeconds", 0.2f, "Fixed ECS zone detection tick interval in seconds.");
            ZoneDetectionOpsWarningThreshold = configFile.Bind("Runtime", "ZoneDetectionOpsWarningThreshold", 10000, "Warn when ECS detection players*zones exceeds this threshold.");
        }

        private static string ResolveBluelockConfigPath(string fileName)
        {
            var rootDir = Path.Combine(Paths.ConfigPath, "Bluelock");
            Directory.CreateDirectory(rootDir);
            return Path.Combine(rootDir, fileName);
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

        private static void InitializeZoneRuntimeSystems()
        {
            try
            {
                var options = GetZoneRuntimeModeOptions();
                
                // Use ZoneCore.Server which safely returns null if world not ready
                var serverWorld = VAuto.Zone.Core.ZoneCore.Server;
                if (serverWorld == null || !serverWorld.IsCreated)
                {
                    Logger.LogWarning("[BlueLock] Server world not ready during Load(); deferring ECS initialization.");
                    _ecsInitializationDeferred = true; // Mark for deferred initialization
                    return; // Will be initialized when world becomes available
                }
                
                _ecsInitializationDeferred = false; // Clear deferred flag if successful
                _ = serverWorld.GetOrCreateSystemManaged<VAuto.Zone.Systems.ZoneBootstrapSystem>();

                if (options.EcsZoneDetectionEnabled)
                {
                    _ = serverWorld.GetOrCreateSystemManaged<VAuto.Zone.Systems.ZoneDetectionSystem>();
                }

                if (options.EcsFlowExecutionEnabled || options.EcsTemplateLifecycleEnabled)
                {
                    _ = serverWorld.GetOrCreateSystemManaged<VAuto.Zone.Systems.ZoneTransitionRouterSystem>();
                }

                var flowLifecycleManager = new FlowLifecycleManager();
                VAuto.Zone.Systems.FlowExecutionSystem.SetFlowLifecycle(flowLifecycleManager);

                if (options.RuntimeMode == ZoneRuntimeMode.EcsOnly)
                {
                    var hasRouter = options.EcsFlowExecutionEnabled || options.EcsTemplateLifecycleEnabled;
                    if (!hasRouter)
                    {
                        throw new InvalidOperationException("EcsOnly mode requires router-capable ECS pipeline.");
                    }
                }

                Logger.LogInfo(
                    $"[BlueLock] Runtime mode: {options.RuntimeMode} (legacy={options.LegacyZonePipelineEnabled}, ecsDetect={options.EcsZoneDetectionEnabled}, ecsFlow={options.EcsFlowExecutionEnabled}, ecsTemplate={options.EcsTemplateLifecycleEnabled})");
                EnsureRuntimeServicesInitialized();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Failed to initialize runtime zone systems: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Retry deferred ECS initialization. Called when Server world becomes available.
        /// </summary>
        internal static void RetryDeferredEcsInitialization()
        {
            if (_ecsInitializationDeferred)
            {
                Logger.LogInfo("[BlueLock] Retrying deferred ECS initialization...");
                _ecsInitializationDeferred = false;
                InitializeZoneRuntimeSystems();
            }

            if (!_runtimeServicesInitialized)
            {
                EnsureRuntimeServicesInitialized();
            }
        }

        private static void EnsureRuntimeServicesInitialized()
        {
            if (_runtimeServicesInitialized)
            {
                return;
            }

            try
            {
                // Initialize ZoneConfigService early - doesn't need server world
                ZoneConfigService.Initialize();
                Logger.LogInfo("ZoneConfigService initialized");

                var serverWorld = VAuto.Zone.Core.ZoneCore.Server;
                if (serverWorld == null || !serverWorld.IsCreated)
                {
                    return;
                }

                // ArenaTerritory.InitializeArenaGrid(); // Excluded from headless builds
                Logger.LogInfo("Arena territory (headless stub)");

                KitService.Initialize();
                Logger.LogInfo("KitService initialized");

                ZoneBossSpawnerService.Initialize();
                Logger.LogInfo("ZoneBossSpawnerService initialized");

                StaggeredManifestationService.Initialize();
                Logger.LogInfo("StaggeredManifestationService initialized");

                ConfigureAbilityUi();
                AbilityUi.Initialize();
                Logger.LogInfo("AbilityUi initialized");

                ZonePlayerTagService.Initialize(
                    msg => Logger.LogInfo($"[ZonePlayerTagService] {msg}"),
                    msg => Logger.LogWarning($"[ZonePlayerTagService] {msg}"));
                Logger.LogInfo("ZonePlayerTagService initialized");

                ZoneGlowBorderService.QueueRebuild("startup");
                _runtimeServicesInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Territory init failed: {ex.Message}");
            }
        }

        private void LoadJsonConfiguration()
        {
            try
            {
                TypedJsonConfigManager.TryLoadOrCreate(
                    _configPath,
                    () => new ZoneJsonConfig(),
                    out _jsonConfig,
                    out var createdDefault,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                        WriteIndented = true
                    },
                    ValidateZoneLifecycleJsonConfig,
                    message => Logger.LogInfo($"[BlueLock] {message}"),
                    message => Logger.LogWarning($"[BlueLock] {message}"),
                    message => Logger.LogError($"[BlueLock] {message}"));

                _jsonConfig ??= new ZoneJsonConfig();
                ApplyZoneLifecycleMigrations(_jsonConfig);
                if (createdDefault)
                {
                    Logger.LogInfo($"[BlueLock] Created default zone lifecycle config at {_configPath}");
                }
                Logger.LogInfo($"[BlueLock] Loaded JSON configuration from {_configPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] Failed to load JSON configuration: {ex.Message}");
                _jsonConfig = new ZoneJsonConfig();
            }
        }

        private void SaveJsonConfiguration()
        {
            TypedJsonConfigManager.TrySave(
                _configPath,
                _jsonConfig,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                },
                message => Logger.LogInfo($"[BlueLock] {message}"),
                message => Logger.LogError($"[BlueLock] {message}"));
        }

        private static (bool IsValid, string Error) ValidateZoneLifecycleJsonConfig(ZoneJsonConfig config)
        {
            if (config == null)
            {
                return (false, "Config is null.");
            }

            if (config.CheckIntervalMs < 25)
            {
                return (false, "CheckIntervalMs must be >= 25.");
            }

            if (config.Mappings == null || config.Mappings.Count == 0)
            {
                return (false, "Mappings must contain at least one entry.");
            }

            return (true, string.Empty);
        }

        private void ApplyZoneLifecycleMigrations(ZoneJsonConfig config)
        {
            if (config == null)
            {
                return;
            }

            var migration = new ZoneLifecycleConfigVersionMigration();
            var report = migration.DryRun(config);
            if (report.HasChanges)
            {
                config = migration.Apply(config);
                _jsonConfig = config;
                Logger.LogInfo($"[BlueLock] Migration '{migration.Name}' applied: {string.Join("; ", report.Changes)}");
                SaveJsonConfiguration();
                return;
            }

            if (string.IsNullOrWhiteSpace(config.SchemaVersion))
            {
                config.SchemaVersion = ZoneJsonConfig.CurrentConfigVersion;
                SaveJsonConfiguration();
                return;
            }

            if (!string.Equals(config.ConfigVersion, ZoneJsonConfig.CurrentConfigVersion, StringComparison.Ordinal))
            {
                Logger.LogWarning($"[BlueLock] Zone lifecycle config version '{config.ConfigVersion}' differs from runtime '{ZoneJsonConfig.CurrentConfigVersion}'. Running compatibility mode.");
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
                        LogConfigValidationResult(configValidation, "hot-reload");
                        var requestedMode = RuntimeMode?.Value ?? ZoneRuntimeMode.Legacy;
                        if (requestedMode != _bootRuntimeMode)
                        {
                            Logger.LogWarning($"[BlueLock] Runtime mode hot-reload change ignored. requested={requestedMode}, bootMode={_bootRuntimeMode}.");
                        }
                        if (!configValidation.Success)
                        {
                            Logger.LogWarning($"[BlueLock] Config validation failed during hot-reload; effective runtime mode remains '{_bootRuntimeMode}'.");
                        }
                        
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

            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] Error checking configuration changes: {ex.Message}");
            }
        }

        private static void LogConfigValidationResult(ProcessConfigService.ValidationResult validation, string context)
        {
            if (validation == null)
            {
                Logger.LogWarning($"[BlueLock] Config validation returned null during {context}.");
                return;
            }

            foreach (var warning in validation.Warnings)
            {
                Logger.LogWarning($"[BlueLock][ConfigValidation][{context}] {warning}");
            }

            foreach (var error in validation.Errors)
            {
                Logger.LogError($"[BlueLock][ConfigValidation][{context}] {error}");
            }

            if (!validation.Success)
            {
                Logger.LogWarning($"[BlueLock] Configuration validation completed with {validation.Errors.Count} error(s) during {context}.");
            }
        }

        #region Public Configuration Accessors
        public static bool IsEnabled => GeneralEnabled?.Value ?? true;
        public static ZoneRuntimeMode RuntimeModeValue => _bootRuntimeModeLocked ? _bootRuntimeMode : (RuntimeMode?.Value ?? ZoneRuntimeMode.Legacy);
        public static float EcsDetectionTickSecondsValue => Math.Max(0.05f, EcsDetectionTickSeconds?.Value ?? 0.2f);
        public static int ZoneDetectionOpsWarningThresholdValue => ZoneDetectionOpsWarningThreshold?.Value ?? 10000;
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
        public static string GlowSystemDefaultBuffGuidsValue => GlowSystemDefaultBuffGuids?.Value ?? "4345235,54252435,65245252";
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
                ? Path.Combine(Paths.ConfigPath, "Bluelock", "state", "sandbox_progression")
                : SandboxProgressionSnapshotFilePath.Value;
        public static bool SandboxProgressionVerboseLogsValue => SandboxProgressionVerboseLogs?.Value ?? false;
        public static bool DebugModeEnabled => DebugMode?.Value ?? false;

        public static ZoneRuntimeModeOptions GetZoneRuntimeModeOptions()
        {
            return ZoneRuntimeModeOptions.FromMode(RuntimeModeValue);
        }

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
                                ("AB_Militia_LightArrow_SpawnMinions_Summon", -1191185326),
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
                var runtime = GetZoneRuntimeModeOptions();
                if (!runtime.LegacyZonePipelineEnabled || runtime.EcsZoneDetectionEnabled)
                {
                    return;
                }

                // Delegate zone detection to the dedicated service
                ZoneDetectionService.ProcessZoneDetection();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BlueLock] Auto zone detection failed: {ex.Message}");
            }
        }

        public static bool ForcePlayerEnterZone(Entity player, string zoneId = "")
        {
            return ZoneDetectionService.ForcePlayerEnterZone(player, zoneId);
        }

        public static bool ForcePlayerExitZone(Entity player)
        {
            return ZoneDetectionService.ForcePlayerExitZone(player);
        }

        private static DateTime _lastArenaCleanupTime;
        private static readonly Dictionary<int, DateTime> _lastZoneReviveByPlayer = new();

        [HarmonyPatch(typeof(ProjectM.BacktraceSystem), nameof(ProjectM.BacktraceSystem.OnUpdate))]
        [HarmonyPostfix]
        public static void BacktraceSystem_OnUpdate_Postfix()
        {
            try
            {
                ApplyArenaDamageToPlayers();
                ProcessArenaDeathRespawns();
                ProcessZoneAutoRevives();
                ZoneNoDurabilityService.Tick(ZoneCore.EntityManager);

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

        private static void ProcessZoneAutoRevives()
        {
            try
            {
                var server = ZoneCore.Server;
                if (server == null || !server.IsCreated)
                {
                    return;
                }

                var em = server.EntityManager;
                if (em == default)
                {
                    return;
                }

                var query = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
                try
                {
                    var players = query.ToEntityArray(Allocator.Temp);
                    var now = DateTime.UtcNow;

                    foreach (var player in players)
                    {
                        try
                        {
                            if (!em.Exists(player))
                            {
                                continue;
                            }

                            var zoneState = VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(player);
                            if (zoneState == null || string.IsNullOrWhiteSpace(zoneState.CurrentZoneId))
                            {
                                continue;
                            }

                            if (!em.HasComponent<Health>(player))
                            {
                                continue;
                            }

                            var health = em.GetComponentData<Health>(player);
                            if (health.Value > 0)
                            {
                                continue;
                            }

                            if (_lastZoneReviveByPlayer.TryGetValue(player.Index, out var lastRevive) &&
                                (now - lastRevive).TotalSeconds < 1.0)
                            {
                                continue;
                            }

                            health.Value = health.MaxHealth > 0 ? health.MaxHealth : 1f;
                            em.SetComponentData(player, health);

                            if (ArenaRespawnHelper.TeleportToArenaCenter(player, zoneState.CurrentZoneId))
                            {
                                _playerZoneStates[player] = zoneState.CurrentZoneId;
                            }

                            _lastZoneReviveByPlayer[player.Index] = now;
                            Logger.LogInfo($"[BlueLock] Auto-revived player {player.Index} in zone '{zoneState.CurrentZoneId}'.");
                        }
                        catch
                        {
                            // Skip per-player errors.
                        }
                    }

                    players.Dispose();
                }
                finally
                {
                    query.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Zone auto-revive processing failed: {ex.Message}");
            }
        }

        private static void ProcessArenaDeathRespawns()
        {
            try
            {
                var server = ZoneCore.Server;
                if (server == null || !server.IsCreated)
                    return;

                var em = server.EntityManager;
                if (em == default)
                    return;

                var query = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
                try
                {
                    var playerEntities = query.ToEntityArray(Allocator.Temp);
                    foreach (var playerEntity in playerEntities)
                    {
                        try
                        {
                            if (!em.Exists(playerEntity))
                                continue;

                            // Only process consumed arena deaths after player is alive again.
                            if (!ArenaDeathTracker.IsArenaDeath(playerEntity, out _))
                                continue;

                            if (!em.HasComponent<Health>(playerEntity))
                                continue;

                            var health = em.GetComponentData<Health>(playerEntity);
                            if (health.Value <= 0)
                                continue;

                            if (!ArenaDeathTracker.TryConsumeRecentArenaDeath(playerEntity, out var zoneId))
                                continue;

                            if (string.IsNullOrWhiteSpace(zoneId))
                                continue;

                            if (!ArenaRespawnHelper.TeleportToArenaCenter(playerEntity, zoneId))
                            {
                                Logger.LogDebug($"[BlueLock] Arena respawn teleport failed for player {playerEntity.Index} zone '{zoneId}'.");
                                continue;
                            }

                            // Keep player zone state pinned to arena zone on death-respawn.
                            _playerZoneStates[playerEntity] = zoneId;
                            Logger.LogInfo($"[BlueLock] Arena respawn routed player {playerEntity.Index} back to zone '{zoneId}'.");
                        }
                        catch
                        {
                            // Skip individual player on error.
                        }
                    }

                    playerEntities.Dispose();
                }
                finally
                {
                    query.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[BlueLock] Arena respawn processing failed: {ex.Message}");
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

                                // Register arena death if lethal
                                if (health.Value <= 0)
                                {
                                    ArenaDeathTracker.RegisterArenaDeath(playerEntity, zoneState.CurrentZoneId);

                                    // Immediate in-zone revive: restore health and route player back to the same zone.
                                    health.Value = health.MaxHealth > 0 ? health.MaxHealth : 1f;
                                    em.SetComponentData(playerEntity, health);

                                    if (ArenaRespawnHelper.TeleportToArenaCenter(playerEntity, zoneState.CurrentZoneId))
                                    {
                                        _playerZoneStates[playerEntity] = zoneState.CurrentZoneId;
                                        Logger.LogInfo($"[BlueLock] Immediate arena revive for player {playerEntity.Index} in zone '{zoneState.CurrentZoneId}'.");
                                    }

                                    // Reset damage ramp after revive to avoid immediate chained lethal ticks.
                                    damageState.CurrentDamage = 20f;
                                    damageState.InitialDamage = 20f;
                                    damageState.LastTickTime = now;
                                    em.SetComponentData(playerEntity, damageState);
                                    continue;
                                }

                                em.SetComponentData(playerEntity, health);
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
        public const string CurrentConfigVersion = "1.1.0";
        public string SchemaVersion { get; set; } = CurrentConfigVersion;
        public string ConfigVersion { get; set; } = CurrentConfigVersion;
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
        public string FlowId { get; set; } = "ZoneDefault";
        public string[] OnEnter { get; set; } = new string[] { "store" };
        public string[] OnExit { get; set; } = new string[] { "message" };
        public ZoneMustFlow[] MustFlows { get; set; } = Array.Empty<ZoneMustFlow>();
        public bool UseGlobalDefaults { get; set; } = false;
        public string MapIconChangePrefab { get; set; } = "";
    }

    public class ZoneMustFlow
    {
        public string Action { get; set; } = string.Empty;
        public bool Critical { get; set; } = true;
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
            // Retry deferred ECS initialization when world becomes available.
            Plugin.RetryDeferredEcsInitialization();
            
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
            StaggeredManifestationService.ProcessPending(UnifiedCore.EntityManager);
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