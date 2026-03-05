using System;
using System.Linq;
using VampireCommandFramework;

namespace Blueluck.Commands
{
    public static class KitCommands
    {
        [Command("kit list", description: "List available kits")]
        public static void ListKits(ChatCommandContext ctx)
        {
            if (Plugin.Kits?.IsInitialized != true)
            {
                ctx.Error("Kit service not initialized.");
                return;
            }

            var names = Plugin.Kits.ListKitNames()
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (names.Length == 0)
            {
                ctx.Reply("No kits configured.");
                return;
            }

            ctx.Reply("Kits:");
            foreach (var name in names)
            {
                ctx.Reply($"  - {name}");
            }
        }

        [Command("kit", description: "Apply a kit to yourself", adminOnly: true)]
        public static void ApplyKit(ChatCommandContext ctx, string kitName)
        {
            if (Plugin.Kits?.IsInitialized != true)
            {
                ctx.Error("Kit service not initialized.");
                return;
            }

            var player = ctx.Event?.SenderCharacterEntity ?? Unity.Entities.Entity.Null;
            if (player == Unity.Entities.Entity.Null)
            {
                ctx.Error("No player character entity.");
                return;
            }

            if (string.IsNullOrWhiteSpace(kitName))
            {
                ctx.Error("Usage: !kit <name>");
                return;
            }

            if (!Plugin.Kits.ApplyKit(player, kitName))
            {
                ctx.Error($"Failed to apply kit '{kitName}'.");
                return;
            }

            ctx.Reply($"Applied kit '{kitName}'.");
        }
    }
}
