using VampireCommandFramework;
using ProjectM;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VAuto.Commands.Converters;
using System.Linq;
using ProjectM.Network;
using Stunlock.Core;
using System.Reflection;
using System;

namespace VAuto.Commands.Core
{
    [CommandGroup("kit", "EndGameKit management commands")]
    public static class KitCommands
    {
        private static Type _arenaPlayerServiceType;
        private static MethodInfo _tryGetCharacterPositionMethod;
        private static MethodInfo _isInZoneMethod;
        private static PropertyInfo _arenaCenterProperty;
        private static PropertyInfo _arenaRadiusProperty;

        /// <summary>
        /// Apply all enabled kits to the player (enter lifecycle).
        /// Usage: .kit enter [player]
        /// </summary>
        [Command("enter", "Apply all enabled kits (enter lifecycle)", adminOnly: true)]
        public static void KitEnterCommand(ICommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: {error}");
                    return;
                }

                var kitNames = EndGameKitCommandHelper.GetKitProfileNames(system);
                var appliedKits = 0;
                var failedKits = 0;

                ctx.Reply(Plugin.Log, $"[Kit] Starting lifecycle enter for {targetPlayer.CharacterName} - applying all enabled kits...");

                foreach (var kitName in kitNames)
                {
                    var profile = EndGameKitCommandHelper.GetKitProfile(system, kitName);
                    if (profile == null || !EndGameKitCommandHelper.GetBool(profile, "Enabled", true))
                        continue;

                    try
                    {
                        if (!EndGameKitCommandHelper.TryApplyKit(system, targetPlayer.CharacterEntity, kitName, out var applyError))
                        {
                            ctx.Reply(Plugin.Log, $"[Kit] ✗ Failed to apply kit {kitName} to {targetPlayer.CharacterName}: {applyError}");
                            failedKits++;
                            continue;
                        }

                        var profileName = EndGameKitCommandHelper.GetString(profile, "Name", kitName);
                        ctx.Reply(Plugin.Log, $"[Kit] ✓ Applied kit: {profileName} to {targetPlayer.CharacterName}");
                        appliedKits++;
                    }
                    catch (Exception ex)
                    {
                        ctx.Reply(Plugin.Log, $"[Kit] ✗ Failed to apply kit {kitName} to {targetPlayer.CharacterName}: {ex.Message}");
                        failedKits++;
                    }
                }

                ctx.Reply(Plugin.Log, $"[Kit] Lifecycle enter complete for {targetPlayer.CharacterName}: {appliedKits} kits applied, {failedKits} failed");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error during enter lifecycle: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove all kits from the player (exit lifecycle).
        /// Usage: .kit exit [player]
        /// </summary>
        [Command("exit", "Remove all kits (exit lifecycle)", adminOnly: true)]
        public static void KitExitCommand(ICommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: {error}");
                    return;
                }

                ctx.Reply(Plugin.Log, $"[Kit] Starting lifecycle exit for {targetPlayer.CharacterName} - removing all kits...");

