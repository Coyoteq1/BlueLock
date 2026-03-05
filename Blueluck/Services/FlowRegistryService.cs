using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Core;
using VAuto.Services.Interfaces;
using Blueluck.Models;
using VAutomationCore.Core;
using VAutomationCore.Core.Api;
using VAutomationCore.Services;

namespace Blueluck.Services
{
    /// <summary>
    /// Service for managing and executing flows on zone transitions.
    /// Implements IService from VAutomationCore.
    /// </summary>
    public class FlowRegistryService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.FlowRegistry");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private string _configPath;
        private readonly Dictionary<string, List<FlowAction>> _flows = new(StringComparer.OrdinalIgnoreCase);
        private PrefabToGuidService? _prefabToGuid;
        private readonly Dictionary<int, List<Entity>> _borderFxByZone = new();
        private readonly Dictionary<int, List<Entity>> _bossesByZone = new();

        // Essential flow actions only
        public static class FlowActions
        {
            public const string SetPvp = "zone.setpvp";
            public const string SendMessage = "zone.sendmessage";
            public const string SpawnBoss = "zone.spawnboss";
            public const string RemoveBoss = "zone.removeboss";
            public const string ApplyBorderFx = "zone.applyborderfx";
            public const string RemoveBorderFx = "zone.removeborderfx";
            public const string ApplyZoneBuff = "zone.applyzonebuff";
            public const string RemoveZoneBuff = "zone.removezonebuff";
        }

