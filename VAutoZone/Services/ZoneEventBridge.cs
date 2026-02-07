using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAutomationCore.Configuration;
using VAuto.Core.Lifecycle;
using VAuto.Zone.Services;
using ProjectM;
using ProjectM.Network;

namespace VAuto.Zone
{
    /// <summary>
    /// Bridge service that monitors player positions and triggers lifecycle events
    /// when players enter/exit arena zones.
    /// Uses the ArenaTerritory system for zone detection.
    /// </summary>
    public class ZoneEventBridge : IDisposable
    {
        private static readonly string _logPrefix = "[ZoneEventBridge]";
        
        private readonly ManualLogSource _log;
        private readonly EntityManager _entityManager;
        private readonly EntityQuery _playerQuery;
        private readonly Dictionary<ulong, PlayerZoneState> _playerStates = new();
        private readonly ZoneLifecycleConfig _config;
        private readonly GlobalLifecycleDefaults _defaults;
        
        private System.Timers.Timer _checkTimer;
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Tracks the last known zone for each player
        /// </summary>
        private class PlayerZoneState
        {
            public ulong SteamId { get; set; }
            public string LastZoneId { get; set; } = string.Empty;
            public float3 LastPosition { get; set; }
            public DateTime LastUpdate { get; set; }
        }

        public ZoneEventBridge(ManualLogSource log, EntityManager entityManager)
        {
            _log = log;
            _entityManager = entityManager;
            
            // Create player query for position updates (User + LocalTransform + PlayerCharacter)
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerCharacter>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            // Load configuration
            _config = LoadConfig();
            _defaults = LoadDefaults();
            
            _log.LogInfo($"{_logPrefix} Initialized with {_config.Mappings.Count} zone mappings");
        }

        /// <summary>
        /// Start monitoring player positions
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            
            // Initial scan
            ScanAllPlayers();
            
            // Setup timer for periodic checks
            var intervalMs = Math.Max(50, _config.CheckIntervalMs);
            _checkTimer = new System.Timers.Timer(intervalMs);
            _checkTimer.Elapsed += OnCheckTimerElapsed;
            _checkTimer.Start();
            
            _log.LogInfo($"{_logPrefix} Started monitoring ({intervalMs}ms interval)");
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _checkTimer = null;
            
            _log.LogInfo($"{_logPrefix} Stopped");
        }

        /// <summary>
        /// Force check for a specific player
        /// </summary>
        public void CheckPlayer(ulong steamId, float3 position)
        {
            if (_disposed || !_isRunning) return;
            
            var currentZoneId = GetZoneId(position);
            
            if (!_playerStates.TryGetValue(steamId, out var state))
            {
                // New player tracking
                state = new PlayerZoneState { SteamId = steamId };
                _playerStates[steamId] = state;
            }

            var hasChanged = state.LastZoneId != currentZoneId;
            var isFirstCheck = string.IsNullOrEmpty(state.LastZoneId);

            if (isFirstCheck || hasChanged)
            {
                var previousZone = state.LastZoneId;
                state.LastZoneId = currentZoneId;
                state.LastPosition = position;
                state.LastUpdate = DateTime.UtcNow;

                if (hasChanged && !isFirstCheck)
                {
                    // Player transitioned between zones
                    OnZoneTransition(steamId, previousZone, currentZoneId, position);
                }
                else if (isFirstCheck && !string.IsNullOrEmpty(currentZoneId))
                {
                    // Player started inside a zone
                    OnZoneEnter(steamId, currentZoneId, position);
                }
            }
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
                    
                    CheckPlayer(user.PlatformId, transform.Position);
                }
                
