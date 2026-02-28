using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Zone.Core;
using VAuto.Zone.Models;
using VAutomationCore.Core.Logging;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service responsible for detecting zone transitions and managing player zone states.
    /// Extracted from Plugin.cs to improve maintainability and testability.
    /// </summary>
    public static class ZoneDetectionService
    {
        private static readonly ManualLogSource _log = Plugin.Logger;
        private static readonly CoreLogger _coreLog = Plugin.CoreLog;
        
        // Zone detection state (main-thread only)
        private static readonly Dictionary<Entity, string> _playerZoneStates = new();
        private static readonly Dictionary<Entity, PendingZoneTransition> _pendingZoneTransitions = new();
        private static readonly Dictionary<Entity, DateTime> _lastCommittedZoneTransitions = new();
        private static readonly Dictionary<Entity, string> _playerZoneLocks = new();
        private static readonly Dictionary<Entity, string> _playerOriginalNames = new();
        
        private static float _lastZoneDetectionUpdateTime;
        private static readonly object _stateLock = new();

        private struct PendingZoneTransition
        {
            public string PreviousZoneId { get; set; }
            public string CandidateZoneId { get; set; }
            public DateTime FirstSeenUtc { get; set; }
        }

        /// <summary>
        /// Process zone detection for all players.
        /// Called from Plugin's main thread update loop.
        /// </summary>
        public static void ProcessZoneDetection()
        {
            try
            {
                if (!Plugin.IsEnabled || !Plugin.IntegrationSendZoneEventsValue)
                {
                    return;
                }

                var em = UnifiedCore.EntityManager;
                var now = (float)UnityEngine.Time.realtimeSinceStartup;
                var intervalSeconds = Math.Max(0.05f, Plugin.CheckIntervalMs / 1000f);
                
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

                        if (!string.IsNullOrWhiteSpace(previousZoneId) &&
                            IsZoneLockedForPlayer(player, previousZoneId, em))
                        {
                            _pendingZoneTransitions.Remove(player);
                            _playerZoneStates[player] = previousZoneId;
                            TryTeleportPlayerToZoneCenter(player, previousZoneId, em);
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

                // Cleanup stale tracked players
                CleanupStalePlayers(em, stillSeen);
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneDetectionService] ProcessZoneDetection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current zone state for a player.
        /// </summary>
        public static string GetPlayerZoneState(Entity player)
        {
            lock (_stateLock)
            {
                return _playerZoneStates.TryGetValue(player, out var zoneId) ? zoneId : string.Empty;
            }
        }

        /// <summary>
        /// Get all players currently in a specific zone.
        /// </summary>
        public static List<Entity> GetPlayersInZone(string zoneId)
        {
            lock (_stateLock)
            {
                return _playerZoneStates
                    .Where(kvp => string.Equals(kvp.Value, zoneId, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        /// <summary>
        /// Get the count of players in a specific zone.
        /// </summary>
        public static int GetPlayersInZoneCount(string zoneId)
        {
            lock (_stateLock)
            {
                return _playerZoneStates.Count(kvp => string.Equals(kvp.Value, zoneId, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Clear all zone state tracking for a player.
        /// </summary>
        public static void ClearPlayerState(Entity player)
        {
            lock (_stateLock)
            {
                _playerZoneStates.Remove(player);
                _pendingZoneTransitions.Remove(player);
                _lastCommittedZoneTransitions.Remove(player);
                _playerZoneLocks.Remove(player);
                _playerOriginalNames.Remove(player);
            }
        }

        /// <summary>
        /// Force a player into a specific zone (for admin commands).
        /// </summary>
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

                lock (_stateLock)
                {
                    _pendingZoneTransitions.Remove(player);
                    _lastCommittedZoneTransitions.Remove(player);
                    _playerZoneLocks.Remove(player);
                }

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
                
                lock (_stateLock)
                {
                    _playerZoneStates[player] = zone.Id;
                    _lastCommittedZoneTransitions[player] = DateTime.UtcNow;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneDetectionService] ForcePlayerEnterZone failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Force a player to exit their current zone.
        /// </summary>
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
                lock (_stateLock)
                {
                    if (_playerZoneStates.TryGetValue(player, out var tracked))
                    {
                        zoneId = tracked ?? string.Empty;
                    }
                }

                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    return false;
                }

                lock (_stateLock)
                {
                    _pendingZoneTransitions.Remove(player);
                    _lastCommittedZoneTransitions.Remove(player);
                    _playerZoneLocks.Remove(player);
                }
                
                HandleZoneExit(player, zoneId);
                
                lock (_stateLock)
                {
                    _playerZoneStates.Remove(player);
                }
                
                VAutomationCore.Services.ZoneEventBridge.RemovePlayerState(player);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneDetectionService] ForcePlayerExitZone failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a zone is locked for a player (e.g., during boss fights).
        /// </summary>
        private static bool IsZoneLockedForPlayer(Entity player, string zoneId, EntityManager em)
        {
            lock (_stateLock)
            {
                if (!_playerZoneLocks.TryGetValue(player, out var lockedZoneId) ||
                    string.IsNullOrWhiteSpace(lockedZoneId) ||
                    !string.Equals(lockedZoneId, zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Unlock on death
                if (em.HasComponent<Health>(player))
                {
                    var health = em.GetComponentData<Health>(player);
                    if (health.Value <= 0f)
                    {
                        _playerZoneLocks.Remove(player);
                        return false;
                    }
                }

                // Unlock on win (zone boss no longer alive)
                if (!ZoneBossSpawnerService.IsZoneBossAlive(zoneId))
                {
                    _playerZoneLocks.Remove(player);
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Determine if a zone transition should be committed based on timing and cooldowns.
        /// </summary>
        private static bool ShouldCommitZoneTransition(Entity player, string previousZoneId, string candidateZoneId, DateTime nowUtc)
        {
            var requiredSeconds = string.IsNullOrWhiteSpace(candidateZoneId)
                ? Plugin.ZoneExitTransitionConfirmSecondsValue
                : Plugin.ZoneEnterTransitionConfirmSecondsValue;

            lock (_stateLock)
            {
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
                    (nowUtc - lastCommittedUtc).TotalSeconds < Plugin.ZoneTransitionCooldownSecondsValue)
                {
                    return false;
                }

                _pendingZoneTransitions.Remove(player);
                _lastCommittedZoneTransitions[player] = nowUtc;
                return true;
            }
        }

        /// <summary>
        /// Handle zone exit logic.
        /// </summary>
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
                _coreLog.LogDebug($"[ZoneDetectionService] Zone '{zoneId}' exit actions: {string.Join(", ", actionOrder)}");
                
                var context = new PluginZoneLifecycleContext(player, zoneId, em);
                foreach (var step in Plugin._zoneLifecycleStepRegistry.BuildExitSteps(actionOrder))
                {
                    step.Execute(context);
                }

                ZoneNoDurabilityService.StopTracking(player, em);
                
                lock (_stateLock)
                {
                    _playerZoneLocks.Remove(player);
                    TryRestoreOriginalPlayerName(player, em);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneDetectionService] HandleZoneExit failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle zone enter logic.
        /// </summary>
        private static void HandleZoneEnter(Entity player, string zoneId)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(player))
                {
                    return;
                }

                ZoneNoDurabilityService.StartTracking(player, em);

                if (IsSandboxZone(zoneId))
                {
                    TryRunZoneEnterStep("DebugEventBridge.OnZoneEnterStart", () => TryInvokeDebugEventBridgeZoneEnterStart(player, zoneId));
                }

                if (!string.IsNullOrWhiteSpace(zoneId))
                {
                    lock (_stateLock)
                    {
                        _playerZoneLocks[player] = zoneId;
                    }
                }
                TryApplyZoneNameTag(player, zoneId, em);

                var actionOrder = ResolveLifecycleActionsForZone(zoneId, isEnter: true);
                _coreLog.LogDebug($"[ZoneDetectionService] Zone '{zoneId}' enter actions: {string.Join(", ", actionOrder)}");
                
                var context = new PluginZoneLifecycleContext(player, zoneId, em);
                foreach (var step in Plugin._zoneLifecycleStepRegistry.BuildEnterSteps(actionOrder))
                {
                    step.Execute(context);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[ZoneDetectionService] HandleZoneEnter failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleanup players that are no longer in the query.
        /// </summary>
        private static void CleanupStalePlayers(EntityManager em, HashSet<Entity> stillSeen)
        {
            var stalePlayers = new List<Entity>();
            
            lock (_stateLock)
            {
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
                    _playerZoneLocks.Remove(stale);
                    _playerOriginalNames.Remove(stale);
                    
                    KitService.ClearPlayerTrackingForEntity(stale, em);
                    AbilityUi.ClearStateForDisconnectedPlayer(stale, em);
                    VAutomationCore.Services.ZoneEventBridge.RemovePlayerZoneState(stale);
                }
            }
        }

        /// <summary>
        /// Resolve lifecycle actions for a zone based on configuration.
        /// </summary>
        private static IReadOnlyList<string> ResolveLifecycleActionsForZone(string zoneId, bool isEnter)
        {
            var defaults = isEnter ? Plugin.DefaultEnterLifecycleActions : Plugin.DefaultExitLifecycleActions;
            if (Plugin._jsonConfig == null || !Plugin._jsonConfig.Enabled || Plugin._jsonConfig.Mappings == null || Plugin._jsonConfig.Mappings.Count == 0)
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

        /// <summary>
        /// Get lifecycle mapping for a zone.
        /// </summary>
        private static bool TryGetLifecycleMappingForZone(string zoneId, out ZoneMapping mapping)
        {
            mapping = null;
            if (Plugin._jsonConfig?.Mappings == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(zoneId) && Plugin._jsonConfig.Mappings.TryGetValue(zoneId, out mapping))
            {
                return true;
            }

            if (Plugin._jsonConfig.Mappings.TryGetValue("*", out mapping))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Normalize lifecycle action tokens.
        /// </summary>
        private static string NormalizeLifecycleActionToken(string rawToken, bool isEnter)
        {
            if (!LifecycleActionToken.TryParse(rawToken, out var baseToken, out var parameter))
            {
                return string.Empty;
            }

            var token = baseToken;
            string normalized;
            if (isEnter)
            {
                normalized = token switch
                {
                    "store" => "snapshot_save",
                    "snapshot" => "snapshot_save",
                    "blood" => "set_blood",
                    "message" => "zone_enter_message",
                    "kit_apply" => "apply_kit",
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
            else
            {
                normalized = token switch
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

            return string.IsNullOrWhiteSpace(parameter)
                ? normalized
                : $"{normalized}:{parameter}";
        }

        /// <summary>
        /// Apply zone name tag to player.
        /// </summary>
        private static void TryApplyZoneNameTag(Entity player, string zoneId, EntityManager em)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    return;
                }

                if (!em.HasComponent<PlayerCharacter>(player))
                {
                    return;
                }

                var pc = em.GetComponentData<PlayerCharacter>(player);
                var userEntity = pc.UserEntity;
                if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return;
                }

                var user = em.GetComponentData<User>(userEntity);
                var currentName = user.CharacterName.ToString();
                if (string.IsNullOrWhiteSpace(currentName))
                {
                    return;
                }

                lock (_stateLock)
                {
                    if (!_playerOriginalNames.ContainsKey(player))
                    {
                        _playerOriginalNames[player] = StripLeadingZoneTag(currentName);
                    }

                    var baseName = _playerOriginalNames[player];
                    var tagged = $"[{zoneId}] {baseName}";
                    if (string.Equals(currentName, tagged, StringComparison.Ordinal))
                    {
                        return;
                    }

                    user.CharacterName = tagged;
                    em.SetComponentData(userEntity, user);
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug($"[ZoneDetectionService] Zone name tag apply failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore original player name.
        /// </summary>
        private static void TryRestoreOriginalPlayerName(Entity player, EntityManager em)
        {
            try
            {
                lock (_stateLock)
                {
                    if (!_playerOriginalNames.TryGetValue(player, out var original) || string.IsNullOrWhiteSpace(original))
                    {
                        return;
                    }

                    if (!em.HasComponent<PlayerCharacter>(player))
                    {
                        _playerOriginalNames.Remove(player);
                        return;
                    }

                    var pc = em.GetComponentData<PlayerCharacter>(player);
                    var userEntity = pc.UserEntity;
                    if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                    {
                        _playerOriginalNames.Remove(player);
                        return;
                    }

                    var user = em.GetComponentData<User>(userEntity);
                    user.CharacterName = original;
                    em.SetComponentData(userEntity, user);
                    _playerOriginalNames.Remove(player);
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug($"[ZoneDetectionService] Zone name tag restore failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Strip leading zone tag from name.
        /// </summary>
        private static string StripLeadingZoneTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            if (name.Length > 3 && name[0] == '[')
            {
                var close = name.IndexOf(']');
                if (close > 0 && close + 2 <= name.Length && name[close + 1] == ' ')
                {
                    return name[(close + 2)..];
                }
            }

            return name;
        }

        /// <summary>
        /// Teleport player to zone center.
        /// </summary>
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
                _log.LogWarning($"[ZoneDetectionService] Teleport to zone center failed ({zoneId}): {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get or create player query for zone detection.
        /// </summary>
        private static EntityQuery GetOrCreateAutoZonePlayerQuery(EntityManager em)
        {
            return em.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
        }

        /// <summary>
        /// Get best position for an entity.
        /// </summary>
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

        /// <summary>
        /// Check if zone is a sandbox zone.
        /// </summary>
        private static bool IsSandboxZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return false;
            }

            return ZoneConfigService.HasTag(zoneId, "sandbox");
        }

        /// <summary>
        /// Try to invoke debug event bridge for zone enter start.
        /// </summary>
        private static void TryInvokeDebugEventBridgeZoneEnterStart(Entity characterEntity, string zoneId)
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
                    _log.LogDebug($"[ZoneDetectionService] DebugEventBridge type not found for zone '{zoneId}' (enter start)");
                    return;
                }

                var zoneUnlockEnabled = ZoneConfigService.IsSandboxUnlockEnabled(zoneId, Plugin.SandboxProgressionDefaultZoneUnlockEnabledValue);
                var method = bridgeType.GetMethod(
                    "OnPlayerEnterZone",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity), typeof(bool) },
                    null);

                if (method != null)
                {
                    _log.LogDebug($"[ZoneDetectionService] Invoking DebugEventBridge.OnPlayerEnterZone(entity={characterEntity.Index}:{characterEntity.Version}, enableUnlock={zoneUnlockEnabled}) for zone '{zoneId}'");
                    method.Invoke(null, new object[] { characterEntity, zoneUnlockEnabled });
                    _log.LogInfo($"[ZoneDetectionService] DebugEventBridge.OnPlayerEnterZone completed for zone '{zoneId}'");
                    return;
                }

                method = bridgeType.GetMethod(
                    "OnPlayerEnterZone",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity) },
                    null);

                if (method == null)
                {
                    _log.LogDebug($"[ZoneDetectionService] DebugEventBridge.OnPlayerEnterZone method not found for zone '{zoneId}'");
                    return;
                }

                _log.LogDebug($"[ZoneDetectionService] Invoking DebugEventBridge.OnPlayerEnterZone(entity={characterEntity.Index}:{characterEntity.Version}) for zone '{zoneId}'");
                method.Invoke(null, new object[] { characterEntity });
                _log.LogInfo($"[ZoneDetectionService] DebugEventBridge.OnPlayerEnterZone completed for zone '{zoneId}'");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneDetectionService] DebugEventBridge reflection invoke failed for zone '{zoneId}': {ex.ToString()}");
            }
        }

        /// <summary>
        /// Try to invoke debug event bridge for player in zone.
        /// </summary>
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
                    _log.LogDebug($"[ZoneDetectionService] DebugEventBridge type not found for OnPlayerIsInZone");
                    return;
                }

                var zoneUnlockEnabled = ZoneConfigService.IsSandboxUnlockEnabled(zoneId, Plugin.SandboxProgressionDefaultZoneUnlockEnabledValue);
                var method = bridgeType.GetMethod(
                    "OnPlayerIsInZone",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Entity), typeof(string), typeof(bool) },
                    null);

                if (method == null)
                {
                    _log.LogDebug($"[ZoneDetectionService] DebugEventBridge.OnPlayerIsInZone method not found");
                    return;
                }

                _log.LogDebug($"[ZoneDetectionService] Invoking DebugEventBridge.OnPlayerIsInZone(entity={characterEntity.Index}:{characterEntity.Version}, zone='{zoneId}', enableUnlock={zoneUnlockEnabled})");
                method.Invoke(null, new object[] { characterEntity, zoneId, zoneUnlockEnabled });
                _log.LogInfo($"[ZoneDetectionService] DebugEventBridge.OnPlayerIsInZone completed");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneDetectionService] DebugEventBridge reflection invoke failed (OnPlayerIsInZone): {ex.ToString()}");
            }
        }

        /// <summary>
        /// Run zone enter step with error handling.
        /// </summary>
        private static void TryRunZoneEnterStep(string stepName, Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneDetectionService] ZoneEnter step '{stepName}' failed: {ex}");
            }
        }

        /// <summary>
        /// Run zone exit step with error handling.
        /// </summary>
        private static void TryRunZoneExitStep(string stepName, Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneDetectionService] ZoneExit step '{stepName}' failed: {ex}");
            }
        }

        /// <summary>
        /// Capture return position for zone exit.
        /// </summary>
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
                    _log.LogDebug($"[ZoneDetectionService] Return position capture skipped: could not resolve platform id for entity {player.Index}:{player.Version}");
                    return;
                }

                if (VAuto.Zone.Plugin._zoneReturnPositions.ContainsKey(platformId))
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
                if (VAuto.Zone.Plugin._zoneReturnPositions.Count >= MaxStoredPositions)
                {
                    var oldestPlatformId = VAuto.Zone.Plugin._zoneReturnPositions.Keys.First();
                    VAuto.Zone.Plugin._zoneReturnPositions.Remove(oldestPlatformId);
                    _log.LogDebug($"[ZoneDetectionService] Evicted return position for platform {oldestPlatformId}");
                }

                VAuto.Zone.Plugin._zoneReturnPositions[platformId] = pos;
                _log.LogDebug($"[ZoneDetectionService] Captured return position for platform {platformId}: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[ZoneDetectionService] CaptureReturnPositionIfNeeded failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate return position.
        /// </summary>
        private static bool IsValidReturnPosition(float3 pos)
        {
            return !(Math.Abs(pos.x) < 0.5f && Math.Abs(pos.z) < 0.5f);
        }

        /// <summary>
        /// Resolve platform ID for player.
        /// </summary>
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
    }
}