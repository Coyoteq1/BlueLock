using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Utilities;

namespace VAutomationCore.Core.Utilities
{
    /// <summary>
    /// Developer utilities for debugging and development.
    /// Provides tools for entity inspection, logging, and debugging helpers.
    /// </summary>
    public static class DevUtil
    {
        private static bool _initialized;
        private static readonly object _initLock = new object();
        private static readonly Dictionary<string, DebugInfo> _debugInfo = new Dictionary<string, DebugInfo>();
        private static readonly List<DebugWatcher> _watchers = new List<DebugWatcher>();

        #region Debug Information

        /// <summary>
        /// Debug information structure.
        /// </summary>
        public class DebugInfo
        {
            public string Id;
            public string Category;
            public string Description;
            public object Value;
            public DateTime CreatedTime;
            public DateTime LastUpdated;
            public int UpdateCount;
            public bool IsActive;
        }

        /// <summary>
        /// Debug watcher for monitoring entities or systems.
        /// </summary>
        public class DebugWatcher
        {
            public string Id;
            public string Name;
            public Func<bool> Condition;
            public Action<DebugContext> Handler;
            public TimeSpan? Interval;
            public DateTime LastCheck;
            public int TriggerCount;
            public bool IsActive;
            public string Category;
        }

        /// <summary>
        /// Context passed to debug watchers.
        /// </summary>
        public class DebugContext
        {
            public DateTime CheckTime;
            public int TriggerCount;
            public Dictionary<string, object> Data;
            public bool Canceled;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the developer utilities.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;

