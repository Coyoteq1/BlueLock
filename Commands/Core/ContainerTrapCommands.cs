using VampireCommandFramework;
using Unity.Mathematics;
using VAuto.Core.Services;
using System;
using System.Collections.Generic;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for setting traps on containers using position-based tracking.
    /// Usage: Stand near a container and use the command.
    /// </summary>
    public static class ContainerTrapCommands
    {
        [Command("trap set", shortHand: "ts", description: "Set a trap at your location", adminOnly: true)]
        public static void TrapSet(ChatCommandContext ctx)
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var pos))
                {
                    ctx.Reply("[Trap] Error: Could not determine your position");
                    return;
                }
                var ownerId = GetPlayerPlatformId(ctx);
                
                // Set the trap using the service
                ContainerTrapService.SetTrap(pos, ownerId, "container");
                
                ctx.Reply($"[Trap] ✅ Trap set at your location!");
                ctx.Reply($"  Position: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                ctx.Reply($"  - Glow radius: {TrapSpawnRules.Config.ContainerGlowRadius}m");
                ctx.Reply($"  - Damage: {TrapSpawnRules.Config.TrapDamageAmount}");
                ctx.Reply($"  - Duration: {TrapSpawnRules.Config.TrapDuration}s");
                
                Plugin.Log.LogInfo($"[Trap] Container trap set at {pos} for owner {ownerId}");
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
                Plugin.Log.LogError($"[Trap] Set error: {ex.Message}");
            }
        }
        
        [Command("trap remove", shortHand: "tr", description: "Remove trap at your location", adminOnly: true)]
        public static void TrapRemove(ChatCommandContext ctx)
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var pos))
                {
                    ctx.Reply("[Trap] Error: Could not determine your position");
                    return;
                }
                
                // Find nearest trap first
                var nearest = ContainerTrapService.FindNearestTrap(pos, 10f);
                if (nearest == null)
                {
                    ctx.Reply("[Trap] No traps found nearby (within 10m)");
                    return;
                }
                
                var trapPos = nearest.Value.Position;
                
                // Remove it
                if (ContainerTrapService.RemoveTrap(trapPos))
                {
                    ctx.Reply("[Trap] ✅ Trap removed!");
                    Plugin.Log.LogInfo($"[Trap] Container trap removed at {trapPos}");
                }
                else
                {
                    ctx.Reply("[Trap] Failed to remove trap");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap list", shortHand: "tl", description: "List all trapped containers", adminOnly: true)]
        public static void TrapList(ChatCommandContext ctx)
        {
            try
            {
                var traps = ContainerTrapService.GetAllTraps();
                
                ctx.Reply($"[Trap] === Trapped Locations ({traps.Count}) ===");
                
                if (traps.Count == 0)
                {
                    ctx.Reply("  No traps set");
                    return;
                }
                
                int i = 0;
                foreach (var kvp in traps)
                {
                    i++;
                    var pos = kvp.Key;
                    var trap = kvp.Value;
                    var status = trap.IsArmed ? "✅ ARMED" : "❌ DISARMED";
                    var triggered = trap.Triggered ? " (TRIGGERED)" : "";
                    ctx.Reply($"  {i}. {status}{triggered}");
                    ctx.Reply($"     at ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                    ctx.Reply($"     Owner: {trap.OwnerPlatformId}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap arm", shortHand: "ta", description: "Arm/disarm trap at your location", adminOnly: true)]
        public static void TrapArm(ChatCommandContext ctx, string action = "toggle")
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var pos))
                {
                    ctx.Reply("[Trap] Error: Could not determine your position");
                    return;
                }
                
                // Find nearest trap
                var nearest = ContainerTrapService.FindNearestTrap(pos, 10f);
                if (nearest == null)
                {
                    ctx.Reply("[Trap] No traps found nearby");
                    return;
                }
                
                var trapPos = nearest.Value.Position;
                var trap = nearest.Value.Trap;
                
                // Determine new armed state
                bool newArmed;
                if (action == "toggle")
                {
                    newArmed = !trap.IsArmed;
                }
                else if (action == "on" || action == "arm")
                {
                    newArmed = true;
                }
                else if (action == "off" || action == "disarm")
                {
                    newArmed = false;
                }
                else
                {
                    newArmed = !trap.IsArmed;
                }
                
                // Update
                if (ContainerTrapService.SetArmed(trapPos, newArmed))
                {
                    var status = newArmed ? "ARMED" : "DISARMED";
                    ctx.Reply($"[Trap] ✅ Trap at {trapPos} is now {status}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap trigger", shortHand: "tt", description: "Test trigger a trap at your location", adminOnly: true)]
        public static void TrapTrigger(ChatCommandContext ctx)
        {
            try
            {
                if (!TryGetPlayerPosition(ctx, out var pos))
                {
                    ctx.Reply("[Trap] Error: Could not determine your position");
                    return;
                }
                var intruderId = GetPlayerPlatformId(ctx);
                
                // Find nearest trap
                var nearest = ContainerTrapService.FindNearestTrap(pos, 10f);
                if (nearest == null)
                {
                    ctx.Reply("[Trap] No traps found nearby");
                    return;
                }
                
                var trapPos = nearest.Value.Position;
                var trap = nearest.Value.Trap;
                
                // Trigger it
                if (ContainerTrapService.TriggerTrap(trapPos, intruderId))
                {
                    ctx.Reply($"[Trap] ⚠️ TRAP TRIGGERED!");
                    ctx.Reply($"  Location: ({trapPos.x:F0}, {trapPos.y:F0}, {trapPos.z:F0})");
                    ctx.Reply($"  Damage: {trap.DamageAmount}");
                    ctx.Reply($"  Duration: {trap.Duration}s");
                    
                    Plugin.Log.LogInfo($"[Trap] Test trigger at {trapPos} by {intruderId}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap clear", shortHand: "tc", description: "Clear all traps", adminOnly: true)]
        public static void TrapClear(ChatCommandContext ctx)
        {
            var count = ContainerTrapService.GetTrapCount();
            ContainerTrapService.ClearAll();
            ctx.Reply($"[Trap] ✅ Cleared {count} traps");
        }
        
        #region Helper Methods
        
        private static bool TryGetPlayerPosition(ChatCommandContext ctx, out float3 position)
        {
            position = float3.zero;
            try
            {
                var serverWorld = VRCore.ServerWorld;
                if (serverWorld == null)
                {
                    Plugin.Log.LogWarning("[Trap] Server world not available");
                    return false;
                }

                var entityManager = serverWorld.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (characterEntity == Entity.Null || !entityManager.Exists(characterEntity))
                {
                    Plugin.Log.LogWarning("[Trap] Sender character entity not found");
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
                Plugin.Log.LogError($"[Trap] GetPlayerPosition failed: {ex.Message}");
                return false;
            }
        }

        private static ulong GetPlayerPlatformId(ChatCommandContext ctx)
        {
            try
            {
                return ctx.User.PlatformId;
            }
            catch
            {
                return 0;
            }
        }
        
        #endregion
    }
}
