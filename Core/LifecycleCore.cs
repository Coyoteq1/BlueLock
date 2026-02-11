using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Zone.Core;

namespace VAuto.Zone.Core.Lifecycle
{
    /// <summary>
    /// Core lifecycle models used across all lifecycle services.
    /// </summary>
    public class LifecycleModels
    {
        /// <summary>
        /// Represents a lifecycle stage with its actions.
        /// </summary>
        public class LifecycleStage
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public List<LifecycleAction> Actions { get; set; } = new List<LifecycleAction>();
        }

        /// <summary>
        /// Represents a single lifecycle action.
        /// </summary>
        public class LifecycleAction
        {
            public string Type { get; set; }
            public string ConfigId { get; set; }
            public string Message { get; set; }
            public string BuffId { get; set; }
            public string EventPrefab { get; set; }
            public float3? Position { get; set; }
            public string StoreKey { get; set; }
            public string Prefix { get; set; }
            public bool ShouldSaveState { get; set; }
            public bool ShouldRestoreState { get; set; }
            public bool ShouldClearBuffs { get; set; }
            public bool ShouldResetCooldowns { get; set; }
            public bool ShouldTeleport { get; set; }
        }

        /// <summary>
        /// Context data passed to lifecycle actions.
        /// </summary>
        public class LifecycleContext
        {
            public Entity UserEntity { get; set; }
            public Entity CharacterEntity { get; set; }
            public string ZoneId { get; set; }
            public Dictionary<string, object> StoredData { get; set; } = new Dictionary<string, object>();
            public float3 Position { get; set; }
        }
    }

    /// <summary>
    /// Base interface for lifecycle action handlers.
    /// </summary>
    public interface ILifecycleActionHandler
    {
        bool Execute(LifecycleModels.LifecycleAction action, LifecycleModels.LifecycleContext context);
    }

    /// <summary>
    /// Result enum for spellbook granting operations.
    /// </summary>
    public enum GrantResult
    {
        Success,
        InventoryFull,
        AlreadyOwned,
        Failed
    }

    /// <summary>
    /// Result enum for VBlood unlock operations.
    /// </summary>
    public enum UnlockResult
    {
        Success,
        AlreadyUnlocked,
        Failed,
        ConditionsNotMet
    }

    /// <summary>
    /// Zone definition for ECS-based tracking.
    /// </summary>
    public struct ZoneDefinition
    {
        public Entity ZoneEntity;
        public float3 Center;
        public float Radius;
        public string ZoneId;
        public bool IsLifecycleZone;
    }

    /// <summary>
    /// Player zone state for event bridge.
    /// </summary>
    public class PlayerZoneState
    {
        public string CurrentZoneId { get; set; }
        public string PreviousZoneId { get; set; }
        public bool WasInZone { get; set; }
        public DateTime? EnteredAt { get; set; }
        public DateTime? ExitedAt { get; set; }
    }
}

/// <summary>
/// Core static class for VAutoZone Lifecycle providing access to game systems and lifecycle patterns.
/// Consolidates functionality from Vlifecycle patterns for unified lifecycle management.
/// </summary>
public static class LifecycleCore
{
    private static bool _isInitialized;
    private static ManualLogSource _log;
    
    public static ManualLogSource Log => _log ??= ZoneCore.Log;
    public static EntityManager EntityManager => ZoneCore.EntityManager;
    public static World Server => ZoneCore.Server;
    
    /// <summary>
    /// Indicates whether LifecycleCore has been initialized.
    /// </summary>
    public static bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initialize the lifecycle core system.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        Log.LogInfo("[LifecycleCore] Initialized");
    }

    /// <summary>
    /// Shutdown the lifecycle core system.
    /// </summary>
    public static void Shutdown()
    {
        _isInitialized = false;
        Log.LogInfo("[LifecycleCore] Shutdown");
    }

    #region Logging Extensions

    public static void LogInfo(string message) => Log.LogInfo($"[LifecycleCore] {message}");
    public static void LogWarning(string message) => Log.LogWarning($"[LifecycleCore] {message}");
    public static void LogError(string message) => Log.LogError($"[LifecycleCore] {message}");
    public static void LogDebug(string message) => Log.LogDebug($"[LifecycleCore] {message}");

    #endregion

    #region Entity Utilities

    public static float3 GetPosition(Entity entity)
    {
        return ZoneCore.GetPosition(entity);
    }

    public static void SetPosition(Entity entity, float3 position)
    {
        ZoneCore.SetPosition(entity, position);
    }

    public static Entity GetCharacterFromUser(Entity user)
    {
        // Placeholder - would need game-specific implementation
        return Entity.Null;
    }

    #endregion
}
