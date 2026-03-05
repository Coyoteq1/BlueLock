using System;
using System.Linq;
using System.Text;
using VampireCommandFramework;

namespace Blueluck.Commands
{
    /// <summary>
    /// Minimal zone inspection/reload commands. Gameplay effects are driven by zone config + flows + kits.
    /// </summary>
    public static class ZoneCommands
    {
        [Command("zone status", shortHand: "zs", description: "Show zone status")]
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

            ctx.Reply(sb.ToString());
        }

        [Command("zone list", shortHand: "zl", description: "List all configured zones")]
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

        [Command("zone reload", shortHand: "zr", description: "Reload zone configuration")]
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

        [Command("flow reload", description: "Reload flows.json from disk")]
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

        [Command("zone debug", description: "Toggle zone detection debug mode")]
        public static void ZoneDebug(ChatCommandContext ctx)
        {
            var current = Plugin.ZoneDetectionDebugMode?.Value ?? false;
            Plugin.ZoneDetectionDebugMode.Value = !current;
            ctx.Reply($"Debug mode: {!current}");
        }
    }
}

