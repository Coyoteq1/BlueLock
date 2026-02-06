using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using VAuto.EndGameKit;
using VAuto.EndGameKit.Requests;
using VAuto.Core;

namespace VAuto.EndGameKit.Systems
{
    /// <summary>
    /// ECS system that processes kit-related requests asynchronously
    /// Decouples commands from direct system calls for better ECS compliance
    /// </summary>
    public partial class KitRequestSystem : SystemBase
    {
        private EndGameKitSystem _kitSystem;

        public override void OnCreate()
        {
            base.OnCreate();

            // Get reference to the kit system (assuming it's created via factory)
            if (EndGameKitSystemFactory.IsCreated())
            {
                _kitSystem = EndGameKitSystemFactory.GetInstance();
            }
            else
            {
                // Fallback: create system if not exists (shouldn't happen in normal flow)
                VRCore.Initialize();
                var em = VRCore.EntityManager;
                _kitSystem = new EndGameKitSystem(em);
                _kitSystem.Initialize();
            }
        }

        public override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process ApplyKit requests
            foreach (var (request, entity) in SystemAPI.Query<ApplyKitRequest>().WithEntityAccess())
            {
                ProcessApplyKitRequest(request, entity, ecb);
            }

            // Process RemoveKit requests
            foreach (var (request, entity) in SystemAPI.Query<RemoveKitRequest>().WithEntityAccess())
            {
                ProcessRemoveKitRequest(request, entity, ecb);
            }

            // Process GetKitProfiles requests
            foreach (var (request, entity) in SystemAPI.Query<GetKitProfilesRequest>().WithEntityAccess())
            {
                ProcessGetKitProfilesRequest(request, entity, ecb);
            }

            ecb.Playback(EntityManager);
        }

        private void ProcessApplyKitRequest(ApplyKitRequest request, Entity requestEntity, EntityCommandBuffer ecb)
        {
            if (!EntityManager.Exists(request.Player))
            {
                AddErrorResponse(requestEntity, "Invalid player entity", ecb);
                return;
            }

            if (request.KitName.Length == 0)
            {
                AddErrorResponse(requestEntity, "KitName cannot be empty", ecb);
                return;
            }

            var kitName = request.KitName.ToString();
            var result = _kitSystem.TryApplyKit(request.Player, kitName, out var error);

            if (result)
            {
                AddSuccessResponse(requestEntity, ecb);
            }
            else
            {
                AddErrorResponse(requestEntity, error, ecb);
            }

            // Remove the request entity
            ecb.DestroyEntity(requestEntity);
        }

        private void ProcessRemoveKitRequest(RemoveKitRequest request, Entity requestEntity, EntityCommandBuffer ecb)
        {
            if (!EntityManager.Exists(request.Player))
            {
                AddErrorResponse(requestEntity, "Invalid player entity", ecb);
                return;
            }

            var result = _kitSystem.RemoveKit(request.Player, out var error);

            if (result)
            {
                AddSuccessResponse(requestEntity, ecb);
            }
            else
            {
                AddErrorResponse(requestEntity, error, ecb);
            }

            // Remove the request entity
            ecb.DestroyEntity(requestEntity);
        }

        private void ProcessGetKitProfilesRequest(GetKitProfilesRequest request, Entity requestEntity, EntityCommandBuffer ecb)
        {
            try
            {
                var profileNames = _kitSystem.GetKitProfileNames();

                // Create response with profile names
                var responseEntity = ecb.CreateEntity();
                ecb.AddComponent(responseEntity, new KitProfilesResponse
                {
                    // Note: BlobArray creation would require more complex setup
                    // For now, we'll use a simple response
                });

                // For now, just mark as successful
                AddSuccessResponse(requestEntity, ecb);
            }
            catch (System.Exception ex)
            {
                AddErrorResponse(requestEntity, $"Failed to get kit profiles: {ex.Message}", ecb);
            }

            // Remove the request entity
            ecb.DestroyEntity(requestEntity);
        }

        private void AddSuccessResponse(Entity requestEntity, EntityCommandBuffer ecb)
        {
            ecb.AddComponent(requestEntity, new KitResponse
            {
                Success = true,
                Error = "",
                Data = ""
            });
        }

        private void AddErrorResponse(Entity requestEntity, string error, EntityCommandBuffer ecb)
        {
            ecb.AddComponent(requestEntity, new KitResponse
            {
                Success = false,
                Error = error,
                Data = ""
            });
        }
    }
}
