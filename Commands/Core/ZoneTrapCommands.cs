using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Core;
using VAuto.Core.Services;
using System;
using System.Collections.Generic;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Zone-based trap commands - creates small trigger zones (1-2m radius).
    /// Triggers when players enter the zone. Admin only.
    /// </summary>
    public static class ZoneTrapCommands
    {
        /// <summary>
        /// Get the position of the player who sent the command.
        /// </summary>
        private static float3 GetPlayerPosition(ChatCommandContext ctx)
        {
            try
            {
                var platformId = ctx.User.PlatformId;
                
                var query = VRCore.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                
                foreach (var entity in entities)
                {
                    var pc = VRCore.EntityManager.GetComponentData<PlayerCharacter>(entity);
                    var userEntity = pc.UserEntity;
                    
                    if (userEntity == Entity.Null) continue;
                    
                    var user = VRCore.EntityManager.GetComponentData<User>(userEntity);
                    if (user.IsConnected && user.PlatformId == platformId)
                    {
                        // Found the player's character, get position
                        if (VRCore.EntityManager.HasComponent<LocalTransform>(entity))
                        {
                            var pos = VRCore.EntityManager.GetComponentData<LocalTransform>(entity).Position;
                            entities.Dispose();
                            return pos;
                        }
                        else if (VRCore.EntityManager.HasComponent<Translation>(entity))
                        {
                            var pos = VRCore.EntityManager.GetComponentData<Translation>(entity).Value;
                            entities.Dispose();
                            return pos;
                        }
                    }
                }
                
                entities.Dispose();
                return float3.zero;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Trap] GetPlayerPosition failed: {ex.Message}");
                return float3.zero;
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
        
        [Command("trap create", shortHand: "tc", description: "Create a trap zone at your location (2m radius, admin only)", adminOnly: true)]
        public static void TrapCreate(ChatCommandContext ctx, string type = "container")
        {
            try
            {
                var position = GetPlayerPosition(ctx);
                var ownerId = GetPlayerPlatformId(ctx);
                
                // Validate type
                type = type.ToLower();
                if (type != "container" && type != "waypoint" && type != "border")
                {
                    ctx.Reply($"[Trap] Invalid type '{type}'. Use: container, waypoint, or border");
                    return;
                }
                
                TrapZoneService.CreateZone(position, ownerId, 2f, type);
                
                ctx.Reply($"[Trap] ✅ Created {type} trap zone!");
                ctx.Reply($"  Position: ({position.x:F0}, {position.y:F0}, {position.z:F0})");
                ctx.Reply($"  Radius: 2m");
                
                Plugin.Log.LogInfo($"[Trap] Admin created {type} zone at {position} for owner {ownerId}");
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap delete", shortHand: "td", description: "Delete trap zone at your location (admin only)", adminOnly: true)]
        public static void TrapDelete(ChatCommandContext ctx)
        {
            try
            {
                var position = GetPlayerPosition(ctx);
                
                if (TrapZoneService.RemoveNearestZone(position, 5f))
                {
                    ctx.Reply("[Trap] ✅ Nearest trap zone removed");
                }
                else
                {
                    ctx.Reply("[Trap] No trap zones found nearby (within 5m)");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap list", shortHand: "tl", description: "List all trap zones (admin only)", adminOnly: true)]
        public static void TrapList(ChatCommandContext ctx)
        {
            try
            {
                var zones = TrapZoneService.GetAllZones();
                
                ctx.Reply($"[Trap] === Trap Zones ({zones.Count}) ===");
                
                if (zones.Count == 0)
                {
                    ctx.Reply("  No trap zones created");
                    return;
                }
                
                int i = 0;
                foreach (var kvp in zones)
                {
                    i++;
                    var pos = kvp.Key;
                    var zone = kvp.Value;
                    var status = zone.IsArmed ? "✅ ARMED" : "❌ DISARMED";
                    var triggered = zone.Triggered ? " (TRIGGERED)" : "";
                    ctx.Reply($"  {i}. {status}{triggered} [{zone.TrapType}]");
                    ctx.Reply($"     at ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                    ctx.Reply($"     Radius: {zone.Radius}m | Owner: {zone.OwnerPlatformId}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap arm", shortHand: "ta", description: "Arm/disarm nearest trap zone (admin only)", adminOnly: true)]
        public static void TrapArm(ChatCommandContext ctx, string action = "toggle")
        {
            try
            {
                var position = GetPlayerPosition(ctx);
                var zones = TrapZoneService.GetAllZones();
                
                // Find nearest zone
                float3? nearestPos = null;
                foreach (var kvp in zones)
                {
                    if (math.distance(position, kvp.Key) <= 5f)
                    {
                        nearestPos = kvp.Key;
                        break;
                    }
                }
                
                if (nearestPos == null)
                {
                    ctx.Reply("[Trap] No zones found nearby (within 5m)");
                    return;
                }
                
                bool newArmed = action switch
                {
                    "on" or "arm" => true,
                    "off" or "disarm" => false,
                    _ => !zones[nearestPos.Value].IsArmed
                };
                
                if (TrapZoneService.SetArmed(nearestPos.Value, newArmed))
                {
                    var status = newArmed ? "ARMED" : "DISARMED";
                    ctx.Reply($"[Trap] ✅ Zone is now {status}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap check", shortHand: "tch", description: "Check if you're in a trap zone", adminOnly: false)]
        public static void TrapCheck(ChatCommandContext ctx)
        {
            try
            {
                var position = GetPlayerPosition(ctx);
                var isInZone = TrapZoneService.IsInZone(position);
                
                if (isInZone)
                {
                    ctx.Reply("[Trap] ⚠️ You are inside a trap zone!");
                }
                else
                {
                    ctx.Reply("[Trap] ✅ You are not in any trap zone");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Trap] Error: {ex.Message}");
            }
        }
        
        [Command("trap clear", shortHand: "tcl", description: "Clear all trap zones (admin only)", adminOnly: true)]
        public static void TrapClear(ChatCommandContext ctx)
        {
            var count = TrapZoneService.GetZoneCount();
            TrapZoneService.ClearAll();
            ctx.Reply($"[Trap] ✅ Cleared {count} trap zones");
            Plugin.Log.LogInfo($"[Trap] Admin cleared {count} trap zones");
        }
    }
}
