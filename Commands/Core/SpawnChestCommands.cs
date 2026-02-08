using ProjectM;

using ProjectM.Network;

using Stunlock.Core;

using Unity.Entities;

using Unity.Mathematics;

using Unity.Transforms;

using VampireCommandFramework;

using VAuto.Core;

using System;

using System.Collections.Generic;



namespace VAuto.Commands.Core

{

    /// <summary>

    /// Commands for spawning reward chests at waypoints (kill streak rewards).

    /// Only admins can spawn chests.

    /// </summary>

    public static class SpawnChestCommands

    {

        private static bool TryGetPlayerPosition(ChatCommandContext ctx, out float3 position)

        {

            position = float3.zero;

            try

            {

                var world = VRCore.ServerWorld;

                if (world == null)

                {

                    Plugin.Log.LogWarning("[Chest] Server world not available");

                    return false;

                }



                var entityManager = world.EntityManager;

                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;

                if (characterEntity == Entity.Null || !entityManager.Exists(characterEntity))

                {

                    Plugin.Log.LogWarning("[Chest] Sender character entity not found");

                    return false;

                }



                if (entityManager.HasComponent<LocalTransform>(characterEntity))

                {

                    position = entityManager.GetComponentData<LocalTransform>(characterEntity).Position;

                    return true;

                }



                if (entityManager.HasComponent<Translation>(characterEntity))

                {

                    position = entityManager.GetComponentData<Translation>(characterEntity).Value;

                    return true;

                }



                return false;

            }

            catch (Exception ex)

            {

                Plugin.Log.LogError($"[Chest] GetPlayerPosition failed: {ex.Message}");

                return false;

            }

        }

        

        /// <summary>

        /// Get player platform ID from context.

        /// </summary>

        private static ulong GetPlayerPlatformId(ChatCommandContext ctx)

        {

            try { return ctx.User.PlatformId; }

            catch { return 0; }

        }

        

        /// <summary>

        /// Check if player is admin.

        /// </summary>

        private static bool IsPlayerAdmin(ChatCommandContext ctx)

        {

            try { return ctx.User.IsAdmin; }

            catch { return false; }

        }

        

        [Command("spawnchest", shortHand: "sc", description: "Spawn a reward chest at your location (admin only)", adminOnly: true)]

        public static void SpawnChest(ChatCommandContext ctx, string type = "normal")

        {

            try

            {

                if (!TryGetPlayerPosition(ctx, out var playerPos))

                {

                    ctx.Reply("[Chest] Error: Could not get player position.");

                    return;

                }

                var playerId = GetPlayerPlatformId(ctx);

                

                // Check admin permission

                if (!IsPlayerAdmin(ctx))

                {

                    ctx.Reply("[Chest] ❌ Admin access required to spawn chests");

                    return;

                }

                

                // Determine chest type

                var chestType = type.ToLower() switch

                {

                    "rare" or "r" => ChestRewardType.Rare,

                    "epic" or "e" => ChestRewardType.Epic,

                    "legendary" or "l" => ChestRewardType.Legendary,

                    _ => ChestRewardType.Normal

                };

                

                // Spawn the chest

                var chestEntity = ChestSpawnService.SpawnChest(playerPos, playerId, chestType);

                

                ctx.Reply($"[Chest] ✅ Spawned {chestType} chest!");

                ctx.Reply($"  Position: ({playerPos.x:F0}, {playerPos.y:F0}, {playerPos.z:F0})");

                ctx.Reply($"  Spawned by admin: {playerId}");

                

                Plugin.Log.LogInfo($"[Chest] Admin {playerId} spawned {chestType} chest at {playerPos}");

            }

            catch (Exception ex)

            {

                ctx.Reply($"[Chest] Error: {ex.Message}");

                Plugin.Log.LogError($"[Chest] Spawn error: {ex}");

            }

        }

        

        [Command("spawnwaypointchest", shortHand: "swc", description: "Spawn chest at waypoint castle (admin only)", adminOnly: true)]

