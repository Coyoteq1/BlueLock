using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core.Lifecycle;

namespace VAuto.Zone
{
    /// <summary>
    /// Zone Lifecycle Bridge - Central integration point between zone events and lifecycle stages.
    /// 
    /// BRIDGE RESPONSIBILITIES (this class ONLY):
    /// - Detect player position changes and zone transitions
    /// - Resolve lifecycle stage names from configuration
    /// - Build stage names with {ZoneId} interpolation
    /// - Route stage names to Vlifecycle for execution
    /// - Handle backward compatibility with legacy configs
    /// - Manage spellbook menu lifecycle
    /// - Route DebugEvent backup/restore to DebugEventBridge
    /// 
    /// BRIDGE DOES NOT:
    /// - Execute action handlers directly
    /// - Modify inventory, buffs, or player state
    /// - Parse Vlifecycle.json action definitions
    /// - Mutate ECS entities (delegated to DebugEventBridge)
    /// 
    /// CONFIG PRECEDENCE (enforced order):
    /// 1. Zone-specific lifecycle mapping
    /// 2. Wildcard lifecycle mapping
    /// 3. Legacy mapping (optional, last)
    /// 4. Otherwise → no lifecycle triggered
    /// 
    /// ZONE OVERLAP RULE:
    /// - First matching zone wins (no multi-zone stacking)
    /// - GetPrimaryZoneAtPosition() returns first match
    /// 
    /// INTEGRATED SYSTEMS:
    /// - Spellbook Menu: ZUI-based spell categorization and favorites
    /// - DebugEvent System: Backup/restore player progression via DebugEventBridge
    /// </summary>
    public class ZoneLifecycleBridge : IDisposable
    {
        private static ZoneLifecycleBridge _instance;
        public static ZoneLifecycleBridge Instance => _instance;
        
        private static readonly string _logPrefix = "[ZoneLifecycleBridge]";
        
        // Core dependencies
        private readonly ManualLogSource _log;
        private readonly EntityManager _entityManager;
        private readonly EntityQuery _playerQuery;
        
        // Configuration
        private ZoneLifecycleConfig _config;
        private bool _legacyModeEnabled;
        private bool _isInitialized;
        
        // Player state tracking
        private readonly Dictionary<ulong, PlayerZoneState> _playerStates = new();
        
        // Spellbook menu tracking
        private readonly Dictionary<ulong, SpellMenuState> _spellMenuStates = new();
        
        // Timers
        private System.Timers.Timer _checkTimer;
        private bool _isRunning;
        private bool _disposed;
        
        #region Lifecycle Stage Names
        
        // Default stage names for common scenarios
        public const string DEFAULT_ON_ENTER = "default.onEnter";
        public const string DEFAULT_IS_IN_ZONE = "default.isInZone";
        public const string DEFAULT_ON_EXIT = "default.onExit";
        
        #endregion
        
        #region Initialization
        
        public ZoneLifecycleBridge(ManualLogSource log, EntityManager entityManager)
        {
            _instance = this;
            _log = log;
            _entityManager = entityManager;
            
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerCharacter>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            
            _config = LoadConfig();
            DetectLegacyConfigs();
            InitializeSpellbookMenu();
            
            _log.LogInfo($"{_logPrefix} Initialized with {_config.Zones?.Count ?? 0} zones");
        }
        
        /// <summary>
        /// Initialize the bridge and start monitoring.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            _isInitialized = true;
            StartMonitoring();
            
            _log.LogInfo($"{_logPrefix} Bridge initialized and monitoring started");
        }
        
        /// <summary>
        /// Start position monitoring loop.
        /// </summary>
        public void StartMonitoring()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            ScanAllPlayers();
            
            var intervalMs = Math.Max(50, _config.CheckIntervalMs);
            _checkTimer = new System.Timers.Timer(intervalMs);
            _checkTimer.Elapsed += OnCheckTimerElapsed;
            _checkTimer.Start();
            
