using VampireCommandFramework;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using System.Collections.Generic;
using VAuto.Commands.Converters;
using VAuto.Models;
using VAuto.Services;
using VAuto.Services.Systems;
using System.Linq;

namespace VAuto.Commands.Core
{
    [CommandGroup("player", "Player management commands")]
    public class PlayerCommands
    {
        /// <summary>
        /// Get PrefabGUID from entity.
        /// Usage: Helper method for command converters
        /// </summary>
        public static PrefabGUID GetPrefabGUIDFromEntity(Entity entity)
        {
            try
            {
                return VRCore.EntityManager.GetComponentData<PrefabGUID>(entity);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Player] Error getting PrefabGUID from entity: {ex.Message}");
                return new PrefabGUID((int)0);
            }
        }

        /// <summary>
        /// Give item to player using PrefabGUID and amount.
        /// Usage: .player give <guid> <amount> [player]
        /// </summary>
        [Command("give", "Give item to player using PrefabGUID", adminOnly: true)]
        public static void GiveItem(ChatCommandContext ctx, PrefabGUID guid, int amount, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var charEntity = targetPlayer.CharacterEntity;
                
                var serverGameManager = VRCore.ServerWorld?.GetExistingSystemManaged<ServerGameManager>();
                if (serverGameManager == null)
                {
                    ctx.Reply(Plugin.Log, "[Player] Error: ServerGameManager not available");
                    return;
                }

                var success = serverGameManager.TryAddInventoryItem(charEntity, guid, amount);
                
                if (success && success.NewEntity != Entity.Null)
                {
                    var prefabName = guid.LookupName();
                    ctx.Reply(Plugin.Log, $"[Player] ✓ Gave {amount}x {prefabName} to {targetPlayer.CharacterName}");
                }
                else
                {
                    var prefabName = guid.LookupName();
                    ctx.Reply(Plugin.Log, $"[Player] ✗ Failed to give {amount}x {prefabName} to {targetPlayer.CharacterName} (inventory full or invalid item)");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error giving item: {ex.Message}");
            }
        }