        public static void SpawnWaypointChest(ChatCommandContext ctx)

        {

            try

            {

                var playerId = GetPlayerPlatformId(ctx);

                

                if (!IsPlayerAdmin(ctx))

                {

                    ctx.Reply("[Chest] ❌ Admin access required");

                    return;

                }

                

                // Get waypoint position (castle)

                var waypointPos = GetWaypointCastlePosition();

                

                // Spawn 2 chests at waypoint

                int chestCount = 2;

                

                for (int i = 0; i < chestCount; i++)

                {

                    // Offset each chest slightly

                    var offset = new float3((i % 3) * 2f, 0, (i / 3) * 2f);

                    var chestPos = waypointPos + offset;

                    

                    ChestSpawnService.SpawnChest(chestPos, playerId, ChestRewardType.Normal);

                }

                

                ctx.Reply($"[Chest] ✅ Spawned {chestCount} chests at waypoint castle!");

                Plugin.Log.LogInfo($"[Chest] Admin {playerId} spawned {chestCount} chests at waypoint {waypointPos}");

            }

            catch (Exception ex)

            {

                ctx.Reply($"[Chest] Error: {ex.Message}");

            }

        }

        

        [Command("chest list", shortHand: "cl", description: "List spawned chests (admin only)", adminOnly: true)]

        public static void ChestList(ChatCommandContext ctx)

        {

            try

            {

                var chests = ChestSpawnService.GetAllChests();

                

                ctx.Reply($"[Chest] === Active Chests ({chests.Count}) ===");

                

                if (chests.Count == 0)

                {

                    ctx.Reply("  No active chests");

                    return;

                }

                

                foreach (var chest in chests)

                {

                    ctx.Reply(($"  📦 {chest.Value.ChestType} at ({chest.Value.Position.x:F0}, {chest.Value.Position.y:F0}, {chest.Value.Position.z:F0})"));

                    ctx.Reply($"     Spawned by: {chest.Value.SpawnedByPlatformId} | Elapsed: {GetElapsedTime(chest.Value.SpawnedTime)}");

                }

            }

            catch (Exception ex)

            {

                ctx.Reply($"[Chest] Error: {ex.Message}");

            }

        }

        

        [Command("chest remove", shortHand: "cr", description: "Remove chest at your location (admin only)", adminOnly: true)]

        public static void ChestRemove(ChatCommandContext ctx)

        {

            try

            {

                if (!TryGetPlayerPosition(ctx, out var playerPos))

                {

                    ctx.Reply("[Chest] Error: Could not get player position.");

                    return;

                }



                if (ChestSpawnService.RemoveNearestChest(playerPos, 5f))

                {

                    ctx.Reply("[Chest] ✅ Nearest chest removed");

                }

                else

                {

                    ctx.Reply("[Chest] No chests found nearby (within 5m)");

                }

            }

            catch (Exception ex)

            {

                ctx.Reply($"[Chest] Error: {ex.Message}");

            }

        }

        

        [Command("chest clear", shortHand: "cc", description: "Remove all spawned chests (admin only)", adminOnly: true)]

        public static void ChestClear(ChatCommandContext ctx)

        {

            var count = ChestSpawnService.GetChestCount();

            ChestSpawnService.ClearAll();

            ctx.Reply($"[Chest] ✅ Cleared {count} chests");

            Plugin.Log.LogInfo($"[Chest] Admin cleared {count} chests");

        }

        

        #region Helper Methods

        

        private static float3 GetWaypointCastlePosition()

        {

            // Default waypoint castle position

            return new float3(0, 0, 0);

        }

        

        private static string GetElapsedTime(DateTime spawnTime)

        {

            var elapsed = DateTime.UtcNow - spawnTime;

            if (elapsed.TotalMinutes < 1) return $"{elapsed.TotalSeconds:F0}s";

            if (elapsed.TotalHours < 1) return $"{elapsed.TotalMinutes:F0}m";

            return $"{elapsed.TotalHours:F0}h";

        }

        

        #endregion

    }

}

