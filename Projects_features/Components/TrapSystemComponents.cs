using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    #region Kill Streak Tracking
    
    /// <summary>
    /// Tracks player kill streak counter and statistics.
    /// Attached to player entities.
    /// </summary>
    public struct KillStreakTracker : IComponentData
    {
        /// <summary>
        /// Current consecutive kill count.
        /// </summary>
        public int CurrentStreak;
        
        /// <summary>
        /// Total kills accumulated this session.
        /// </summary>
        public int TotalKills;
        
        /// <summary>
        /// Timestamp of last kill.
        /// </summary>
        public double LastKillTime;
        
        /// <summary>
        /// Number of chests claimed by this player.
        /// </summary>
        public int ChestsClaimed;
        
        /// <summary>
        /// Number of chests spawned for this player.
        /// </summary>
        public int ChestsSpawned;
        
        /// <summary>
        /// Streak reset cooldown in seconds.
        /// </summary>
        public float StreakResetTime;
    }
    
    #endregion
    
    #region Chest Spawn System
    
    /// <summary>
    /// Marks an entity as a kill-streak reward chest.
    /// Only interactable by players with required streak.
    /// </summary>
    public struct KillStreakChest : IComponentData
    {
        /// <summary>
        /// Minimum kill streak required to interact.
        /// </summary>
        public int RequiredStreak;
        
        /// <summary>
        /// Whether this chest has been claimed.
        /// </summary>
        public bool IsClaimed;
        
        /// <summary>
        /// Entity of the player who spawned this chest.
        /// </summary>
        public Entity OwnerEntity;
        
        /// <summary>
        /// Owner's player name for logging.
        /// </summary>
        public FixedString64Bytes OwnerName;
        
        /// <summary>
        /// Timestamp when chest was spawned.
        /// </summary>
        public double SpawnTime;
        
        /// <summary>
        /// Expiration time for the chest.
        /// </summary>
        public double ExpirationTime;
        
        /// <summary>
        /// Which waypoint this chest is associated with.
        /// </summary>
        public int WaypointIndex;
    }
    
    /// <summary>
    /// Spawn point for kill-streak chests.
    /// </summary>
    public struct ChestSpawnPoint : IComponentData
    {
        /// <summary>
        /// Position where chest should spawn.
        /// </summary>
        public float3 Position;
        
        /// <summary>
        /// Associated waypoint index.
        /// </summary>
        public int WaypointIndex;
        
        /// <summary>
        /// Whether this spawn point is currently active.
        /// </summary>
        public bool IsActive;
        
        /// <summary>
        /// Last time a chest was spawned here.
        /// </summary>
        public double LastSpawnTime;
        
        /// <summary>
        /// Minimum time between spawns.
        /// </summary>
        public float SpawnCooldown;
    }
    
    /// <summary>
    /// Configuration for chest spawn regions.
    /// </summary>
    [System.Serializable]
    public class ChestSpawnRegionConfig
    {
        public string regionId;
        public string regionName;
        public List<WaypointConfig> waypoints;
        public int maxChestsPerPlayer = 2;
        public float spawnRadius = 50f;
        public int requiredStreak = 5;
        public int chestPrefabId;
        public float chestLifetime = 300f; // 5 minutes
    }
    
    /// <summary>
    /// Waypoint configuration within a region.
    /// </summary>
    [System.Serializable]
    public class WaypointConfig
    {
        public int waypointIndex;
        public float3 position;
        public int castleLevel = 15;
        public List<float3> chestSpawnOffsets;
    }
    
    #endregion
    
    #region Container Trap System
    
    /// <summary>
    /// Marks a container as having an active trap.
    /// </summary>
    public struct ContainerTrap : IComponentData
    {
        /// <summary>
        /// Entity of the trap owner (who set it).
        /// </summary>
        public Entity OwnerEntity;
        
        /// <summary>
        /// Owner's player name.
        /// </summary>
        public FixedString64Bytes OwnerName;
        
        /// <summary>
        /// Whether the trap is currently armed.
        /// </summary>
        public bool IsArmed;
        
        /// <summary>
        /// Whether the trap has been triggered.
        /// </summary>
        public bool IsTriggered;
        
        /// <summary>
        /// Number of times trap can trigger (0 = infinite).
        /// </summary>
        public int MaxTriggers;
        
        /// <summary>
        /// Current trigger count.
        /// </summary>
        public int TriggerCount;
        
        /// <summary>
        /// Cooldown between triggers.
        /// </summary>
        public float CooldownSeconds;
        
        /// <summary>
        /// Last trigger timestamp.
        /// </summary>
        public double LastTriggerTime;
        
        /// <summary>
        /// Radius for detecting intruders.
        /// </summary>
        public float DetectionRadius;
    }
    
    /// <summary>
    /// Trap activation ability to spawn when triggered.
    /// </summary>
    public struct TrapAbility : IComponentData
    {
        /// <summary>
        /// Prefab ID of the ability to spawn.
        /// </summary>
        public int AbilityPrefabId;
        
        /// <summary>
        /// Spawn offset from trap center.
        /// </summary>
        public float3 SpawnOffset;
        
        /// <summary>
        /// Damage per tick.
        /// </summary>
        public float DamagePerTick;
        
        /// <summary>
        /// Tick rate in seconds.
        /// </summary>
        public float TickRate;
        
        /// <summary>
        /// Duration of the ability effect.
        /// </summary>
        public float Duration;
        
        /// <summary>
        /// Whether to show glow effect.
        /// </summary>
        public bool HasGlow;
        
        /// <summary>
        /// Glow color (RGB).
        /// </summary>
        public float3 GlowColor;
        
        /// <summary>
        /// Glow intensity.
        /// </summary>
        public float GlowIntensity;
        
        /// <summary>
        /// Particle system prefab ID (0 = none).
        /// </summary>
        public int ParticlePrefabId;
    }
    
    /// <summary>
    /// Log of trap trigger events for notifications.
    /// </summary>
    public struct TrapTriggerLog : IBufferElementData
    {
        /// <summary>
        /// Timestamp of trigger.
        /// </summary>
        public double Timestamp;
        
        /// <summary>
        /// Intruder entity name.
        /// </summary>
        public FixedString64Bytes IntruderName;
        
        /// <summary>
        /// Location of trigger.
        /// </summary>
        public float3 TriggerPosition;
        
        /// <summary>
        /// Whether trap was sprung.
        /// </summary>
        public bool TrapSprung;
    }
    
    #endregion
    
    #region Waypoint Expansion Trap System
    
    /// <summary>
    /// Waypoint trap requiring 10-kill streak.
    /// Separate from container traps with own spawning logic.
    /// </summary>
    public struct WaypointTrap : IComponentData
    {
        /// <summary>
        /// Required kill streak to trigger (10).
        /// </summary>
        public int RequiredStreak;
        
        /// <summary>
        /// Waypoint index this trap is associated with.
        /// </summary>
        public int WaypointIndex;
        
        /// <summary>
        /// Whether trap is active.
        /// </summary>
        public bool IsActive;
        
        /// <summary>
        /// Whether trap has been triggered.
        /// </summary>
        public bool IsTriggered;
        
        /// <summary>
        /// Entity of the player who placed this trap.
        /// </summary>
        public Entity PlacerEntity;
        
        /// <summary>
        /// Placer's player name.
        /// </summary>
        public FixedString64Bytes PlacerName;
        
        /// <summary>
        /// Damage multiplier for this trap.
        /// </summary>
        public float DamageMultiplier;
        
        /// <summary>
        /// Radius of effect.
        /// </summary>
        public float EffectRadius;
        
        /// <summary>
        /// Duration of effect.
        /// </summary>
        public float EffectDuration;
    }
    
    /// <summary>
    /// Waypoint trap ability configuration.
    /// </summary>
    public struct WaypointTrapAbility : IComponentData
    {
        /// <summary>
        /// Prefab ID for the damaging entity.
        /// </summary>
        public int DamageEntityPrefabId;
        
        /// <summary>
        /// Prefab ID for visual effects.
        /// </summary>
        public int VisualPrefabId;
        
        /// <summary>
        /// Damage amount per hit.
        /// </summary>
        public float DamageAmount;
        
        /// <summary>
        /// Attack speed (hits per second).
        /// </summary>
        public float AttackSpeed;
        
        /// <summary>
        /// Visual color (R, G, B).
        /// </summary>
        public float3 TrapColor;
        
        /// <summary>
        /// Whether to pulse the glow.
        /// </summary>
        public bool PulseEffect;
        
        /// <summary>
        /// Pulse speed.
        /// </summary>
        public float PulseSpeed;
    }
    
    #endregion
    
    #region Notifications
    
    /// <summary>
    /// Message buffer for player notifications.
    /// </summary>
    public struct PlayerMessageBuffer : IBufferElementData
    {
        /// <summary>
        /// Message text.
        /// </summary>
        public FixedString128Bytes Message;
        
        /// <summary>
        /// Display duration in seconds.
        /// </summary>
        public float Duration;
        
        /// <summary>
        /// Message priority (higher = more important).
        /// </summary>
        public int Priority;
        
        /// <summary>
        /// Message type for styling.
        /// </summary>
        public MessageType Type;
        
        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public double CreatedAt;
    }
    
    /// <summary>
    /// Types of player messages.
    /// </summary>
    public enum MessageType : byte
    {
        Info = 0,
        Warning = 1,
        Alert = 2,
        TrapTriggered = 3,
        ChestEarned = 4,
        StreakBonus = 5
    }
    
    #endregion
    
    #region Configuration Classes
    
    /// <summary>
    /// Root configuration for trap system.
    /// </summary>
    [System.Serializable]
    public class TrapSystemConfig
    {
        public string version = "1.0";
        public ChestSpawnConfig chestSpawn;
        public ContainerTrapConfig containerTrap;
        public WaypointTrapConfig waypointTrap;
    }
    
    /// <summary>
    /// Chest spawn system configuration.
    /// </summary>
    [System.Serializable]
    public class ChestSpawnConfig
    {
        public bool enabled = true;
        public int requiredStreak = 5;
        public int chestsPerKill = 2;
        public List<ChestSpawnRegionConfig> regions;
        public int chestPrefabId;
        public float chestLifetime = 300f;
        public float spawnRadius = 50f;
    }
    
    /// <summary>
    /// Container trap configuration.
    /// </summary>
    [System.Serializable]
    public class ContainerTrapConfig
    {
        public bool enabled = true;
        public int abilityPrefabId;
        public float damagePerTick = 50f;
        public float tickRate = 0.5f;
        public float duration = 10f;
        public float detectionRadius = 10f;
        public int maxTriggers = 3;
        public float cooldownSeconds = 60f;
        public GlowConfig glow;
        public int particlePrefabId;
    }
    
    /// <summary>
    /// Waypoint trap configuration.
    /// </summary>
    [System.Serializable]
    public class WaypointTrapConfig
    {
        public bool enabled = true;
        public int requiredStreak = 10;
        public List<WaypointTrapData> waypoints;
        public int damageEntityPrefabId;
        public int visualPrefabId;
        public float damageAmount = 100f;
        public float attackSpeed = 2f;
        public float effectRadius = 15f;
        public float effectDuration = 15f;
        public GlowConfig glow;
    }
    
    /// <summary>
    /// Glow effect configuration.
    /// </summary>
    [System.Serializable]
    public class GlowConfig
    {
        public bool enabled = true;
        public float r = 1f;
        public float g = 0f;
        public float b = 0f;
        public float intensity = 2f;
        public float pulseSpeed = 1f;
    }
    
    /// <summary>
    /// Individual waypoint trap data.
    /// </summary>
    [System.Serializable]
    public class WaypointTrapData
    {
        public int index;
        public float3 position;
        public int castleLevel = 15;
        public bool isActive = true;
        public float spawnWeight = 1f;
    }
    
    #endregion
}
