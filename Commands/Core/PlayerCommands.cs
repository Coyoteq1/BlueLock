using VampireCommandFramework;
using VAuto;
using VAuto.Core;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Player management commands for VAutomationEvents
    /// </summary>
    public static class PlayerCommands
    {
        [Command("profile", shortHand: "profile", description: "Show player profile", adminOnly: false)]
        public static void Profile(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Player] User viewing profile");
            ctx.Reply("[Player] Profile command executed.");
        }

        [Command("stats", shortHand: "stats", description: "Show player stats", adminOnly: false)]
        public static void Stats(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Player] User viewing stats");
            ctx.Reply("[Player] Stats command executed.");
        }

        [Command("serverinfo", shortHand: "si", description: "Show server information", adminOnly: false)]
        public static void ServerInfo(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Player] User requesting server info");
            ctx.Reply($"[Player] VAutomationEvents v{MyPluginInfo.Version}");
            ctx.Reply("Server info command executed.");
        }

        [Command("help", shortHand: "help", description: "Show available commands", adminOnly: false)]
        public static void Help(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Player] User requesting help");
            ctx.Reply("[VAutomationEvents Commands]");
            ctx.Reply("  .arenaenter / .ae - Enter the arena");
            ctx.Reply("  .arenaexit / .ax - Exit the arena");
            ctx.Reply("  .pvp - Toggle PvP mode");
            ctx.Reply("  .glowlist / .gl - List glow zones");
            ctx.Reply("  .zonelist / .zl - List zones");
            ctx.Reply("  .zoneinfo / .zi - Current zone info");
            ctx.Reply("  .profile - View your profile");
            ctx.Reply("  .stats - View your stats");
            ctx.Reply("  .serverinfo / .si - Server information");
            ctx.Reply("  .help - Show this help");
        }
    }
}
