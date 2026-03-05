using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAuto.Core;
using VAuto.Services.Interfaces;
using VAutomationCore.Services;

namespace Blueluck.Services
{
    /// <summary>
    /// Boss-zone co-op override using PvP buff state.
    /// Players inside a co-op boss zone are forced to non-PvP together and restored on exit.
    /// </summary>
    public sealed class BossCoopService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.BossCoop");

        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private readonly Dictionary<int, HashSet<Entity>> _membersByZone = new();
        private readonly Dictionary<Entity, PlayerCoopState> _playerStates = new();
        private PrefabGUID _pvpBuffGuid = PrefabGUID.Empty;
        private DebugEventsSystem? _debugEventsSystem;
        private Func<Entity, Entity, bool>? _forceJoinClan;
        private readonly System.Random _rng = new();
        private bool _loggedClanApiUnavailable;

        private sealed class PlayerCoopState
        {
            public int CoopZoneRefCount;
            public bool HadPvpBeforeCoop;
        }

        public void Initialize()
        {
            IsInitialized = true;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _debugEventsSystem = world.GetExistingSystemManaged<DebugEventsSystem>();
                _forceJoinClan = BuildForceJoinClanInvoker(_debugEventsSystem);
            }

            // Resolve once. If this fails, service still initializes but becomes no-op.
            if (!TryResolvePvpBuffGuid(out _pvpBuffGuid))
            {
                _log.LogWarning("[BossCoop] Buff_PvP_Enabled guid not resolved; co-op override disabled.");
            }

