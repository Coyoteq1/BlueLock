using ProjectM;
using Unity.Entities;
using Unity.Collections;
using VAuto.Core;

namespace VAuto.Core.Lifecycle
{
    public sealed class VBloodRepairRefreshSystem : SystemBase
    {
        private EntityQuery _requestQuery;

        public override void OnCreate()
        {
            // Use the new ECS helper for query creation
            _requestQuery = ECSHelper.CreateEntityQuery<PendingVbloodRepairRefresh>(ComponentType.AccessMode.ReadOnly);
        }

        public override void OnUpdate()
        {
            if (_requestQuery.IsEmpty)
                return;

            try
            {
                var world = World;
                var handle = world.GetExistingSystem<RepairVBloodProgressionSystem>();
                if (handle == default)
                    handle = world.GetOrCreateSystem<RepairVBloodProgressionSystem>();

                ref var state = ref world.Unmanaged.GetExistingSystemState<RepairVBloodProgressionSystem>();
                ref var sys = ref world.Unmanaged.GetUnsafeSystemRef<RepairVBloodProgressionSystem>(handle);
                sys.OnUpdate(ref state);
            }
            catch
            {
                // best effort
            }
            finally
            {
                // Use ECS helper for safe entity destruction
                var entities = _requestQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    var entityManager = VRCore.EntityManager;
                    foreach (var entity in entities)
                    {
                        if (entityManager.Exists(entity))
                        {
                            entityManager.DestroyEntity(entity);
                        }
                    }
                }
                finally
                {
                    ECSHelper.SafeDispose(ref entities);
                }
            }
        }

        public override void OnDestroy()
        {
            _requestQuery?.Dispose();
            base.OnDestroy();
        }
    }
}
