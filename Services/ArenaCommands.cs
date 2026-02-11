using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Zone.Core;
using VAuto.Zone.Services;

namespace VAuto.Zone.Commands
{
    /// <summary>
    /// Arena-specific commands for VAutoZone.
    /// Provides commands for arena player management and territory checks.
    /// </summary>
    [CommandGroup("arena", "a")]
    public static class ArenaCommands
    {
        /// <summary>
        /// Show help for arena commands.
        /// </summary>
        [Command("help", shortHand: "h", description: "Show arena command help", adminOnly: false)]
        public static void ArenaHelp(ChatCommandContext ctx)
        {
            var message = @"<color=#FFD700>[Arena Commands]</color>
<color=#00FFFF>.arena status (.a s)</color> - Show arena status and player count
<color=#00FFFF>.arena players (.a p) [list]</color> - List players in arena
<color=#00FFFF>.arena enter (.a e)</color> - Check arena entry status
<color=#00FFFF>.arena exit (.a x)</color> - Check arena exit status
<color=#00FFFF>.arena config (.a c)</color> - Show arena configuration [Admin]";
            ctx.Reply(message);
        }

        /// <summary>
        /// Show current arena status.
        /// </summary>
        [Command("status", shortHand: "s", description: "Show arena status", adminOnly: false)]
        public static void ArenaStatus(ChatCommandContext ctx)
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var playerPos))
                {
                    ctx.Reply("[Arena] Error: Could not determine your position");
                    return;
                }

                var isInArena = ArenaTerritory.IsInArenaTerritory(playerPos);
                var zoneId = ArenaTerritory.GetArenaZoneId(playerPos);
                
                var blocks = ArenaTerritory.GetArenaBlocks();
                
                var message = $"<color=#FFD700>[Arena Status]</color>\n" +
                              $"Position: ({playerPos.x:F0}, {playerPos.y:F0}, {playerPos.z:F0})\n" +
                              $"In Arena: {(isInArena ? "<color=#00FF00>Yes</color>" : "<color=#FF0000>No</color>")}\n" +
                              $"Zone: {zoneId ?? "None"}\n" +
                              $"Arena Blocks: {blocks.Count}\n" +
                              $"Glow Border: {(ArenaTerritory.EnableGlowBorder ? "Enabled" : "Disabled")}";
                
                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaStatus error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error retrieving arena status.</color>");
            }
        }

        /// <summary>
        /// List players currently in the arena.
        /// </summary>
        [Command("players", shortHand: "p", description: "List players in arena", adminOnly: false)]
        public static void ArenaPlayers(ChatCommandContext ctx, string action = "count")
        {
            try
            {
                var allPlayers = GetAllOnlinePlayers();
                var playersInArena = new List<(string name, float3 position)>();
                
                foreach (var player in allPlayers)
                {
                    if (ArenaTerritory.IsInArenaTerritory(player.position))
                    {
                        playersInArena.Add(player);
                    }
                }

                switch (action.ToLower())
                {
                    case "list":
                    case "l":
                        if (playersInArena.Count == 0)
                        {
                            ctx.Reply("[Arena] No players currently in arena.");
                            return;
                        }
                        
                        var message = $"<color=#FFD700>[Arena Players ({playersInArena.Count})]</color>\n";
                        foreach (var (name, pos) in playersInArena.Take(10))
                        {
                            message += $"{name}: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})\n";
                        }
                        if (playersInArena.Count > 10)
                        {
                            message += $"... and {playersInArena.Count - 10} more.";
                        }
                        ctx.Reply(message);
                        break;
                        
                    default:
                        ctx.Reply($"Players in arena: {playersInArena.Count}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaPlayers error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error listing arena players.</color>");
            }
        }

        /// <summary>
        /// Check arena entry status.
        /// </summary>
        [Command("enter", shortHand: "e", description: "Check arena entry status", adminOnly: false)]
        public static void ArenaEnter(ChatCommandContext ctx)
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var playerPos))
                {
                    ctx.Reply("[Arena] Error: Could not determine your position.");
                    return;
                }

                var inArena = ArenaTerritory.IsInArenaTerritory(playerPos);
                if (inArena)
                {
                    ctx.Reply("<color=#00FF00>You are inside the arena territory.</color>");
                }
                else
                {
                    var zoneId = ArenaTerritory.GetArenaZoneId(playerPos);
                    ctx.Reply($"<color=#FF0000>You are outside the arena. Zone ID: {zoneId ?? "None"}</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaEnter error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error checking arena entry.</color>");
            }
        }

        /// <summary>
        /// Check arena exit status.
        /// </summary>
        [Command("exit", shortHand: "x", description: "Check arena exit status", adminOnly: false)]
        public static void ArenaExit(ChatCommandContext ctx)
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var playerPos))
                {
                    ctx.Reply("[Arena] Error: Could not determine your position.");
                    return;
                }

                var inArena = ArenaTerritory.IsInArenaTerritory(playerPos);
                if (inArena)
                {
                    ctx.Reply($"<color=#FFD700>You are still inside the arena.</color> Use '.arena exit' when you leave.");
                }
                else
                {
                    ctx.Reply("<color=#00FF00>You have left the arena territory.</color>");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaExit error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error checking arena exit.</color>");
            }
        }

        /// <summary>
        /// Show arena configuration.
        /// </summary>
        [Command("config", shortHand: "c", description: "Show arena configuration", adminOnly: true)]
        public static void ArenaConfig(ChatCommandContext ctx)
        {
            try
            {
                var message = $"<color=#FFD700>[Arena Configuration]</color>\n";
                message += $"Zone ID: {ArenaTerritory.ZoneId}\n";
                message += $"Center: ({ArenaTerritory.ArenaGridCenter.x:F0}, {ArenaTerritory.ArenaGridCenter.y:F0}, {ArenaTerritory.ArenaGridCenter.z:F0})\n";
                message += $"Radius: {ArenaTerritory.ArenaGridRadius}m\n";
                message += $"Block Size: {ArenaTerritory.BlockSize}m\n";
                message += $"Region Type: {ArenaTerritory.ArenaRegionType}\n";
                message += $"Glow Border: {(ArenaTerritory.EnableGlowBorder ? "Enabled" : "Disabled")}\n";
                message += $"Glow Prefab: {ArenaTerritory.GlowPrefab}\n";
                message += $"Glow Spacing: {ArenaTerritory.GlowSpacingMeters}m\n";
                message += $"Corner Radius: {ArenaTerritory.GlowCornerRadius}m\n";
                message += $"Spawn Corners: {(ArenaTerritory.SpawnGlowInCorners ? "Yes" : "No")}";
                
                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"ArenaConfig error: {ex.Message}");
                ctx.Reply("<color=#FF0000>Error retrieving config.</color>");
            }
        }

        #region Helper Methods

        private static bool TryGetPlayerPosition(ChatCommandContext ctx, out float3 position)
        {
            position = float3.zero;
            try
            {
                var serverWorld = ZoneCore.Server;
                if (serverWorld == null) return false;

                var entityManager = serverWorld.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !entityManager.Exists(characterEntity)) return false;

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
            catch
            {
                return false;
            }
        }

        private static List<(string name, float3 position)> GetAllOnlinePlayers()
        {
            var players = new List<(string, float3)>();
            
            try
            {
                var em = ZoneCore.EntityManager;
                var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
                var entities = query.ToEntityArray(Allocator.Temp);
                
                foreach (var entity in entities)
                {
                    var pc = em.GetComponentData<PlayerCharacter>(entity);
                    var userEntity = pc.UserEntity;
                    
                    if (userEntity == Entity.Null) continue;
                    if (!em.HasComponent<User>(userEntity)) continue;
                    
                    var user = em.GetComponentData<User>(userEntity);
                    float3 pos = float3.zero;
                    
                    if (em.HasComponent<LocalTransform>(entity))
                    {
                        pos = em.GetComponentData<LocalTransform>(entity).Position;
                    }
                    else if (em.HasComponent<Translation>(entity))
                    {
                        pos = em.GetComponentData<Translation>(entity).Value;
                    }
                    
                    players.Add((user.CharacterName.ToString(), pos));
                }
                
                entities.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[Arena] GetAllOnlinePlayers failed: {ex.Message}");
            }
            
            return players;
        }

        #endregion
    }
}