            _log.LogInfo("[BossCoop] Initialized.");
        }

        public void Cleanup()
        {
            try
            {
                // Best-effort restore for players that had PvP before co-op.
                foreach (var kvp in _playerStates)
                {
                    var player = kvp.Key;
                    var state = kvp.Value;
                    if (state != null && state.HadPvpBeforeCoop)
                    {
                        RestorePvp(player);
                    }
                }
            }
            catch
            {
                // ignored
            }

            _membersByZone.Clear();
            _playerStates.Clear();
            _debugEventsSystem = null;
            _forceJoinClan = null;
            IsInitialized = false;
            _log.LogInfo("[BossCoop] Cleaned up.");
        }

        public void OnBossZoneEnter(Entity player, int zoneHash, bool forceJoinClan, bool shuffleClan)
        {
            if (!IsInitialized || player == Entity.Null || zoneHash == 0 || _pvpBuffGuid == PrefabGUID.Empty)
            {
                return;
            }

            if (!_membersByZone.TryGetValue(zoneHash, out var members))
            {
                members = new HashSet<Entity>();
                _membersByZone[zoneHash] = members;
            }

            if (members.Add(player))
            {
                AcquireCoop(player);
            }

            // Enforce non-PvP on all members currently inside this boss zone.
            foreach (var member in members)
            {
                ForceNonPvp(member);
            }

            if (forceJoinClan)
            {
                ForceJoinClanForZone(zoneHash, shuffleClan);
            }
        }

        public void OnBossZoneExit(Entity player, int zoneHash)
        {
            if (!IsInitialized || player == Entity.Null || zoneHash == 0 || _pvpBuffGuid == PrefabGUID.Empty)
            {
                return;
            }

            if (_membersByZone.TryGetValue(zoneHash, out var members))
            {
                members.Remove(player);
                if (members.Count == 0)
                {
                    _membersByZone.Remove(zoneHash);
                }
                else
                {
                    // Keep remaining members forced non-PvP.
                    foreach (var member in members)
                    {
                        ForceNonPvp(member);
                    }
                }
            }

            ReleaseCoop(player);
        }

        private void AcquireCoop(Entity player)
        {
            if (!_playerStates.TryGetValue(player, out var state) || state == null)
            {
                state = new PlayerCoopState();
                _playerStates[player] = state;
            }

            if (state.CoopZoneRefCount == 0)
            {
                state.HadPvpBeforeCoop = HasPvp(player);
            }

            state.CoopZoneRefCount++;
            ForceNonPvp(player);
        }

        private void ReleaseCoop(Entity player)
        {
            if (!_playerStates.TryGetValue(player, out var state) || state == null)
            {
                return;
            }

            state.CoopZoneRefCount--;
            if (state.CoopZoneRefCount > 0)
            {
                return;
            }

            if (state.HadPvpBeforeCoop)
            {
                RestorePvp(player);
            }

            _playerStates.Remove(player);
        }

        private void ForceNonPvp(Entity player)
        {
            if (_pvpBuffGuid == PrefabGUID.Empty)
            {
                return;
            }

            GameActionService.TryRemoveBuff(player, _pvpBuffGuid);
        }

        private void RestorePvp(Entity player)
        {
            if (_pvpBuffGuid == PrefabGUID.Empty)
            {
                return;
            }

            if (!TryResolveUserEntity(player, out var userEntity))
            {
                return;
            }

            GameActionService.InvokeAction("applybuff", new object[] { userEntity, player, _pvpBuffGuid, -1f });
        }

        private bool HasPvp(Entity player)
        {
            try
            {
                var em = VAutomationCore.Core.UnifiedCore.EntityManager;
                if (em == default || player == Entity.Null || !em.Exists(player))
                {
                    return false;
                }

                return BuffUtility.TryGetBuff(em, player, _pvpBuffGuid, out var buff) && buff != Entity.Null && em.Exists(buff);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveUserEntity(Entity player, out Entity userEntity)
        {
            userEntity = Entity.Null;
            try
            {
                var em = VAutomationCore.Core.UnifiedCore.EntityManager;
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

        private static bool TryResolvePvpBuffGuid(out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (Plugin.PrefabToGuid?.IsInitialized == true && Plugin.PrefabToGuid.TryGetGuid("Buff_PvP_Enabled", out guid))
            {
                return guid.GuidHash != 0;
            }

            return PrefabGuidConverter.TryGetGuid("Buff_PvP_Enabled", out guid) && guid.GuidHash != 0;
        }

        private void ForceJoinClanForZone(int zoneHash, bool shuffle)
        {
            if (!_membersByZone.TryGetValue(zoneHash, out var members) || members.Count < 2)
            {
                return;
            }

            if (_forceJoinClan == null)
            {
                if (!_loggedClanApiUnavailable)
                {
                    _loggedClanApiUnavailable = true;
                    _log.LogWarning("[BossCoop] Clan API not available in this server build; using PvP-suppression co-op fallback.");
                }
                return;
            }

            var ordered = members.Where(x => x != Entity.Null).ToList();
            if (ordered.Count < 2)
            {
                return;
            }

            if (shuffle)
            {
                // Fisher-Yates shuffle for deterministic O(n) in-place shuffle.
                for (var i = ordered.Count - 1; i > 0; i--)
                {
                    var j = _rng.Next(i + 1);
                    (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
                }
            }

            var leader = ordered[0];
            for (var i = 1; i < ordered.Count; i++)
            {
                _forceJoinClan(ordered[i], leader);
            }
        }

        private static Func<Entity, Entity, bool>? BuildForceJoinClanInvoker(DebugEventsSystem? debugEventsSystem)
        {
            if (debugEventsSystem == null)
            {
                return null;
            }

            try
            {
                var methods = debugEventsSystem.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m =>
                    {
                        var n = m.Name ?? string.Empty;
                        return n.IndexOf("JoinClan", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("SetClan", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("JoinSubClan", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("SetSubClan", StringComparison.OrdinalIgnoreCase) >= 0;
                    })
                    .ToArray();

                if (methods.Length == 0)
                {
                    return null;
                }

                // Supported signature patterns (best effort):
                // 1) (Entity memberCharacter, Entity leaderCharacter)
                // 2) (FromCharacter member, Entity leaderCharacter)
                // 3) (FromCharacter member, ulong leaderPlatformId)
                // 4) (ulong memberPlatformId, ulong leaderPlatformId)
                foreach (var method in methods)
                {
                    var ps = method.GetParameters();
                    if (ps.Length != 2) continue;

                    var p0 = ps[0].ParameterType;
                    var p1 = ps[1].ParameterType;

                    if (p0 == typeof(Entity) && p1 == typeof(Entity))
                    {
                        return (member, leader) =>
                        {
                            try { method.Invoke(debugEventsSystem, new object[] { member, leader }); return true; }
                            catch { return false; }
                        };
                    }

                    if (p0 == typeof(FromCharacter) && p1 == typeof(Entity))
                    {
                        return (member, leader) =>
                        {
                            try
                            {
                                if (!TryResolveUserEntity(member, out var memberUser)) return false;
                                var from = new FromCharacter { User = memberUser, Character = member };
                                method.Invoke(debugEventsSystem, new object[] { from, leader });
                                return true;
                            }
                            catch { return false; }
                        };
                    }

                    if (p0 == typeof(FromCharacter) && p1 == typeof(ulong))
                    {
                        return (member, leader) =>
                        {
                            try
                            {
                                if (!TryResolveUserEntity(member, out var memberUser)) return false;
                                if (!TryGetPlatformId(leader, out var leaderPid)) return false;
                                var from = new FromCharacter { User = memberUser, Character = member };
                                method.Invoke(debugEventsSystem, new object[] { from, leaderPid });
                                return true;
                            }
                            catch { return false; }
                        };
                    }

                    if (p0 == typeof(ulong) && p1 == typeof(ulong))
                    {
                        return (member, leader) =>
                        {
                            try
                            {
                                if (!TryGetPlatformId(member, out var memberPid)) return false;
                                if (!TryGetPlatformId(leader, out var leaderPid)) return false;
                                method.Invoke(debugEventsSystem, new object[] { memberPid, leaderPid });
                                return true;
                            }
                            catch { return false; }
                        };
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static bool TryGetPlatformId(Entity characterEntity, out ulong platformId)
        {
            platformId = 0;
            try
            {
                var em = VAutomationCore.Core.UnifiedCore.EntityManager;
                if (!TryResolveUserEntity(characterEntity, out var userEntity))
                {
                    return false;
                }

                if (em == default || userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                {
                    return false;
                }

                platformId = em.GetComponentData<User>(userEntity).PlatformId;
                return platformId != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