        public void Initialize()
        {
            Plugin.EnsureConfigFile(
                "flows.json",
                json =>
                {
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.TryGetProperty("flows", out var flows)
                        && flows.ValueKind == JsonValueKind.Object
                        && flows.EnumerateObject().MoveNext();
                },
                new
                {
                    flows = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                });

            _configPath = Path.Combine(Paths.ConfigPath, "Blueluck", "flows.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? Paths.ConfigPath);

            _prefabToGuid = Plugin.PrefabToGuid;

            RegisterFlowActionAliases();
            LoadFlows();

            IsInitialized = true;
            _log.LogInfo($"[FlowRegistry] Initialized with {_flows.Count} flows.");
        }

        public void Cleanup()
        {
            CleanupAllSpawnedEntities();
            _flows.Clear();
            IsInitialized = false;
            _log.LogInfo("[FlowRegistry] Cleaned up.");
        }

        /// <summary>
        /// Registers flow action aliases with Core's FlowService.
        /// </summary>
        private void RegisterFlowActionAliases()
        {
            try
            {
                // Register only essential action aliases
                FlowService.RegisterActionAlias(FlowActions.SetPvp, "SetPvp", replace: true);
                FlowService.RegisterActionAlias(FlowActions.SendMessage, "SendMessage", replace: true);
                FlowService.RegisterActionAlias(FlowActions.SpawnBoss, "SpawnBoss", replace: true);
                FlowService.RegisterActionAlias(FlowActions.RemoveBoss, "RemoveBoss", replace: true);
                FlowService.RegisterActionAlias(FlowActions.ApplyBorderFx, "ApplyBorderFx", replace: true);
                FlowService.RegisterActionAlias(FlowActions.RemoveBorderFx, "RemoveBorderFx", replace: true);
                FlowService.RegisterActionAlias(FlowActions.ApplyZoneBuff, "ApplyZoneBuff", replace: true);
                FlowService.RegisterActionAlias(FlowActions.RemoveZoneBuff, "RemoveZoneBuff", replace: true);

                _log.LogInfo("[FlowRegistry] Registered essential action aliases.");
            }
            catch (Exception ex)
            {
                _log.LogError($"[FlowRegistry] Failed to register action aliases: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads flows from config file.
        /// </summary>
        public void LoadFlows()
        {
            _flows.Clear();

            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateDefaultFlows();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 128
                };

                var config = JsonSerializer.Deserialize<FlowConfig>(json, options);
                if (config?.Flows != null)
                {
                    foreach (var flow in config.Flows)
                    {
                        _flows[flow.Key] = flow.Value.ToList();
                    }
                }

                _log.LogInfo($"[FlowRegistry] Loaded {_flows.Count} flows.");
            }
            catch (Exception ex)
            {
                _log.LogError($"[FlowRegistry] Failed to load flows: {ex.Message}");
            }
        }

        /// <summary>
        /// Reloads flows from disk.
        /// </summary>
        public void Reload()
        {
            LoadFlows();
        }

        /// <summary>
        /// Executes a flow by ID for a player.
        /// </summary>
        public void ExecuteFlow(string flowId, Entity player, string zoneId, int zoneHash)
        {
            if (string.IsNullOrEmpty(flowId))
                return;

            if (!_flows.TryGetValue(flowId, out var actions))
            {
                _log.LogWarning($"[FlowRegistry] Flow not found: {flowId}");
                return;
            }

            _log.LogInfo($"[FlowRegistry] Executing flow: {flowId} for player {player.Index}");

            foreach (var action in actions)
            {
                ExecuteAction(action, player, zoneId, zoneHash);
            }
        }

        // Public, non-config entry points for zone gameplay logic (ZoneTransitionService uses these as a fallback
        // when no explicit FlowOnEnter/FlowOnExit is configured on the zone).
        public void SetPvp(Entity player, bool enabled, int zoneHash = 0)
        {
            HandleSetPvp(new FlowAction { Action = FlowActions.SetPvp, Value = enabled }, player, zoneHash);
        }

        public void SendMessage(Entity player, string message, int zoneHash = 0)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            HandleSendMessage(new FlowAction { Action = FlowActions.SendMessage, Message = message }, player, zoneHash);
        }

        public void ApplyBorderFx(Entity player, int zoneHash, string vfxPrefab, float radius, int segments)
        {
            HandleApplyBorderFx(new FlowAction
            {
                Action = FlowActions.ApplyBorderFx,
                VfxPrefab = vfxPrefab,
                Radius = radius,
                Segments = segments
            }, player, zoneHash);
        }

        public void RemoveBorderFx(int zoneHash)
        {
            RemoveBorderFxInternal(zoneHash);
        }

        public bool EnsureBosses(Entity player, int zoneHash, string bossPrefab, int quantity = 1, bool randomInZone = true, float3? position = null)
        {
            if (string.IsNullOrWhiteSpace(bossPrefab))
                return false;

            PruneDeadBossEntities(zoneHash);
            if (_bossesByZone.TryGetValue(zoneHash, out var existing) && existing.Count > 0)
                return false;

            var action = new FlowAction
            {
                Action = FlowActions.SpawnBoss,
                Prefab = bossPrefab,
                Quantity = quantity,
                RandomInZone = randomInZone
            };

            if (position.HasValue)
            {
                action.Position = new[] { position.Value.x, position.Value.y, position.Value.z };
            }

            HandleSpawnBoss(action, player, zoneHash);
            return true;
        }

        public void RemoveBosses(int zoneHash)
        {
            RemoveBossesInternal(zoneHash);
        }

        private void ExecuteAction(FlowAction action, Entity player, string zoneId, int zoneHash)
        {
            try
            {
                switch (action.Action)
                {
                    case FlowActions.SetPvp:
                        HandleSetPvp(action, player, zoneHash);
                        break;
                    case FlowActions.SendMessage:
                        HandleSendMessage(action, player, zoneHash);
                        break;
                    case FlowActions.SpawnBoss:
                        HandleSpawnBoss(action, player, zoneHash);
                        break;
                    case FlowActions.RemoveBoss:
                        HandleRemoveBoss(action, player, zoneHash);
                        break;
                    case FlowActions.ApplyBorderFx:
                        HandleApplyBorderFx(action, player, zoneHash);
                        break;
                    case FlowActions.RemoveBorderFx:
                        HandleRemoveBorderFx(action, player, zoneHash);
                        break;
                    case FlowActions.ApplyZoneBuff:
                        HandleApplyZoneBuff(action, player, zoneHash);
                        break;
                    case FlowActions.RemoveZoneBuff:
                        HandleRemoveZoneBuff(action, player, zoneHash);
                        break;
                    default:
                        _log.LogWarning($"[FlowRegistry] Unknown action: {action.Action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[FlowRegistry] Error executing action {action.Action}: {ex.Message}");
            }
        }

        private void HandleSetPvp(FlowAction action, Entity player, int zoneHash)
        {
            var enabled = action.Value is bool b && b;
            _log.LogInfo($"[FlowRegistry] SetPvp: {enabled} for player {player.Index}");

            if (!TryResolveUserEntity(player, out var userEntity))
            {
                _log.LogWarning("[FlowRegistry] SetPvp skipped: user entity not resolved.");
                return;
            }

            if (!TryResolvePrefabGuid("Buff_PvP_Enabled", out var pvpBuffGuid))
            {
                _log.LogWarning("[FlowRegistry] SetPvp skipped: Buff_PvP_Enabled prefab not resolved.");
                return;
            }

            if (enabled)
            {
                GameActionService.InvokeAction("applybuff", new object[] { userEntity, player, pvpBuffGuid, -1f });
            }
            else
            {
                GameActionService.InvokeAction("removebuff", new object[] { player, pvpBuffGuid });
            }
        }

        private void HandleSendMessage(FlowAction action, Entity player, int zoneHash)
        {
            var message = action.Message ?? action.Value?.ToString();
            if (!string.IsNullOrEmpty(message))
            {
                _log.LogInfo($"[FlowRegistry] Message to player {player.Index}: {message}");

                if (TryResolveUserEntity(player, out var userEntity))
                {
                    GameActionService.InvokeAction("sendmessagetouser", new object[] { userEntity, message });
                }
            }
        }

        private void HandleSpawnBoss(FlowAction action, Entity player, int zoneHash)
        {
            var bossPrefab = action.Prefab?.ToString();
            var qty = action.Quantity > 0 ? action.Quantity : 1;
            var randomInZone = action.RandomInZone;
            
            _log.LogInfo($"[FlowRegistry] SpawnBoss: {bossPrefab} x{qty} for player {player.Index}, randomInZone: {randomInZone}");

            if (string.IsNullOrWhiteSpace(bossPrefab))
            {
                return;
            }

            if (!TryResolvePrefabGuid(bossPrefab, out var bossGuid))
            {
                _log.LogWarning($"[FlowRegistry] Boss prefab '{bossPrefab}' not resolved");
                return;
            }

            if (!UnifiedCore.TryGetPrefabEntity(bossGuid, out var prefabEntity) || prefabEntity == Entity.Null)
            {
                _log.LogWarning($"[FlowRegistry] Boss prefab entity not found for '{bossPrefab}' ({bossGuid.GuidHash})");
                return;
            }

            ZoneDefinition? zone = null;
            var zoneFound = Plugin.ZoneConfig != null && Plugin.ZoneConfig.TryGetZoneByHash(zoneHash, out zone) && zone != null;
            for (int i = 0; i < qty; i++)
            {
                var spawnPos = float3.zero;
                if (zoneFound)
                {
                    if (randomInZone)
                    {
                        spawnPos = zone!.GetRandomPositionInside();
                    }
                    else if (action.Position != null && action.Position.Length >= 3)
                    {
                        spawnPos = new float3(action.Position[0], action.Position[1], action.Position[2]);
                    }
                    else
                    {
                        spawnPos = zone!.GetCenterFloat3();
                    }
                }

                var spawned = UnifiedCore.EntityManager.Instantiate(prefabEntity);
                GameActionService.InvokeAction("setposition", new object[] { spawned, spawnPos });

                if (!_bossesByZone.TryGetValue(zoneHash, out var list))
                {
                    list = new List<Entity>();
                    _bossesByZone[zoneHash] = list;
                }
                list.Add(spawned);
            }
        }

        private void HandleRemoveBoss(FlowAction action, Entity player, int zoneHash)
        {
            _log.LogInfo($"[FlowRegistry] RemoveBoss for zone {zoneHash} player {player.Index}");
            RemoveBossesInternal(zoneHash);
        }

        private void HandleApplyBorderFx(FlowAction action, Entity player, int zoneHash)
        {
            var vfxPrefab = action.VfxPrefab?.ToString();
            var radius = action.Radius > 0 ? action.Radius : 50f;
            var segments = action.Segments > 0 ? action.Segments : 24;
            
            _log.LogInfo($"[FlowRegistry] ApplyBorderFx: {vfxPrefab} with {segments} segments, radius {radius} for zone {zoneHash}");

            if (string.IsNullOrWhiteSpace(vfxPrefab))
            {
                return;
            }

            if (!TryResolvePrefabGuid(vfxPrefab, out var vfxGuid))
            {
                _log.LogWarning($"[FlowRegistry] VFX prefab '{vfxPrefab}' not resolved");
                return;
            }

            if (!UnifiedCore.TryGetPrefabEntity(vfxGuid, out var prefabEntity) || prefabEntity == Entity.Null)
            {
                _log.LogWarning($"[FlowRegistry] VFX prefab entity not found for '{vfxPrefab}' ({vfxGuid.GuidHash})");
                return;
            }

            if (Plugin.ZoneConfig?.TryGetZoneByHash(zoneHash, out var zone) != true || zone == null)
            {
                _log.LogWarning($"[FlowRegistry] Zone not found for border FX: {zoneHash}");
                return;
            }

            RemoveBorderFxInternal(zoneHash);

            var center = zone.GetCenterFloat3();
            var spawnedList = new List<Entity>(segments);
            for (int i = 0; i < segments; i++)
            {
                var angle = i * math.PI * 2f / segments;
                var pos = new float3(center.x + math.cos(angle) * radius, center.y, center.z + math.sin(angle) * radius);
                var spawned = UnifiedCore.EntityManager.Instantiate(prefabEntity);
                GameActionService.InvokeAction("setposition", new object[] { spawned, pos });
                spawnedList.Add(spawned);
            }

            _borderFxByZone[zoneHash] = spawnedList;
        }

        private void HandleRemoveBorderFx(FlowAction action, Entity player, int zoneHash)
        {
            _log.LogInfo($"[FlowRegistry] RemoveBorderFx for zone {zoneHash}");
            RemoveBorderFxInternal(zoneHash);
        }

        private void HandleApplyZoneBuff(FlowAction action, Entity player, int zoneHash)
        {
            var buffPrefab = action.BuffPrefab?.ToString();
            var duration = action.Duration;
            
            _log.LogInfo($"[FlowRegistry] ApplyZoneBuff: {buffPrefab} for player {player.Index}, duration: {duration}");

            if (string.IsNullOrWhiteSpace(buffPrefab))
            {
                return;
            }

            if (!TryResolveUserEntity(player, out var userEntity))
            {
                _log.LogWarning("[FlowRegistry] ApplyZoneBuff skipped: user entity not resolved.");
                return;
            }

            if (!TryResolvePrefabGuid(buffPrefab, out var buffGuid))
            {
                _log.LogWarning($"[FlowRegistry] Buff prefab '{buffPrefab}' not resolved");
                return;
            }

            var appliedDuration = duration == 0f ? -1f : duration;
            GameActionService.InvokeAction("applybuff", new object[] { userEntity, player, buffGuid, appliedDuration });
        }

        private void HandleRemoveZoneBuff(FlowAction action, Entity player, int zoneHash)
        {
            var buffPrefab = action.BuffPrefab?.ToString();
            
            _log.LogInfo($"[FlowRegistry] RemoveZoneBuff: {buffPrefab} for player {player.Index}");

            if (string.IsNullOrWhiteSpace(buffPrefab))
            {
                return;
            }

            if (!TryResolvePrefabGuid(buffPrefab, out var buffGuid))
            {
                _log.LogWarning($"[FlowRegistry] Buff prefab '{buffPrefab}' not resolved");
                return;
            }

            GameActionService.InvokeAction("removebuff", new object[] { player, buffGuid });
        }

        private bool TryResolvePrefabGuid(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            var token = prefabName.Trim();
            if (_prefabToGuid?.IsInitialized == true && _prefabToGuid.TryGetGuid(token, out guid))
            {
                return guid.GuidHash != 0;
            }

            return PrefabGuidConverter.TryGetGuid(token, out guid) && guid.GuidHash != 0;
        }

        private static bool TryResolveUserEntity(Entity player, out Entity userEntity)
        {
            userEntity = Entity.Null;

            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default || player == Entity.Null || !em.Exists(player) || !em.HasComponent<PlayerCharacter>(player))
                {
                    return false;
                }

                var pc = em.GetComponentData<PlayerCharacter>(player);
                if (pc.UserEntity == Entity.Null || !em.Exists(pc.UserEntity))
                {
                    return false;
                }

                userEntity = pc.UserEntity;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RemoveBorderFxInternal(int zoneHash)
        {
            if (!_borderFxByZone.TryGetValue(zoneHash, out var entities) || entities.Count == 0)
            {
                return;
            }

            TryDestroyEntities(entities);
            _borderFxByZone.Remove(zoneHash);
        }

        private void RemoveBossesInternal(int zoneHash)
        {
            if (_bossesByZone.TryGetValue(zoneHash, out var entities) && entities.Count > 0)
            {
                TryDestroyEntities(entities);
                _bossesByZone.Remove(zoneHash);
            }
        }

        private void PruneDeadBossEntities(int zoneHash)
        {
            if (!_bossesByZone.TryGetValue(zoneHash, out var entities) || entities.Count == 0)
                return;

            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default)
                    return;

                entities.RemoveAll(e => e == Entity.Null || !em.Exists(e));
                if (entities.Count == 0)
                    _bossesByZone.Remove(zoneHash);
            }
            catch
            {
                // ignored
            }
        }

