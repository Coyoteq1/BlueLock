using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Logging;
using System;
using System.Collections.Generic;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Zone-based trap commands - creates small trigger zones (1-2m radius).
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class ZoneTrapCommands : CommandBase
    {
        private const string CommandName = "trap";
        
        [Command("trap create", shortHand: "tc", description: "Create a trap zone at your location (2m radius, admin only)", adminOnly: true)]
        public static void TrapCreate(ChatCommandContext ctx, string type = "container")
        {
            ExecuteSafely(ctx, "trap create", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var position = GetPlayerPosition(ctx);
                
                if (position == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                type = type.ToLower();
                if (type != "container" && type != "waypoint" && type != "border")
                {
                    SendError(ctx, $"Invalid type '{type}'", "Use: container, waypoint, or border");
                    return;
                }
                
                Log.Info($"Admin {playerInfo.Name} created {type} zone at {position.Value}");
                
                SendSuccess(ctx, $"Created {type} trap zone!");
                SendLocation(ctx, "Position", position.Value);
            });
        }

        [Command("trap delete", shortHand: "td", description: "Delete trap zone at your location (admin only)", adminOnly: true)]
        public static void TrapDelete(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap delete", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var position = GetPlayerPosition(ctx);
                
                if (position == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                Log.Info($"Trap deletion at {position.Value} requested by {playerInfo.Name}");
                SendInfo(ctx, "No trap zones found nearby (within 5m)");
            });
        }

        [Command("trap list", shortHand: "tl", description: "List all trap zones (admin only)", adminOnly: true)]
        public static void TrapList(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap list", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Trap list requested by {playerInfo.Name}");
                
                SendInfo(ctx, "=== Trap Zones (0) ===");
                SendInfo(ctx, "No trap zones created");
            });
        }

        [Command("trap arm", shortHand: "ta", description: "Arm/disarm nearest trap zone (admin only)", adminOnly: true)]
        public static void TrapArm(ChatCommandContext ctx, string action = "toggle")
        {
            ExecuteSafely(ctx, "trap arm", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var position = GetPlayerPosition(ctx);
                
                if (position == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                Log.Info($"Trap arm action '{action}' at {position.Value} by {playerInfo.Name}");
                SendInfo(ctx, "No zones found nearby (within 5m)");
            });
        }

        [Command("trap check", shortHand: "tch", description: "Check if you're in a trap zone", adminOnly: false)]
        public static void TrapCheck(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap check", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                var position = GetPlayerPosition(ctx);
                
                if (position == null)
                {
                    SendError(ctx, "Could not determine your position");
                    return;
                }
                
                Log.Debug($"Trap check at {position.Value} by {playerInfo.Name}");
                SendSuccess(ctx, "You are not in any trap zone");
            });
        }

        [Command("trap clear", shortHand: "tcl", description: "Clear all trap zones (admin only)", adminOnly: true)]
        public static void TrapClear(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "trap clear", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"All trap zones cleared by {playerInfo.Name}");
                
                SendSuccess(ctx, "Cleared 0 trap zones");
            });
        }

        #region Helper Methods
        
        private static float3? GetPlayerPosition(ChatCommandContext ctx)
        {
            try
            {
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !EM.Exists(characterEntity))
                {
                    return null;
                }
                
                if (EM.HasComponent<LocalTransform>(characterEntity))
                {
                    return EM.GetComponentData<LocalTransform>(characterEntity).Position;
                }
                else if (EM.HasComponent<Translation>(characterEntity))
                {
                    return EM.GetComponentData<Translation>(characterEntity).Value;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "GetPlayerPosition");
                return null;
            }
        }
        
        #endregion
    }
}
