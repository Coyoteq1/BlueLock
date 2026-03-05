using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Blueluck.Models;
using Unity.Entities;
using VampireCommandFramework;

namespace Blueluck.Commands
{
    /// <summary>
    /// Minimal zone inspection/reload commands. Gameplay effects are driven by zone config + flows + kits.
    /// </summary>
    public static class ZoneCommands
    {
        [Command("enterarena", description: "Force-enter an arena zone by name or hash", adminOnly: true)]
        public static void EnterArena(ChatCommandContext ctx, string zoneNameOrHash)
        {
            ForceEnterZone(ctx, zoneNameOrHash, "ArenaZone");
        }

        [Command("exitarena", description: "Force-exit your current arena zone", adminOnly: true)]
        public static void ExitArena(ChatCommandContext ctx)
        {
            ForceExitZone(ctx, "ArenaZone");
        }

        [Command("enterboss", description: "Force-enter a boss zone by name or hash", adminOnly: true)]
        public static void EnterBoss(ChatCommandContext ctx, string zoneNameOrHash)
        {
            ForceEnterZone(ctx, zoneNameOrHash, "BossZone");
        }

        [Command("exitboss", description: "Force-exit your current boss zone", adminOnly: true)]
        public static void ExitBoss(ChatCommandContext ctx)
        {
            ForceExitZone(ctx, "BossZone");
        }

        [Command("zone status", shortHand: "zs", description: "Show zone status", adminOnly: true)]
        public static void ZoneStatus(ChatCommandContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Blueluck Zone Status ===");

            if (Plugin.ZoneConfig?.IsInitialized == true)
            {
                var zones = Plugin.ZoneConfig.GetZones();
                sb.AppendLine($"Active Zones: {zones.Count}");

                foreach (var zone in zones.Take(8))
                {
                    sb.AppendLine($"  - {zone.Name} ({zone.Type}) hash={zone.Hash} enabled={zone.Enabled}");
                }
            }
            else
            {
                sb.AppendLine("Zone config not initialized");
            }

            sb.AppendLine($"Flows: {(Plugin.FlowRegistry?.IsInitialized == true ? "initialized" : "disabled/not-ready")}");
            sb.AppendLine($"Kits: {(Plugin.Kits?.IsInitialized == true ? "initialized" : "disabled/not-ready")}");
            sb.AppendLine($"Progress: {(Plugin.Progress?.IsInitialized == true ? "initialized" : "disabled/not-ready")}");
            sb.AppendLine($"Validation: {(Plugin.FlowValidation?.IsInitialized == true ? "initialized" : "disabled/not-ready")}");

            ctx.Reply(sb.ToString());
        }

        [Command("zone list", shortHand: "zl", description: "List all configured zones", adminOnly: true)]
        public static void ZoneList(ChatCommandContext ctx)
        {
            if (Plugin.ZoneConfig?.IsInitialized != true)
            {
                ctx.Error("Zone config not initialized");
                return;
            }

            var zones = Plugin.ZoneConfig.GetZones();
            var sb = new StringBuilder();
            sb.AppendLine($"=== Zones ({zones.Count}) ===");

            foreach (var zone in zones)
            {
                var center = zone.Center;
                sb.AppendLine(zone.Name);
                sb.AppendLine($"  Type: {zone.Type}, Hash: {zone.Hash}");
                sb.AppendLine($"  Center: [{center[0]:F1}, {center[1]:F1}, {center[2]:F1}]");
                sb.AppendLine($"  Radius: Entry={zone.EntryRadius}, Exit={zone.ExitRadius}");
                sb.AppendLine($"  Enabled: {zone.Enabled}");
                if (!string.IsNullOrEmpty(zone.FlowOnEnter) || !string.IsNullOrEmpty(zone.FlowOnExit))
                {
                    sb.AppendLine($"  Flows: enter='{zone.FlowOnEnter}' exit='{zone.FlowOnExit}'");
                }
                sb.AppendLine();
            }

            ctx.Reply(sb.ToString());
        }

        [Command("zone reload", shortHand: "zr", description: "Reload zone configuration", adminOnly: true)]
        public static void ZoneReload(ChatCommandContext ctx)
        {
            try
            {
                if (Plugin.ZoneConfig?.IsInitialized == true)
                {
                    Plugin.ZoneConfig.Reload();
                    ctx.Reply("Zone configuration reloaded.");
                }
                else
                {
                    ctx.Error("Zone config not initialized");
                }
            }
            catch (Exception ex)
            {
                ctx.Error($"Failed to reload: {ex.Message}");
            }
        }

        [Command("flow reload", description: "Reload flows.json from disk", adminOnly: true)]
        public static void FlowReload(ChatCommandContext ctx)
        {
            if (Plugin.FlowRegistry?.IsInitialized != true)
            {
                ctx.Error("FlowRegistry not initialized");
                return;
            }

            Plugin.FlowRegistry.Reload();
            ctx.Reply("Flows reloaded.");
        }

