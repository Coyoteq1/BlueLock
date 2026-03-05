using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VAutomationCore.Abstractions;
using VAutomationCore.Core;
using VAutomationCore.Core.ECS.Components;

namespace Blueluck.Systems
{
    /// <summary>
    /// Applies a visual-only border effect to players near the zone edge using buffs.
    /// Server-side only. Throttled to avoid ECS/network spam.
    /// </summary>
    public partial class ZoneBorderVisualSystem : SystemBase
    {
        private const float DefaultTickSeconds = 0.25f;

        private EntityQuery _playerQuery;
        private float _nextTickTime;
        private int _tickId;
        private readonly Dictionary<Entity, ActiveBorderVisualState> _active = new();
        private readonly List<Entity> _toRemove = new();

        private struct ActiveBorderVisualState
        {
            public int ZoneHash;
            public int Tier;
            public PrefabGUID BuffGuid;
            public int LastSeenTick;
        }

        public override void OnCreate()
        {
            try
            {
                var localToWorldType = Il2CppType.Of<LocalToWorld>(throwOnFailure: false);
                var playerType = Il2CppType.Of<PlayerCharacter>(throwOnFailure: false);

                if (localToWorldType == null || playerType == null)
                {
                    Plugin.LogWarning("[Blueluck][ECS] ZoneBorderVisualSystem disabled: IL2CPP component types missing.");
                    Enabled = false;
                    return;
                }

                var playerQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                    .AddAll(new ComponentType(localToWorldType, ComponentType.AccessMode.ReadOnly))
                    .AddAll(new ComponentType(playerType, ComponentType.AccessMode.ReadOnly));
                _playerQuery = GetEntityQuery(ref playerQueryBuilder);
                playerQueryBuilder.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.LogWarning($"[Blueluck][ECS] ZoneBorderVisualSystem disabled: {ex.Message}");
                Enabled = false;
            }
        }

        public override void OnUpdate()
        {
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextTickTime)
            {
                return;
            }

            _nextTickTime = now + DefaultTickSeconds;

            if (Plugin.ZoneConfig?.IsInitialized != true)
            {
                return;
            }

            var em = EntityManager;
            var players = _playerQuery.ToEntityArray(Allocator.Temp);

            try
            {
                _tickId++;

                foreach (var player in players)
                {
                    ProcessPlayer(em, player);
                }

                CleanupMissingPlayers(em);
            }
            finally
            {
                players.Dispose();
            }
        }

        private void ProcessPlayer(EntityManager em, Entity player)
        {
            if (player == Entity.Null || !em.Exists(player))
            {
                return;
            }

            var zoneHash = 0;
            if (em.HasComponent<EcsPlayerZoneState>(player))
            {
                zoneHash = em.GetComponentData<EcsPlayerZoneState>(player).CurrentZoneHash;
            }

            if (zoneHash == 0 || Plugin.ZoneConfig?.TryGetZoneByHash(zoneHash, out var zone) != true || zone == null)
            {
                EnsureRemoved(em, player);
                return;
            }

            var cfg = zone.BorderVisual;
            if (cfg == null || cfg.Range <= 0f || cfg.IntensityMax <= 0)
            {
                EnsureRemoved(em, player);
                return;
            }

            var pos = em.GetComponentData<LocalToWorld>(player).Position;
            var center = zone.GetCenterFloat3();
            var distFromCenter = math.distance(pos, center);
            var edgeRadius = zone.ExitRadius > 0f ? zone.ExitRadius : zone.EntryRadius;
            var distToEdge = math.abs(distFromCenter - edgeRadius);

            if (distToEdge > cfg.Range)
            {
                EnsureRemoved(em, player);
                return;
            }

            var tier = ComputeTier(distToEdge, cfg.Range, cfg.IntensityMax);
            if (!TryResolveBorderBuffGuid(cfg, tier, out var buffGuid))
            {
                EnsureRemoved(em, player);
                return;
            }

            var hasState = _active.TryGetValue(player, out var state);
            if (hasState)
            {
                state.LastSeenTick = _tickId;
            }

            if (hasState && state.ZoneHash == zoneHash && state.Tier == tier && state.BuffGuid == buffGuid)
            {
                _active[player] = state;
                return; // No change
            }

            if (hasState && state.BuffGuid != PrefabGUID.Empty && cfg.RemoveOnExit)
            {
                Buffs.RemoveBuff(player, state.BuffGuid);
            }

            if (!TryResolveUserEntity(em, player, out var userEntity))
            {
                // Can't apply without a valid "from character". Keep state removed.
                _active.Remove(player);
                return;
            }

            // Prefer distinct prefabs per tier to avoid stack mutation assumptions.
            Buffs.AddBuff(userEntity, player, buffGuid, duration: -1f, immortal: false);

            _active[player] = new ActiveBorderVisualState
            {
                ZoneHash = zoneHash,
                Tier = tier,
                BuffGuid = buffGuid,
                LastSeenTick = _tickId
            };
        }

