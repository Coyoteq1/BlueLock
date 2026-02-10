ususing System;
using System.Reflection;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using UnityEngine;

namespace VAutomationCore.Core
{
    /// <summary>
    /// Utility class for converting between PrefabGUID and prefab names.
    /// Uses reflection to access the internal PrefabCollectionSystem.
    /// </summary>
    public static class PrefabGuidConverter
    {
        private static PrefabCollectionSystem _prefabCollectionSystem;
        
        /// <summary>
        /// Initializes the converter by caching the PrefabCollectionSystem.
        /// Call this during plugin initialization.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                var world = GetServerWorld();
                if (world != null)
                {
                    _prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PrefabGuidConverter] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to get a PrefabGUID from a prefab name.
        /// </summary>
        public static bool TryGetGuid(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            if (string.IsNullOrWhiteSpace(prefabName))
                return false;

            RefreshPrefabCollection();
            if (_prefabCollectionSystem == null)
                return false;

            var members = _prefabCollectionSystem.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var member in members)
            {
                object value = member switch
                {
                    FieldInfo f => f.GetValue(_prefabCollectionSystem),
                    PropertyInfo p => p.GetValue(_prefabCollectionSystem),
                    _ => null
                };

                if (value == null) continue;
                if (TryGetGuidFromDictionary(value, prefabName, out guid))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get a prefab name from a PrefabGUID.
        /// </summary>
        public static bool TryGetName(PrefabGUID guid, out string prefabName)
        {
            prefabName = string.Empty;
            if (guid.GuidHash == 0L)
                return false;

            RefreshPrefabCollection();
            if (_prefabCollectionSystem == null)
                return false;

            var members = _prefabCollectionSystem.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var member in members)
            {
                object value = member switch
                {
                    FieldInfo f => f.GetValue(_prefabCollectionSystem),
                    PropertyInfo p => p.GetValue(_prefabCollectionSystem),
                    _ => null
                };

                if (value == null) continue;
                if (TryGetNameFromDictionary(value, guid, out prefabName))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Refreshes the cached PrefabCollectionSystem reference.
        /// </summary>
        private static void RefreshPrefabCollection()
        {
            try
            {
                var world = GetServerWorld();
                if (world != null)
                {
                    _prefabCollectionSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                }
            }
            catch
            {
                // Ignore refresh errors
            }
        }

        /// <summary>
        /// Gets the server World.
        /// </summary>
        private static World GetServerWorld()
        {
            foreach (var world in World.s_AllWorlds)
            {
                if (world.Name == "Server")
                    return world;
            }
            return null;
        }

        /// <summary>
        /// Tries to get a PrefabGUID from a dictionary using the prefab name.
        /// </summary>
        private static bool TryGetGuidFromDictionary(object value, string prefabName, out PrefabGUID guid)
        {
            guid = default;
            if (value is not System.Collections.IDictionary dict)
                return false;

            if (dict.Contains(prefabName))
            {
                var dictValue = dict[prefabName];
                if (dictValue is PrefabGUID pg)
                {
                    guid = pg;
                    return true;
                }

                if (dictValue is int intGuid)
                {
                    guid = new PrefabGUID((int)intGuid);
                    return true;
                }
            }

            foreach (var key in dict.Keys)
            {
                if (key is not string keyStr) continue;
                if (!keyStr.Equals(prefabName, StringComparison.OrdinalIgnoreCase)) continue;

                var dictValue = dict[key];
                if (dictValue is PrefabGUID pg)
                {
                    guid = pg;
                    return true;
                }

                if (dictValue is int intGuid)
                {
                    guid = new PrefabGUID((int)intGuid);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to get a prefab name from a dictionary using the PrefabGUID.
        /// </summary>
        private static bool TryGetNameFromDictionary(object value, PrefabGUID guid, out string prefabName)
        {
            prefabName = string.Empty;
            if (value is not System.Collections.IDictionary dict)
                return false;

            foreach (var key in dict.Keys)
            {
                if (key is not string keyStr) continue;

                var dictValue = dict[key];
                if (dictValue is PrefabGUID pg && pg.GuidHash == guid.GuidHash)
                {
                    prefabName = keyStr;
                    return true;
                }

                if (dictValue is int intGuid && (long)intGuid == guid.GuidHash)
                {
                    prefabName = keyStr;
                    return true;
                }
            }

            return false;
        }
    }
}
