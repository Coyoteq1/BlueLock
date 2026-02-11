using System;
using System.Collections.Generic;
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
using ProjectM.Gameplay.Systems;
using Unity.Collections;
using Stunlock.Core;
using Stunlock.Network;
using VAuto.Zone.Services;
using VAuto.Zone.Services.Building;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore.Core;
using VAutomationCore.Core.Arena;
using VAutomationCore.Core.Config;
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
        public static ConfigEntry<bool> BuildingPlacementRestrictionsDisabled;
        
        // Kit Settings
        public static ConfigEntry<bool> KitAutoEquipEnabled;
        public static ConfigEntry<bool> KitRestoreOnExit;
        public static ConfigEntry<bool> KitBroadcastEquips;
        public static ConfigEntry<string> KitDefaultName;
        public static ConfigEntry<string> KitDefinitionsPath;
        
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
        private static System.Timers.Timer _hotReloadTimer;
        #endregion

        // Auto zone detection state (main-thread only, updated from Harmony OnUpdate patch)
        private static readonly Dictionary<Entity, string> _playerZoneStates = new();
        private static readonly Dictionary<string, List<Entity>> _zoneBorderEntities = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Entity, float3> _zoneReturnPositions = new();
        private static readonly string[] LifecycleAssemblyNames = { "Cycleborn", "Vlifecycle" };
        private static volatile bool _pendingZoneBorderRebuild;
        private static float _lastZoneDetectionUpdateTime;
        private static ArenaLifecycleManager _arenaLifecycleManager;

        public Plugin()
        {
            Instance = this;
        }

        public override void Load()
        {
            try
            {
                // Initialize configuration path
                _configPath = VAutoPathMap.ResolveConfigFile(
                    VAutoModule.Zone,
                    "VAuto.ZoneLifecycle.json",
                    Path.Combine(Paths.ConfigPath, "VAuto.ZoneLifecycle.json"));
                _zonesConfigPath = VAutoPathMap.GetConfigFile(VAutoModule.Zone, "VAuto.Zones.json");
                _kitsConfigPath = VAutoPathMap.GetConfigFile(VAutoModule.Zone, "VAuto.Kits.json");

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
                _arenaLifecycleManager = new ArenaLifecycleManager(CoreLog);
                
                // Initialize all services using ServiceInitializer
                var servicesInitialized = ServiceInitializer.InitializeAll(CoreLog);
                if (!servicesInitialized)
                {
                    Logger.LogWarning("[VAutoZone] Some services failed to initialize");
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
                    AbilityUi.Initialize();
                    Logger.LogInfo("AbilityUi initialized");

                    // Queue border build to run on server update thread when world is ready.
                    RequestZoneBorderRebuild();
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
                Logger.LogInfo("[VAutoZone] Commands registered");

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
            var cfgPath = VAutoPathMap.ResolveConfigFile(
                VAutoModule.Zone,
                "VAuto.Zone.cfg",
                Path.Combine(Paths.ConfigPath, "VAutoZone.cfg"));
            var configFile = new ConfigFile(cfgPath, true);

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
            BuildingPlacementRestrictionsDisabled = configFile.Bind("Integration", "BuildingPlacementRestrictionsDisabled", false, "Disable building placement restrictions. Castle Heart placement is blocked while this is enabled.");

            // Kit Settings
            KitAutoEquipEnabled = configFile.Bind("Kit Settings", "AutoEquipEnabled", true, "Enable automatic kit equipping on zone enter");
            KitRestoreOnExit = configFile.Bind("Kit Settings", "RestoreOnExit", true, "Enable gear restoration on zone exit");
            KitBroadcastEquips = configFile.Bind("Kit Settings", "BroadcastEquips", false, "Broadcast kit equip messages to all players");
            KitDefaultName = configFile.Bind("Kit Settings", "DefaultKit", "startkit", "Default kit name to use when zone has no specific kit");

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
            _lastConfigCheck = File.Exists(_configPath) ? File.GetLastWriteTime(_configPath) : DateTime.MinValue;
            _lastZonesConfigCheck = File.Exists(_zonesConfigPath) ? File.GetLastWriteTime(_zonesConfigPath) : DateTime.MinValue;
            _lastKitsConfigCheck = File.Exists(_kitsConfigPath) ? File.GetLastWriteTime(_kitsConfigPath) : DateTime.MinValue;
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

                if (File.Exists(_zonesConfigPath))
                {
                    var zonesLastModified = File.GetLastWriteTime(_zonesConfigPath);
                    if (zonesLastModified > _lastZonesConfigCheck)
                    {
                        _lastZonesConfigCheck = zonesLastModified;
                        ZoneConfigService.Reload();
                        RequestZoneBorderRebuild();
                        Logger.LogInfo("[VAutoZone] Zones config hot-reloaded and border rebuild queued");
                    }
                }

                if (File.Exists(_kitsConfigPath))
                {
                    var kitsLastModified = File.GetLastWriteTime(_kitsConfigPath);
                    if (kitsLastModified > _lastKitsConfigCheck)
                    {
                        _lastKitsConfigCheck = kitsLastModified;
                        KitService.Reload();
                        Logger.LogInfo("[VAutoZone] Kits config hot-reloaded");
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
        public static bool BuildingPlacementRestrictionsDisabledValue => BuildingPlacementRestrictionsDisabled?.Value ?? false;
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
        #endregion

        public static void SetGlowSystemEnabled(bool enabled)
        {
            if (GlowSystemEnabled != null)
            {
                GlowSystemEnabled.Value = enabled;
            }
        }

        public static void ForceGlowRebuild()
        {
            RequestZoneBorderRebuild();
            ProcessPendingZoneBorderRebuild();
        }

        public static void ClearGlowBordersNow()
        {
            ClearAllZoneBorders();
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
            _harmony?.UnpatchSelf();
            Logger.LogInfo("VAutoZone unloaded");
        }

        private static void RebuildAllZoneBorders()
        {
            try
            {
                ClearAllZoneBorders();

                if (!GlowSystemEnabledValue)
                {
                    Logger.LogInfo("[VAutoZone] Glow system disabled; skipped zone border rebuild");
                    return;
                }

                var zones = ZoneConfigService.GetAllZones();
                if (zones == null || zones.Count == 0)
                {
                    Logger.LogInfo("[VAutoZone] No zones found for border build");
                    return;
                }

                var em = UnifiedCore.EntityManager;
                var spacing = 3f;
                var builtZones = 0;
                var builtEntities = 0;

                foreach (var zone in zones)
                {
                    if (zone == null || string.IsNullOrWhiteSpace(zone.Id))
                    {
                        continue;
                    }

                    if (!ZoneConfigService.IsAutoGlowEnabledForZone(zone.Id))
                    {
                        continue;
                    }

                    if (!TryResolveZonePrefab(zone.GlowPrefab, zone.GlowPrefabId, out var carpetPrefabGuid, out var carpetPrefabEntity))
                    {
                        Logger.LogWarning($"[VAutoZone] Could not resolve carpet prefab '{zone.GlowPrefab}' ({zone.GlowPrefabId}) for zone '{zone.Id}', skipping border");
                        continue;
                    }

                    Entity overlayGlowPrefabEntity = Entity.Null;
                    var hasOverlayGlow = TryResolveZonePrefab(zone.BorderGlowPrefab, zone.BorderGlowPrefabId, out _, out overlayGlowPrefabEntity);

                    var borderPoints = GetZoneBorderPoints(zone, spacing);
                    if (borderPoints.Count == 0)
                    {
                        continue;
                    }

                    var zoneEntities = new List<Entity>(borderPoints.Count * 2);
                    var glowHeight = zone.GlowSpawnHeight > 0f ? zone.GlowSpawnHeight : 0.3f;

                    var borderBaseY = zone.GlowSpawnHeight;
                    foreach (var point in borderPoints)
                    {
                        var carpet = em.Instantiate(carpetPrefabEntity);
                        var carpetPos = new float3(point.x, borderBaseY, point.z);
                        if (em.HasComponent<LocalTransform>(carpet))
                        {
                            var t = em.GetComponentData<LocalTransform>(carpet);
                            t.Position = carpetPos;
                            em.SetComponentData(carpet, t);
                        }
                        else if (em.HasComponent<Translation>(carpet))
                        {
                            var t = em.GetComponentData<Translation>(carpet);
                            t.Value = carpetPos;
                            em.SetComponentData(carpet, t);
                        }
                        zoneEntities.Add(carpet);
                        builtEntities++;

                        if (hasOverlayGlow)
                        {
                            var glow = em.Instantiate(overlayGlowPrefabEntity);
                            var glowPos = new float3(point.x, borderBaseY + glowHeight, point.z);

                            if (em.HasComponent<LocalTransform>(glow))
                            {
                                var t = em.GetComponentData<LocalTransform>(glow);
                                t.Position = glowPos;
                                em.SetComponentData(glow, t);
                            }
                            else if (em.HasComponent<Translation>(glow))
                            {
                                var t = em.GetComponentData<Translation>(glow);
                                t.Value = glowPos;
                                em.SetComponentData(glow, t);
                            }

                            zoneEntities.Add(glow);
                            builtEntities++;
                        }
                    }

                    _zoneBorderEntities[zone.Id] = zoneEntities;
                    builtZones++;
                }

                Logger.LogInfo($"[VAutoZone] Built glow borders for {builtZones} zones ({builtEntities} entities)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] RebuildAllZoneBorders failed: {ex.Message}");
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
                Logger.LogWarning($"[VAutoZone] ClearAllZoneBorders failed: {ex.Message}");
            }
        }

        private static List<float3> GetZoneBorderPoints(ZoneDefinition zone, float spacing)
        {
            var points = new List<float3>();
            var safeSpacing = Math.Max(1f, spacing);
            var shape = (zone.Shape ?? string.Empty).Trim();

            if (shape.Equals("Circle", StringComparison.OrdinalIgnoreCase))
            {
                var radius = Math.Max(1f, zone.Radius);
                var circumference = Math.Max(8f, 2f * (float)Math.PI * radius);
                var pointCount = Math.Max(12, (int)(circumference / safeSpacing));

                for (var i = 0; i < pointCount; i++)
                {
                    var angle = (i / (float)pointCount) * 2f * (float)Math.PI;
                points.Add(new float3(
                        zone.CenterX + (float)Math.Cos(angle) * radius,
                        zone.GlowSpawnHeight,
                        zone.CenterZ + (float)Math.Sin(angle) * radius));
                }
                return points;
            }

            var isRectLike =
                shape.Equals("Rectangle", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("Rect", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("Square", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("Box", StringComparison.OrdinalIgnoreCase);

            if (!isRectLike)
            {
                return points;
            }

            var hasExplicitBounds = !(Math.Abs(zone.MinX) < 0.001f &&
                                      Math.Abs(zone.MaxX) < 0.001f &&
                                      Math.Abs(zone.MinZ) < 0.001f &&
                                      Math.Abs(zone.MaxZ) < 0.001f);

            float minX;
            float maxX;
            float minZ;
            float maxZ;
            if (hasExplicitBounds)
            {
                minX = Math.Min(zone.MinX, zone.MaxX);
                maxX = Math.Max(zone.MinX, zone.MaxX);
                minZ = Math.Min(zone.MinZ, zone.MaxZ);
                maxZ = Math.Max(zone.MinZ, zone.MaxZ);
            }
            else
            {
                var half = Math.Max(1f, zone.Radius);
                minX = zone.CenterX - half;
                maxX = zone.CenterX + half;
                minZ = zone.CenterZ - half;
                maxZ = zone.CenterZ + half;
            }

            for (var x = minX; x <= maxX; x += safeSpacing)
            {
                points.Add(new float3(x, zone.GlowSpawnHeight, minZ));
                points.Add(new float3(x, zone.GlowSpawnHeight, maxZ));
            }

            for (var z = minZ + safeSpacing; z < maxZ; z += safeSpacing)
            {
                points.Add(new float3(minX, zone.GlowSpawnHeight, z));
                points.Add(new float3(maxX, zone.GlowSpawnHeight, z));
            }

            return points;
        }

        private static bool TryResolveZonePrefab(string prefabName, int prefabId, out PrefabGUID guid, out Entity prefabEntity)
        {
            guid = PrefabGUID.Empty;
            prefabEntity = Entity.Null;

            // Prefer explicit prefab name.
            if (!string.IsNullOrWhiteSpace(prefabName) && ZoneCore.TryResolvePrefabEntity(prefabName, out guid, out prefabEntity))
            {
                return true;
            }

            // Backward-compatible numeric GUID fallback.
            if (prefabId != 0)
            {
                guid = new PrefabGUID(prefabId);
                return ZoneCore.TryGetPrefabEntity(guid, out prefabEntity);
            }

            return false;
        }

        internal static void ProcessAutoZoneDetection()
        {
            try
            {
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

                var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerCharacter>(),
                    ComponentType.ReadOnly<LocalTransform>());

                var players = query.ToEntityArray(Allocator.Temp);
                var stillSeen = new HashSet<Entity>();

                try
                {
                    foreach (var player in players)
                    {
                        if (!em.Exists(player) || !em.HasComponent<LocalTransform>(player))
                        {
                            continue;
                        }

                        stillSeen.Add(player);

                        var position = em.GetComponentData<LocalTransform>(player).Position;
                        var zone = ZoneConfigService.GetZoneAtPosition(position.x, position.z);
                        var newZoneId = zone?.Id ?? string.Empty;

                        if (!string.IsNullOrEmpty(newZoneId))
                        {
                            TryInvokeDebugEventBridgeIsInZone(player);
                        }

                        _playerZoneStates.TryGetValue(player, out var previousZoneId);
                        previousZoneId ??= string.Empty;

                        if (string.Equals(previousZoneId, newZoneId, StringComparison.OrdinalIgnoreCase))
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
                    _playerZoneStates.Remove(stale);
                    VAutomationCore.Services.ZoneEventBridge.RemovePlayerZoneState(stale);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] Auto zone detection failed: {ex.Message}");
            }
        }

        private static void RequestZoneBorderRebuild()
        {
            _pendingZoneBorderRebuild = true;
        }

        private static void ProcessPendingZoneBorderRebuild()
        {
            if (!_pendingZoneBorderRebuild)
            {
                return;
            }

            try
            {
                _ = UnifiedCore.Server;
                RebuildAllZoneBorders();
                _pendingZoneBorderRebuild = false;
                Logger.LogInfo("[VAutoZone] Pending zone border rebuild completed");
            }
            catch (Exception ex)
            {
                // Keep pending so it retries on next server update when world is available.
                Logger.LogWarning($"[VAutoZone] Pending zone border rebuild deferred: {ex.Message}");
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

                _arenaLifecycleManager?.OnPlayerEntered(player, zoneId);
                SendZoneEnterSystemMessage(player, zoneId, em);
                KitService.ApplyKitOnEnter(zoneId, player, em);
                ApplyZoneTemplatesOnEnter(player, zoneId, em);
                AbilityUi.OnZoneEnter(player, zoneId);
                HandleZoneTeleportEnter(player, zoneId, em);
                if (ZoneBossSpawnerService.TryHandlePlayerEnter(player, zoneId, out var bossSpawnMessage))
                {
                    Logger.LogInfo($"[VAutoZone] {bossSpawnMessage}");
                }

                TryInvokeLifecycleManager("OnPlayerEnter", player, zoneId);
                TryInvokeAnnouncementZoneEnter(player, zoneId, em);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] HandleZoneEnter failed: {ex.Message}");
            }
        }

        private static void SendZoneEnterSystemMessage(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                if (!em.HasComponent<PlayerCharacter>(player))
                {
                    return;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(player).UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return;
                }

                var configured = ZoneConfigService.GetEnterMessageForZone(zoneId);
                var messageText = string.IsNullOrWhiteSpace(configured) ? $"Welcome to zone {zoneId}" : configured;
                var msg = new FixedString512Bytes(messageText);
                ServerChatUtils.SendSystemMessageToClient(em, em.GetComponentData<User>(userEntity), ref msg);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] Failed to send zone enter message: {ex.Message}");
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
                    var result = BuildingService.Instance.LoadTemplate(template, userEntity, player, 0f);
                    if (result != null)
                    {
                        Logger.LogWarning($"[VAutoZone] Zone '{zoneId}' template '{template}' failed: {result}");
                    }
                    else
                    {
                        Logger.LogInfo($"[VAutoZone] Zone '{zoneId}' applied template '{template}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] Failed to apply zone templates for '{zoneId}': {ex.Message}");
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

                _arenaLifecycleManager?.OnPlayerExited(player, zoneId);
                SendZoneExitSystemMessage(player, zoneId, em);
                KitService.RestoreKitOnExit(zoneId, player, em);
                AbilityUi.OnZoneExit(player, zoneId);
                ZoneBossSpawnerService.HandlePlayerExit(player, zoneId);
                HandleZoneTeleportExit(player, zoneId, em);

                TryInvokeLifecycleManager("OnPlayerExit", player, zoneId);
                TryInvokeDebugEventBridge(false, player, zoneId);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] HandleZoneExit failed: {ex.Message}");
            }
        }

        private static void SendZoneExitSystemMessage(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                if (!em.HasComponent<PlayerCharacter>(player))
                {
                    return;
                }

                var userEntity = em.GetComponentData<PlayerCharacter>(player).UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return;
                }

                var configured = ZoneConfigService.GetExitMessageForZone(zoneId);
                var messageText = string.IsNullOrWhiteSpace(configured) ? "You left event" : configured;
                var msg = new FixedString512Bytes(messageText);
                ServerChatUtils.SendSystemMessageToClient(em, em.GetComponentData<User>(userEntity), ref msg);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] Failed to send zone exit message: {ex.Message}");
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

                var position = em.HasComponent<LocalTransform>(characterEntity)
                    ? em.GetComponentData<LocalTransform>(characterEntity).Position
                    : float3.zero;

                var lifecycleType = ResolveLifecycleType("VAuto.Core.Lifecycle.ArenaLifecycleManager");
                if (lifecycleType == null)
                {
                    return;
                }

                var instanceProp = lifecycleType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var lifecycleInstance = instanceProp?.GetValue(null);
                if (lifecycleInstance == null)
                {
                    return;
                }

                var method = lifecycleType.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(Entity), typeof(Entity), typeof(string), typeof(float3) },
                    null);

                method?.Invoke(lifecycleInstance, new object[] { userEntity, characterEntity, zoneId, position });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] Lifecycle reflection invoke failed ({methodName}): {ex.Message}");
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
                Logger.LogWarning($"[VAutoZone] Announcement reflection invoke failed (ZoneEnter): {ex.Message}");
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

        private static Type ResolveLifecycleType(string fullTypeName)
        {
            foreach (var assemblyName in LifecycleAssemblyNames)
            {
                var type = Type.GetType($"{fullTypeName}, {assemblyName}");
                if (type != null)
                {
                    return type;
                }
            }

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetType(fullTypeName);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }
            catch
            {
            }

            return null;
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
                    return;
                }

                var method = isEnter
                    ? bridgeType.GetMethod("OnPlayerEnterZone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null)
                    : bridgeType.GetMethod("OnPlayerExitZone", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);

                method?.Invoke(null, new object[] { characterEntity });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] DebugEventBridge reflection invoke failed: {ex.Message}");
            }
        }

        private static void TryInvokeDebugEventBridgeIsInZone(Entity characterEntity)
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
                    return;
                }

                var method = bridgeType.GetMethod(
                    "OnPlayerIsInZone",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity) },
                    null);

                method?.Invoke(null, new object[] { characterEntity });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] DebugEventBridge reflection invoke failed (IsInZone): {ex.Message}");
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
                if (!em.HasComponent<LocalTransform>(player))
                {
                    return;
                }

                var transform = em.GetComponentData<LocalTransform>(player);
                _zoneReturnPositions[player] = transform.Position;

                transform.Position = new float3(tx, ty, tz);
                em.SetComponentData(player, transform);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] Zone enter teleport failed ({zoneId}): {ex.Message}");
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
                if (!em.HasComponent<LocalTransform>(player))
                {
                    _zoneReturnPositions.Remove(player);
                    return;
                }

                if (_zoneReturnPositions.TryGetValue(player, out var returnPos))
                {
                    var transform = em.GetComponentData<LocalTransform>(player);
                    transform.Position = returnPos;
                    em.SetComponentData(player, transform);
                }

                _zoneReturnPositions.Remove(player);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] Zone exit return teleport failed ({zoneId}): {ex.Message}");
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

                if (_playerZoneStates.TryGetValue(player, out var previousZoneId) &&
                    !string.IsNullOrWhiteSpace(previousZoneId) &&
                    !string.Equals(previousZoneId, zone.Id, StringComparison.OrdinalIgnoreCase))
                {
                    HandleZoneExit(player, previousZoneId);
                }

                TryTeleportPlayerToZoneCenter(player, zone.Id, em);
                HandleZoneEnter(player, zone.Id);
                _playerZoneStates[player] = zone.Id;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] ForcePlayerEnterZone failed: {ex.Message}");
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

                HandleZoneExit(player, zoneId);
                _playerZoneStates.Remove(player);
                VAutomationCore.Services.ZoneEventBridge.RemovePlayerZoneState(player);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[VAutoZone] ForcePlayerExitZone failed: {ex.Message}");
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
                    transform.Position = new float3(zone.CenterX, y, zone.CenterZ);
                    em.SetComponentData(player, transform);
                    return true;
                }

                if (em.HasComponent<Translation>(player))
                {
                    var translation = em.GetComponentData<Translation>(player);
                    y = translation.Value.y;
                    translation.Value = new float3(zone.CenterX, y, zone.CenterZ);
                    em.SetComponentData(player, translation);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[VAutoZone] Teleport to zone center failed ({zoneId}): {ex.Message}");
            }

            return false;
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
                            if (em.Exists(fromCharacter.User) && em.HasComponent<User>(fromCharacter.User))
                            {
                                var user = em.GetComponentData<User>(fromCharacter.User);
                                var message = new FixedString512Bytes(isCarpet
                                    ? "Can't place carpets while build restrictions are disabled."
                                    : "Can't place Castle Hearts while build restrictions are disabled.");
                                ServerChatUtils.SendSystemMessageToClient(em, user, ref message);
                            }
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
                Plugin.Logger.LogWarning($"[VAutoZone] PlaceTileModel patch failed: {ex.Message}");
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
                Plugin.Logger.LogWarning($"[VAutoZone] AbilityRunScripts patch failed: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
        [HarmonyPostfix]
        public static void ServerBootstrap_OnUpdate_Postfix()
        {
            Plugin.ProcessAutoZoneDetection();
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
                Plugin.Logger.LogWarning($"[VAutoZone] Failed to persist kit usage data on save: {ex.Message}");
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

                var userData = em.GetComponentData<User>(userEntity);
                KitService.EnsurePlayerRegistered(userData.PlatformId);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[VAutoZone] OnUserConnected kit registration failed: {ex.Message}");
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
                Plugin.Logger.LogWarning($"[VAutoZone] DownedEvent patch failed: {ex.Message}");
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
            try
            {
                var buffServiceType = Type.GetType("ScarletCore.Services.BuffService, ScarletCore");
                if (buffServiceType == null)
                {
                    return;
                }

                var method = buffServiceType.GetMethod(
                    "TryRemoveBuff",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity), typeof(PrefabGUID) },
                    null);

                method?.Invoke(null, new object[] { target, buffGuid });
            }
            catch
            {
            }
        }

    }
}
