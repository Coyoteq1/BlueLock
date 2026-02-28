using Unity.Collections;
using Unity.Entities;
using VAuto.Zone.Services;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.ECS.Components;
using VAutomationCore.Core.Lifecycle;

namespace VAuto.Zone.Systems
{
    public class FlowExecutionSystem : SystemBase
    {
        private static IFlowLifecycle _flowLifecycle;
        private EntityQuery _transitionQuery;

        public static void SetFlowLifecycle(IFlowLifecycle lifecycle)
        {
            _flowLifecycle = lifecycle;
        }

        protected override void OnCreate()
        {
            _transitionQuery = GetEntityQuery(ComponentType.ReadOnly<ZoneTransitionEvent>());
            RequireForUpdate<ZoneTransitionEvent>();
        }

        protected override void OnUpdate()
        {
            if (_flowLifecycle == null) return;

            var em = EntityManager;
            var events = _transitionQuery.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var evtEntity in events)
                {
                    var evt = em.GetComponentData<ZoneTransitionEvent>(evtEntity);

                    if (evt.NewZoneHash != 0)
                    {
                        var zoneId = ZoneHashUtility.GetZoneId(evt.NewZoneHash);
                        var zone = ZoneConfigService.GetZoneById(zoneId);
                        if (zone != null)
                        {
                            _flowLifecycle.ExecuteEnterFlow(zone.FlowId, evt.Player);
                        }
                    }

                    if (evt.OldZoneHash != 0)
                    {
                        var zoneId = ZoneHashUtility.GetZoneId(evt.OldZoneHash);
                        var zone = ZoneConfigService.GetZoneById(zoneId);
                        if (zone != null)
                        {
                            _flowLifecycle.ExecuteExitFlow(zone.FlowId, evt.Player);
                        }
                    }
                }
            }
            finally
            {
                events.Dispose();
            }
        }
    }
}
