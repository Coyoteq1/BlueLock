using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VAuto.Zone.Models;
using VAuto.Zone.Services;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using ProjectM.Gameplay.Systems;
using ProjectM.Debugging;
using ProjectM.Scripting;
using Stunlock.Core;
using VAutomationCore;
using VAutomationCore.Core;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Config;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Services;
namespace VAuto.Zone.Services
{
    /// <summary>
    /// Zone Event Bridge - Connects VAutoZone position monitoring to Vlifecycle stage execution.
    /// 
    /// Architecture:
    /// 1. Monitor player positions via ECS query
    /// 2. Detect zone transitions (enter, exit, reconnection, respawn)
    /// 3. read_file zone configuration to determine which stages should fire
    /// 4. Pass stage names to Vlifecycle's ArenaLifecycleManager
    /// 5. Vlifecycle owns actual action execution and behavior enforcement
    /// 
    /// Three-Stage Lifecycle Pattern:
    /// - onEnter: One-time effects when crossing INTO a zone
    ///   * Store inventory state, apply zone buffs, send messages, set markers
    ///   
    /// - isInZone: Repeated effects while player remains INSIDE zone
    ///   * Reassert buff enforcement, reapply blood type, validate config
    ///   * This stage must be IDEMPOTENT
    ///   
    /// - onExit: One-time effects when crossing OUT of zone
    ///   * Restore inventory, remove zone buffs, cleanup markers, send farewell
    /// 
    /// Handle Reconnection/Respawn Scenarios:
    /// - Player reconnects inside zone → fire onEnter + isInZone
    /// - Player respawns inside zone → fire isInZone (assume state was lost)
    /// 
    /// V Rising's Eventually Consistent ECS:
    /// - Systems may invalidate state (buffs expire, items unstored)
    /// - Mods must reassert intent periodically (isInZone)
    /// </summary>
    public class ZoneEventBridge : IDisposable
    {
        private static readonly string _logPrefix = "[ZoneEventBridge]";
        
        private readonly CoreLogger _log;
        private readonly EntityManager _entityManager;
        private readonly EntityQuery _playerQuery;
        private readonly Dictionary<ulong, PlayerZoneState> _playerStates = new();
        private readonly ZoneLifecycleConfig _config;
        
        private System.Timers.Timer _checkTimer;
        private bool _isRunning;
        private bool _disposed;

        public ZoneEventBridge(CoreLogger log, EntityManager entityManager)
        {
            _log = log;
            _entityManager = entityManager;
            
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerCharacter>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            _config = LoadConfig();
            
            _log.LogInfo($"{_logPrefix} Initialized with isInZone interval: {_config.IsInZoneIntervalSeconds}s");
        }

        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            ScanAllPlayers();
            
            var intervalMs = Math.Max(50, _config.CheckIntervalMs);
            _checkTimer = new System.Timers.Timer(intervalMs);
            _checkTimer.Elapsed += OnCheckTimerElapsed;
            _checkTimer.Start();
            
            _log.LogInfo($"{_logPrefix} Started monitoring ({intervalMs}ms interval)");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _checkTimer = null;
            
            _log.LogInfo($"{_logPrefix} Stopped");
        }

        public void CheckPlayer(ulong steamId, float3 position)
        {
            if (_disposed || !_isRunning) return;
            
            var currentZoneId = GetZoneId(position);
            
            if (!_playerStates.TryGetValue(steamId, out var state))
            {
                state = new PlayerZoneState { SteamId = steamId };
                _playerStates[steamId] = state;
                _log.LogDebug($"{_logPrefix} New player tracking: {steamId}");
            }

            DetectAndProcessTransition(state, currentZoneId, position);
            UpdatePosition(state, position);
            
            if (!string.IsNullOrEmpty(currentZoneId))
            {
                CheckIsInZone(state, currentZoneId, position);
            }
        }