                entities.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Scan error: {ex.Message}");
            }
        }

        private void OnZoneTransition(ulong steamId, string previousZone, string currentZone, float3 position)
        {
            if (!string.IsNullOrEmpty(previousZone))
            {
                OnZoneExit(steamId, previousZone, position);
            }
            
            if (!string.IsNullOrEmpty(currentZone))
            {
                OnZoneEnter(steamId, currentZone, position);
            }
        }

        private void OnZoneEnter(ulong steamId, string zoneId, float3 position)
        {
            _log.LogInfo($"{_logPrefix} Player {steamId} entered zone '{zoneId}'");
            
            // Trigger lifecycle events
            TriggerLifecycleActions(steamId, zoneId, true, position);
        }

        private void OnZoneExit(ulong steamId, string zoneId, float3 position)
        {
            _log.LogInfo($"{_logPrefix} Player {steamId} exited zone '{zoneId}'");
            
            // Trigger lifecycle events
            TriggerLifecycleActions(steamId, zoneId, false, position);
        }

        private void TriggerLifecycleActions(ulong steamId, string zoneId, bool isEnter, float3 position)
        {
            if (!_config.Enabled)
            {
                _log.LogDebug($"{_logPrefix} Zone-lifecycle wiring disabled");
                return;
            }

            // Get lifecycle manager if available
            var lifecycleManager = GetLifecycleManager();
            if (lifecycleManager == null)
            {
                _log.LogWarning($"{_logPrefix} Lifecycle manager not available");
                return;
            }

            // Get actions for this zone
            var actions = GetActionsForZone(zoneId, isEnter);
            if (actions.Count == 0)
            {
                _log.LogDebug($"{_logPrefix} No actions configured for zone '{zoneId}' ({ (isEnter ? "enter" : "exit") })");
                return;
            }

            _log.LogInfo($"{_logPrefix} Triggering {actions.Count} actions for {steamId} in zone '{zoneId}'");

            // Execute each action
            foreach (var actionName in actions)
            {
                try
                {
                    ExecuteLifecycleAction(lifecycleManager, steamId, actionName, isEnter);
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"{_logPrefix} Failed to execute action '{actionName}': {ex.Message}");
                }
            }
        }

        private List<string> GetActionsForZone(string zoneId, bool isEnter)
        {
            if (_config.Mappings.TryGetValue(zoneId, out var mapping))
            {
                return isEnter ? mapping.OnEnter : mapping.OnExit;
            }

            // Check for wildcard mapping
            if (_config.Mappings.TryGetValue("*", out var wildcard))
            {
                return isEnter ? wildcard.OnEnter : wildcard.OnExit;
            }

            // Return defaults if configured
            return isEnter ? _defaults.DefaultEnterActions : _defaults.DefaultExitActions;
        }

        private void ExecuteLifecycleAction(ArenaLifecycleManager lifecycleManager, ulong steamId, string actionName, bool isEnter)
        {
            var stageName = isEnter ? "onEnterArenaZone" : "onExitArenaZone";
            
            // Check if we have actions configured for this stage
            var stageActions = lifecycleManager.GetStageDetails(stageName);
            if (stageActions.TryGetValue("ActionCount", out var count) && (int)count > 0)
            {
                _log.LogDebug($"{_logPrefix} Using pre-configured stage '{stageName}'");
                lifecycleManager.TriggerLifecycleStage(stageName, new LifecycleContext
                {
                    CharacterEntity = Entity.Null,
                    Position = float3.zero
                });
            }
            else
            {
                _log.LogDebug($"{_logPrefix} Stage '{stageName}' has no actions configured");
            }
        }

        private string GetZoneId(float3 position)
        {
            // Use ArenaTerritory to check if position is in arena
            if (ArenaTerritory.IsInArenaTerritory(position))
            {
                return ArenaTerritory.ZoneId;
            }
            
            return string.Empty;
        }

        private ArenaLifecycleManager GetLifecycleManager()
        {
            try
            {
                // Try to get ArenaLifecycleManager via reflection (loaded by Vlifecycle)
                var type = Type.GetType("VAuto.Core.Lifecycle.ArenaLifecycleManager, Vlifecycle");
                if (type != null)
                {
                    var instanceProp = type.GetProperty("Instance", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    return instanceProp?.GetValue(null) as ArenaLifecycleManager;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Failed to get lifecycle manager: {ex.Message}");
            }
            
            return null;
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

        private GlobalLifecycleDefaults LoadDefaults()
        {
            try
            {
                var configPath = GetConfigFilePath("VAuto.LifecycleDefaults.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<GlobalLifecycleDefaults>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new GlobalLifecycleDefaults();
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Failed to load defaults: {ex.Message}");
            }
            
            return new GlobalLifecycleDefaults();
        }

        private string GetConfigFilePath(string fileName)
        {
            // Check multiple locations
            var locations = new[]
            {
                Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", fileName),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "config", "VAuto", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "VAuto", fileName)
            };

            foreach (var path in locations)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return locations[0];
        }

        /// <summary>
        /// Get current tracking statistics
        /// </summary>
        public Dictionary<string, object> GetStats()
        {
            var stats = new Dictionary<string, object>
            {
                ["IsRunning"] = _isRunning,
                ["TrackedPlayers"] = _playerStates.Count,
                ["Enabled"] = _config.Enabled,
                ["CheckIntervalMs"] = _config.CheckIntervalMs,
                ["ZoneMappings"] = _config.Mappings.Count
            };

            // Count players in each zone
            var zoneCounts = _playerStates.Values
                .Where(s => !string.IsNullOrEmpty(s.LastZoneId))
                .GroupBy(s => s.LastZoneId)
                .ToDictionary(g => g.Key, g => g.Count());
            
            stats["PlayersInZones"] = zoneCounts;
            
            return stats;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            Stop();
            _playerStates.Clear();
        }
    }
}
