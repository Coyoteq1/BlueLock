using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace VAuto.Zone.Core
{
    /// <summary>
    /// Core static class for VAutoZone providing access to game systems.
    /// </summary>
    public static class ZoneCore
    {
        private static bool _isInitialized;
        private static PrefabCollectionSystem _prefabCollection;
        
        public static World Server { get; } = GetWorld("Server") ?? throw new Exception("There is no Server world!");
        public static EntityManager EntityManager { get; } = Server.EntityManager;
        public static ManualLogSource Log { get; } = Plugin.Logger;
        public static string ConfigPath { get; } = Paths.ConfigPath;
        
        /// <summary>
        /// Indicates whether ZoneCore has been initialized.
        /// </summary>
        public static bool IsInitialized 
        { 
            get => _isInitialized; 
            internal set => _isInitialized = value; 
        }
        
        /// <summary>
        /// Provides access to the PrefabCollectionSystem.
        /// </summary>
        public static PrefabCollectionSystem PrefabCollection 
        { 
            get => _prefabCollection; 
            internal set => _prefabCollection = value; 
        }

        private static World GetWorld(string name)
        {
            foreach (var world in World.s_AllWorlds)
            {
                if (world.Name == name) return world;
            }
            return null;
        }

        #region Logging Extensions

        public static void LogInfo(string message) => Log.LogInfo($"[VAutoZone] {message}");
        public static void LogWarning(string message) => Log.LogWarning($"[VAutoZone] {message}");
        public static void LogError(string message) => Log.LogError($"[VAutoZone] {message}");
        public static void LogDebug(string message) => Log.LogDebug($"[VAutoZone] {message}");

        public static void LogException(string message, Exception ex)
        {
            Log.LogError($"[VAutoZone] {message}");
            Log.LogError($"[VAutoZone] Exception: {ex.Message}");
            if (ex.InnerException != null)
            {
                Log.LogError($"[VAutoZone] Inner: {ex.InnerException.Message}");
            }
        }

        #endregion

        #region Entity Utilities

        public static float3 GetPosition(Entity entity)
        {
            if (EntityManager == default || entity == Entity.Null) return float3.zero;
            try
            {
                if (EntityManager.HasComponent<LocalTransform>(entity))
                    return EntityManager.GetComponentData<LocalTransform>(entity).Position;
                if (EntityManager.HasComponent<Translation>(entity))
                    return EntityManager.GetComponentData<Translation>(entity).Value;
            }
            catch (Exception ex)
            {
                LogException("Failed to get entity position", ex);
            }
            return float3.zero;
        }

        public static void SetPosition(Entity entity, float3 position)
        {
            if (EntityManager == default || entity == Entity.Null) return;
            try
            {
                if (EntityManager.HasComponent<LocalTransform>(entity))
                {
                    var transform = EntityManager.GetComponentData<LocalTransform>(entity);
                    transform.Position = position;
                    EntityManager.SetComponentData(entity, transform);
                }
                else if (EntityManager.HasComponent<Translation>(entity))
                {
                    var translation = EntityManager.GetComponentData<Translation>(entity);
                    translation.Value = position;
                    EntityManager.SetComponentData(entity, translation);
                }
            }
            catch (Exception ex)
            {
                LogException("Failed to set entity position", ex);
            }
        }

        public static void DestroyEntity(Entity entity)
        {
            if (EntityManager == default || entity == Entity.Null) return;
            try
            {
                if (EntityManager.Exists(entity)) EntityManager.DestroyEntity(entity);
            }
            catch (Exception ex)
            {
                LogException("Failed to destroy entity", ex);
            }
        }

        #endregion

        #region Prefab Utilities

        /// <summary>
        /// Attempts to get an entity from a PrefabGUID.
        /// </summary>
        public static bool TryGetPrefabEntity(PrefabGUID guid, out Entity entity)
        {
            entity = Entity.Null;
            try
            {
                // This is a placeholder - actual implementation depends on available game APIs
                // For now, return the GUID as string representation
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a human-readable name for a PrefabGUID.
        /// </summary>
        public static string GetPrefabName(PrefabGUID guid)
        {
            return $"Prefab_{guid.GuidHash}";
        }

        #endregion

        #region Arena Zone Management

        private static readonly Dictionary<string, ArenaZoneDef> _arenaZones = new Dictionary<string, ArenaZoneDef>();

        public class ArenaZoneDef
        {
            public string ZoneId;
            public float3 Position;
            public float Radius;
        }

        public static void RegisterArena(string zoneId, float3 position, float radius)
        {
            if (!_arenaZones.ContainsKey(zoneId))
                _arenaZones[zoneId] = new ArenaZoneDef { ZoneId = zoneId, Position = position, Radius = radius };
            LogInfo($"Registered arena: {zoneId}");
        }

        public static void UnregisterArena(string zoneId)
        {
            if (_arenaZones.ContainsKey(zoneId)) _arenaZones.Remove(zoneId);
            LogInfo($"Unregistered arena: {zoneId}");
        }

        public static bool IsPositionInArena(float3 position)
        {
            foreach (var zone in _arenaZones.Values)
            {
                if (math.distancesq(zone.Position, position) <= zone.Radius * zone.Radius) return true;
            }
            return false;
        }

        public static string GetArenaIdAtPosition(float3 position)
        {
            foreach (var zone in _arenaZones.Values)
            {
                if (math.distancesq(zone.Position, position) <= zone.Radius * zone.Radius)
                    return zone.ZoneId;
            }
            return string.Empty;
        }

        public static List<string> GetAllArenaIds() => new List<string>(_arenaZones.Keys);

        #endregion
    }
}
