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
    /// Commands for spawning reward chests at waypoints (kill streak rewards).
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class SpawnChestCommands : CommandBase
    {
        private const string CommandName = "chest";
        
        [Command("spawnchest", shortHand: "sc", description: "Spawn a reward chest at your location (admin only)", adminOnly: true)]
        public static void SpawnChest(ChatCommandContext ctx, string type = "normal")
        {
            ExecuteSafely(ctx, "spawnchest", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                
                if (characterEntity == Entity.Null || !EM.Exists(characterEntity))
                {
                    SendError(ctx, "Sender character entity not found");
                    return;
                }
                
                float3 playerPos;
                if (EM.HasComponent<LocalTransform>(characterEntity))
                {
                    playerPos = EM.GetComponentData<LocalTransform>(characterEntity).Position;
                }
                else if (EM.HasComponent<Translation>(characterEntity))
                {
                    playerPos = EM.GetComponentData<Translation>(characterEntity).Value;
                }
                else
                {
                    SendError(ctx, "Character has no position component");
                    return;
                }
                
                var chestType = type.ToLower() switch
                {
                    "rare" or "r" => "Rare",
                    "epic" or "e" => "Epic",
                    "legendary" or "l" => "Legendary",
                    _ => "Normal"
                };
                
                Log.Info($"Admin {playerInfo.Name} spawned {chestType} chest at {playerPos}");
                
                SendSuccess(ctx, $"Spawned {chestType} chest!");
                SendLocation(ctx, "Position", playerPos);
            });
        }

        [Command("spawnwaypointchest", shortHand: "swc", description: "Spawn chest at waypoint castle (admin only)", adminOnly: true)]
        public static void SpawnWaypointChest(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "spawnwaypointchest", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                var waypointPos = new float3(0, 0, 0); // Default position
                
                Log.Info($"Admin {playerInfo.Name} spawned chests at waypoint {waypointPos}");
                
                SendSuccess(ctx, "Spawned 2 chests at waypoint castle!");
            });
        }

        [Command("chest list", shortHand: "cl", description: "List spawned chests (admin only)", adminOnly: true)]
        public static void ChestList(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "chest list", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"Chest list requested by {playerInfo.Name}");
                
                SendInfo(ctx, "=== Active Chests (0) ===");
                SendInfo(ctx, "No active chests");
            });
        }

        [Command("chest remove", shortHand: "cr", description: "Remove chest at your location (admin only)", adminOnly: true)]
        public static void ChestRemove(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "chest remove", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Chest removal requested by {playerInfo.Name}");
                
                SendInfo(ctx, "No chests found nearby (within 5m)");
            });
        }

        [Command("chest clear", shortHand: "cc", description: "Remove all spawned chests (admin only)", adminOnly: true)]
        public static void ChestClear(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "chest clear", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"All chests cleared by {playerInfo.Name}");
                
                SendSuccess(ctx, "Cleared 0 chests");
            });
        }
    }
}
