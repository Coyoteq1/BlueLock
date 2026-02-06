using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Core.Components
{
    /// <summary>
    /// Zone configuration data models for JSON deserialization
    /// </summary>

    [Serializable]
    public class ZoneConfig
    {
        public string version;
        public bool enabled;
        public List<ZoneDefinition> zones;
        public ZoneDefaults @default;
    }

    [Serializable]
    public class ZoneDefinition
    {
        public int id;
        public string name;
        public string type;
        public float3Json center;
        public float radius;
        public bool pvpEnabled;
        public bool safeZone;
        public PrefabGlowConfig prefabGlow;
        public ArenaRules arenaRules;
        public ZonePermissions permissions;
        public ZoneLifecycle lifecycle;
    }

    [Serializable]
    public class PrefabGlowConfig
    {
        public bool enabled;
        public int prefabGuid;
        public GlowColor glowColor;
        public float glowIntensity = 2.0f;
        public float pulseSpeed = 1.0f;
        public float range = 100f;
    }

    [Serializable]
    public class GlowColor
    {
        public float r = 0.0f;
        public float g = 0.8f;
        public float b = 1.0f;
        public float a = 0.3f;
    }

    [Serializable]
    public class ZoneDefaults
    {
        public GlowColor glowColor;
        public float glowIntensity = 2.0f;
        public float pulseSpeed = 1.0f;
        public float range = 100f;
    }

    [Serializable]
    public class ArenaRules
    {
        public bool allowBuilding = true;
        public bool allowCrafting = true;
        public bool allowServants = true;
        public bool allowTeleporters = true;
        public bool allowRedistribution = true;
    }

    [Serializable]
    public class ZonePermissions
    {
        public bool allowEntry = true;
        public bool allowManage = true;
        public bool allowSpectate = true;
    }

    [Serializable]
    public class ZoneLifecycle
    {
        public bool autoRepairOnEntry = true;
        public int autoRepairEntryThreshold = 75;
        public bool autoRepairOnExit = true;
        public int autoRepairExitThreshold = 50;
    }

    /// <summary>
    /// JSON converter for float3
    /// </summary>
    [Serializable]
    public struct float3Json
    {
        public float x;
        public float y;
        public float z;

        public static implicit operator float3(float3Json v) => new float3(v.x, v.y, v.z);
        public static implicit operator float3Json(float3 v) => new float3Json { x = v.x, y = v.y, z = v.z };
    }

    /// <summary>
    /// Zone type enumeration
    /// </summary>
    public enum ZoneType
    {
        None = 0,
        MainArena = 1,
        PvPArena = 2,
        GlowZone = 3,
        SafeZone = 4,
        Custom = 5
    }

    // === Runtime State Classes ===

    /// <summary>
    /// Player zone state tracking
    /// </summary>
    [Serializable]
    public class ZonePlayerState
    {
        public Entity Player;
        public int ZoneId = -1;
        public ZoneType ZoneType = ZoneType.None;
        public double EntryTime;
    }

    /// <summary>
    /// Safe zone protection state
    /// </summary>
    [Serializable]
    public class SafeZoneProtectionState
    {
        public Entity Entity;
        public double ProtectionEndTime = double.MaxValue;
        public int DamagePrevented;
    }

    /// <summary>
    /// Zone boundary definition for runtime use
    /// </summary>
    [Serializable]
    public class ZoneBoundaryState
    {
        public int ZoneId;
        public ZoneType ZoneType;
        public float3 Center;
        public float Radius;
        public bool PvPEnabled;
        public bool SafeZone;
        public PrefabGlowConfig GlowConfig;
    }

    /// <summary>
    /// Combat state tracking for automation
    /// </summary>
    [Serializable]
    public class CombatState
    {
        public Entity Entity;
        public bool InCombat;
        public Entity Target;
        public double LastCombatTime;
        public int CombatStreak;
    }

    /// <summary>
    /// Spawn state tracking for automation
    /// </summary>
    [Serializable]
    public class SpawnState
    {
        public Entity Entity;
        public bool IsSpawned;
        public double SpawnTime;
        public int SpawnCount;
        public Entity Spawner;
        public int SpawnType;
        public float3 SpawnPosition;
    }

    // === ECS Components ===

    /// <summary>
    /// Tag component for players currently inside a zone
    /// </summary>
    public struct ZonePlayerTag : IComponentData
    {
        public int ZoneId;
        public ZoneType ZoneType;
    }

    /// <summary>
    /// Stores zone entry timestamp for duration tracking
    /// </summary>
    public struct ZoneEntryTimestamp : IComponentData
    {
        public int ZoneId;
        public double EntryTime;
    }

    /// <summary>
    /// Component for zone boundaries - used for zone detection
    /// </summary>
    public struct ZoneBoundary : IComponentData
    {
        public int ZoneId;
        public ZoneType ZoneType;
        public float3 Center;
        public float Radius;
        public bool PvPEnabled;
        public bool SafeZone;
    }

    /// <summary>
    /// Buffer element for tracking multiple zone memberships
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ZoneMembershipElement : IBufferElementData
    {
        public int ZoneId;
        public ZoneType ZoneType;
        public double EntryTime;
    }

    /// <summary>
    /// Safe zone protection component
    /// </summary>
    public struct SafeZoneProtection : IComponentData
    {
        public double ProtectionEndTime;
        public int DamagePrevented;
    }

    /// <summary>
    /// Temporary component for zone detection results.
    /// Created by ZoneDetectionSystem, consumed by ZoneTransitionSystem.
    /// </summary>
    public struct ZoneDetectionResult : IComponentData
    {
        public int DetectedZoneId;
        public ZoneType DetectedZoneType;
        public double DetectionTime;
    }
}
