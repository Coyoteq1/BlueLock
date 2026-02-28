using System.IO;
using BepInEx;
using Unity.Entities;
using VampireCommandFramework;
using VAuto.Zone.Services;

namespace VAuto.Zone.Commands
{
    public static class FlowCommands
    {
        [Command("enter", shortHand: "enf", description: "Enter flow/zone quickly (e.g. .enter flow1)", adminOnly: true)]
        public static void EnterFlow(ChatCommandContext ctx, string flowOrZone = "")
        {
            var character = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
            if (character == Entity.Null)
            {
                ctx.Reply("<color=#FF0000>Error: Could not resolve your character entity.</color>");
                return;
            }

            var resolved = QuickZoneCommands.ResolveFlowAlias(flowOrZone);
            var zoneId = string.IsNullOrWhiteSpace(resolved) ? ZoneConfigService.GetDefaultZoneId() : resolved;
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                ctx.Reply("<color=#FF0000>Error: No zone provided and no default zone set.</color>");
                return;
            }

            if (!Plugin.ForcePlayerEnterZone(character, zoneId))
            {
                ctx.Reply($"<color=#FF0000>Error: Failed to enter zone '{zoneId}'.</color>");
                return;
            }

            ctx.Reply($"<color=#00FF00>Entered zone '{zoneId}'.</color>");
        }

        [Command("exit", shortHand: "exf", description: "Exit current flow/zone quickly (e.g. .exit flow1)", adminOnly: true)]
        public static void ExitFlow(ChatCommandContext ctx, string flowOrZone = "")
        {
            var character = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
            if (character == Entity.Null)
            {
                ctx.Reply("<color=#FF0000>Error: Could not resolve your character entity.</color>");
                return;
            }

            var expectedZoneId = QuickZoneCommands.ResolveFlowAlias(flowOrZone);
            if (!string.IsNullOrWhiteSpace(expectedZoneId))
            {
                var state = VAutomationCore.Services.ZoneEventBridge.GetPlayerZoneState(character);
                var currentZoneId = state?.CurrentZoneId ?? string.Empty;
                if (!string.Equals(currentZoneId, expectedZoneId, System.StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Reply($"<color=#FF0000>Error: You are in zone '{(string.IsNullOrWhiteSpace(currentZoneId) ? "none" : currentZoneId)}', not '{expectedZoneId}'.</color>");
                    return;
                }
            }

            if (!Plugin.ForcePlayerExitZone(character))
            {
                ctx.Reply("<color=#FF0000>Error: You are not in an active zone or exit failed.</color>");
                return;
            }

            ctx.Reply("<color=#00FF00>Exited zone.</color>");
        }
        [Command("seed", shortHand: "seed", description: "Reseed flow/database configs from embedded defaults", adminOnly: true)]
        public static void SeedConfigs(ChatCommandContext ctx, string target = "flows", bool force = false)
        {
            target = target?.Trim().ToLowerInvariant() ?? "flows";
            var baseConfigPath = Path.Combine(BepInEx.Paths.ConfigPath, "Bluelock");

            VAuto.Zone.Services.ConfigSeedService.SeedSummary summary;
            switch (target)
            {
                case "db":
                case "database":
                case "databases":
                    summary = ConfigSeedService.EnsureDatabaseFiles(baseConfigPath, true, force);
                    break;
                case "flow":
                case "flows":
                    summary = ConfigSeedService.EnsureFlowFiles(baseConfigPath, true, force);
                    break;
                default:
                    ctx.Reply("<color=#FF0000>Error: target must be 'flows' or 'database'.</color>");
                    return;
            }

            ctx.Reply($"<color=#00FF00>Seeded {target} (Created={summary.Created} Upgraded={summary.Upgraded} Skipped={summary.Skipped} Failed={summary.Failed}). Force={force}</color>");
        }
    }
}
