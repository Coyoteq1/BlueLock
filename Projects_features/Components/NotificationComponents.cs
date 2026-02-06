using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Request to send a notification to a player.
    /// </summary>
    public struct NotificationRequest : IComponentData
    {
        public ulong TargetPlatformId;
        public FixedString512Bytes Message;
        public float3 WorldPos;  // Optional: for location context
        public NotificationType Type;
        public float Duration;
    }

    /// <summary>
    /// Types of notifications.
    /// </summary>
    public enum NotificationType : byte
    {
        Info = 0,
        Warning = 1,
        Alert = 2,
        TrapTriggered = 3,
        ChestEarned = 4,
        StreakBonus = 5,
        SystemMessage = 6
    }

    /// <summary>
    /// Buffer of pending notifications for a player.
    /// </summary>
    public struct PlayerNotificationBuffer : IBufferElementData
    {
        public FixedString512Bytes Message;
        public NotificationType Type;
        public double CreatedAt;
        public float Duration;
    }

    /// <summary>
    /// Tag for notification dispatch system singleton.
    /// </summary>
    public struct NotificationDispatchTag : IComponentData { }

    /// <summary>
    /// Chat message request for sending to players.
    /// </summary>
    public struct ChatMessageRequest : IComponentData
    {
        public ulong TargetPlatformId;  // 0 for broadcast
        public FixedString512Bytes Message;
        public ChatChannel Channel;
    }

    /// <summary>
    /// Chat channels for message routing.
    /// </summary>
    public enum ChatChannel : byte
    {
        Global = 0,
        Local = 1,
        Private = 2,
        System = 3
    }
}
