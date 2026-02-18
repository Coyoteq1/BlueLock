using System;
using System.Collections.Generic;
using System.Reflection;
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
        private static World _server;

        private static readonly object PrefabLookupLock = new();
        private static bool _prefabLookupCached;
        private static FieldInfo _prefabGuidToEntityMapField;
        private static FieldInfo _prefabGuidToEntityDictionaryField;
        private static FieldInfo _prefabLookupMapField;
        private static MethodInfo _prefabLookupTryGetValueWithoutLogging;
        private static MethodInfo _prefabLookupMapGetter;
        private static MethodInfo _spawnableNameToPrefabGuidDictionaryGetter;
        
        public static World Server
        {
            get
            {
                if (_server == null || !_server.IsCreated)
                {
                    _server = GetWorld("Server");
                }
                return _server;
            }
        }

        public static EntityManager EntityManager => Server != null ? Server.EntityManager : default;
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
                // Prefer cached PrefabCollection if available
                var system = PrefabCollection;
                var server = Server;
                if (system == null && server != null)
                {
                    system = server.GetExistingSystemManaged<PrefabCollectionSystem>();
                    PrefabCollection = system;
                }

                if (system == null)
                {
                    return false;
                }

                EnsurePrefabLookupCached(system);

                // Fast-path known field names (cached).
                if (TryGetPrefabFromCachedField(system, _prefabGuidToEntityMapField, guid, out entity) ||
                    TryGetPrefabFromCachedField(system, _prefabGuidToEntityDictionaryField, guid, out entity))
                {
                    return entity != Entity.Null;
                }

                // Optional lookup-map method (some versions expose a faster internal map).
                if (_prefabLookupMapField != null && _prefabLookupTryGetValueWithoutLogging != null)
                {
                    try
                    {
                        var lookupMap = _prefabLookupMapField.GetValue(system);
                        if (lookupMap != null)
                        {
                            var args = new object[] { guid, Entity.Null };
                            var ok = (bool)_prefabLookupTryGetValueWithoutLogging.Invoke(lookupMap, args);
                            if (ok && args[1] is Entity found && found != Entity.Null && EntityManager.Exists(found))
                            {
                                entity = found;
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore; we'll use the slower fallback below.
                    }
                }

                // Public getter path for current interop builds.
                if (TryGetPrefabFromLookupMapGetter(system, guid, out entity))
                {
                    return true;
                }

                // Slow-path: scan all fields for a dictionary that maps PrefabGUID -> Entity.
                var type = typeof(PrefabCollectionSystem);
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object value;
                    try
                    {
                        value = f.GetValue(system);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value is not System.Collections.IDictionary dict)
                    {
                        continue;
                    }

                    if (!dict.Contains(guid))
                    {
                        continue;
                    }

                    if (dict[guid] is Entity e && e != Entity.Null && EntityManager.Exists(e))
                    {
                        entity = e;
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogException("Failed to get prefab entity", ex);
                entity = Entity.Null;
                return false;
            }
        }

        private static void EnsurePrefabLookupCached(PrefabCollectionSystem system)
        {
            if (_prefabLookupCached)
            {
                return;
            }

            lock (PrefabLookupLock)
            {
                if (_prefabLookupCached)
                {
                    return;
                }

                var type = typeof(PrefabCollectionSystem);
                _prefabGuidToEntityMapField =
                    type.GetField("_PrefabGuidToEntityMap", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                _prefabGuidToEntityDictionaryField =
                    type.GetField("_PrefabGuidToEntityDictionary", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    type.GetField("_PrefabGuidToEntityDictionary", BindingFlags.Instance | BindingFlags.NonPublic);

                _prefabLookupMapField =
                    type.GetField("_PrefabLookupMap", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (_prefabLookupMapField != null)
                {
                    var lookupType = _prefabLookupMapField.FieldType;
                    _prefabLookupTryGetValueWithoutLogging =
                        lookupType.GetMethod("TryGetValueWithoutLogging", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                _prefabLookupMapGetter =
                    type.GetMethod("get_PrefabLookupMap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _spawnableNameToPrefabGuidDictionaryGetter =
                    type.GetMethod("get_SpawnableNameToPrefabGuidDictionary", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                _prefabLookupCached = true;
            }
        }

        private static bool TryGetPrefabFromCachedField(PrefabCollectionSystem system, FieldInfo field, PrefabGUID guid, out Entity entity)
        {
            entity = Entity.Null;
            if (field == null)
            {
                return false;
            }

            try
            {
                var value = field.GetValue(system);
                if (value is System.Collections.IDictionary dict && dict.Contains(guid))
                {
                    if (dict[guid] is Entity e && e != Entity.Null && EntityManager.Exists(e))
                    {
                        entity = e;
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryGetPrefabFromLookupMapGetter(PrefabCollectionSystem system, PrefabGUID guid, out Entity entity)
        {
            entity = Entity.Null;
            if (_prefabLookupMapGetter == null)
            {
                return false;
            }

            try
            {
                var lookupMap = _prefabLookupMapGetter.Invoke(system, null);
                if (lookupMap == null)
                {
                    return false;
                }

                var lookupType = lookupMap.GetType();
                var tryGet =
                    lookupType.GetMethod("TryGetValueWithoutLogging", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                    lookupType.GetMethod("TryGetValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tryGet == null)
                {
                    return false;
                }

                var args = new object[] { guid, Entity.Null };
                var okObj = tryGet.Invoke(lookupMap, args);
                var ok = okObj is bool b && b;
                if (!ok)
                {
                    return false;
                }

                if (args.Length >= 2 && args[1] is Entity found && found != Entity.Null && EntityManager.Exists(found))
                {
                    entity = found;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve a prefab by name to a GUID and entity.
        /// </summary>
        public static bool TryResolvePrefabEntity(string prefabName, out PrefabGUID guid, out Entity entity)
        {
            guid = PrefabGUID.Empty;
            entity = Entity.Null;

            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            try
            {
                var system = PrefabCollection;
                var server = Server;
                if (system == null && server != null)
                {
                    system = server.GetExistingSystemManaged<PrefabCollectionSystem>();
                    PrefabCollection = system;
                }

                if (system == null)
                {
                    return false;
                }

                EnsurePrefabLookupCached(system);

                // Spawnable-name dictionary is the best runtime source when it exists.
                var type = typeof(PrefabCollectionSystem);
                var nameMapField = type.GetField("_SpawnableNameToPrefabGuidDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
                if (TryResolveSpawnableNameMap(nameMapField?.GetValue(system), prefabName, out var resolvedGuid))
                {
                    guid = resolvedGuid;
                    return TryGetPrefabEntity(guid, out entity);
                }

                // Current interop exposes a public getter instead of private field in some versions.
                if (_spawnableNameToPrefabGuidDictionaryGetter != null &&
                    TryResolveSpawnableNameMap(_spawnableNameToPrefabGuidDictionaryGetter.Invoke(system, null), prefabName, out resolvedGuid))
                {
                    guid = resolvedGuid;
                    return TryGetPrefabEntity(guid, out entity);
                }

                // Fallback to PrefabResolver catalog for non-spawnable prefab names (e.g. TM_*).
                if (PrefabResolver.TryResolve(prefabName, out var catalogGuid))
                {
                    guid = catalogGuid;
                    return TryGetPrefabEntity(guid, out entity);
                }

                return false;
            }
            catch (Exception ex)
            {
                LogException("Failed to resolve prefab by name", ex);
                return false;
            }
        }

        private static bool TryResolveSpawnableNameMap(object nameMapObj, string prefabName, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            if (nameMapObj is not System.Collections.IDictionary dict || string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            // Fast exact lookup first.
            if (dict.Contains(prefabName) && TryConvertToPrefabGuid(dict[prefabName], out guid))
            {
                return true;
            }

            // Some map implementations are case-sensitive.
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (entry.Key is string key &&
                    string.Equals(key, prefabName, StringComparison.OrdinalIgnoreCase) &&
                    TryConvertToPrefabGuid(entry.Value, out guid))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryConvertToPrefabGuid(object value, out PrefabGUID guid)
        {
            guid = PrefabGUID.Empty;
            switch (value)
            {
                case PrefabGUID prefabGuid:
                    guid = prefabGuid;
                    return guid != PrefabGUID.Empty;
                case int hash:
                    guid = new PrefabGUID(hash);
                    return hash != 0;
                case uint hash:
                    guid = new PrefabGUID(unchecked((int)hash));
                    return hash != 0;
            }

            try
            {
                var type = value?.GetType();
                if (type == null)
                {
                    return false;
                }

                var hashProperty = type.GetProperty("GuidHash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (hashProperty?.GetValue(value) is int guidHash && guidHash != 0)
                {
                    guid = new PrefabGUID(guidHash);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
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
