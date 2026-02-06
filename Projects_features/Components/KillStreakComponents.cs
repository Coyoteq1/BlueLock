using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Tracks player kill streak state.
    /// Persistent per-player, resets on death or timeout.
    /// </summary>
    public struct KillStreak : IComponentData
    {
        public int Current;
        public double LastKillTime;
        public double LastDeathTime;
        public int MaxStreak;
        public Entity LastVictim;
    }

    /// <summary>
    /// Configuration for kill streak thresholds and timeouts.
    /// Singleton component.
    /// </summary>
    public struct KillStreakConfig : IComponentData
    {
        public int ChestThreshold;
        public int WaypointThreshold;
        public double TimeoutSeconds;
        public bool AnnouncementsEnabled;
        public int AnnouncementThreshold;
    }

    /// <summary>
    /// Tag component for players with an active kill streak.
    /// </summary>
    public struct ActiveKillStreak : IComponentData
    {
        public int StreakLevel;
    }

    /// <summary>
    /// Event component for kill streak announcements.
    /// </summary>
    public struct KillStreakAnnouncement : IComponentData
    {
        public Entity Killer;
        public Entity Victim;
        public int StreakCount;
        public double Timestamp;
    }

    /// <summary>
    /// Buffer element for kill feed entries.
    /// </summary>
    public struct KillFeedEntry : IBufferElementData
    {
        public Entity Killer;
        public Entity Victim;
        public double Timestamp;
        public int KillerStreakBefore;
        public FixedString32Bytes KillMethod;
    }

    /// <summary>
    /// Notification component for colored chat messages.
    /// </summary>
    public struct ChatNotification : IComponentData
    {
        public FixedString128Bytes Message;
        public float4 Color;
        public bool IsImportant;
    }
}
