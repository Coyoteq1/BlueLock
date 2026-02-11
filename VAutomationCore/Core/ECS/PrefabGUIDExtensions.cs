using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Stunlock.Core;
using Unity.Entities;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.ECS
{
    /// <summary>
    /// Extension methods for PrefabGUID operations.
    /// Provides safe access to PrefabGUID data including name resolution.
    /// </summary>
    public static class PrefabGUIDExtensions
    {
        private static readonly CoreLogger _log = new CoreLogger("PrefabGUIDExtensions");
        
        // Cached prefab name lookup - loaded from PrefabIndex.json
        private static Dictionary<int, string>? _prefabNames;
        private static readonly object _initLock = new object();
        private static bool _initialized;
        
        /// <summary>
        /// Gets the prefab name from a PrefabGUID using cached lookup.
        /// Falls back to hex string if not found.
        /// </summary>
        public static string Name(this PrefabGUID prefabGuid)
        {
            try
            {
                var hash = prefabGuid.GuidHash;
                if (hash == 0) return "Null";
                
                // Lazy-load prefab names
                EnsureInitialized();
                
                if (_prefabNames != null && _prefabNames.TryGetValue(hash, out var name))
                {
                    return name;
                }
                
                // Fallback to hex representation
                return $"Prefab_0x{hash:X8}";
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
                return $"Prefab_0x{prefabGuid.GuidHash:X8}";
            }
        }
        
        /// <summary>
        /// Gets the prefab name from a GUID hash.
        /// </summary>
        private static string GetPrefabName(int guidHash)
        {
            EnsureInitialized();
            
            if (_prefabNames != null && _prefabNames.TryGetValue(guidHash, out var name))
            {
                return name;
            }
            
            return $"Prefab_0x{guidHash:X8}";
        }
        
        /// <summary>
        /// Ensures the prefab name cache is loaded.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            
            lock (_initLock)
            {
                if (_initialized) return;
                
                try
                {
                    // Try to load from common paths
                    var paths = new[]
                    {
                        "PrefabIndex.json",
                        Path.Combine(Path.GetDirectoryName(typeof(PrefabGUIDExtensions).Assembly.Location) ?? "", "PrefabIndex.json"),
                        Path.Combine(AppContext.BaseDirectory, "PrefabIndex.json")
                    };
                    
                    foreach (var path in paths)
                    {
                        if (File.Exists(path))
                        {
                            var json = File.ReadAllText(path);
                            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                            if (data != null)
                            {
                                _prefabNames = new Dictionary<int, string>();
                                foreach (var kvp in data)
                                {
                                    _prefabNames[kvp.Value] = kvp.Key;
                                }
                                _log.Info($"Loaded {_prefabNames.Count} prefab names from {path}");
                                break;
                            }
                        }
                    }
                    
                    if (_prefabNames == null)
                    {
                        _prefabNames = new Dictionary<int, string>();
                        _log.Info("PrefabIndex.json not found - using hex fallback");
                    }
                }
                catch (Exception ex)
                {
                    _log.Exception(ex);
                    _prefabNames = new Dictionary<int, string>();
                }
                
                _initialized = true;
            }
        }
        
        /// <summary>
        /// Checks if a PrefabGUID is valid (non-zero hash).
        /// </summary>
        public static bool IsValid(this PrefabGUID prefabGuid)
        {
            return prefabGuid.GuidHash != 0;
        }
        
        /// <summary>
        /// Gets the GUID hash as a string for display/logging.
        /// </summary>
        public static string ToHexString(this PrefabGUID prefabGuid)
        {
            return $"0x{prefabGuid.GuidHash:X8}";
        }
    }
}