                try
                {
                    _initialized = true;
                    Plugin.Log.LogInfo("[DevUtil] Initialized successfully");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[DevUtil] Initialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if utilities are ready.
        /// </summary>
        public static bool IsReady()
        {
            return _initialized;
        }

        #endregion

        #region Debug Information Management

        /// <summary>
        /// Add debug information.
        /// </summary>
        /// <param name="id">Unique ID</param>
        /// <param name="category""Category</param>
        /// <param name="description""Description</param>
        /// <param name="value""Value to track</param>
        public static void AddDebugInfo(string id, string category, string description, object value)
        {
            if (string.IsNullOrEmpty(id)) return;

            lock (_debugInfo)
            {
                var info = new DebugInfo
                {
                    Id = id,
                    Category = category,
                    Description = description,
                    Value = value,
                    CreatedTime = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    UpdateCount = 1,
                    IsActive = true
                };

                _debugInfo[id] = info;
                Plugin.Log.LogDebug($"[DevUtil] Added debug info '{id}'");
            }
        }

        /// <summary>
        /// Update debug information.
        /// </summary>
        /// <param name="id""ID to update</param>
        /// <param name="value""New value</param>
        public static void UpdateDebugInfo(string id, object value)
        {
            lock (_debugInfo)
            {
                if (_debugInfo.TryGetValue(id, out var info))
                {
                    info.Value = value;
                    info.LastUpdated = DateTime.UtcNow;
                    info.UpdateCount++;
                    _debugInfo[id] = info;
                }
            }
        }

        /// <summary>
        /// Get debug information.
        /// </summary>
        /// <param name="id""ID to retrieve</param>
        /// <returns>Debug info or null</returns>
        public static DebugInfo? GetDebugInfo(string id)
        {
            lock (_debugInfo)
            {
                if (_debugInfo.TryGetValue(id, out var info))
                    return info;
            }
            return null;
        }

        /// <summary>
        /// Get all debug information.
        /// </summary>
        /// <returns>List of debug info</returns>
        public static List<DebugInfo> GetAllDebugInfo()
        {
            lock (_debugInfo)
            {
                return _debugInfo.Values.ToList();
            }
        }

        /// <summary>
        /// Remove debug information.
        /// </summary>
        /// <param name="id""ID to remove</param>
        public static void RemoveDebugInfo(string id)
        {
            lock (_debugInfo)
            {
                if (_debugInfo.Remove(id))
                {
                    Plugin.Log.LogDebug($"[DevUtil] Removed debug info '{id}'");
                }
            }
        }

        #endregion

        #region Debug Watching

        /// <summary>
        /// Add a debug watcher.
        /// </summary>
        /// <param name="id""Unique ID</param>
        /// <param name="name""Name</param>
        /// <param name="condition""Condition to check</param>
        /// <param name="handler""Handler when condition is met</param>
        /// <param name="interval""Check interval</param>
        /// <param name="category""Category</param>
        public static void AddWatcher(
            string id,
            string name,
            Func<bool> condition,
            Action<DebugContext> handler,
            TimeSpan? interval = null,
            string category = "General")
        {
            if (string.IsNullOrEmpty(id) || condition == null || handler == null) return;

            lock (_watchers)
            {
                var watcher = new DebugWatcher
                {
                    Id = id,
                    Name = name,
                    Condition = condition,
                    Handler = handler,
                    Interval = interval,
                    LastCheck = DateTime.UtcNow,
                    TriggerCount = 0,
                    IsActive = true,
                    Category = category
                };

                _watchers.Add(watcher);
                Plugin.Log.LogDebug($"[DevUtil] Added watcher '{id}'");
            }
        }

        /// <summary>
        /// Remove a debug watcher.
        /// </summary>
        /// <param name="id""ID to remove</param>
        public static void RemoveWatcher(string id)
        {
            lock (_watchers)
            {
                var watcher = _watchers.FirstOrDefault(w => w.Id == id);
                if (watcher != null)
                {
                    _watchers.Remove(watcher);
                    Plugin.Log.LogDebug($"[DevUtil] Removed watcher '{id}'");
                }
            }
        }

        /// <summary>
        /// Process all active watchers.
        /// </summary>
        public static void ProcessWatchers()
        {
            if (!IsReady()) return;

            var now = DateTime.UtcNow;

            lock (_watchers)
            {
                foreach (var watcher in _watchers.Where(w => w.IsActive).ToList())
                {
                    try
                    {
                        // Check interval if specified
                        if (watcher.Interval.HasValue)
                        {
                            if ((now - watcher.LastCheck) < watcher.Interval.Value)
                                continue;
                        }

                        // Check condition
                        if (watcher.Condition())
                        {
                            var context = new DebugContext
                            {
                                CheckTime = now,
                                TriggerCount = ++watcher.TriggerCount,
                                Data = new Dictionary<string, object>(),
                                Canceled = false
                            };

                            // Execute handler
                            watcher.Handler?.Invoke(context);

                            // Update last check time
                            watcher.LastCheck = now;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"[DevUtil] Error in watcher '{watcher.Id}': {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Get all active watchers.
        /// </summary>
        /// <returns>List of watchers</returns>
        public static List<DebugWatcher> GetWatchers()
        {
            lock (_watchers)
            {
                return _watchers.Where(w => w.IsActive).ToList();
            }
        }

        #endregion

        #region Entity Inspection

        /// <summary>
        /// Get detailed information about an entity.
        /// </summary>
        /// <param name="entity""Entity to inspect</param>
        /// <returns>Entity information</returns>
        public static EntityInfo GetEntityInfo(Entity entity)
        {
            if (!IsReady() || entity == Entity.Null) return null;

            try
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                if (em == null || !em.Exists(entity)) return null;

                var info = new EntityInfo
                {
                    Entity = entity,
                    Components = new List<ComponentInfo>(),
                    Archetype = em.GetArchetype(entity).ToString(),
                    IsEnabled = em.IsEnabled(entity),
                    IsEmpty = em.IsEmpty(entity)
                };

                // Get all components
                var types = em.GetComponentTypes(entity);
                foreach (var type in types)
                {
                    var component = em.GetComponentData<object>(entity, type);
                    info.Components.Add(new ComponentInfo
                    {
                        Type = type.ToString(),
                        Data = component,
                        IsShared = em.HasComponent<ISharedComponentData>(entity, type)
                    });
                }

                return info;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DevUtil] Error getting entity info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Log entity information to console.
        /// </summary>
        /// <param name="entity""Entity to log</param>
        public static void LogEntity(Entity entity)
        {
            var info = GetEntityInfo(entity);
            if (info == null)
            {
                Plugin.Log.LogWarning($"[DevUtil] Entity {entity} does not exist or cannot be inspected");
                return;
            }

            Plugin.Log.LogInfo($"[DevUtil] Entity {entity} Info:");
            Plugin.Log.LogInfo($"  Archetype: {info.Archetype}");
            Plugin.Log.LogInfo($"  Enabled: {info.IsEnabled}");
            Plugin.Log.LogInfo($"  Empty: {info.IsEmpty}");
            Plugin.Log.LogInfo($"  Components: {info.Components.Count}");

            foreach (var component in info.Components)
            {
                Plugin.Log.LogInfo($"    {component.Type}: {component.Data}");
            }
        }

        /// <summary>
        /// Get entities by component type.
        /// </summary>
        /// <param name="componentType""Component type</param>
        /// <returns>List of entities</returns>
        public static List<Entity> GetEntitiesByComponent(Type componentType)
        {
            if (!IsReady()) return new List<Entity>();

            try
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                var query = em.CreateEntityQuery(componentType);
                return query.ToEntityArray(Allocator.Temp).ToList();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DevUtil] Error getting entities by component: {ex.Message}");
                return new List<Entity>();
            }
        }

        #endregion

        #region System Inspection

        /// <summary>
        /// Get information about all systems.
        /// </summary>
        /// <returns>List of system info</returns>
        public static List<SystemInfo> GetSystemInfo()
        {
            if (!IsReady()) return new List<SystemInfo>();

            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return new List<SystemInfo>();

                var systems = world.Systems;
                return systems.Select(s => new SystemInfo
                {
                    Name = s.GetType().Name,
                    Type = s.GetType().FullName,
                    IsEnabled = s.Enabled,
                    UpdateType = s.UpdateType.ToString(),
                    UpdateFrequency = s.UpdateFrequency
                }).ToList();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DevUtil] Error getting system info: {ex.Message}");
                return new List<SystemInfo>();
            }
        }

        /// <summary>
        /// Log system information.
        /// </summary>
        public static void LogSystemInfo()
        {
            var systems = GetSystemInfo();
            if (systems == null || systems.Count == 0)
            {
                Plugin.Log.LogWarning("[DevUtil] No systems found");
                return;
            }

            Plugin.Log.LogInfo("[DevUtil] System Information:");
            foreach (var system in systems)
            {
                Plugin.Log.LogInfo($"  {system.Name} ({system.Type})");
                Plugin.Log.LogInfo($"    Enabled: {system.IsEnabled}");
                Plugin.Log.LogInfo($"    Update Type: {system.UpdateType}");
                Plugin.Log.LogInfo($"    Update Frequency: {system.UpdateFrequency}");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get memory usage statistics.
        /// </summary>
        /// <returns>Memory statistics</returns>
        public static MemoryStats GetMemoryStats()
        {
            try
            {
                var stats = new MemoryStats
                {
                    TotalMemory = GC.GetTotalMemory(false),
                    ManagedHeapSize = GC.GetTotalMemory(false),
                    ManagedHeapFragmentation = GC.GetTotalMemory(false) - GC.GetTotalMemory(true),
                    Generation0Collections = GC.CollectionCount(0),
                    Generation1Collections = GC.CollectionCount(1),
                    Generation2Collections = GC.CollectionCount(2)
                };

                return stats;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DevUtil] Error getting memory stats: {ex.Message}");
                return new MemoryStats();
            }
        }

        /// <summary>
        /// Log memory statistics.
        /// </summary>
        public static void LogMemoryStats()
        {
            var stats = GetMemoryStats();
            Plugin.Log.LogInfo("[DevUtil] Memory Statistics:");
            Plugin.Log.LogInfo($"  Total Memory: {stats.TotalMemory / 1024 / 1024:F2} MB");
            Plugin.Log.LogInfo($"  Managed Heap: {stats.ManagedHeapSize / 1024 / 1024:F2} MB");
            Plugin.Log.LogInfo($"  Heap Fragmentation: {stats.ManagedHeapFragmentation / 1024 / 1024:F2} MB");
            Plugin.Log.LogInfo($"  Gen 0 Collections: {stats.Generation0Collections}");
            Plugin.Log.LogInfo($"  Gen 1 Collections: {stats.Generation1Collections}");
            Plugin.Log.LogInfo($"  Gen 2 Collections: {stats.Generation2Collections}");
        }

        /// <summary>
        /// Get performance statistics.
        /// </summary>
        /// <returns>Performance statistics</returns>
        public static PerformanceStats GetPerformanceStats()
        {
            try
            {
                var stats = new PerformanceStats
                {
                    FrameRate = 1000f / Time.deltaTime,
                    DeltaTime = Time.deltaTime,
                    TimeScale = Time.timeScale,
                    UnscaledTime = Time.unscaledTime,
                    FrameCount = Time.frameCount
                };

                return stats;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DevUtil] Error getting performance stats: {ex.Message}");
                return new PerformanceStats();
            }
        }

        /// <summary>
        /// Log performance statistics.
        /// </summary>
        public static void LogPerformanceStats()
        {
            var stats = GetPerformanceStats();
            Plugin.Log.LogInfo("[DevUtil] Performance Statistics:");
            Plugin.Log.LogInfo($"  Frame Rate: {stats.FrameRate:F2} FPS");
            Plugin.Log.LogInfo($"  Delta Time: {stats.DeltaTime * 1000:F2} ms");
            Plugin.Log.LogInfo($"  Time Scale: {stats.TimeScale}");
            Plugin.Log.LogInfo($"  Unscaled Time: {stats.UnscaledTime:F2} s");
            Plugin.Log.LogInfo($"  Frame Count: {stats.FrameCount}");
        }

        #endregion

        #region Data Structures

        /// <summary>
        /// Entity information structure.
        /// </summary>
        public class EntityInfo
        {
            public Entity Entity;
            public string Archetype;
            public bool IsEnabled;
            public bool IsEmpty;
            public List<ComponentInfo> Components;
        }

        /// <summary>
        /// Component information structure.
        /// </summary>
        public class ComponentInfo
        {
            public string Type;
            public object Data;
            public bool IsShared;
        }

        /// <summary>
        /// System information structure.
        /// </summary>
        public class SystemInfo
        {
            public string Name;
            public string Type;
            public bool IsEnabled;
            public string UpdateType;
            public float UpdateFrequency;
        }

        /// <summary>
        /// Memory statistics structure.
        /// </summary>
        public class MemoryStats
        {
            public long TotalMemory;
            public long ManagedHeapSize;
            public long ManagedHeapFragmentation;
            public int Generation0Collections;
            public int Generation1Collections;
            public int Generation2Collections;
        }

        /// <summary>
        /// Performance statistics structure.
        /// </summary>
        public class PerformanceStats
        {
            public float FrameRate;
            public float DeltaTime;
            public float TimeScale;
            public float UnscaledTime;
            public int FrameCount;
        }

        #endregion
    }
}