        /// <summary>
        /// Teleport a player to specified coordinates.
        /// Usage: .player teleport <x> <y> <z> [player]
        /// </summary>
        [Command("teleport", "Teleport a player to coordinates", adminOnly: true)]
        public static void Teleport(ChatCommandContext ctx, float x, float y, float z, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var charEntity = targetPlayer.CharacterEntity;
                var pos = new float3(x, y, z);
                
                charEntity.Write(new Translation { Value = pos });
                charEntity.Write(new LastTranslation { Value = pos });
                
                var charName = VRCore.EntityManager.HasComponent<PlayerCharacter>(charEntity) 
                    ? VRCore.EntityManager.GetComponentData<PlayerCharacter>(charEntity).Name.ToString()
                    : targetPlayer.CharacterName;
                    
                ctx.Reply(Plugin.Log, $"[Player] ✓ Teleported {charName} to {pos}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error teleporting: {ex.Message}");
            }
        }

        /// <summary>
        /// Teleport a player to a predefined zone using WAI position lookup.
        /// Usage: .player tp <zone> [player]
        /// </summary>
        [Command("tp", "Teleport a player to a zone", adminOnly: true)]
        public static void TeleportToZone(ChatCommandContext ctx, string zoneName, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var zonePosition = GetZonePositionFromWAI(zoneName);
                
                if (zonePosition == null)
                {
                    ctx.Reply(Plugin.Log, $"[Player] Error: Zone '{zoneName}' not found. Available zones: {string.Join(", ", GetAvailableZones())}");
                    return;
                }

                var charEntity = targetPlayer.CharacterEntity;
                charEntity.Write(new Translation { Value = zonePosition.Value });
                charEntity.Write(new LastTranslation { Value = zonePosition.Value });
                
                var charName = VRCore.EntityManager.HasComponent<PlayerCharacter>(charEntity) 
                    ? VRCore.EntityManager.GetComponentData<PlayerCharacter>(charEntity).Name.ToString()
                    : targetPlayer.CharacterName;
                    
                ctx.Reply(Plugin.Log, $"[Player] ✓ Teleported {charName} to zone '{zoneName}' {zonePosition.Value}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error teleporting to zone: {ex.Message}");
            }
        }

        /// <summary>
        /// List all available teleport zones.
        /// Usage: .player zones
        /// </summary>
        [Command("zones", "List all available teleport zones", adminOnly: true)]
        public static void ListZones(ChatCommandContext ctx)
        {
            try
            {
                var zones = GetAvailableZones();
                ctx.Reply(Plugin.Log, "[Player] Available zones:");
                foreach (var zone in zones)
                {
                    var pos = GetZonePositionFromWAI(zone);
                    ctx.Reply(Plugin.Log, $"  • {zone} - {pos?.Value}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error listing zones: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a player is in a specific zone.
        /// Usage: .player isinzone <zone> [player]
        /// </summary>
        [Command("isinzone", "Check if player is in a specific zone", adminOnly: true)]
        public static void IsInZone(ChatCommandContext ctx, string zoneName, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerService = VRCore.ServiceContainer.GetService<PlayerService>();
                
                if (playerService == null)
                {
                    ctx.Reply(Plugin.Log, "[Player] Error: PlayerService not available");
                    return;
                }

                var isInZone = playerService.IsPlayerInZone(targetPlayer.CharacterEntity, zoneName);
                
                ctx.Reply(Plugin.Log, $"[Player] {targetPlayer.CharacterName} is {(isInZone ? "✓" : "✗")} in zone '{zoneName}'");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error checking zone: {ex.Message}");
            }
        }

        /// <summary>
        /// List all zones a player is currently in.
        /// Usage: .player myzones [player]
        /// </summary>
        [Command("myzones", "List all zones player is currently in", adminOnly: true)]
        public static void MyZones(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerService = VRCore.ServiceContainer.GetService<PlayerService>();
                
                if (playerService == null)
                {
                    ctx.Reply(Plugin.Log, "[Player] Error: PlayerService not available");
                    return;
                }

                var zones = playerService.GetPlayerZones(targetPlayer.CharacterEntity);
                
                if (zones.Count == 0)
                {
                    ctx.Reply(Plugin.Log, $"[Player] {targetPlayer.CharacterName} is not in any zone");
                }
                else
                {
                    ctx.Reply(Plugin.Log, $"[Player] {targetPlayer.CharacterName} is in zones: {string.Join(", ", zones)}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error getting player zones: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a player is in any PvP zone.
        /// Usage: .player ispvp [player]
        /// </summary>
        [Command("ispvp", "Check if player is in PvP zone", adminOnly: true)]
        public static void IsPvP(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerModel = new Player(targetPlayer.UserEntity);
                
                var isPvP = playerModel.IsInPvPZone();
                
                ctx.Reply(Plugin.Log, $"[Player] {targetPlayer.CharacterName} is {(isPvP ? "✓" : "✗")} in PvP zone");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error checking PvP status: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a player is in a safe zone.
        /// Usage: .player issafe [player]
        /// </summary>
        [Command("issafe", "Check if player is in safe zone", adminOnly: true)]
        public static void IsSafe(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerModel = new Player(targetPlayer.UserEntity);
                
                var isSafe = playerModel.IsInSafeZone();
                
                ctx.Reply(Plugin.Log, $"[Player] {targetPlayer.CharacterName} is {(isSafe ? "✓" : "✗")} in safe zone");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error checking safe zone status: {ex.Message}");
            }
        }

        /// <summary>
        /// Enable auto systems for a player.
        /// Usage: .player autoenable [player]
        /// </summary>
        [Command("autoenable", "Enable auto systems for player", adminOnly: true)]
        public static void AutoEnable(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerModel = new Player(targetPlayer.UserEntity);
                
                // Apply auto systems
                if (playerModel.GetBloodQuality() < 100)
                {
                    playerModel.SetBloodQualityTo100();
                    ctx.Reply(Plugin.Log, $"[Player] ✓ Auto blood enabled for {targetPlayer.CharacterName}");
                }
                
                // Apply auto unlocks
                var debugEventsSystem = VRCore.ServerWorld?.GetExistingSystemManaged<DebugEventsSystem>();
                if (debugEventsSystem != null)
                {
                    var fromCharacter = new FromCharacter
                    {
                        User = targetPlayer.UserEntity,
                        Character = targetPlayer.CharacterEntity
                    };
                    
                    debugEventsSystem.UnlockAllResearch(fromCharacter);
                    debugEventsSystem.UnlockAllVBloods(fromCharacter);
                    debugEventsSystem.CompleteAllAchievements(fromCharacter);
                    
                    ctx.Reply(Plugin.Log, $"[Player] ✓ Auto unlocks enabled for {targetPlayer.CharacterName}");
                }
                
                ctx.Reply(Plugin.Log, $"[Player] Auto systems enabled for {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error enabling auto systems: {ex.Message}");
            }
        }

        /// <summary>
        /// Set blood quality to 100 for player.
        /// Usage: .player autoblood [player]
        /// </summary>
        [Command("autoblood", "Set blood quality to 100", adminOnly: true)]
        public static void AutoBlood(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerModel = new Player(targetPlayer.UserEntity);
                
                var success = playerModel.SetBloodQualityTo100();
                
                if (success)
                {
                    ctx.Reply(Plugin.Log, $"[Player] ✓ Blood quality set to 100 for {targetPlayer.CharacterName}");
                }
                else
                {
                    ctx.Reply(Plugin.Log, $"[Player] ✗ Failed to set blood quality for {targetPlayer.CharacterName}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error setting blood quality: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply auto unlocks for player.
        /// Usage: .player autounlock [player]
        /// </summary>
        [Command("autounlock", "Apply auto unlocks", adminOnly: true)]
        public static void AutoUnlock(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                
                var debugEventsSystem = VRCore.ServerWorld?.GetExistingSystemManaged<DebugEventsSystem>();
                if (debugEventsSystem == null)
                {
                    ctx.Reply(Plugin.Log, "[Player] Error: DebugEventsSystem not available");
                    return;
                }

                var fromCharacter = new FromCharacter
                {
                    User = targetPlayer.UserEntity,
                    Character = targetPlayer.CharacterEntity
                };
                
                debugEventsSystem.UnlockAllResearch(fromCharacter);
                debugEventsSystem.UnlockAllVBloods(fromCharacter);
                debugEventsSystem.CompleteAllAchievements(fromCharacter);
                
                ctx.Reply(Plugin.Log, $"[Player] ✓ Auto unlocks applied to {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error applying auto unlocks: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply auto kits for player based on current zone.
        /// Usage: .player autokit [player]
        /// </summary>
        [Command("autokit", "Apply auto kits based on zone", adminOnly: true)]
        public static void AutoKit(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerModel = new Player(targetPlayer.UserEntity);

                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Player] Error: {error}");
                    return;
                }

                var playerZones = playerModel.GetZones();
                var appliedKits = 0;

                if (EndGameKitCommandHelper.TryHasKitApplied(system, targetPlayer.CharacterEntity, out var hasKitApplied, out _)
                    && hasKitApplied)
                {
                    ctx.Reply(Plugin.Log, $"[Player] {targetPlayer.CharacterName} already has a kit applied");
                    return;
                }

                var kitNames = EndGameKitCommandHelper.GetKitProfileNames(system);
                foreach (var kitName in kitNames)
                {
                    var profile = EndGameKitCommandHelper.GetKitProfile(system, kitName);
                    if (profile == null || !EndGameKitCommandHelper.GetBool(profile, "Enabled", true))
                        continue;

                    bool shouldApply = false;
                    var autoApply = EndGameKitCommandHelper.GetBool(profile, "AutoApplyOnZoneEntry", false);
                    var autoZones = EndGameKitCommandHelper.GetStringList(profile, "AutoApplyZones");
                    var allowInPvP = EndGameKitCommandHelper.GetBool(profile, "AllowInPvP", false);

                    if (autoApply && autoZones.Count > 0)
                    {
                        shouldApply = autoZones.Any(zone =>
                            playerZones.Any(playerZone =>
                                playerZone.Equals(zone, StringComparison.OrdinalIgnoreCase)));
                    }

                    if (!shouldApply && (playerModel.IsInPvPZone() || playerModel.IsInEndGameZone()))
                    {
                        shouldApply = allowInPvP;
                    }

                    if (shouldApply)
                    {
                        if (EndGameKitCommandHelper.TryApplyKit(system, targetPlayer.CharacterEntity, kitName, out var applyError))
                        {
                            ctx.Reply(Plugin.Log, $"[Player] ✓ Auto-applied kit '{kitName}' to {targetPlayer.CharacterName}");
                            appliedKits++;
                            break;
                        }

                        ctx.Reply(Plugin.Log, $"[Player] ✗ Failed to auto-apply kit '{kitName}': {applyError}");
                    }
                }

                ctx.Reply(Plugin.Log, $"[Player] Auto kits applied: {appliedKits} kits to {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Player] Error applying auto kits: {ex.Message}");
            }
        }

        #region Helper Methods

        private static float3? GetZonePositionFromWAI(string zoneName)
        {
            try
            {
                // Use WAI (World AI) to find zone positions dynamically
                // First try to find zone by name in existing zone system
                var zoneTracking = VRCore.ServiceContainer.GetService<VAuto.Services.Systems.ZoneTrackingService>();
                if (zoneTracking != null)
                {
                    // Access zone boundaries via reflection (since _zoneBoundaries is private)
                    var zoneBoundariesField = typeof(VAuto.Services.Systems.ZoneTrackingService)
                        .GetField("_zoneBoundaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (zoneBoundariesField != null)
                    {
                        var zoneBoundaries = zoneBoundariesField.GetValue(zoneTracking) as System.Collections.IList;
                        if (zoneBoundaries != null)
                        {
                            foreach (var zone in zoneBoundaries)
                            {
                                var zoneId = zone.GetType().GetProperty("ZoneId")?.GetValue(zone);
                                var zoneType = zone.GetType().GetProperty("ZoneType")?.GetValue(zone);
                                var center = zone.GetType().GetProperty("Center")?.GetValue(zone);
                                
                                // Check if zone matches by name or type
                                if ((zoneId?.ToString().Equals(zoneName, StringComparison.OrdinalIgnoreCase) == true) ||
                                    (zoneType?.ToString().Equals(zoneName, StringComparison.OrdinalIgnoreCase) == true))
                                {
                                    if (center is float3 position)
                                    {
                                        return position;
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback to hardcoded positions for common zones
                var zonePositions = new System.Collections.Generic.Dictionary<string, float3>
                {
                    {"spawn", new float3(0, 0, 0)},
                    {"castle", new float3(-1000, 5, -500)},
                    {"pvp", new float3(-1500, 5, -1000)},
                    {"arena", new float3(-1000, 5, -500)},
                    {"brighthaven", new float3(1000, 0, 1000)},
                    {"farbane", new float3(-2000, 0, -1500)},
                    {"dunley", new float3(500, 0, -800)},
                    {"mosswick", new float3(800, 0, 1200)},
                    {"silverlight", new float3(-1200, 0, 800)},
                    {"gloomgrove", new float3(1500, 0, 500)}
                };

                zonePositions.TryGetValue(zoneName.ToLower(), out var position);
                return position;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Player] Error getting zone position from WAI for '{zoneName}': {ex.Message}");
                return null;
            }
        }

        private static List<string> GetAvailableZones()
        {
            try
            {
                var zones = new List<string>();
                
                // Get zones from the zone tracking system
                var zoneTracking = VRCore.ServiceContainer.GetService<VAuto.Services.Systems.ZoneTrackingService>();
                if (zoneTracking != null)
                {
                    var zoneBoundariesField = typeof(VAuto.Services.Systems.ZoneTrackingService)
                        .GetField("_zoneBoundaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (zoneBoundariesField != null)
                    {
                        var zoneBoundaries = zoneBoundariesField.GetValue(zoneTracking) as System.Collections.IList;
                        if (zoneBoundaries != null)
                        {
                            foreach (var zone in zoneBoundaries)
                            {
                                var zoneId = zone.GetType().GetProperty("ZoneId")?.GetValue(zone);
                                if (zoneId != null)
                                {
                                    zones.Add(zoneId.ToString());
                                }
                            }
                        }
                    }
                }
                
                // Add common zone names that might not be in the tracking system
                zones.AddRange(new[] { "spawn", "castle", "pvp", "arena", "brighthaven", "farbane", "dunley", "mosswick", "silverlight", "gloomgrove" });
                
                return zones.Distinct().OrderBy(z => z).ToList();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Player] Error getting available zones: {ex.Message}");
                return new List<string> { "spawn", "castle", "pvp" };
            }
        }

        #endregion
    }
}