        private void DetectAndProcessTransition(PlayerZoneState state, string newZoneId, float3 position)
        {
            var prevZone = state.CurrentZoneId;
            var exitedZone = !string.IsNullOrEmpty(prevZone) && prevZone != newZoneId;
            var enteredZone = string.IsNullOrEmpty(prevZone) && !string.IsNullOrEmpty(newZoneId);
            var changedZone = !string.IsNullOrEmpty(prevZone) && !string.IsNullOrEmpty(newZoneId) && prevZone != newZoneId;
            var reconnection = !state.WasInZone && !string.IsNullOrEmpty(newZoneId) && string.IsNullOrEmpty(prevZone);

            // Get character entity and user entity for lifecycle calls
            var characterEntity = FindCharacterEntity(state.SteamId);
            var userEntity = FindUserEntityFromCharacter(characterEntity);

            // Update PreviousZone before processing
            if (exitedZone || changedZone)
            {
                // Use ArenaLifecycleManager for exit
                if (characterEntity != Entity.Null)
                {
                    _log.LogDebug($"{_logPrefix} [DEBUG] OnPlayerExit - characterEntity: {characterEntity}, userEntity: {userEntity}");
                    ArenaLifecycleManager.Instance.OnPlayerExit(userEntity, characterEntity, prevZone, position);
                }
                else
                {
                    _log.LogWarning($"{_logPrefix} [DEBUG] OnPlayerExit skipped - characterEntity is Null");
                }
            }

            if (enteredZone || changedZone || reconnection)
            {
                // Use ArenaLifecycleManager for enter
                if (characterEntity != Entity.Null)
                {
                    _log.LogDebug($"{_logPrefix} [DEBUG] OnPlayerEnter - characterEntity: {characterEntity}, userEntity: {userEntity}");
                    ArenaLifecycleManager.Instance.OnPlayerEnter(userEntity, characterEntity, newZoneId, position);
                }

                state.LastIsInZoneTrigger = DateTime.UtcNow;
            }

            // Update tracking state
            state.PreviousZoneId = prevZone;
            state.CurrentZoneId = newZoneId ?? "";
            state.WasInZone = !string.IsNullOrEmpty(newZoneId);
            state.LastZoneEnterTime = (enteredZone || reconnection) ? DateTime.UtcNow : state.LastZoneEnterTime;
        }

        private void CheckIsInZone(PlayerZoneState state, string zoneId, float3 position)
        {
            if (!state.WasInZone) return;

            var now = DateTime.UtcNow;
            var elapsed = state.LastIsInZoneTrigger == default(DateTime)
                ? float.MaxValue
                : (float)(now - state.LastIsInZoneTrigger).TotalSeconds;

            if (elapsed >= _config.IsInZoneIntervalSeconds)
            {
                var characterEntity = FindCharacterEntity(state.SteamId);
                if (characterEntity != Entity.Null)
                {
                    var userEntity = FindUserEntityFromCharacter(characterEntity);
                    _log.LogDebug($"{_logPrefix} [DEBUG] CheckIsInZone - characterEntity: {characterEntity}, userEntity: {userEntity}");
                    _log.LogDebug($"{_logPrefix} Triggering isInZone for {state.SteamId} in zone {zoneId}");
                    ArenaLifecycleManager.Instance.OnPlayerEnter(userEntity, characterEntity, zoneId, position);
                }
                state.LastIsInZoneTrigger = now;
            }
        }

        private void UpdatePosition(PlayerZoneState state, float3 position)
        {
            state.LastPositionX = position.x;
            state.LastPositionY = position.y;
            state.LastPositionZ = position.z;
            state.LastUpdate = DateTime.UtcNow;
        }

