using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// Core lifecycle interface for player arena services.
    /// Implement this interface to participate in the arena player lifecycle system.
    /// </summary>
    public interface IArenaLifecycleService
    {
        /// <summary>
        /// Called when a player enters the arena
        /// </summary>
        /// <param name="user">User entity</param>
        /// <param name="character">Character entity</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if entry was successful</returns>
        bool OnPlayerEnter(Entity user, Entity character, string arenaId);

        /// <summary>
        /// Called when a player exits the arena
        /// </summary>
        /// <param name="user">User entity</param>
        /// <param name="character">Character entity</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if exit was successful</returns>
        bool OnPlayerExit(Entity user, Entity character, string arenaId);

        /// <summary>
        /// Called when arena lifecycle starts
        /// </summary>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if start was successful</returns>
        bool OnArenaStart(string arenaId);

        /// <summary>
        /// Called when arena lifecycle ends
        /// </summary>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if end was successful</returns>
        bool OnArenaEnd(string arenaId);
    }

    /// <summary>
    /// Lifecycle event data for player events
    /// </summary>
    public record PlayerLifecycleEvent
    {
        public Entity UserEntity { get; init; }
        public Entity CharacterEntity { get; init; }
        public ulong PlatformId { get; init; }
        public string CharacterName { get; init; }
        public string ArenaId { get; init; }
        public float3 Position { get; init; }
        public quaternion Rotation { get; init; }
        public PlayerLifecycleEventType EventType { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public Dictionary<string, object> EventData { get; init; } = new();
    }

    /// <summary>
    /// Lifecycle event types for players
    /// </summary>
    public enum PlayerLifecycleEventType
    {
        Enter,
        Exit,
        Teleport,
        Respawn,
        Death,
        ZoneChange
    }
}