            _log.LogInfo($"{_logPrefix} Monitoring started ({intervalMs}ms interval)");
        }
        
        /// <summary>
        /// Stop position monitoring.
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _checkTimer = null;
            
            _log.LogInfo($"{_logPrefix} Monitoring stopped");
        }
        
        #endregion
        
        #region Player Zone Events
        
        /// <summary>
        /// Handle player entering a zone.
        /// </summary>
        public void OnPlayerEnterZone(ulong steamId, string zoneId, float3 position)
        {
            if (!_config.Enabled) return;
            if (string.IsNullOrEmpty(zoneId)) return;
            
            var zoneDef = GetZoneDefinition(zoneId);
            if (zoneDef == null || !zoneDef.Settings.Enabled)
            {
                _log.LogDebug($"{_logPrefix} Zone {zoneId} not found or disabled");
                return;
            }
            
            // Update player state
            UpdatePlayerState(steamId, zoneId, position);
            
            // Build stage names with zone ID interpolation
            var onEnterStage = BuildStageName(zoneDef.Lifecycle.OnEnterStage, zoneId);
            var isInZoneStage = BuildStageName(zoneDef.Lifecycle.IsInZoneStage, zoneId);
            
            // Trigger onEnter lifecycle stage
            if (!string.IsNullOrEmpty(onEnterStage))
            {
                TriggerStage(onEnterStage, steamId, zoneId, position);
            }
            
            // Immediately trigger isInZone for enforcement
            if (!string.IsNullOrEmpty(isInZoneStage))
            {
                TriggerStage(isInZoneStage, steamId, zoneId, position);
                UpdateIsInZoneTrigger(steamId);
            }
            
            // Handle spellbook menu on enter
            HandleSpellbookOnEnter(steamId, zoneId);
            
            // Send enter message if configured
            SendZoneMessage(steamId, zoneDef.Settings.EnterMessage);
            
            _log.LogInfo($"{_logPrefix} Player {steamId} entered zone {zoneId}");
        }
        
        /// <summary>
        /// Handle player exiting a zone.
        /// </summary>
        public void OnPlayerExitZone(ulong steamId, string zoneId, float3 position)
        {
            if (!_config.Enabled) return;
            if (string.IsNullOrEmpty(zoneId)) return;
            
            var zoneDef = GetZoneDefinition(zoneId);
            if (zoneDef == null) return;
            
            // Build stage name with zone ID interpolation
            var onExitStage = BuildStageName(zoneDef.Lifecycle.OnExitStage, zoneId);
            
            // Trigger onExit lifecycle stage
            if (!string.IsNullOrEmpty(onExitStage))
            {
                TriggerStage(onExitStage, steamId, zoneId, position);
            }
            
            // Handle spellbook menu on exit
            HandleSpellbookOnExit(steamId, zoneId);
            
            // Send exit message if configured
            SendZoneMessage(steamId, zoneDef.Settings.ExitMessage);
            
            // Clear player state
            ClearPlayerState(steamId);
            
            _log.LogInfo($"{_logPrefix} Player {steamId} exited zone {zoneId}");
        }
        
        /// <summary>
        /// Handle player position change (called by ZoneEventBridge).
        /// </summary>
        public void CheckPlayerPosition(ulong steamId, float3 position)
        {
            if (_disposed || !_isRunning) return;
            
            var currentZoneId = GetZoneId(position);
            
            if (!_playerStates.TryGetValue(steamId, out var state))
            {
                state = new PlayerZoneState { SteamId = steamId };
                _playerStates[steamId] = state;
            }
            
            DetectAndProcessTransition(state, currentZoneId, position);
            UpdatePosition(state, position);
            
            if (!string.IsNullOrEmpty(currentZoneId))
            {
                CheckIsInZone(state, currentZoneId, position);
            }
        }
        
        #endregion
        
        #region Stage Name Resolution
        
        /// <summary>
        /// Resolve lifecycle mapping for a zone using precedence:
        /// 1. Zone-specific mapping
        /// 2. Wildcard mapping
        /// 3. Legacy default (if enabled)
        /// </summary>
        public UnifiedLifecycleMapping ResolveMapping(string zoneId, out bool usedWildcard)
        {
            usedWildcard = false;
            
            // 1. Zone-specific mapping
            if (_config.Mappings.TryGetValue(zoneId, out var zoneMapping))
            {
                return zoneMapping;
            }
            
            // 2. Wildcard mapping
            if (_config.WildcardMapping != null && _config.WildcardMapping.UseWildcardDefaults)
            {
                usedWildcard = true;
                return _config.WildcardMapping;
            }
            
            // 3. Legacy fallback
            if (_legacyModeEnabled)
            {
                return GetLegacyMapping(zoneId);
            }
            
            return new UnifiedLifecycleMapping();
        }
        
        /// <summary>
        /// Build stage name with zone ID interpolation.
        /// Replaces {ZoneId} placeholder with actual zone ID.
        /// </summary>
        public string BuildStageName(string baseStage, string zoneId)
        {
            if (string.IsNullOrEmpty(baseStage)) return "";
            if (string.IsNullOrEmpty(zoneId)) return baseStage;
            
            // Interpolate {ZoneId} placeholder
            return baseStage.Replace("{ZoneId}", zoneId);
        }
        
        /// <summary>
        /// Get zone definition by ID.
        /// </summary>
        public UnifiedZoneDefinition GetZoneDefinition(string zoneId)
        {
            return _config?.GetZoneById(zoneId);
        }
        
        #endregion
        
        #region Vlifecycle Integration
        
        /// <summary>
        /// Trigger a lifecycle stage using ArenaLifecycleManager API.
        /// Replaces reflection-based TriggerStage with direct API calls.
        /// </summary>
        public void TriggerStage(string stageName, ulong steamId, string zoneId, float3 position)
        {
            if (!_config.Enabled)
            {
                _log.LogDebug($"{_logPrefix} Zone-lifecycle wiring disabled, skipping stage: {stageName}");
                return;
            }
            
            var characterEntity = FindCharacterEntity(steamId);
            if (characterEntity == Entity.Null)
            {
                _log.LogWarning($"{_logPrefix} Could not find character entity for player {steamId}");
                return;
            }
            
            // Route to ArenaLifecycleManager based on stage type
            if (stageName.Contains("onEnter") || stageName.Contains("isInZone"))
            {
                ArenaLifecycleManager.Instance.OnPlayerEnter(characterEntity, characterEntity, zoneId);
            }
            else if (stageName.Contains("onExit"))
            {
                ArenaLifecycleManager.Instance.OnPlayerExit(characterEntity, characterEntity, zoneId);
            }
            
            _log.LogInfo($"{_logPrefix} Triggered stage '{stageName}' for player {steamId} in zone '{zoneId}'");
        }
        
        /// <summary>
        /// Find character entity for a Steam ID.
        /// Uses EntityManager.HasComponent and EntityManager.Exists for safety.
        /// </summary>
        private Entity FindCharacterEntity(ulong steamId)
        {
            try
            {
                var entities = _playerQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var entity in entities)
                    {
                        if (entity == Entity.Null) continue;
                        if (!_entityManager.HasComponent<User>(entity)) continue;
                        if (!_entityManager.Exists(entity)) continue;
                        
                        var user = _entityManager.GetComponentData<User>(entity);
                        if (user.PlatformId == steamId)
                        {
                            return entity;
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
                _log.LogWarning($"{_logPrefix} Failed to find character entity: {ex.Message}");
            }
            return Entity.Null;
        }
        
        #endregion
        
        #region Spellbook Menu Lifecycle
        
        private bool _spellbookInitialized;
        
        /// <summary>
        /// Initialize spellbook menu integration.
        /// </summary>
        private void InitializeSpellbookMenu()
        {
            try
            {
                // Try to load ZUI spell menu integration
                var zuiType = Type.GetType("VLifecycle.Services.Lifecycle.ZUISpellMenu, VLifecycle");
                if (zuiType != null)
                {
                    var initMethod = zuiType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                    initMethod?.Invoke(null, null);
                    _spellbookInitialized = true;
                    _log.LogInfo($"{_logPrefix} Spellbook menu integration initialized");
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug($"{_logPrefix} Spellbook menu not available: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle spellbook menu on zone enter.
        /// </summary>
        private void HandleSpellbookOnEnter(ulong steamId, string zoneId)
        {
            var zoneDef = GetZoneDefinition(zoneId);
            if (zoneDef == null) return;
            
            // Check if spellbook should open on enter
            var mapping = ResolveMapping(zoneId, out _);
            
            if (mapping.EnableSpellbookMenu)
            {
                var menuStage = BuildStageName(SPELL_MENU_OPEN, zoneId);
                TriggerStage(menuStage, steamId, zoneId, float3.zero);
                
                if (!_spellMenuStates.TryGetValue(steamId, out var state))
                {
                    state = new SpellMenuState();
                    _spellMenuStates[steamId] = state;
                }
                
                state.MenuOpen = true;
                state.CurrentZoneId = zoneId;
            }
        }
        
        /// <summary>
        /// Handle spellbook menu on zone exit.
        /// </summary>
        private void HandleSpellbookOnExit(ulong steamId, string zoneId)
        {
            if (!_spellMenuStates.TryGetValue(steamId, out var state)) return;
            if (state.CurrentZoneId != zoneId) return;
            
            var mapping = ResolveMapping(zoneId, out _);
            
            if (state.MenuOpen)
            {
                var menuStage = BuildStageName(SPELL_MENU_CLOSE, zoneId);
                TriggerStage(menuStage, steamId, zoneId, float3.zero);
                
                state.MenuOpen = false;
                state.CurrentZoneId = "";
            }
        }
        
        /// <summary>
        /// Open spell menu for a player.
        /// </summary>
        public void OpenSpellMenu(ulong steamId, string category = "Combat")
        {
            try
            {
                var zuiType = Type.GetType("VLifecycle.Services.Lifecycle.ZUISpellMenu, VLifecycle");
                if (zuiType != null)
                {
                    var openMethod = zuiType.GetMethod("OpenSpellMenu", BindingFlags.Public | BindingFlags.Static);
                    openMethod?.Invoke(null, new object[] { steamId.ToString() });
                    
                    if (_spellMenuStates.TryGetValue(steamId, out var state))
                    {
                        state.LastCategory = category;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Failed to open spell menu: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Debug Event System Integration
        
        #region Reconnect/Respawn Handling
        
        /// <summary>
        /// Handle player reconnection - fire isInZone immediately.
        /// </summary>
        public void OnPlayerReconnected(ulong steamId, float3 position)
        {
            var zoneId = GetZoneId(position);
            if (string.IsNullOrEmpty(zoneId)) return;
            
            // Update player state
            if (!_playerStates.TryGetValue(steamId, out var state))
            {
                state = new PlayerZoneState { SteamId = steamId };
                _playerStates[steamId] = state;
            }
            
            state.CurrentZoneId = zoneId;
            state.WasInZone = true;
            
            // Fire isInZone immediately for enforcement
            var mapping = ResolveMapping(zoneId, out _);
            var isInZoneStage = BuildStageName(mapping.IsInZoneStage, zoneId);
            
            if (!string.IsNullOrEmpty(isInZoneStage))
            {
                TriggerStage(isInZoneStage, steamId, zoneId, position);
                state.LastIsInZoneTrigger = DateTime.UtcNow;
                
                _log.LogInfo($"{_logPrefix} Fired isInZone on reconnect for {steamId} in zone {zoneId}");
            }
            
            // Handle spellbook on reconnect
            HandleSpellbookOnEnter(steamId, zoneId);
            
            // Handle DebugEvent backup on reconnect
            HandleDebugEventOnEnter(steamId, zoneId);
        }
        
        /// <summary>
        /// Handle player respawn - fire isInZone immediately.
        /// </summary>
        public void OnPlayerRespawned(ulong steamId, float3 position)
        {
            var zoneId = GetZoneId(position);
            if (string.IsNullOrEmpty(zoneId)) return;
            
            // Fire isInZone for reassertion
            var mapping = ResolveMapping(zoneId, out _);
            var isInZoneStage = BuildStageName(mapping.IsInZoneStage, zoneId);
            
            if (!string.IsNullOrEmpty(isInZoneStage))
            {
                TriggerStage(isInZoneStage, steamId, zoneId, position);
                
                if (_playerStates.TryGetValue(steamId, out var state))
                {
                    state.LastIsInZoneTrigger = DateTime.UtcNow;
                }
                
                _log.LogInfo($"{_logPrefix} Fired isInZone on respawn for {steamId} in zone {zoneId}");
            }
        }
        
        #endregion
        
        #region Private Helpers
        
        private void DetectAndProcessTransition(PlayerZoneState state, string newZoneId, float3 position)
        {
            var prevZone = state.CurrentZoneId;
            var exitedZone = !string.IsNullOrEmpty(prevZone) && prevZone != newZoneId;
            var enteredZone = string.IsNullOrEmpty(prevZone) && !string.IsNullOrEmpty(newZoneId);
            var changedZone = !string.IsNullOrEmpty(prevZone) && !string.IsNullOrEmpty(newZoneId) && prevZone != newZoneId;
            var reconnection = !state.WasInZone && !string.IsNullOrEmpty(newZoneId) && string.IsNullOrEmpty(prevZone);
            
            if (exitedZone || changedZone)
            {
                OnPlayerExitZone(state.SteamId, prevZone, position);
            }
            
            if (enteredZone || changedZone || reconnection)
            {
                OnPlayerEnterZone(state.SteamId, newZoneId, position);
            }
            
            state.PreviousZoneId = prevZone;
            state.CurrentZoneId = newZoneId ?? "";
            state.WasInZone = !string.IsNullOrEmpty(newZoneId);
            state.LastZoneEnterTime = (enteredZone || reconnection) ? DateTime.UtcNow : state.LastZoneEnterTime;
        }
        
        private void CheckIsInZone(PlayerZoneState state, string zoneId, float3 position)
        {
            if (!state.WasInZone) return;
            
            var mapping = ResolveMapping(zoneId, out _);
            var stageName = BuildStageName(mapping.IsInZoneStage, zoneId);
            
            if (string.IsNullOrEmpty(stageName)) return;
            
            var now = DateTime.UtcNow;
            var elapsed = state.LastIsInZoneTrigger == default 
                ? double.MaxValue 
                : (now - state.LastIsInZoneTrigger).TotalSeconds;
            
            if (elapsed >= _config.IsInZoneIntervalSeconds)
            {
                _log.LogDebug($"{_logPrefix} Triggering isInZone for {state.SteamId} in zone {zoneId}");
                
                TriggerStage(stageName, state.SteamId, zoneId, position);
                state.LastIsInZoneTrigger = now;
            }
        }
        
        private void UpdatePlayerState(ulong steamId, string zoneId, float3 position)
        {
            if (!_playerStates.TryGetValue(steamId, out var state))
            {
                state = new PlayerZoneState { SteamId = steamId };
                _playerStates[steamId] = state;
            }
            
            state.CurrentZoneId = zoneId;
            state.LastPositionX = position.x;
            state.LastPositionY = position.y;
            state.LastPositionZ = position.z;
        }
        
        private void UpdatePosition(PlayerZoneState state, float3 position)
        {
            state.LastPositionX = position.x;
            state.LastPositionY = position.y;
            state.LastPositionZ = position.z;
            state.LastUpdate = DateTime.UtcNow;
        }
        
        private void UpdateIsInZoneTrigger(ulong steamId)
        {
            if (_playerStates.TryGetValue(steamId, out var state))
            {
                state.LastIsInZoneTrigger = DateTime.UtcNow;
            }
        }
        
        private void ClearPlayerState(ulong steamId)
        {
            if (_playerStates.TryGetValue(steamId, out var state))
            {
                state.PreviousZoneId = state.CurrentZoneId;
                state.CurrentZoneId = "";
                state.WasInZone = false;
            }
        }
        
        private void SendZoneMessage(ulong steamId, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            
            _log.LogInfo($"{_logPrefix} [To {steamId}] {message}");
            // Actual implementation would send chat message to player
        }
        
        private string GetZoneId(float3 position)
        {
            var zone = _config?.GetPrimaryZoneAtPosition(position);
            return zone?.Id ?? "";
        }
        
        private ZoneLifecycleConfig LoadConfig()
        {
            try
            {
                var configPath = GetConfigFilePath("VAuto.ZoneLifecycle.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ZoneLifecycleConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _log.LogInfo($"{_logPrefix} Loaded config from {configPath}");
                    return config ?? new ZoneLifecycleConfig();
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Failed to load config: {ex.Message}");
            }
            
            return new ZoneLifecycleConfig();
        }
        
        private string GetConfigFilePath(string fileName)
        {
            var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto");
            var primaryPath = Path.Combine(configDir, fileName);
            
            if (File.Exists(primaryPath)) return primaryPath;
            
            var pluginDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "config", "VAuto");
            var pluginPath = Path.Combine(pluginDir, fileName);
            
            if (File.Exists(pluginPath)) return pluginPath;
            
            return primaryPath;
        }
        
        private void DetectLegacyConfigs()
        {
            var legacyZonePath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "arena_zones.json");
            var legacyTerritoryPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "arena_territory.json");
            var legacyLifecyclePath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "VAuto.ZoneLifecycle.json");
            
            var hasLegacyFiles = File.Exists(legacyZonePath) || 
                                 File.Exists(legacyTerritoryPath) ||
                                 HasLegacyActions(legacyLifecyclePath);
            
            if (hasLegacyFiles)
            {
                _legacyModeEnabled = true;
                _log.LogWarning($@"
{_logPrefix} Legacy configuration files detected!
=========================================================
Legacy support is DEPRECATED. Please migrate to unified config:
- arena_zones.json → Use 'zones' array in VAuto.ZoneLifecycle.json
- arena_territory.json → Use 'territory' in zone definition
- onEnterActions/onExitActions → Use 'lifecycle' stage references
=========================================================
Legacy execution will be disabled after v2.0. Update your configs!
                ");
            }
        }
        
        private bool HasLegacyActions(string configPath)
        {
            if (!File.Exists(configPath)) return false;
            
            try
            {
                var json = File.ReadAllText(configPath);
                return json.Contains("onEnterActions") || json.Contains("onExitActions");
            }
            catch
            {
                return false;
            }
        }
        
        private UnifiedLifecycleMapping GetLegacyMapping(string zoneId)
        {
            return new UnifiedLifecycleMapping
            {
                OnEnterStage = "legacy.onEnter",
                OnExitStage = "legacy.onExit"
            };
        }
        
        private void OnCheckTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_disposed || !_isRunning) return;
            ScanAllPlayers();
        }
        
        private void ScanAllPlayers()
        {
            if (_disposed || !_isRunning) return;
            
            try
            {
                var entities = _playerQuery.ToEntityArray(Allocator.Temp);
                
                foreach (var entity in entities)
                {
                    var pc = _entityManager.GetComponentData<PlayerCharacter>(entity);
                    var userEntity = pc.UserEntity;
                    
                    if (userEntity == Entity.Null) continue;
                    if (!_entityManager.HasComponent<User>(userEntity)) continue;
                    
                    var user = _entityManager.GetComponentData<User>(userEntity);
                    var transform = _entityManager.GetComponentData<LocalTransform>(entity);
                    
                    CheckPlayerPosition(user.PlatformId, transform.Position);
                }
                
                entities.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Scan error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Get bridge statistics.
        /// </summary>
        public Dictionary<string, object> GetStats()
        {
            return new Dictionary<string, object>
            {
                ["IsRunning"] = _isRunning,
                ["IsInitialized"] = _isInitialized,
                ["Enabled"] = _config.Enabled,
                ["TrackedPlayers"] = _playerStates.Count,
                ["ZonesCount"] = _config.Zones?.Count ?? 0,
                ["LegacyModeEnabled"] = _legacyModeEnabled,
                ["SpellbookEnabled"] = _spellbookInitialized
            };
        }
        
        /// <summary>
        /// Get zone at player position.
        /// </summary>
        public string GetPlayerZone(ulong steamId)
        {
            return _playerStates.TryGetValue(steamId, out var state) 
                ? state.CurrentZoneId 
                : "";
        }
        
        /// <summary>
        /// Check if player is in a specific zone.
        /// </summary>
        public bool IsPlayerInZone(ulong steamId, string zoneId)
        {
            return _playerStates.TryGetValue(steamId, out var state) 
                && state.CurrentZoneId == zoneId 
                && state.WasInZone;
        }
        
        /// <summary>
        /// Get spell menu state for a player.
        /// </summary>
        public SpellMenuState GetSpellMenuState(ulong steamId)
        {
            return _spellMenuStates.TryGetValue(steamId, out var state) 
                ? state 
                : new SpellMenuState();
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            StopMonitoring();
            
            // Fire exit stages for all players in zones
            foreach (var state in _playerStates.Values.ToList())
            {
                if (!string.IsNullOrEmpty(state.CurrentZoneId))
                {
                    var position = new float3(state.LastPositionX, state.LastPositionY, state.LastPositionZ);
                    OnPlayerExitZone(state.SteamId, state.CurrentZoneId, position);
                }
            }
            
            _playerStates.Clear();
            _spellMenuStates.Clear();
            
            _log.LogInfo($"{_logPrefix} Bridge disposed");
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    /// <summary>
    /// Player zone tracking state.
    /// </summary>
    public class PlayerZoneState
    {
        public ulong SteamId { get; set; }
        public string CurrentZoneId { get; set; } = "";
        public string PreviousZoneId { get; set; } = "";
        public float LastPositionX { get; set; }
        public float LastPositionY { get; set; }
        public float LastPositionZ { get; set; }
        public DateTime LastZoneEnterTime { get; set; }
        public DateTime LastIsInZoneTrigger { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool WasInZone { get; set; }
    }
    
    /// <summary>
    /// Spell menu state tracking.
    /// </summary>
    public class SpellMenuState
    {
        public bool MenuOpen { get; set; }
        public string CurrentZoneId { get; set; } = "";
        public string LastCategory { get; set; } = "Combat";
        public List<string> FavoriteSpells { get; set; } = new();
    }
}
