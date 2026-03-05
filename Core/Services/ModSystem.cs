using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Services;

namespace VAutomationCore.Core.Services
{
    /// <summary>
    /// Mod enumeration and management system.
    /// Provides information about loaded mods and their capabilities.
    /// </summary>
    public static class ModSystem
    {
        private static bool _initialized;
        private static readonly object _initLock = new object();
        private static readonly Dictionary<string, ModInfo> _loadedMods = new Dictionary<string, ModInfo>();
        private static readonly Dictionary<string, List<ModInfo>> _categoryIndex = new Dictionary<string, List<ModInfo>>();
        private static readonly Dictionary<string, List<ModInfo>> _dependencyIndex = new Dictionary<string, List<ModInfo>>();

        #region Mod Information Structures

        /// <summary>
        /// Information about a loaded mod.
        /// </summary>
        public class ModInfo
        {
            public string Name;
            public string Version;
            public string Author;
            public string Description;
            public string MainClass;
            public string[] Dependencies;
            public string[] OptionalDependencies;
            public string[] Conflicts;
            public string Category;
            public string[] Tags;
            public bool IsEnabled;
            public bool IsActive;
            public DateTime LoadedTime;
            public Dictionary<string, object> Metadata;
            public List<ModCapability> Capabilities;
        }

        /// <summary>
        /// Mod capability information.
        /// </summary>
        public class ModCapability
        {
            public string Name;
            public string Type;
            public string Version;
            public bool IsAvailable;
            public Dictionary<string, object> Properties;
        }

        /// <summary>
        /// Mod statistics for monitoring.
        /// </summary>
        public class ModStatistics
        {
            public int TotalMods;
            public int EnabledMods;
            public int ActiveMods;
            public Dictionary<string, int> ModsByCategory;
            public Dictionary<string, int> ModsByDependency;
            public Dictionary<string, int> ModsByCapability;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the mod system.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;

