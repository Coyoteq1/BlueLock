using Unity.Entities;
using VampireCommandFramework;
using VAuto.Zone.Services;

namespace VAuto.Zone.Commands
{
    public static class QuickZoneCommands
    {
        [Command("enter", shortHand: "en", description: "Enter zone immediately. Empty zone uses default.", adminOnly: true)]
        public static void Enter(ChatCommandContext ctx, string zoneId = "")
        {
            var character = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
            if (character == Entity.Null)
            {
                ctx.Reply("<color=#FF0000>Error: Could not resolve your character entity.</color>");
                return;
            }

            var resolvedZoneId = string.IsNullOrWhiteSpace(zoneId)
                ? ZoneConfigService.GetDefaultZoneId()
                : zoneId;

            if (string.IsNullOrWhiteSpace(resolvedZoneId))
            {
                ctx.Reply("<color=#FF0000>Error: No zone provided and no default zone set. Use .z default [name].</color>");
                return;
            }

            if (!Plugin.ForcePlayerEnterZone(character, resolvedZoneId))
            {
                ctx.Reply($"<color=#FF0000>Error: Failed to enter zone '{resolvedZoneId}'.</color>");
                return;
            }

            ctx.Reply($"<color=#00FF00>Entered zone '{resolvedZoneId}'.</color>");
        }

        [Command("exit", shortHand: "ex", description: "Exit current zone immediately.", adminOnly: true)]
        public static void Exit(ChatCommandContext ctx)
        {
            var character = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
            if (character == Entity.Null)
            {
                ctx.Reply("<color=#FF0000>Error: Could not resolve your character entity.</color>");
                return;
            }

            if (!Plugin.ForcePlayerExitZone(character))
            {
                ctx.Reply("<color=#FF0000>Error: You are not in an active zone or exit failed.</color>");
                return;
            }

            ctx.Reply("<color=#00FF00>Exited zone.</color>");
        }
    }
}
