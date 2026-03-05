using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Il2CppInterop.Runtime;
using ProjectM;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.ECS.Components;

namespace Blueluck.Systems
{
    /// <summary>
    /// ECS system for detecting zone transitions.
    /// </summary>
    public partial class ZoneDetectionSystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private EntityQuery _zoneQuery;
        private int _updateCounter;
        private float _nextDetectionTickTime;
        private static readonly System.Reflection.MethodInfo? SetComponentDataGeneric = typeof(EntityManager).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "SetComponentData" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(Entity));
        private static readonly System.Reflection.MethodInfo? AddComponentGeneric = typeof(EntityManager).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "AddComponent" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Entity));

        private struct ZoneCandidate
        {
            public Entity Entity;
            public ZoneComponent Zone;
        }

        public override void OnCreate()
        {
            try
            {
                var localToWorldType = Il2CppType.Of<LocalToWorld>(throwOnFailure: false);
                var playerType = Il2CppType.Of<PlayerCharacter>(throwOnFailure: false);
                var zoneType = Il2CppType.Of<ZoneComponent>(throwOnFailure: false);

                if (localToWorldType == null || playerType == null || zoneType == null)
                {
                    Plugin.LogWarning("[Blueluck][ECS] ZoneDetectionSystem disabled: IL2CPP component types missing.");
                    Enabled = false;
                    return;
                }

                var playerQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                    .AddAll(new ComponentType(localToWorldType, ComponentType.AccessMode.ReadOnly))
                    .AddAll(new ComponentType(playerType, ComponentType.AccessMode.ReadOnly));
                _playerQuery = GetEntityQuery(ref playerQueryBuilder);
                playerQueryBuilder.Dispose();

                var zoneQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                    .AddAll(new ComponentType(zoneType, ComponentType.AccessMode.ReadOnly));
                _zoneQuery = GetEntityQuery(ref zoneQueryBuilder);
                zoneQueryBuilder.Dispose();

                RequireForUpdate(_zoneQuery);
            }
            catch (Exception ex)
            {
                Plugin.LogWarning($"[Blueluck][ECS] ZoneDetectionSystem disabled: {ex.Message}");
                Enabled = false;
            }
        }

        public override void OnUpdate()
        {
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextDetectionTickTime)
            {
                return;
            }

            var intervalMs = Plugin.ZoneDetectionCheckIntervalMs?.Value ?? 500;
            _nextDetectionTickTime = now + (intervalMs / 1000f);

            var em = EntityManager;
            var players = _playerQuery.ToEntityArray(Allocator.Temp);
            var zones = _zoneQuery.ToEntityArray(Allocator.Temp);

            try
            {
                var sortedZones = new List<ZoneCandidate>(zones.Length);
                for (var i = 0; i < zones.Length; i++)
                {
                    var zoneEntity = zones[i];
                    sortedZones.Add(new ZoneCandidate
                    {
                        Entity = zoneEntity,
                        Zone = em.GetComponentData<ZoneComponent>(zoneEntity)
                    });
                }

                sortedZones.Sort((a, b) => ZoneDetectionOrdering.Compare(a.Zone, b.Zone));

                var opCount = players.Length * sortedZones.Count;
                _updateCounter++;

                var debugMode = Plugin.ZoneDetectionDebugMode?.Value ?? false;
                if (debugMode && _updateCounter % 50 == 0)
                {
                    Plugin.LogInfo($"[Blueluck][ECS] ZoneDetection players={players.Length} zones={sortedZones.Count} ops~={opCount}");
                }

                foreach (var player in players)
                {
                    var pos = em.GetComponentData<LocalToWorld>(player).Position;
                    var state = em.HasComponent<EcsPlayerZoneState>(player)
                        ? em.GetComponentData<EcsPlayerZoneState>(player)
                        : new EcsPlayerZoneState { CurrentZoneHash = 0 };

                    var newZone = 0;

                    for (var i = 0; i < sortedZones.Count; i++)
                    {
                        var zone = sortedZones[i].Zone;
                        var distSq = math.distancesq(pos, zone.Center);

                        var inside = state.CurrentZoneHash == zone.ZoneHash
                            ? distSq <= zone.ExitRadiusSq
                            : distSq <= zone.EntryRadiusSq;

                        if (inside)
                        {
                            newZone = zone.ZoneHash;
                            break;
                        }
                    }

                    if (newZone != state.CurrentZoneHash)
                    {
                        var oldZoneHash = state.CurrentZoneHash;
                        EmitZoneTransition(em, player, oldZoneHash, newZone);
                        state.CurrentZoneHash = newZone;

                        if (em.HasComponent<EcsPlayerZoneState>(player))
                        {
                            em.SetComponentData(player, state);
                        }
                        else
                        {
                            AddOrSetComponentData(em, player, state);
                        }

                        if (debugMode)
                        {
                            Plugin.LogInfo($"[ZoneTransition][detect] player={player.Index} oldHash={oldZoneHash} newHash={newZone}");
                        }
                    }
                }
            }
            finally
            {
                players.Dispose();
                zones.Dispose();
            }
        }

        private static void EmitZoneTransition(EntityManager em, Entity player, int oldZone, int newZone)
        {
            var eventType = Il2CppType.Of<ZoneTransitionEvent>(throwOnFailure: false);
            if (eventType == null)
            {
                Plugin.LogWarning("[Blueluck][ECS] ZoneTransitionEvent type unavailable.");
                return;
            }

            var e = em.CreateEntity(new ComponentType(eventType, ComponentType.AccessMode.ReadWrite));
            AddOrSetComponentData(em, e, new ZoneTransitionEvent
            {
                Player = player,
                OldZoneHash = oldZone,
                NewZoneHash = newZone
            });
        }

        private static void AddOrSetComponentData<T>(EntityManager em, Entity entity, T value) where T : struct
        {
            try
            {
                if (em.HasComponent<T>(entity))
                {
                    em.SetComponentData(entity, value);
                    return;
                }

                if (AddComponentGeneric != null)
                {
                    AddComponentGeneric.MakeGenericMethod(typeof(T)).Invoke(em, new object[] { entity });
                }

                if (SetComponentDataGeneric != null)
                {
                    SetComponentDataGeneric.MakeGenericMethod(typeof(T)).Invoke(em, new object[] { entity, value });
                    return;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogWarning($"[Blueluck][ECS] Failed to write component {typeof(T).Name}: {ex.Message}");
            }
        }
    }
}