                try
                {
                    // Scan for loaded mods
                    ScanLoadedMods();

                    _initialized = true;
                    Plugin.Log.LogInfo("[ModSystem] Initialized successfully");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[ModSystem] Initialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if mod system is ready.
        /// </summary>
        public static bool IsReady()
        {
            return _initialized;
        }

        #endregion

        #region Mod Scanning

        /// <summary>
        /// Scan for loaded mods.
        /// </summary>
        private static void ScanLoadedMods()
        {
            try
            {
                // Get all loaded assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Check if assembly is a mod
                        if (IsModAssembly(assembly))
                        {
                            var modInfo = ExtractModInfo(assembly);
                            if (modInfo != null)
                            {
                                RegisterMod(modInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"[ModSystem] Error scanning assembly {assembly.FullName}: {ex.Message}");
                    }
                }

                Plugin.Log.LogInfo($"[ModSystem] Scanned {assemblies.Length} assemblies, found {_loadedMods.Count} mods");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ModSystem] Error scanning mods: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if an assembly is a mod.
        /// </summary>
        private static bool IsModAssembly(System.Reflection.Assembly assembly)
        {
            // Check for common mod attributes or patterns
            var name = assembly.GetName().Name;
            return name != null && (name.Contains("Mod") || name.Contains("Plugin") || name.Contains("Addon"));
        }

        /// <summary>
        /// Extract mod information from an assembly.
        /// </summary>
        private static ModInfo? ExtractModInfo(System.Reflection.Assembly assembly)
        {
            try
            {
                var modInfo = new ModInfo
                {
                    Name = assembly.GetName().Name,
                    Version = assembly.GetName().Version.ToString(),
                    Author = "Unknown",
                    Description = "No description available",
                    MainClass = null,
                    Dependencies = Array.Empty<string>(),
                    OptionalDependencies = Array.Empty<string>(),
                    Conflicts = Array.Empty<string>(),
                    Category = "Uncategorized",
                    Tags = Array.Empty<string>(),
                    IsEnabled = true,
                    IsActive = true,
                    LoadedTime = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>(),
                    Capabilities = new List<ModCapability>()
                };

                // Try to get mod attributes
                var attributes = assembly.GetCustomAttributesData();
                foreach (var attr in attributes)
                {
                    switch (attr.AttributeType.Name)
                    {
                        case "AssemblyTitleAttribute":
                            modInfo.Name = attr.ConstructorArguments[0].Value.ToString();
                            break;
                        case "AssemblyDescriptionAttribute":
                            modInfo.Description = attr.ConstructorArguments[0].Value.ToString();
                            break;
                        case "AssemblyCompanyAttribute":
                            modInfo.Author = attr.ConstructorArguments[0].Value.ToString();
                            break;
                        case "AssemblyVersionAttribute":
                            modInfo.Version = attr.ConstructorArguments[0].Value.ToString();
                            break;
                    }
                }

                // Try to find main class
                var mainClass = FindMainClass(assembly);
                if (mainClass != null)
                {
                    modInfo.MainClass = mainClass.FullName;
                }

                // Try to find dependencies
                var dependencies = FindDependencies(assembly);
                if (dependencies != null)
                {
                    modInfo.Dependencies = dependencies;
                }

                // Try to find capabilities
                var capabilities = FindCapabilities(assembly);
                if (capabilities != null)
                {
                    modInfo.Capabilities = capabilities;
                }

                return modInfo;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ModSystem] Error extracting mod info from {assembly.FullName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Find the main class in a mod assembly.
        /// </summary>
        private static System.Type? FindMainClass(System.Reflection.Assembly assembly)
        {
            try
            {
                // Look for classes that might be main mod classes
                var types = assembly.GetTypes();
                return types.FirstOrDefault(t =>
                    t.Name.Contains("Mod") ||
                    t.Name.Contains("Plugin") ||
                    t.Name.Contains("Main") ||
                    t.Name.Contains("Core")
                );
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ModSystem] Error finding main class in {assembly.FullName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Find dependencies in a mod assembly.
        /// </summary>
        private static string[]? FindDependencies(System.Reflection.Assembly assembly)
        {
            try
            {
                // Look for dependency attributes or patterns
                var attributes = assembly.GetCustomAttributesData();
                var dependencies = new List<string>();

                foreach (var attr in attributes)
                {
                    if (attr.AttributeType.Name.Contains("Dependency"))
                    {
                        dependencies.Add(attr.ConstructorArguments[0].Value.ToString());
                    }
                }

                return dependencies.Count > 0 ? dependencies.ToArray() : null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ModSystem] Error finding dependencies in {assembly.FullName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Find capabilities in a mod assembly.
        /// </summary>
        private static List<ModCapability>? FindCapabilities(System.Reflection.Assembly assembly)
        {
            try
            {
                var capabilities = new List<ModCapability>();
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // Check for capability interfaces or attributes
                    if (type.Name.Contains("Service") || type.Name.Contains("Manager") || type.Name.Contains("Provider"))
                    {
                        capabilities.Add(new ModCapability
                        {
                            Name = type.Name,
                            Type = type.FullName,
                            Version = "1.0.0",
                            IsAvailable = true,
                            Properties = new Dictionary<string, object>()
                        });
                    }
                }

                return capabilities.Count > 0 ? capabilities : null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ModSystem] Error finding capabilities in {assembly.FullName}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Mod Management

        /// <summary>
        /// Register a mod.
        /// </summary>
        /// <param name="modInfo""Mod information</param>
        /// <returns>True if registered</returns>
        public static bool RegisterMod(ModInfo modInfo)
        {
            if (modInfo == null || string.IsNullOrEmpty(modInfo.Name)) return false;

            lock (_loadedMods)
            {
                if (_loadedMods.ContainsKey(modInfo.Name))
                {
                    Plugin.Log.LogWarning($"[ModSystem] Mod '{modInfo.Name}' already registered, updating");
                }

                _loadedMods[modInfo.Name] = modInfo;

                // Index by category
                if (!string.IsNullOrEmpty(modInfo.Category))
                {
                    if (!_categoryIndex.ContainsKey(modInfo.Category))
                        _categoryIndex[modInfo.Category] = new List<ModInfo>();
                    _categoryIndex[modInfo.Category].Add(modInfo);
                }

                // Index by dependencies
                if (modInfo.Dependencies != null)
                {
                    foreach (var dependency in modInfo.Dependencies)
                    {
                        if (!_dependencyIndex.ContainsKey(dependency))
                            _dependencyIndex[dependency] = new List<ModInfo>();
                        _dependencyIndex[dependency].Add(modInfo);
                    }
                }

                Plugin.Log.LogInfo($"[ModSystem] Registered mod '{modInfo.Name}'");
                return true;
            }
        }

        /// <summary>
        /// Unregister a mod.
        /// </summary>
        /// <param name="name""Mod name</param>
        /// <returns>True if unregistered</returns>
        public static bool UnregisterMod(string name)
        {
            lock (_loadedMods)
            {
                if (_loadedMods.Remove(name))
                {
                    // Remove from indexes
                    foreach (var kvp in _categoryIndex)
                    {
                        kvp.Value.RemoveAll(m => m.Name == name);
                    }

                    foreach (var kvp in _dependencyIndex)
                    {
                        kvp.Value.RemoveAll(m => m.Name == name);
                    }

                    Plugin.Log.LogInfo($"[ModSystem] Unregistered mod '{name}'");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get mod information.
        /// </summary>
        /// <param name="name""Mod name</param>
        /// <returns>Mod info or null</returns>
        public static ModInfo? GetMod(string name)
        {
            lock (_loadedMods)
            {
                if (_loadedMods.TryGetValue(name, out var modInfo))
                    return modInfo;
            }
            return null;
        }

        /// <summary>
        /// Get all loaded mods.
        /// </summary>
        /// <returns>List of mods</returns>
        public static List<ModInfo> GetAllMods()
        {
            lock (_loadedMods)
            {
                return _loadedMods.Values.ToList();
            }
        }

        /// <summary>
        /// Get mods by category.
        /// </summary>
        /// <param name="category""Category name</param>
        /// <returns>List of mods</returns>
        public static List<ModInfo> GetModsByCategory(string category)
        {
            lock (_categoryIndex)
            {
                if (_categoryIndex.TryGetValue(category, out var mods))
                    return mods.ToList();
            }
            return new List<ModInfo>();
        }

        /// <summary>
        /// Get mods by dependency.
        /// </summary>
        /// <param name="dependency""Dependency name</param>
        /// <returns>List of mods</returns>
        public static List<ModInfo> GetModsByDependency(string dependency)
        {
            lock (_dependencyIndex)
            {
                if (_dependencyIndex.TryGetValue(dependency, out var mods))
                    return mods.ToList();
            }
            return new List<ModInfo>();
        }

        #endregion

        #region Capability Management

        /// <summary>
        /// Check if a mod has a specific capability.
        /// </summary>
        /// <param name="modName""Mod name</param>
        /// <param name="capabilityName""Capability name</param>
        /// <returns>True if capability exists</returns>
        public static bool HasCapability(string modName, string capabilityName)
        {
            var modInfo = GetMod(modName);
            if (modInfo == null) return false;

            return modInfo.Capabilities.Any(c => c.Name == capabilityName);
        }

        /// <summary>
        /// Get mod capabilities.
        /// </summary>
        /// <param name="modName""Mod name</param>
        /// <returns>List of capabilities</returns>
        public static List<ModCapability> GetCapabilities(string modName)
        {
            var modInfo = GetMod(modName);
            return modInfo?.Capabilities ?? new List<ModCapability>();
        }

        /// <summary>
        /// Get mods with a specific capability.
        /// </summary>
        /// <param name="capabilityName""Capability name</param>
        /// <returns>List of mods</returns>
        public static List<ModInfo> GetModsWithCapability(string capabilityName)
        {
            return GetAllMods().Where(m => m.Capabilities.Any(c => c.Name == capabilityName)).ToList();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get mod statistics.
        /// </summary>
        /// <returns>Mod statistics</returns>
        public static ModStatistics GetStatistics()
        {
            lock (_loadedMods)
            {
                return new ModStatistics
                {
                    TotalMods = _loadedMods.Count,
                    EnabledMods = _loadedMods.Count(m => m.Value.IsEnabled),
                    ActiveMods = _loadedMods.Count(m => m.Value.IsActive),
                    ModsByCategory = _categoryIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count),
                    ModsByDependency = _dependencyIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count),
                    ModsByCapability = GetAllMods().SelectMany(m => m.Capabilities)
                        .GroupBy(c => c.Name)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }

        /// <summary>
        /// Check if a mod is loaded.
        /// </summary>
        /// <param name="name""Mod name</param>
        /// <returns>True if loaded</returns>
        public static bool IsModLoaded(string name)
        {
            lock (_loadedMods)
            {
                return _loadedMods.ContainsKey(name);
            }
        }

        /// <summary>
        /// Get mod dependencies.
        /// </summary>
        /// <param name="modName""Mod name</param>
        /// <returns>List of dependencies</returns>
        public static List<string> GetDependencies(string modName)
        {
            var modInfo = GetMod(modName);
            return modInfo?.Dependencies?.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Get mod conflicts.
        /// </summary>
        /// <param name="modName""Mod name</param>
        /// <returns>List of conflicts</returns>
        public static List<string> GetConflicts(string modName)
        {
            var modInfo = GetMod(modName);
            return modInfo?.Conflicts?.ToList() ?? new List<string>();
        }

        #endregion
    }
}