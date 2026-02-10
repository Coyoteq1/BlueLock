using VampireCommandFramework;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Player management commands for VAutomationEvents
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class PlayerCommands : CommandBase
    {
        private const string CommandName = "player";
        
        [Command("profile", shortHand: "profile", description: "Show player profile", adminOnly: false)]
        public static void Profile(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "profile", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"User viewing profile: {playerInfo.Name}");
                
                SendInfo(ctx, "Profile command executed.");
                // TODO: Integrate with player profile service
            });
        }

        [Command("stats", shortHand: "stats", description: "Show player stats", adminOnly: false)]
        public static void Stats(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "stats", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"User viewing stats: {playerInfo.Name}");
                
                SendInfo(ctx, "Stats command executed.");
                // TODO: Integrate with player stats service
            });
        }

        [Command("serverinfo", shortHand: "si", description: "Show server information", adminOnly: false)]
        public static void ServerInfo(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "serverinfo", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Server info requested by {playerInfo.Name}");
                
                SendInfo(ctx, $"VAutomationEvents");
                SendInfo(ctx, "Server info command executed.");
            });
        }

        [Command("help", shortHand: "help", description: "Show available commands", adminOnly: false)]
        public static void Help(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "help", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Debug($"Help requested by {playerInfo.Name}");
                
                SendInfo(ctx, "[VAutomationEvents Commands]");
                SendInfo(ctx, "  .arenaenter / .ea - Enter the arena");
                SendInfo(ctx, "  .arenaexit / .ax - Exit the arena");
                SendInfo(ctx, "  .pvp - Toggle PvP mode");
                SendInfo(ctx, "  .glowlist / .gl - List glow zones");
                SendInfo(ctx, "  .zonelist / .zl - List zones");
                SendInfo(ctx, "  .zoneinfo / .zi - Current zone info");
                SendInfo(ctx, "  .profile - View your profile");
                SendInfo(ctx, "  .stats - View your stats");
                SendInfo(ctx, "  .serverinfo / .si - Server information");
                SendInfo(ctx, "  .help - Show this help");
            });
        }
    }
}
