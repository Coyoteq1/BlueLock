using Unity.Collections;
using Unity.Entities;
using VAuto.Zone.Services;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.ECS.Components;

namespace VAuto.Zone.Systems
{
    public class ZoneTemplateLifecycleSystem : SystemBase
    {
        private EntityQuery _transitionQuery;

        protected override void OnCreate()
        {
            _transitionQuery = GetEntityQuery(ComponentType.ReadOnly<ZoneTransitionEvent>());
            RequireForUpdate<ZoneTransitionEvent>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var events = _transitionQuery.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var evtEntity in events)
                {
                    var evt = em.GetComponentData<ZoneTransitionEvent>(evtEntity);

                    if (evt.OldZoneHash != 0)
                    {
                        var oldZoneId = ZoneHashUtility.GetZoneId(evt.OldZoneHash);
                        ZoneTemplateService.ClearAllZoneTemplates(oldZoneId, em);
                    }

                    if (evt.NewZoneHash != 0)
                    {
                        var newZoneId = ZoneHashUtility.GetZoneId(evt.NewZoneHash);
                        ZoneTemplateService.SpawnAllZoneTemplates(newZoneId, em);
                    }

                    em.DestroyEntity(evtEntity);
                }
            }
            finally
            {
                events.Dispose();
            }
        }
    }
}
