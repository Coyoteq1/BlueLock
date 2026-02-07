using System;
using System.Collections.Generic;
using VampireCommandFramework;
using VAuto.Announcement;
using VAuto.Core.Services;

namespace VAuto.Announcement.Commands
{
    /// <summary>
    /// ZUI Menu commands for VAutoannounce plugin.
    /// Provides in-game menu interface for announcement management.
    /// </summary>
    public static class AnnouncementMenuCommands
    {
        // Menu categories
        public const string MENU_CATEGORY = "Announcements";
        public const string MENU_NAME = "announce";

        /// <summary>
        /// Main menu command for announcements configuration.
        /// Usage: /announce - Opens the main announcements menu
        /// </summary>
        [Command("announce", description: "Open VAutoannounce configuration menu")]
        public static void AnnounceMenu(ChatCommandContext ctx)
        {
            try
            {
                // TODO: Integrate with ZUI menu system
                // ZUIMenu.OpenMainMenu(ctx.Player, BuildMainMenu());
                
                ctx.Reply("[VAuto] Announcement menu opened (ZUI integration pending)");
                Plugin.Log.LogInfo("[AnnounceMenu] Menu accessed by player");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AnnounceMenu] Error: {ex.Message}");
                ctx.Reply("[VAuto] Error opening menu");
            }
        }

        /// <summary>
        /// Toggle announcements on/off.
        /// Usage: /announce toggle [on|off]
        /// </summary>
        [Command("announce toggle", description: "Toggle announcement system on/off")]
        public static void AnnounceToggle(ChatCommandContext ctx, bool? enabled = null)
        {
            var newState = enabled ?? !Plugin.AnnouncementsActive;
            if (Plugin.AnnouncementsEnabled != null)
            {
                Plugin.AnnouncementsEnabled.Value = newState;
            }
            
            ctx.Reply($"[VAuto] Announcements {(newState ? "enabled" : "disabled")}");
            Plugin.Log.LogInfo($"[AnnounceToggle] Announcements set to {newState}");
        }

        /// <summary>
        /// Toggle kill announcements.
        /// Usage: /announce kills [on|off]
        /// </summary>
        [Command("announce kills", description: "Toggle kill announcements")]
        public static void AnnounceKills(ChatCommandContext ctx, bool? enabled = null)
        {
            var newState = enabled ?? !Plugin.KillsActive;
            if (Plugin.KillAnnouncements != null)
            {
                Plugin.KillAnnouncements.Value = newState;
            }
            
            ctx.Reply($"[VAuto] Kill announcements {(newState ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Toggle achievement announcements.
        /// Usage: /announce achievements [on|off]
        /// </summary>
        [Command("announce achievements", description: "Toggle achievement announcements")]
        public static void AnnounceAchievements(ChatCommandContext ctx, bool? enabled = null)
        {
            var newState = enabled ?? !Plugin.AchievementsActive;
            if (Plugin.AchievementAnnouncements != null)
            {
                Plugin.AchievementAnnouncements.Value = newState;
            }
            
            ctx.Reply($"[VAuto] Achievement announcements {(newState ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Toggle PvP announcements.
        /// Usage: /announce pvp [on|off]
        /// </summary>
        [Command("announce pvp", description: "Toggle PvP announcements")]
        public static void AnnouncePvP(ChatCommandContext ctx, bool? enabled = null)
        {
            var newState = enabled ?? !Plugin.PvPActive;
            if (Plugin.PvPAnnouncements != null)
            {
                Plugin.PvPAnnouncements.Value = newState;
            }
            
            ctx.Reply($"[VAuto] PvP announcements {(newState ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Toggle event announcements.
        /// Usage: /announce events [on|off]
        /// </summary>
        [Command("announce events", description: "Toggle event announcements")]
        public static void AnnounceEvents(ChatCommandContext ctx, bool? enabled = null)
        {
            var newState = enabled ?? !Plugin.EventsActive;
            if (Plugin.EventAnnouncements != null)
            {
                Plugin.EventAnnouncements.Value = newState;
            }
            
            ctx.Reply($"[VAuto] Event announcements {(newState ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Toggle kill feed.
        /// Usage: /announce feed [on|off]
        /// </summary>
        [Command("announce feed", description: "Toggle kill feed")]
        public static void AnnounceFeed(ChatCommandContext ctx, bool? enabled = null)
        {
            var newState = enabled ?? !Plugin.KillFeedActive;
            if (Plugin.KillFeedEnabled != null)
            {
                Plugin.KillFeedEnabled.Value = newState;
            }
            
            ctx.Reply($"[VAuto] Kill feed {(newState ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Show current announcement settings.
        /// Usage: /announce status
        /// </summary>
        [Command("announce status", description: "Show current announcement settings")]
        public static void AnnounceStatus(ChatCommandContext ctx)
        {
            var status = @"<align=left>
[VAuto] Announcement Settings:
━━━━━━━━━━━━━━━━━━━━━━
Announcements: " + (Plugin.AnnouncementsActive ? "✓" : "✗") + @"
Kill Feed: " + (Plugin.KillFeedActive ? "✓" : "✗") + @"
Kill Announcements: " + (Plugin.KillsActive ? "✓" : "✗") + @"
Achievements: " + (Plugin.AchievementsActive ? "✓" : "✗") + @"
PvP: " + (Plugin.PvPActive ? "✓" : "✗") + @"
Events: " + (Plugin.EventsActive ? "✓" : "✗") + @"
━━━━━━━━━━━━━━━━━━━━━━
</align>";
            
            ctx.Reply(status);
        }

        /// <summary>
        /// Broadcast a custom message.
        /// Usage: /announce broadcast <message>
        /// </summary>
        [Command("announce broadcast", description: "Broadcast a custom message to all players")]
        public static void AnnounceBroadcast(ChatCommandContext ctx, string message)
        {
            try
            {
                AnnouncementService.Broadcast(message, AnnouncementService.NotifyType.Info);
                ctx.Reply("[VAuto] Message broadcasted");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AnnounceBroadcast] Error: {ex.Message}");
                ctx.Reply("[VAuto] Error broadcasting message");
            }
        }
    }
}