                if (!EndGameKitCommandHelper.TryRemoveKit(system, targetPlayer.CharacterEntity, out var removeError))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] ✗ Failed to remove kit from {targetPlayer.CharacterName}: {removeError}");
                    return;
                }

                ctx.Reply(Plugin.Log, $"[Kit] ✓ Kit removed for {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error during exit lifecycle: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply a specific kit by name.
        /// Usage: .kit apply <kitname> [player]
        /// </summary>
        [Command("apply", "Apply a specific kit by name", adminOnly: true)]
        public static void KitApplyCommand(ICommandContext ctx, string kitName, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: {error}");
                    return;
                }

                var profile = EndGameKitCommandHelper.GetKitProfile(system, kitName);

                if (profile == null || !EndGameKitCommandHelper.GetBool(profile, "Enabled", true))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: Kit '{kitName}' not found or not enabled");
                    return;
                }

                var profileName = EndGameKitCommandHelper.GetString(profile, "Name", kitName);
                var description = EndGameKitCommandHelper.GetString(profile, "Description", string.Empty);
                ctx.Reply(Plugin.Log, $"[Kit] Applying kit '{profileName}' to {targetPlayer.CharacterName}");
                if (!string.IsNullOrWhiteSpace(description))
                    ctx.Reply(Plugin.Log, $"[Kit] Description: {description}");
                
                if (!EndGameKitCommandHelper.TryApplyKit(system, targetPlayer.CharacterEntity, kitName, out var applyError))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error applying kit '{kitName}': {applyError}");
                    return;
                }

                ctx.Reply(Plugin.Log, $"[Kit] ✓ Successfully applied kit '{profileName}' to {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error applying kit '{kitName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Apply the Brute kit and set blood quality (zone-only).
        /// Usage: .kit brute [quality]
        /// </summary>
        [Command("brute", "Apply Brute kit and set blood quality (zone-only)", adminOnly: false)]
        public static void KitBruteCommand(ICommandContext ctx, int quality = 100)
        {
            var character = ctx.SenderCharacterEntity;
            if (character == Entity.Null)
            {
                ctx.Reply(Plugin.Log, "[Kit] Error: Could not resolve your character.");
                return;
            }

            if (!IsInArenaZone(character))
            {
                ctx.Reply(Plugin.Log, "[Kit] This command is only available inside the arena zone.");
                return;
            }

            if (!TryApplyKitByName(character, "Brute", out var kitError) &&
                !TryApplyKitByName(character, "Brute_Ready", out kitError))
            {
                ctx.Reply(Plugin.Log, $"[Kit] Failed to apply kit: {kitError}");
                return;
            }

            TrySetBloodQuality(character, quality);
            ctx.Reply(Plugin.Log, $"[Kit] Brute kit applied. Blood quality set to {Math.Clamp(quality, 0, 100)}.");
        }

        private static void TrySetBloodQuality(Entity character, int quality)
        {
            try
            {
                var em = VRCore.EntityManager;
                if (!em.HasComponent<Blood>(character))
                    return;

                var blood = em.GetComponentData<Blood>(character);
                blood.Quality = (float)Math.Clamp(quality, 0, 100);
                em.SetComponentData(character, blood);
            }
            catch
            {
                // ignore
            }
        }

        private static bool IsInArenaZone(Entity character)
        {
            try
            {
                // Cache reflection data
                if (_arenaPlayerServiceType == null)
                {
                    _arenaPlayerServiceType = Type.GetType("VAuto.Arena.Services.ArenaPlayerService, VAutoArena", throwOnError: false);
                    if (_arenaPlayerServiceType == null)
                        return false;

                    _tryGetCharacterPositionMethod = _arenaPlayerServiceType.GetMethod("TryGetCharacterPosition", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    _isInZoneMethod = _arenaPlayerServiceType.GetMethod("IsInZone", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    _arenaCenterProperty = _arenaPlayerServiceType.GetProperty("ArenaCenter", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    _arenaRadiusProperty = _arenaPlayerServiceType.GetProperty("ArenaRadius", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                }

                if (_tryGetCharacterPositionMethod == null || _isInZoneMethod == null || _arenaCenterProperty == null || _arenaRadiusProperty == null)
                    return false;

                object[] args = new object[] { character, default(float3) };
                var ok = (bool)_tryGetCharacterPositionMethod.Invoke(null, args);
                if (!ok)
                    return false;

                var pos = (float3)args[1];
                var center = (float3)_arenaCenterProperty.GetValue(null);
                var radius = (float)_arenaRadiusProperty.GetValue(null);

                return (bool)_isInZoneMethod.Invoke(null, new object[] { pos, center, radius });
            }
            catch
            {
                return false;
            }
        }

        private static bool TryApplyKitByName(Entity character, string kitName, out string error)
        {
            error = string.Empty;
            try
            {
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out error))
                    return false;

                return EndGameKitCommandHelper.TryApplyKit(system, character, kitName, out error);
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }

        /// <summary>
        /// List all available kits.
        /// Usage: .kit list
        /// </summary>
        [Command("list", "List all available kits", adminOnly: true)]
        public static void KitListCommand(ICommandContext ctx)
        {
            try
            {
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: {error}");
                    return;
                }

                var kitNames = EndGameKitCommandHelper.GetKitProfileNames(system);
                var enabledKits = new System.Collections.Generic.List<string>();
                var disabledKits = new System.Collections.Generic.List<string>();

                foreach (var kitName in kitNames)
                {
                    var profile = EndGameKitCommandHelper.GetKitProfile(system, kitName);
                    if (profile == null)
                        continue;

                    if (EndGameKitCommandHelper.GetBool(profile, "Enabled", true))
                        enabledKits.Add(kitName);
                    else
                        disabledKits.Add(kitName);
                }

                ctx.Reply(Plugin.Log, $"[Kit] Available Kits ({kitNames.Count} total):");
                ctx.Reply(Plugin.Log, "");

                if (enabledKits.Count > 0)
                {
                    ctx.Reply(Plugin.Log, "§2Enabled Kits:");
                    foreach (var kit in enabledKits)
                    {
                        var profile = EndGameKitCommandHelper.GetKitProfile(system, kit);
                        if (profile == null)
                            continue;

                        var autoApplyOnZoneEntry = EndGameKitCommandHelper.GetBool(profile, "AutoApplyOnZoneEntry", false);
                        var autoApply = autoApplyOnZoneEntry ? "§aAuto§r" : "§cManual§r";
                        var zones = EndGameKitCommandHelper.GetStringList(profile, "AutoApplyZones");
                        var zonesText = zones.Count > 0 ? string.Join(", ", zones) : "None";
                        var description = EndGameKitCommandHelper.GetString(profile, "Description", string.Empty);
                        var minGs = EndGameKitCommandHelper.GetInt(profile, "MinimumGearScore", 0);
                        ctx.Reply(Plugin.Log, $"  • {kit} - {description}");
                        ctx.Reply(Plugin.Log, $"    Status: {autoApply} | Zones: {zonesText} | Min GS: {minGs}");
                    }
                }

                if (disabledKits.Count > 0)
                {
                    ctx.Reply(Plugin.Log, "");
                    ctx.Reply(Plugin.Log, "§8Disabled Kits:");
                    foreach (var kit in disabledKits)
                    {
                        var profile = EndGameKitCommandHelper.GetKitProfile(system, kit);
                        var description = profile != null
                            ? EndGameKitCommandHelper.GetString(profile, "Description", string.Empty)
                            : string.Empty;
                        ctx.Reply(Plugin.Log, $"  • {kit} - {description}");
                    }
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error listing kits: {ex.Message}");
            }
        }

        /// <summary>
        /// Teleport to a specific zone and apply appropriate kits.
        /// Usage: .kit teleport <zonename> [player]
        /// </summary>
        [Command("teleport", "Teleport to a zone and apply appropriate kits", adminOnly: true)]
        public static void KitTeleportCommand(ICommandContext ctx, string zoneName, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: {error}");
                    return;
                }

                // Get zone position (this would need to be configured or looked up)
                var zonePosition = GetZonePosition(zoneName);
                if (zonePosition == null)
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: Zone '{zoneName}' not found or no position configured");
                    return;
                }

                // Teleport player
                if (VRCore.EntityManager.HasComponent<Translation>(targetPlayer.CharacterEntity))
                {
                    VRCore.EntityManager.SetComponentData(targetPlayer.CharacterEntity, new Translation 
                    { 
                        Value = zonePosition.Value 
                    });
                    ctx.Reply(Plugin.Log, $"[Kit] Teleported {targetPlayer.CharacterName} to zone: {zoneName}");
                }
                else
                {
                    ctx.Reply(Plugin.Log, "[Kit] Error: Could not teleport player (missing Translation component)");
                    return;
                }

                // Apply auto-apply kits for this zone
                if (!EndGameKitCommandHelper.TryApplyKitForZone(system, targetPlayer.UserEntity, targetPlayer.CharacterEntity, zoneName, out var applyError))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] No auto-apply kits configured for zone '{zoneName}' or failed to apply: {applyError}");
                    return;
                }

                ctx.Reply(Plugin.Log, $"[Kit] ✓ Auto-applied kit for zone '{zoneName}' to {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error during teleport: {ex.Message}");
            }
        }

        /// <summary>
        /// List available zones for teleportation.
        /// Usage: .kit zones
        /// </summary>
        [Command("zones", "List available zones for teleportation", adminOnly: true)]
        public static void KitZonesCommand(ICommandContext ctx)
        {
            try
            {
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: {error}");
                    return;
                }

                var kitNames = EndGameKitCommandHelper.GetKitProfileNames(system);
                var allZones = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kitName in kitNames)
                {
                    var profile = EndGameKitCommandHelper.GetKitProfile(system, kitName);
                    if (profile == null || !EndGameKitCommandHelper.GetBool(profile, "Enabled", true))
                        continue;

                    var zones = EndGameKitCommandHelper.GetStringList(profile, "AutoApplyZones");
                    foreach (var zone in zones)
                        allZones.Add(zone);
                }

                if (allZones.Count == 0)
                {
                    ctx.Reply(Plugin.Log, "[Kit] No zones configured for auto-apply");
                    return;
                }

                var zonesList = allZones.ToList();
                ctx.Reply(Plugin.Log, $"[Kit] Available Zones ({zonesList.Count}):");

                foreach (var zone in zonesList)
                {
                    var kits = new System.Collections.Generic.List<string>();
                    foreach (var kitName in kitNames)
                    {
                        var profile = EndGameKitCommandHelper.GetKitProfile(system, kitName);
                        if (profile == null || !EndGameKitCommandHelper.GetBool(profile, "Enabled", true))
                            continue;

                        var autoApply = EndGameKitCommandHelper.GetBool(profile, "AutoApplyOnZoneEntry", false);
                        var zones = EndGameKitCommandHelper.GetStringList(profile, "AutoApplyZones");
                        if (autoApply && zones.Any(z => z.Equals(zone, StringComparison.OrdinalIgnoreCase)))
                            kits.Add(kitName);
                    }

                    ctx.Reply(Plugin.Log, $"  • {zone} - {kits.Count} auto-apply kit(s): {string.Join(", ", kits)}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error listing zones: {ex.Message}");
            }
        }

        /// <summary>
        /// Force reload kit configuration.
        /// Usage: .kit reload
        /// </summary>
        [Command("reload", "Reload kit configuration", adminOnly: true)]
        public static void KitReloadCommand(ICommandContext ctx)
        {
            try
            {
                if (!EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error: {error}");
                    return;
                }

                if (!EndGameKitCommandHelper.TryLoadConfiguration(system, out var loadError))
                {
                    ctx.Reply(Plugin.Log, $"[Kit] Error reloading configuration: {loadError}");
                    return;
                }

                ctx.Reply(Plugin.Log, "[Kit] ✓ Configuration reloaded successfully");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Kit] Error reloading configuration: {ex.Message}");
            }
        }

        #region Helper Methods

        private static float3? GetZonePosition(string zoneName)
        {
            // This would ideally read from a configuration file or zone registry
            // For now, returning some example positions
            var zonePositions = new System.Collections.Generic.Dictionary<string, float3>
            {
                {"StarterZone", new float3(0, 0, 0)},
                {"TutorialZone", new float3(100, 0, 100)},
                {"PvPArena", new float3(200, 0, 200)},
                {"ArenaZone1", new float3(300, 0, 300)},
                {"EndGameZone1", new float3(400, 0, 400)},
                {"EndGameZone2", new float3(500, 0, 500)}
            };

            return zonePositions.TryGetValue(zoneName, out var position) ? position : null;
        }

        #endregion
    }
}

