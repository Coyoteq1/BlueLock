using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAutomationCore.Core.ECS.Components;

namespace VAuto.Zone.Systems
{
    [BurstCompile]
    public partial class ZoneDetectionSystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private EntityQuery _zoneQuery;

        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(ComponentType.ReadOnly<LocalTransform>());
            _zoneQuery = GetEntityQuery(ComponentType.ReadOnly<ZoneComponent>());
            RequireForUpdate<ZoneComponent>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var players = _playerQuery.ToEntityArray(Allocator.Temp);
            var zones = _zoneQuery.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var player in players)
                {
                    if (!em.HasComponent<LocalToWorld>(player)) continue;

                    var pos = em.GetComponentData<LocalToWorld>(player).Position;
                    var state = em.HasComponent<PlayerZoneState>(player)
                        ? em.GetComponentData<PlayerZoneState>(player)
                        : new PlayerZoneState { CurrentZoneHash = 0 };

                    int newZone = 0;

                    for (int i = 0; i < zones.Length; i++)
                    {
                        var zoneEntity = zones[i];
                        var zone = em.GetComponentData<ZoneComponent>(zoneEntity);

                        float distSq = math.distancesq(pos, zone.Center);

                        bool inside = state.CurrentZoneHash == zone.ZoneHash
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
                        EmitZoneTransition(em, player, state.CurrentZoneHash, newZone);
                        state.CurrentZoneHash = newZone;
                        em.SetComponentData(player, state);
                    }
                }
            }
            finally
            {
                players.Dispose();
                zones.Dispose();
            }
        }

        private void EmitZoneTransition(EntityManager em, Entity player, int oldZone, int newZone)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new ZoneTransitionEvent
            {
                Player = player,
                OldZoneHash = oldZone,
                NewZoneHash = newZone
            });
        }
    }
}