using Unity.Entities;
using Unity.Collections;
using VAuto.Core.Components;
using VAuto.Core.Lifecycle;
using Microsoft.Extensions.Logging;

namespace VAuto.Core.Systems
{
    /// <summary>
    /// Bridges ECS request components to lifecycle handlers.
    /// Routes requests to appropriate handlers based on type.
    /// Runs every frame to process pending automation requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LifecycleEventBridgeSystem : SystemBase
    {
        private EntityQuery _pendingRequests;
        private ILogger<LifecycleEventBridgeSystem> _log;
        private Entity _arenaLifecycleService;

        protected override void OnCreate()
        {
            // Query for all pending lifecycle requests
            _pendingRequests = GetEntityQuery(
                ComponentType.ReadOnly<LifecycleRequestBase>()
            );
            
            _log = VAutoCore.LogFactory.CreateLogger<LifecycleEventBridgeSystem>();
            _log.LogInformation("[LifecycleEventBridgeSystem] Initialized");
        }

        protected override void OnUpdate()
        {
            var ecb = World.GetExistingSystemManaged<BeginSimulationEntityCommandBufferSystem>()
                .CreateCommandBuffer();

            // Get ArenaUnlockLifecycleService singleton for handler routing
            if (!SystemAPI.TryGetSingletonEntity(out _arenaLifecycleService))
            {
                return;
            }

            // Process pending requests
            var requests = _pendingRequests.ToComponentDataArray<LifecycleRequestBase>(Allocator.Temp);

            foreach (var request in requests)
            {
                try
                {
                    RouteRequest(request, ecb);
                }
                catch (System.Exception ex)
                {
                    _log.LogError($"Error routing request {request.Type}: {ex.Message}");
                    UpdateRequestStatus(request, RequestStatus.Failed, ex.Message, ecb);
                }
            }

            requests.Dispose();
        }

        /// <summary>
        /// Routes request to appropriate handler based on type.
        /// </summary>
        private void RouteRequest(LifecycleRequestBase request, EntityCommandBuffer ecb)
        {
            switch (request.Type)
            {
                case RequestType.ZoneTransition:
                    RouteZoneTransition(request, ecb);
                    break;
                    
                case RequestType.Repair:
                    RouteRepairRequest(request, ecb);
                    break;
                    
                case RequestType.VBloodUnlock:
                    RouteVBloodUnlockRequest(request, ecb);
                    break;
                    
                case RequestType.SpellbookGrant:
                    RouteSpellbookGrantRequest(request, ecb);
                    break;
                    
                default:
                    _log.LogWarning($"Unknown request type: {request.Type}");
                    UpdateRequestStatus(request, RequestStatus.Failed, $"Unknown request type: {request.Type}", ecb);
                    break;
            }
        }

        private void RouteZoneTransition(LifecycleRequestBase request, EntityCommandBuffer ecb)
        {
            // Route to ArenaUnlockLifecycleService
            _log.LogDebug($"Routing ZoneTransitionRequest to ArenaUnlockLifecycleService");
            UpdateRequestStatus(request, RequestStatus.Processing, null, ecb);
        }

        private void RouteRepairRequest(LifecycleRequestBase request, EntityCommandBuffer ecb)
        {
            // Route to AutoRepairHandler
            _log.LogDebug($"Routing RepairRequest to AutoRepairHandler");
            UpdateRequestStatus(request, RequestStatus.Processing, null, ecb);
        }

        private void RouteVBloodUnlockRequest(LifecycleRequestBase request, EntityCommandBuffer ecb)
        {
            // Route to AutoVBloodUnlockHandler
            _log.LogDebug($"Routing VBloodUnlockRequest to AutoVBloodUnlockHandler");
            UpdateRequestStatus(request, RequestStatus.Processing, null, ecb);
        }

        private void RouteSpellbookGrantRequest(LifecycleRequestBase request, EntityCommandBuffer ecb)
        {
            // Route to AutoSpellbookGrantHandler
            _log.LogDebug($"Routing SpellbookGrantRequest to AutoSpellbookGrantHandler");
            UpdateRequestStatus(request, RequestStatus.Processing, null, ecb);
        }

        private void UpdateRequestStatus(LifecycleRequestBase request, RequestStatus status, string errorMessage, EntityCommandBuffer ecb)
        {
            request.Status = status;
            request.ErrorMessage = errorMessage;
            request.Timestamp = (float)SystemAPI.Time.ElapsedTime;
            ecb.SetComponentData(request.SourceZone, request);
        }
    }
}
