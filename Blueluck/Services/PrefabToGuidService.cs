using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using VAuto.Services.Interfaces;
using VAutomationCore.Core;

namespace Blueluck.Services
{
    /// <summary>
    /// ECS-based service for converting between prefab names and GUIDs using game systems.
    /// </summary>
    public class PrefabToGuidService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.PrefabToGuid");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private readonly Dictionary<string, PrefabGUID> _nameToGuid = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<PrefabGUID, string> _guidToName = new();
        private EntityQuery _prefabQuery;
        private float _nextRefreshTime;
        private const float REFRESH_INTERVAL = 30f; // Refresh every 30 seconds

        public void Initialize()
        {
            SetupPrefabQuery();
            RefreshPrefabCache();
            
            IsInitialized = true;
            _log.LogInfo("[PrefabToGuid] Initialized with ECS-based prefab detection");
        }

        public void Cleanup()
        {
            _nameToGuid.Clear();
            _guidToName.Clear();
            IsInitialized = false;
            _log.LogInfo("[PrefabToGuid] Cleaned up");
        }

        /// <summary>
        /// Gets PrefabGUID by name using ECS systems.
        /// </summary>
        public bool TryGetGuid(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            
            // Check cache first
            if (_nameToGuid.TryGetValue(prefabName, out guid))
                return true;

            // Try to find in game systems
            return TryFindGuidInGameSystems(prefabName, out guid);
        }

        /// <summary>
        /// Gets prefab name by PrefabGUID using ECS systems.
        /// </summary>
        public bool TryGetName(PrefabGUID guid, out string name)
        {
            name = string.Empty;
            
            // Check cache first
            if (_guidToName.TryGetValue(guid, out name))
                return true;

            // Try to find in game systems
            return TryFindNameInGameSystems(guid, out name);
        }

        /// <summary>
        /// Gets all prefab names matching a search pattern.
        /// </summary>
        public List<string> SearchPrefabs(string pattern, int maxResults = 50)
        {
            var results = new List<string>();
            
            // Refresh cache if needed
            if (ShouldRefreshCache())
            {
                RefreshPrefabCache();
            }

            // Search in cache
            foreach (var kvp in _nameToGuid)
            {
                if (kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(kvp.Key);
                    if (results.Count >= maxResults)
                        break;
                }
            }

            return results;
        }

        /// <summary>
        /// Checks if a prefab exists in the game.
        /// </summary>
        public bool PrefabExists(string prefabName)
        {
            return TryGetGuid(prefabName, out _);
        }

        /// <summary>
        /// Checks if a PrefabGUID exists in the game.
        /// </summary>
        public bool PrefabExists(PrefabGUID guid)
        {
            return TryGetName(guid, out _);
        }

        /// <summary>
        /// Sets up ECS query for prefabs.
        /// </summary>
        private void SetupPrefabQuery()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                _log.LogError("[PrefabToGuid] World not available");
                return;
            }

            var em = world.EntityManager;
            _prefabQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PrefabGUID>());
        }

        /// <summary>
        /// Refreshes the prefab cache using ECS systems.
        /// </summary>
        private void RefreshPrefabCache()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return;

                var prefabSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabSystem == null) return;

                // Use reflection to access prefab collections
                var members = prefabSystem.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                foreach (var member in members)
                {
                    var value = member switch
                    {
                        FieldInfo f => f.GetValue(prefabSystem),
                        PropertyInfo p => p.GetValue(prefabSystem),
                        _ => null
                    };

                    if (value is System.Collections.IDictionary dict)
                    {
                        ProcessPrefabDictionary(dict);
                    }
                }

                _nextRefreshTime = UnityEngine.Time.time + REFRESH_INTERVAL;
                _log.LogInfo($"[PrefabToGuid] Refreshed cache with {_nameToGuid.Count} prefabs");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PrefabToGuid] Failed to refresh cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a prefab dictionary to extract mappings.
        /// </summary>
        private void ProcessPrefabDictionary(System.Collections.IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
            {
                if (dictionary[key] is PrefabGUID guid && key is string name)
                {
                    _nameToGuid[name] = guid;
                    _guidToName[guid] = name;
                }
            }
        }

        /// <summary>
        /// Tries to find GUID in game systems using reflection.
        /// </summary>
        private bool TryFindGuidInGameSystems(string prefabName, out PrefabGUID guid)
        {
            guid = default;
            
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return false;

                var prefabSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabSystem == null) return false;

                // Use VAutomationCore's converter as fallback
                return VAuto.Core.PrefabGuidConverter.TryGetGuid(prefabName, out guid);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PrefabToGuid] Error finding GUID for {prefabName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to find name in game systems using reflection.
        /// </summary>
        private bool TryFindNameInGameSystems(PrefabGUID guid, out string name)
        {
            name = string.Empty;
            
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return false;

                var prefabSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabSystem == null) return false;

                // Use VAutomationCore's converter as fallback
                return VAuto.Core.PrefabGuidConverter.TryGetName(guid, out name);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PrefabToGuid] Error finding name for GUID {guid.GuidHash}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if cache should be refreshed.
        /// </summary>
        private bool ShouldRefreshCache()
        {
            return UnityEngine.Time.time >= _nextRefreshTime;
        }

        /// <summary>
        /// Forces an immediate cache refresh.
        /// </summary>
        public void ForceRefresh()
        {
            _nameToGuid.Clear();
            _guidToName.Clear();
            RefreshPrefabCache();
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public (int NameCount, int GuidCount) GetCacheStats()
        {
            return (_nameToGuid.Count, _guidToName.Count);
        }
    }
}