        private void CleanupMissingPlayers(EntityManager em)
        {
            _toRemove.Clear();
            foreach (var kvp in _active)
            {
                if (kvp.Value.LastSeenTick != _tickId)
                {
                    _toRemove.Add(kvp.Key);
                }
            }

            foreach (var player in _toRemove)
            {
                if (_active.TryGetValue(player, out var state) && state.BuffGuid != PrefabGUID.Empty)
                {
                    Buffs.RemoveBuff(player, state.BuffGuid);
                }
                _active.Remove(player);
            }
        }

        private void EnsureRemoved(EntityManager em, Entity player)
        {
            if (!_active.TryGetValue(player, out var state))
            {
                return;
            }

            state.LastSeenTick = _tickId;
            _active.Remove(player);

            if (state.BuffGuid != PrefabGUID.Empty)
            {
                Buffs.RemoveBuff(player, state.BuffGuid);
            }
        }

        private static int ComputeTier(float distToEdge, float range, int intensityMax)
        {
            // distToEdge in [0, range]. 0 is strongest.
            var frac = math.clamp(distToEdge / range, 0f, 0.999999f);
            var tier = intensityMax - (int)math.floor(frac * intensityMax);
            if (tier < 1) tier = 1;
            if (tier > intensityMax) tier = intensityMax;
            return tier;
        }

        private static bool TryResolveUserEntity(EntityManager em, Entity player, out Entity userEntity)
        {
            userEntity = Entity.Null;

            try
            {
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

        private static bool TryResolveBorderBuffGuid(Models.BorderVisualConfig cfg, int tier, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;

            try
            {
                // Prefer explicit per-tier prefabs if provided.
                if (cfg.BuffPrefabs != null && cfg.BuffPrefabs.Length >= tier)
                {
                    var token = cfg.BuffPrefabs[tier - 1]?.Trim();
                    if (!string.IsNullOrWhiteSpace(token) && TryResolvePrefabGuid(token, out guid))
                    {
                        return guid != PrefabGUID.Empty;
                    }
                }

                // Fallback mapping: effect name -> prefab token (single prefab). If you want true tiering,
                // provide BuffPrefabs[] explicitly in zones.json.
                var effect = cfg.Effect?.Trim();
                if (string.IsNullOrWhiteSpace(effect))
                {
                    return false;
                }

                var prefabToken = effect switch
                {
                    "megara_visual" => "_megaraVisual",
                    "solarus_visual" => "_solarusVisual",
                    "manticore_visual" => "_manticoreVisual",
                    "monster_visual" => "_monsterVisual",
                    "dracula_visual" => "_draculaVisual",
                    _ => effect
                };

                return TryResolvePrefabGuid(prefabToken, out guid) && guid != PrefabGUID.Empty;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolvePrefabGuid(string prefabName, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            var token = prefabName.Trim();
            if (Plugin.PrefabToGuid?.IsInitialized == true && Plugin.PrefabToGuid.TryGetGuid(token, out guid))
            {
                return guid.GuidHash != 0;
            }

            return PrefabGuidConverter.TryGetGuid(token, out guid) && guid.GuidHash != 0;
        }
    }
}
