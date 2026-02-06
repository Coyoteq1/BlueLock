using VampireCommandFramework;
using VAuto.Core.Services;

namespace VAuto.Commands.Core
{
    public static class AnnounceCommands
    {
        private static bool _debug;

        [Command("announce help", shortHand: "ah", description: "Show announce commands", adminOnly: false)]
        public static void Help(ChatCommandContext ctx)
        {
            ctx.Reply("[Announce] Commands:");
            ctx.Reply("  .announce <message> - Broadcast message");
            ctx.Reply("  .announce say <message> - Admin message");
            ctx.Reply("  .announce alert <message> - Warning message");
            ctx.Reply("  .announce trap <player> <owner> - Trap alert");
            ctx.Reply("  .announce msg <player> <message> - Private message");
            ctx.Reply("  .announce test - Send test messages");
            ctx.Reply("  .announce debug <true/false> - Toggle debug logging");
        }

        [Command("announce", shortHand: "ann", description: "Broadcast a global message", adminOnly: true)]
        public static void Announce(ChatCommandContext ctx, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Announce] Usage: .announce <message>");
                return;
            }

            AnnouncementService.Broadcast(message, AnnouncementService.NotifyType.Info);
            ctx.Reply("[Announce] Message broadcasted");
        }

        [Command("announce say", shortHand: "as", description: "Send an admin message", adminOnly: true)]
        public static void Say(ChatCommandContext ctx, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Announce] Usage: .announce say <message>");
                return;
            }

            var formattedMessage = $"[ADMIN] {message}";
            AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Info);
            ctx.Reply("[Announce] Admin message sent");
        }

        [Command("announce alert", shortHand: "aa", description: "Send an alert message", adminOnly: true)]
        public static void Alert(ChatCommandContext ctx, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Announce] Usage: .announce alert <message>");
                return;
            }

            var formattedMessage = $"[ALERT] {message}";
            AnnouncementService.Broadcast(formattedMessage, AnnouncementService.NotifyType.Warning);
            ctx.Reply("[Announce] Alert sent");
        }

        [Command("announce trap", shortHand: "at", description: "Broadcast trap trigger alert", adminOnly: false)]
        public static void TrapAlert(ChatCommandContext ctx, string playerName, string trapOwnerName)
        {
            AnnouncementService.BroadcastTrapTrigger(playerName, trapOwnerName, true);
            ctx.Reply("[Announce] Trap alert broadcasted");
        }

        [Command("announce msg", shortHand: "am", description: "Send private message to player", adminOnly: true)]
        public static void PrivateMsg(ChatCommandContext ctx, string playerName, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ctx.Reply("[Announce] Usage: .announce msg <player> <message>");
                return;
            }

            ctx.Reply($"[Announce] Would send to {playerName}: {message}");
        }

        [Command("announce test", shortHand: "atc", description: "Send test announcements", adminOnly: true)]
        public static void Test(ChatCommandContext ctx)
        {
            AnnouncementService.Broadcast("Test broadcast", AnnouncementService.NotifyType.Info);
            AnnouncementService.Broadcast("Test alert", AnnouncementService.NotifyType.Warning);
            AnnouncementService.BroadcastTrapTrigger("Player", "Owner", true);
            ctx.Reply("[Announce] Test messages sent");
        }

        [Command("announce debug", shortHand: "ad", description: "Toggle debug logging", adminOnly: true)]
        public static void Debug(ChatCommandContext ctx, bool enabled = true)
        {
            _debug = enabled;
            ctx.Reply($"[Announce] Debug {(enabled ? "enabled" : "disabled")}");
        }
    }
}