        private void OnCheckTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_disposed || !_isRunning) return;
            ScanAllPlayers();
        }

        private void ScanAllPlayers()
        {
            if (_disposed || !_isRunning) return;

            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            try
            {
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
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Scan error: {ex.Message}");
            }
            finally
            {
                entities.Dispose();
            }
        }

        private void TriggerLifecycleStage(string stageName, ulong steamId, string zoneId, float3 position)
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
                ArenaLifecycleManager.Instance.OnPlayerEnter(characterEntity, characterEntity, zoneId, position);
            }
            else if (stageName.Contains("onExit"))
            {
                ArenaLifecycleManager.Instance.OnPlayerExit(characterEntity, characterEntity, zoneId, position);
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
                        if (!_entityManager.Exists(entity)) continue;
                        
                        // Get the UserEntity from PlayerCharacter
                        if (_entityManager.HasComponent<PlayerCharacter>(entity))
                        {
                            var pc = _entityManager.GetComponentData<PlayerCharacter>(entity);
                            var userEntity = pc.UserEntity;
                            
                            if (userEntity != Entity.Null && _entityManager.HasComponent<User>(userEntity))
                            {
                                var user = _entityManager.GetComponentData<User>(userEntity);
                                if (user.PlatformId == steamId)
                                {
                                    return entity; // Return character entity
                                }
                            }
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

        /// <summary>
        /// Get user entity from character entity using PlayerCharacter.UserEntity.
        /// Uses EntityManager.Exists for safety.
        /// </summary>
        private Entity FindUserEntityFromCharacter(Entity characterEntity)
        {
            if (characterEntity == Entity.Null || !_entityManager.Exists(characterEntity))
            {
                return Entity.Null;
            }

            try
            {
                if (_entityManager.HasComponent<PlayerCharacter>(characterEntity))
                {
                    var pc = _entityManager.GetComponentData<PlayerCharacter>(characterEntity);
                    var userEntity = pc.UserEntity;
                    
                    if (userEntity != Entity.Null && _entityManager.Exists(userEntity))
                    {
                        return userEntity;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Failed to get user entity from character: {ex.Message}");
            }
            
            return Entity.Null;
        }

        private string GetZoneId(float3 position)
        {
            if (ArenaTerritory.IsInArenaTerritory(position))
            {
                return ArenaTerritory.ZoneId;
            }
            
            return string.Empty;
        }

        private ZoneLifecycleConfig LoadConfig()
        {
            try
            {
                var configPath = GetConfigFilePath("VAuto.ZoneLifecycle.json");
                _log.LogDebug($"{_logPrefix} Loading config from: {configPath}");
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    _log.LogDebug($"{_logPrefix} JSON content length: {json.Length} chars");
                    
                    var config = JsonSerializer.Deserialize<ZoneLifecycleConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _log.LogDebug($"{_logPrefix} Config loaded - Enabled: {config?.Enabled}, CheckIntervalMs: {config?.CheckIntervalMs}, Mappings: {config?.Mappings?.Count}");
                    _log.LogInfo($"{_logPrefix} Loaded config from {configPath}");
                    return config ?? new ZoneLifecycleConfig();
                }
                else
                {
                    _log.LogWarning($"{_logPrefix} Config file not found at {configPath}");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"{_logPrefix} Failed to load config: {ex.Message}\n{ex.StackTrace}");
            }
            
            var defaultConfig = new ZoneLifecycleConfig();
            _log.LogDebug($"{_logPrefix} Using default config - IsInZoneIntervalSeconds: {defaultConfig.IsInZoneIntervalSeconds}");
            return defaultConfig;
        }

        private string GetConfigFilePath(string fileName)
        {
            var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto");
            var primaryPath = Path.Combine(configDir, fileName);
            
            if (File.Exists(primaryPath))
            {
                return primaryPath;
            }
            
            var pluginDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "config", "VAuto");
            var pluginPath = Path.Combine(pluginDir, fileName);
            if (File.Exists(pluginPath))
            {
                return pluginPath;
            }
            
            return primaryPath;
        }

        public Dictionary<string, object> GetStats()
        {
            var stats = new Dictionary<string, object>
            {
                ["IsRunning"] = _isRunning,
                ["TrackedPlayers"] = _playerStates.Count,
                ["Enabled"] = _config.Enabled,
                ["CheckIntervalMs"] = _config.CheckIntervalMs,
                ["IsInZoneIntervalSeconds"] = _config.IsInZoneIntervalSeconds,
                ["ZoneMappingsCount"] = _config.Mappings.Count
            };

            var zoneCounts = _playerStates.Values
                .Where(s => !string.IsNullOrEmpty(s.CurrentZoneId))
                .GroupBy(s => s.CurrentZoneId)
                .ToDictionary(g => g.Key, g => g.Count());
            
            stats["PlayersInZones"] = zoneCounts;
            
            return stats;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            Stop();
            
            // Fire exit stages for all players in zones before shutting down
            foreach (var state in _playerStates.Values.ToList())
            {
                if (!string.IsNullOrEmpty(state.CurrentZoneId))
                {
                    var position = new float3(state.LastPositionX, state.LastPositionY, state.LastPositionZ);
                    var stages = _config.GetStagesForZone(state.CurrentZoneId, out _);
                    var exitStageName = stages.BuildStageName(state.CurrentZoneId, "onExit");
                    
                    if (!string.IsNullOrEmpty(exitStageName))
                    {
                        TriggerLifecycleStage(exitStageName, state.SteamId, state.CurrentZoneId, position);
                    }
                }
            }
            
            _playerStates.Clear();
        }
    }
}
