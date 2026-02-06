using System;
using System.Threading.Tasks;
using Unity.Entities;
using VAuto.Core;
using VAuto.EndGameKit.Requests;
using VAuto.EndGameKit.Configuration;
using VAuto.Commands.Core;

namespace VAuto.EndGameKit
{
    /// <summary>
    /// Facade for EndGameKit operations - provides clean synchronous API
    /// while internally using asynchronous ECS request components
    /// </summary>
    public static class EndGameKitFacade
    {
        /// <summary>
        /// Apply an end-game kit to a player
        /// </summary>
        /// <param name="player">Player entity</param>
        /// <param name="kitName">Name of the kit to apply</param>
        /// <returns>Result of the operation</returns>
        public static async Task<KitResult> ApplyKitAsync(Entity player, string kitName)
        {
            try
            {
                VRCore.Initialize();
                var em = VRCore.EntityManager;

                // Create request entity with ApplyKitRequest component
                var requestEntity = em.CreateEntity();
                em.AddComponentData(requestEntity, new ApplyKitRequest
                {
                    Player = player,
                    KitName = kitName,
                    Requester = requestEntity
                });

                // Wait for response (simplified - in practice you'd need proper async waiting)
                // For now, assume immediate processing
                var kitSystem = EndGameKitSystemFactory.GetInstance();
                var result = kitSystem.TryApplyKit(player, kitName, out var error);

                return new KitResult
                {
                    Success = result,
                    Error = error
                };
            }
            catch (Exception ex)
            {
                return new KitResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Remove an end-game kit from a player
        /// </summary>
        /// <param name="player">Player entity</param>
        /// <returns>Result of the operation</returns>
        public static async Task<KitResult> RemoveKitAsync(Entity player)
        {
            try
            {
                VRCore.Initialize();
                var em = VRCore.EntityManager;

                // Create request entity with RemoveKitRequest component
                var requestEntity = em.CreateEntity();
                em.AddComponentData(requestEntity, new RemoveKitRequest
                {
                    Player = player,
                    Requester = requestEntity
                });

                // Process synchronously for now
                var kitSystem = EndGameKitSystemFactory.GetInstance();
                var result = kitSystem.RemoveKit(player, out var error);

                return new KitResult
                {
                    Success = result,
                    Error = error
                };
            }
            catch (Exception ex)
            {
                return new KitResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Get list of available kit profile names
        /// </summary>
        /// <returns>List of kit profile names</returns>
        public static async Task<KitProfilesResult> GetKitProfilesAsync()
        {
            try
            {
                VRCore.Initialize();
                var em = VRCore.EntityManager;

                // Create request entity with GetKitProfilesRequest component
                var requestEntity = em.CreateEntity();
                em.AddComponentData(requestEntity, new GetKitProfilesRequest
                {
                    Requester = requestEntity
                });

                // Process synchronously for now
                var kitSystem = EndGameKitSystemFactory.GetInstance();
                var profileNames = kitSystem.GetKitProfileNames();

                return new KitProfilesResult
                {
                    Success = true,
                    ProfileNames = profileNames
                };
            }
            catch (Exception ex)
            {
                return new KitProfilesResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Check if a kit has been applied to a player
        /// </summary>
        /// <param name="player">Player entity</param>
        /// <returns>Result indicating if kit is applied</returns>
        public static async Task<KitCheckResult> HasKitAppliedAsync(Entity player)
        {
            try
            {
                VRCore.Initialize();
                var em = VRCore.EntityManager;

                // Process synchronously for now
                if (!EndGameKitCommandHelper.TryGetSystem(out var kitSystem, out var error))
                {
                    return new KitCheckResult
                    {
                        Success = false,
                        Error = error
                    };
                }

                var result = ((dynamic)kitSystem).HasKitApplied(player, out error);

                return new KitCheckResult
                {
                    Success = true,
                    HasKit = result,
                    Error = error
                };
            }
            catch (Exception ex)
            {
                return new KitCheckResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Get details of a specific kit profile
        /// </summary>
        /// <param name="kitName">Name of the kit</param>
        /// <returns>Kit profile details</returns>
        public static async Task<KitProfileResult> GetKitProfileAsync(string kitName)
        {
            try
            {
                if (!EndGameKitCommandHelper.TryGetSystem(out var kitSystem, out var error))
                {
                    return new KitProfileResult
                    {
                        Success = false,
                        Error = error
                    };
                }

                var profile = ((dynamic)kitSystem).GetKitProfile(kitName);

                return new KitProfileResult
                {
                    Success = profile != null,
                    Profile = profile,
                    Error = profile == null ? $"Kit '{kitName}' not found" : ""
                };
            }
            catch (Exception ex)
            {
                return new KitProfileResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
    }

    /// <summary>
    /// Result of a kit operation
    /// </summary>
    public class KitResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// Result of getting kit profiles
    /// </summary>
    public class KitProfilesResult : KitResult
    {
        public System.Collections.Generic.List<string> ProfileNames { get; set; } =
            new System.Collections.Generic.List<string>();
    }

    /// <summary>
    /// Result of checking if kit is applied
    /// </summary>
    public class KitCheckResult : KitResult
    {
        public bool HasKit { get; set; }
    }

    /// <summary>
    /// Result of getting kit profile
    /// </summary>
    public class KitProfileResult : KitResult
    {
        public EndGameKitProfile Profile { get; set; }
    }
}
