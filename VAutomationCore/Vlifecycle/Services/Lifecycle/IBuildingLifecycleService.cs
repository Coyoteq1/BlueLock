using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// Core lifecycle interface for building/structure arena services.
    /// Implement this interface to participate in the arena building lifecycle system.
    /// Use cases: Castle Hearts, crafting stations, traps, defensive structures.
    /// </summary>
    public interface IBuildingLifecycleService
    {
        /// <summary>
        /// Called when building lifecycle starts (structure placement begins)
        /// </summary>
        /// <param name="user">User entity (builder)</param>
        /// <param name="structureEntity">Structure entity being placed</param>
        /// <param name="structureName">Structure name (e.g., "CastleHeart", "Workbench")</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if build start was successful</returns>
        bool OnBuildStart(Entity user, Entity structureEntity, string structureName, string arenaId);

        /// <summary>
        /// Called when building lifecycle completes (structure fully placed)
        /// </summary>
        /// <param name="user">User entity (builder)</param>
        /// <param name="structureEntity">Structure entity</param>
        /// <param name="structureName">Structure name</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if build completion was successful</returns>
        bool OnBuildComplete(Entity user, Entity structureEntity, string structureName, string arenaId);

        /// <summary>
        /// Called when building lifecycle is destroyed (structure removed/destroyed)
        /// </summary>
        /// <param name="user">User entity (owner/destroyer)</param>
        /// <param name="structureEntity">Structure entity (now destroyed)</param>
        /// <param name="structureName">Structure name</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if destruction was successful</returns>
        bool OnBuildDestroy(Entity user, Entity structureEntity, string structureName, string arenaId);

        /// <summary>
        /// Called when building is activated (comes online)
        /// </summary>
        /// <param name="user">User entity</param>
        /// <param name="structureEntity">Structure entity</param>
        /// <param name="structureName">Structure name</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if activation was successful</returns>
        bool OnBuildActivate(Entity user, Entity structureEntity, string structureName, string arenaId);

        /// <summary>
        /// Called when building is deactivated (goes offline)
        /// </summary>
        /// <param name="user">User entity</param>
        /// <param name="structureEntity">Structure entity</param>
        /// <param name="structureName">Structure name</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if deactivation was successful</returns>
        bool OnBuildDeactivate(Entity user, Entity structureEntity, string structureName, string arenaId);

        /// <summary>
        /// Called when building ownership is transferred
        /// </summary>
        /// <param name="fromUser">Original owner entity</param>
        /// <param name="toUser">New owner entity</param>
        /// <param name="structureEntity">Structure entity</param>
        /// <param name="structureName">Structure name</param>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if transfer was successful</returns>
        bool OnBuildTransfer(Entity fromUser, Entity toUser, Entity structureEntity, string structureName, string arenaId);
    }

    /// <summary>
    /// Lifecycle event data for building/structure events
    /// </summary>
    public record BuildingLifecycleEvent
    {
        public Entity UserEntity { get; init; }
        public Entity StructureEntity { get; init; }
        public string StructureName { get; init; }
        public string StructurePrefabId { get; init; }
        public string ArenaId { get; init; }
        public float3 Position { get; init; }
        public quaternion Rotation { get; init; }
        public BuildingLifecycleEventType EventType { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public Dictionary<string, object> EventData { get; init; } = new();
    }

    /// <summary>
    /// Common structure types in V Rising
    /// </summary>
    public enum StructureType
    {
        CastleHeart,
        CraftingStation,
        Storage,
        Trap,
        Defensive,
        Furniture,
        Lighting,
        Unknown
    }

    /// <summary>
    /// Lifecycle event types for buildings/structures
    /// </summary>
    public enum BuildingLifecycleEventType
    {
        Start,          // Placement begins
        Complete,       // Fully placed and functional
        Destroy,        // Removed/destroyed
        Activate,       // Comes online
        Deactivate,     // Goes offline
        Transfer        // Ownership change
    }
}
