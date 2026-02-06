using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Request to spawn chests for a player who crossed the kill streak threshold.
    /// </summary>
    public struct ChestSpawnRequest : IComponentData
    {
        public ulong OwnerPlatformId;
        public int Count;               // 2
        public int RequiredStreak;      // 5
        public int RegionMask;          // bitmask or region id filtering
    }

    /// <summary>
    /// Marks a chest entity as owned by a specific player.
    /// </summary>
    public struct ChestOwner : IComponentData
    {
        public ulong OwnerPlatformId;
    }

    /// <summary>
    /// Interaction requirement for opening a chest.
    /// Only players with sufficient kill streak can interact.
    /// </summary>
    public struct InteractionRequirement : IComponentData
    {
        public int MinKillStreak;       // 5 for chest, 10 for waypoint trap
    }

    /// <summary>
    /// Marks where a chest was spawned (which waypoint).
    /// </summary>
    public struct SpawnedAtWaypoint : IComponentData
    {
        public int WaypointId;
        public int RegionId;
    }

    /// <summary>
    /// Tracks chest state.
    /// </summary>
    public struct ChestState : IComponentData
    {
        public bool IsClaimed;
        public bool IsArmed;            // Has trap armed
        public double SpawnTime;
        public double ExpirationTime;
    }

    /// <summary>
    /// Config singleton for chest spawning.
    /// </summary>
    public struct ChestSpawnConfig : IComponentData
    {
        public int ChestPrefabId;
        public int ChestsPerStreak;
        public int RequiredStreak;
        public float ChestLifetime;
        public float SpawnRadius;
    }
}