        [Command("zone debug", description: "Toggle zone detection debug mode", adminOnly: true)]
        public static void ZoneDebug(ChatCommandContext ctx)
        {
            var current = Plugin.ZoneDetectionDebugMode?.Value ?? false;
            Plugin.ZoneDetectionDebugMode.Value = !current;
            ctx.Reply($"Debug mode: {!current}");
        }

        [Command("flow validate", description: "Validate flow configurations", adminOnly: true)]
        public static void FlowValidate(ChatCommandContext ctx)
        {
            if (Plugin.FlowValidation?.IsInitialized != true)
            {
                ctx.Error("FlowValidation service not initialized");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Flow Validation Results ===");

            // Validate all zones
            if (Plugin.ZoneConfig?.IsInitialized == true)
            {
                var zones = Plugin.ZoneConfig.GetZones();
                var errors = new List<string>();
                var warnings = new List<string>();

                foreach (var zone in zones)
                {
                    var result = Plugin.FlowValidation.ValidateZoneFlows(zone);
                    errors.AddRange(result.Errors);
                    warnings.AddRange(result.Warnings);
                }

                sb.AppendLine($"Zones checked: {zones.Count}");

                if (errors.Count > 0)
                {
                    sb.AppendLine($"Errors ({errors.Count}):");
                    foreach (var e in errors.Take(10))
                        sb.AppendLine($"  ! {e}");
                }

                if (warnings.Count > 0)
                {
                    sb.AppendLine($"Warnings ({warnings.Count}):");
                    foreach (var w in warnings.Take(10))
                        sb.AppendLine($"  ? {w}");
                }

                if (errors.Count == 0 && warnings.Count == 0)
                    sb.AppendLine("All zone configurations valid!");
            }
            else
            {
                sb.AppendLine("Zone config not initialized");
            }

            ctx.Reply(sb.ToString());
        }

        private static void ForceEnterZone(ChatCommandContext ctx, string zoneNameOrHash, string expectedType)
        {
            if (Plugin.ZoneConfig?.IsInitialized != true || Plugin.ZoneTransition?.IsInitialized != true)
            {
                ctx.Error("Zone services are not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
            if (player == Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            if (string.IsNullOrWhiteSpace(zoneNameOrHash))
            {
                ctx.Error($"Usage: .{(expectedType == "ArenaZone" ? "enterarena" : "enterboss")} <zone name|hash>");
                return;
            }

            if (!TryResolveZone(zoneNameOrHash, expectedType, out var zone))
            {
                ctx.Error($"{expectedType} '{zoneNameOrHash}' not found.");
                return;
            }

            var currentHash = Plugin.ZoneTransition.GetPlayerZone(player);
            if (currentHash != 0 && currentHash != zone.Hash && Plugin.ZoneConfig.TryGetZoneByHash(currentHash, out var previousZone))
            {
                Plugin.ZoneTransition.OnZoneExit(player, previousZone);
            }

            Plugin.ZoneTransition.OnZoneEnter(player, zone);
            ctx.Reply($"Forced enter: {zone.Name} ({zone.Type}) hash={zone.Hash}");
        }

        private static void ForceExitZone(ChatCommandContext ctx, string expectedType)
        {
            if (Plugin.ZoneConfig?.IsInitialized != true || Plugin.ZoneTransition?.IsInitialized != true)
            {
                ctx.Error("Zone services are not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
            if (player == Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            var currentHash = Plugin.ZoneTransition.GetPlayerZone(player);
            if (currentHash == 0)
            {
                ctx.Error("You are not tracked in a zone.");
                return;
            }

            if (!Plugin.ZoneConfig.TryGetZoneByHash(currentHash, out var zone))
            {
                ctx.Error($"Tracked zone hash {currentHash} no longer exists in config.");
                return;
            }

            if (!string.Equals(zone.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Error($"Current zone is {zone.Type}, not {expectedType}.");
                return;
            }

            Plugin.ZoneTransition.OnZoneExit(player, zone);
            ctx.Reply($"Forced exit: {zone.Name} ({zone.Type}) hash={zone.Hash}");
        }

        private static bool TryResolveZone(string zoneNameOrHash, string expectedType, out ZoneDefinition zone)
        {
            zone = null!;

            if (Plugin.ZoneConfig?.IsInitialized != true)
            {
                return false;
            }

            var zones = Plugin.ZoneConfig.GetZones()
                .Where(z => string.Equals(z.Type, expectedType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (int.TryParse(zoneNameOrHash, out var hash))
            {
                zone = zones.FirstOrDefault(z => z.Hash == hash)!;
                return zone != null;
            }

            zone = zones.FirstOrDefault(z => string.Equals(z.Name, zoneNameOrHash, StringComparison.OrdinalIgnoreCase))!;
            if (zone != null)
            {
                return true;
            }

            zone = zones.FirstOrDefault(z => z.Name.Contains(zoneNameOrHash, StringComparison.OrdinalIgnoreCase))!;
            return zone != null;
        }
    }
}
