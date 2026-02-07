using VAuto.Announcement;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Service for broadcasting announcements to players.
    /// Simple, regular system for admin messages and trap notifications.
    /// </summary>
    public static class AnnouncementService
    {
        private static bool _initialized = false;
        
        /// <summary>
        /// Notification types for announcements.
        /// </summary>
        public enum NotifyType
        {
            Info,
            Warning,
            Error,
            TrapTriggered,
            KillStreak,
            Achievement
        }
        
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Plugin.Log.LogInfo("[AnnouncementService] Initialized");
        }
        
        /// <summary>
        /// Broadcasts a message to all players (global announcement).
        /// </summary>
        public static void Broadcast(string message, NotifyType type = NotifyType.Info)
        {
            Plugin.Log.LogInfo($"[Announcement][BROADCAST][{type}] {message}");
            
            // TODO: Use ProjectM chat system for actual broadcasting
            // Example: ProjectM.UserNameService.BroadcastChatMessage(message)
        }
        
        /// <summary>
        /// Sends a message to a specific player.
        /// </summary>
        public static void SendTo(ulong platformId, string message, NotifyType type = NotifyType.Info)
        {
            Plugin.Log.LogInfo($"[Announcement][To:{platformId}][{type}] {message}");
            
            // TODO: Use whisper/PM system
            // Example: ProjectM.UserNameService.SendWhisper(platformId, message)
        }
        
        /// <summary>
        /// Broadcasts a trap trigger event.
        /// </summary>
        public static void BroadcastTrapTrigger(string playerName, string trapOwnerName, bool isContainerTrap)
        {
            var trapType = isContainerTrap ? "Container Trap" : "Waypoint Trap";
            var message = $"[TRAP] {playerName} triggered {trapOwnerName}'s {trapType}!";
            Plugin.Log.LogInfo($"[Announcement] {message}");
            
            // Broadcast to nearby players only (not global)
            // TODO: Implement area-based broadcasting
        }
        
        /// <summary>
        /// Notifies trap owner about trigger.
        /// </summary>
        public static void NotifyTrapOwner(string ownerName, ulong ownerPlatformId, string intruderName, string location)
        {
            var notifyMessage = $"[TRAP] {intruderName} triggered your trap at {location}!";
            Plugin.Log.LogInfo($"[Announcement] Owner notification to {ownerName} ({ownerPlatformId}): {notifyMessage}");
            
            // Send private notification to trap owner
            SendTo(ownerPlatformId, notifyMessage, NotifyType.TrapTriggered);
        }
        
        /// <summary>
        /// Announces a kill streak milestone.
        /// </summary>
        public static void AnnounceKillStreak(string playerName, int streak)
        {
            var message = $"[KILLSTREAK] {playerName} has {streak} consecutive kills!";
            Plugin.Log.LogInfo($"[Announcement] {message}");
            
            // Broadcast milestone achievements
            if (streak >= 10)
            {
                Broadcast(message + " 🔥", NotifyType.Achievement);
            }
            else
            {
                Broadcast(message, NotifyType.KillStreak);
            }
        }
        
        /// <summary>
        /// Announces chest spawn for a player.
        /// </summary>
        public static void AnnounceChestSpawn(string playerName, int waypointIndex, string waypointName)
        {
            var message = $"[CHEST] Containers spawned for {playerName} at {waypointName}!";
            Plugin.Log.LogInfo($"[Announcement] {message}");
            
            // Notify the player privately
            // TODO: Get player platformId and send private message
        }
    }
}