        private void CleanupAllSpawnedEntities()
        {
            foreach (var kvp in _borderFxByZone.ToArray())
            {
                RemoveBorderFxInternal(kvp.Key);
            }

            foreach (var kvp in _bossesByZone.ToArray())
            {
                RemoveBossesInternal(kvp.Key);
            }

            _borderFxByZone.Clear();
            _bossesByZone.Clear();
        }

        private static void TryDestroyEntities(List<Entity> entities)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (em == default)
                {
                    return;
                }

                foreach (var e in entities)
                {
                    if (e != Entity.Null && em.Exists(e))
                    {
                        em.DestroyEntity(e);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private void CreateDefaultFlows()
        {
            var defaultFlows = new FlowConfig
            {
                Flows = new Dictionary<string, FlowAction[]>
                {
                    ["arena_enter"] = new FlowAction[]
                    {
                        new FlowAction { Action = FlowActions.SetPvp, Value = true },
                        new FlowAction { Action = FlowActions.SendMessage, Message = "PvP Arena: Combat enabled!" }
                    },
                    ["arena_exit"] = new FlowAction[]
                    {
                        new FlowAction { Action = FlowActions.SetPvp, Value = false },
                        new FlowAction { Action = FlowActions.SendMessage, Message = "PvP Arena: Combat disabled!" }
                    },
                    ["boss_enter"] = new FlowAction[]
                    {
                        new FlowAction { 
                            Action = FlowActions.SpawnBoss, 
                            Prefab = "CHAR_Gloomrot_Purifier_VBlood",
                            Quantity = 1,
                            RandomInZone = true
                        },
                        new FlowAction { Action = FlowActions.SendMessage, Message = "Boss encounter begins!" }
                    },
                    ["boss_exit"] = new FlowAction[]
                    {
                        new FlowAction { Action = FlowActions.RemoveBoss },
                        new FlowAction { Action = FlowActions.SendMessage, Message = "Boss encounter ended!" }
                    },
                    // Optional examples (disabled by default; define additional zones to use them):
                    // "buff_zone_enter"/"buff_zone_exit" can apply/remove buffs via zone.applyzonebuff/zone.removezonebuff.
                }
            };

            var json = JsonSerializer.Serialize(defaultFlows, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(_configPath, json);
            _log.LogInfo("[FlowRegistry] Created default flows with multi-boss support");

            foreach (var flow in defaultFlows.Flows)
            {
                _flows[flow.Key] = flow.Value.ToList();
            }
        }

        /// <summary>
        /// Gets a flow by ID.
        /// </summary>
        public bool TryGetFlow(string flowId, out List<FlowAction> actions)
        {
            return _flows.TryGetValue(flowId, out actions);
        }
    }
}
