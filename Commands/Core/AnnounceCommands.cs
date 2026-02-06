using VampireCommandFramework;
using VAuto.Core.Services;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Admin commands for broadcasting announcements.
    /// </summary>
    public static class AnnounceCommands
    {
        [Command("announce", shortHand: "ann", description: "Broadcast a global message", adminOnly: true)]
        public static void Announce(ChatCommandContext ctx, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Announce] Usage: .announce <message>");
                return;
            }
            
            // Broadcast the message
            AnnouncementService.Broadcast(message, AnnouncementService.NotifyType.Info);
            
            ctx.Reply($"[Announce] Broadcasted: {message}");
        }

        [Command("say", shortHand: "s", description: "Send a message to all players", adminOnly: true)]
        public static void Say(ChatCommandContext ctx, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Say] Usage: .say <message>");
                return;
            }
            
            // Format as admin message
            var formattedMessage = $"[ADMIN] {message}";
            AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Info);
            
            ctx.Reply($"[Say] Message sent to all players");
        }

        [Command("alert", shortHand: "a", description: "Send an urgent alert to all players", adminOnly: true)]
        public static void Alert(ChatCommandContext ctx, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Alert] Usage: .alert <message>");
                return;
            }
            
            var formattedMessage = $"[ALERT] {message}";
            AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Warning);
            
            ctx.Reply($"[Alert] Alert sent to all players");
        }

        [Command("trapalert", shortHand: "ta", description: "Broadcast trap trigger alert", adminOnly: false)]
        public static void TrapAlert(ChatCommandContext ctx, string playerName, string trapOwnerName)
        {
            AnnouncementService.BroadcastTrapTrigger(playerName, trapOwnerName, true);
            ctx.Reply($"[TrapAlert] Alert broadcasted");
        }

        [Command("msg", shortHand: "m", description: "Send private message to player", adminOnly: true)]
        public static void PrivateMsg(ChatCommandContext ctx, string playerName, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Msg] Usage: .msg <player> <message>");
                return;
            }
            
            // Would need to resolve player name to platform ID
            ctx.Reply($"[Msg] Would send to {playerName}: {message}");
        }

        [Command("broadcast", shortHand: "bc", description: "Broadcast system message", adminOnly: true)]
        public static void Broadcast(ChatCommandContext ctx, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Broadcast] Usage: .broadcast <message>");
                return;
            }
            
            var formattedMessage = $"[SYSTEM] {message}";
            AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Info);
            
            ctx.Reply("[Broadcast] Message broadcasted to all players");
        }
    }
}
