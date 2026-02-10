using VampireCommandFramework;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.Logging;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Admin commands for broadcasting announcements.
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class AnnounceCommands : CommandBase
    {
        private const string CommandName = "announce";
        
        [Command("announce", shortHand: "ann", description: "Broadcast a global message", adminOnly: true)]
        public static void Announce(ChatCommandContext ctx, string message)
        {
            ExecuteSafely(ctx, CommandName, () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                if (string.IsNullOrWhiteSpace(message))
                {
                    SendError(ctx, "Usage", ".announce <message>");
                    return;
                }
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Announce from {playerInfo.Name}: {message}");
                
                // TODO: Integrate with AnnouncementService when available
                // AnnouncementService.Broadcast(message, AnnouncementService.NotifyType.Info);
                
                SendSuccess(ctx, "Broadcasted message", message);
            });
        }

        [Command("say", shortHand: "s", description: "Send a message to all players", adminOnly: true)]
        public static void Say(ChatCommandContext ctx, string message)
        {
            ExecuteSafely(ctx, "say", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                if (string.IsNullOrWhiteSpace(message))
                {
                    SendError(ctx, "Usage", ".say <message>");
                    return;
                }
                
                // Format as admin message
                var formattedMessage = $"[ADMIN] {message}";
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Admin message from {playerInfo.Name}: {formattedMessage}");
                
                // TODO: Integrate with AnnouncementService
                // AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Info);
                
                SendSuccess(ctx, "Message sent to all players");
            });
        }

        [Command("alert", shortHand: "a", description: "Send an urgent alert to all players", adminOnly: true)]
        public static void Alert(ChatCommandContext ctx, string message)
        {
            ExecuteSafely(ctx, "alert", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                if (string.IsNullOrWhiteSpace(message))
                {
                    SendError(ctx, "Usage", ".alert <message>");
                    return;
                }
                
                var formattedMessage = $"[ALERT] {message}";
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Alert from {playerInfo.Name}: {formattedMessage}");
                
                // TODO: Integrate with AnnouncementService
                // AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Warning);
                
                SendSuccess(ctx, "Alert sent to all players");
            });
        }

        [Command("trapalert", shortHand: "ta", description: "Broadcast trap trigger alert", adminOnly: false)]
        public static void TrapAlert(ChatCommandContext ctx, string playerName, string trapOwnerName)
        {
            ExecuteSafely(ctx, "trapalert", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                Log.Info($"Trap alert: {playerName} triggered by {trapOwnerName}");
                
                // TODO: Integrate with AnnouncementService
                // AnnouncementService.BroadcastTrapTrigger(playerName, trapOwnerName, true);
                
                SendSuccess(ctx, "Alert broadcasted");
            });
        }

        [Command("msg", shortHand: "m", description: "Send private message to player", adminOnly: true)]
        public static void PrivateMsg(ChatCommandContext ctx, string playerName, string message)
        {
            ExecuteSafely(ctx, "msg", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                if (string.IsNullOrWhiteSpace(message))
                {
                    SendError(ctx, "Usage", ".msg <player> <message>");
                    return;
                }
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"Private message from {playerInfo.Name} to {playerName}: {message}");
                
                // TODO: Integrate with player lookup service
                SendSuccess(ctx, $"Would send to {playerName}", message);
            });
        }

        [Command("broadcast", shortHand: "bc", description: "Broadcast system message", adminOnly: true)]
        public static void Broadcast(ChatCommandContext ctx, string message)
        {
            ExecuteSafely(ctx, "broadcast", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                if (string.IsNullOrWhiteSpace(message))
                {
                    SendError(ctx, "Usage", ".broadcast <message>");
                    return;
                }
                
                var formattedMessage = $"[SYSTEM] {message}";
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"System broadcast from {playerInfo.Name}: {formattedMessage}");
                
                // TODO: Integrate with AnnouncementService
                // AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Info);
                
                SendSuccess(ctx, "Message broadcasted to all players");
            });
        }
    }
}
