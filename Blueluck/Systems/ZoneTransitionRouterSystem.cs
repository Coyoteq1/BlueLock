using System;
using Unity.Collections;
using Unity.Entities;
using VAutomationCore.Core.ECS.Components;
using Blueluck.Models;

namespace Blueluck.Systems
{
    /// <summary>
    /// Routes ECS ZoneTransitionEvent entities into Blueluck's ZoneTransitionService.
    /// ZoneDetectionSystem emits ZoneTransitionEvent; this consumes and destroys them.
    /// </summary>
    public partial class ZoneTransitionRouterSystem : SystemBase
    {
        private EntityQuery _transitionQuery;

        public override void OnCreate()
        {
            _transitionQuery = GetEntityQuery(ComponentType.ReadOnly<ZoneTransitionEvent>());
            RequireForUpdate(_transitionQuery);
        }

        public override void OnUpdate()
        {
            if (Plugin.ZoneTransition?.IsInitialized != true || Plugin.ZoneConfig?.IsInitialized != true)
            {
                return;
            }

            var em = EntityManager;
            var events = _transitionQuery.ToEntityArray(Allocator.Temp);
            var data = _transitionQuery.ToComponentDataArray<ZoneTransitionEvent>(Allocator.Temp);

            try
            {
                for (var i = 0; i < events.Length; i++)
                {
                    var evtEntity = events[i];
                    var evt = data[i];

                    if (evt.OldZoneHash != 0 && Plugin.ZoneConfig.TryGetZoneByHash(evt.OldZoneHash, out var oldZone))
                    {
                        Plugin.ZoneTransition.OnZoneExit(evt.Player, oldZone);
                    }

                    if (evt.NewZoneHash != 0 && Plugin.ZoneConfig.TryGetZoneByHash(evt.NewZoneHash, out var newZone))
                    {
                        Plugin.ZoneTransition.OnZoneEnter(evt.Player, newZone);
                    }

                    if (em.Exists(evtEntity))
                    {
                        em.DestroyEntity(evtEntity);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogWarning($"[ZoneTransitionRouterSystem] Error: {ex.Message}");
            }
            finally
            {
                events.Dispose();
                data.Dispose();
            }
        }
    }
}
