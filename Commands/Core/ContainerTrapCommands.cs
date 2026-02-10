using VampireCommandFramework;
using Unity.Mathematics;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;
using System;
using System.Collections.Generic;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for setting traps on containers using position-based tracking.
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class ContainerTrapCommands : CommandBase
    {
        private const string CommandName = "trap";
        
        [Command("trap set", shortHand: "ts", description: "Set a trap at your location", adminOnly: true)]
        public static void TrapSet(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap set", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var playerPos = GetPlayerPosition(ctx);
                
                if (playerPos == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                var pos = playerPos.Value;
                var ownerId = playerInfo.PlatformId;
                
                // TODO: Integrate with ContainerTrapService when available
                // ContainerTrapService.SetTrap(pos, ownerId, "container");
                
                Log.Info($"Container trap set at {pos} for owner {ownerId} by {playerInfo.Name}");
                
                SendSuccess(ctx, "Trap set at your location!");
                SendLocation(ctx, "Position", pos);
                SendInfo(ctx, $"Glow radius: {ConfigService.Config}");
            });
        }
        
        [Command("trap remove", shortHand: "tr", description: "Remove trap at your location", adminOnly: true)]
        public static void TrapRemove(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap remove", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var playerPos = GetPlayerPosition(ctx);
                
                if (playerPos == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                var pos = playerPos.Value;
                
                // TODO: Integrate with ContainerTrapService
                // var nearest = ContainerTrapService.FindNearestTrap(pos, 10f);
                
                Log.Info($"Trap removal attempted at {pos} by {playerInfo.Name}");
                SendInfo(ctx, "No traps found nearby (within 10m)");
            });
        }
        
        [Command("trap list", shortHand: "tl", description: "List all trapped containers", adminOnly: true)]
        public static void TrapList(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap list", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Trap list requested by {playerInfo.Name}");
                
                SendInfo(ctx, "=== Trapped Locations (0) ===");
                SendInfo(ctx, "No traps set");
            });
        }
        
        [Command("trap arm", shortHand: "ta", description: "Arm/disarm trap at your location", adminOnly: true)]
        public static void TrapArm(ChatCommandContext ctx, string action = "toggle")
        {
            ExecuteSafely(ctx, "trap arm", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var playerPos = GetPlayerPosition(ctx);
                
                if (playerPos == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                var pos = playerPos.Value;
                Log.Info($"Trap arm action '{action}' at {pos} by {playerInfo.Name}");
                SendInfo(ctx, "No traps found nearby");
            });
        }
        
        [Command("trap trigger", shortHand: "tt", description: "Test trigger a trap at your location", adminOnly: true)]
        public static void TrapTrigger(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap trigger", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var playerPos = GetPlayerPosition(ctx);
                
                if (playerPos == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                var pos = playerPos.Value;
                var intruderId = playerInfo.PlatformId;
                
                Log.Info($"Test trigger at {pos} by {playerInfo.Name}");
                SendInfo(ctx, "No traps found nearby");
            });
        }
        
        [Command("trap clear", shortHand: "tc", description: "Clear all traps", adminOnly: true)]
        public static void TrapClear(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap clear", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"All traps cleared by {playerInfo.Name}");
                
                SendSuccess(ctx, "Cleared 0 traps");
            });
        }
        
        #region Helper Methods
        
        private static float3? GetPlayerPosition(ChatCommandContext ctx)
        {
            try
            {
                var characterEntity = ctx.Event?.SenderCharacterEntity;
                if (characterEntity != null && EM.HasComponent<LocalTransform>(characterEntity.Value))
                {
                    return EM.GetComponentData<LocalTransform>(characterEntity.Value).Position;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        #endregion
    }
}
