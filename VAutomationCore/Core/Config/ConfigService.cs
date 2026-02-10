using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;
using VAutomationCore.Core.Logging;
using ProjectM;

namespace VAutomationCore.Core.Config
{
    /// <summary>
    /// Centralized configuration service with concurrent caching and lazy loading.
    /// Provides safe JSON serialization/deserialization for mod configurations.
    /// </summary>
    public static class ConfigService
    {
        private static readonly ConcurrentDictionary<string, object> _configs = new();
        private static readonly string _configPath;
        private static readonly object _initLock = new();
        private static readonly CoreLogger _log = new CoreLogger("ConfigService");
        
        static ConfigService()
        {
            _configPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto");
            
            // Ensure config directory exists
            if (!Directory.Exists(_configPath))
            {
                Directory.CreateDirectory(_configPath);
            }
        }
        
        /// <summary>
        /// Gets a configuration object of type T, loading from disk if not cached.
        /// </summary>
        /// <typeparam name="T">The configuration type (must have parameterless constructor).</typeparam>
        /// <param name="fileName">Optional custom filename. Defaults to type name + ".json".</param>
        /// <returns>The loaded or default configuration.</returns>
        public static T GetConfig<T>(string? fileName = null) where T : new()
        {
            var key = typeof(T).Name;
            var configFile = fileName ?? $"{key}.json";
            var fullPath = Path.Combine(_configPath, configFile);
            
            return (T)_configs.GetOrAdd(key, _ =>
            {
                if (!File.Exists(fullPath))
                {
                    var defaultConfig = new T();
                    SaveConfig(defaultConfig, configFile);
                    _log.Info($"Created default config: {configFile}");
                    return defaultConfig;
                }
                
                try
                {
                    var json = File.ReadAllText(fullPath);
                    var result = JsonSerializer.Deserialize<T>(json, GetJsonOptions());
                    
                    if (result == null)
                    {
                        _log.Warning($"Failed to deserialize {configFile}, using defaults");
                        return new T();
                    }
                    
                    _log.Debug($"Loaded config: {configFile}");
                    return result;
                }
                catch (Exception ex)
                {
                    _log.Exception(ex, $"Error loading {configFile}, using defaults");
                    return new T();
                }
            });
        }
        
        /// <summary>
        /// Saves a configuration object to disk and updates the cache.
        /// </summary>
        /// <typeparam name="T">The configuration type.</typeparam>
        /// <param name="config">The configuration to save.</param>
        /// <param name="fileName">Optional custom filename.</param>
        public static void SaveConfig<T>(T config, string? fileName = null)
        {
            var key = typeof(T).Name;
            var configFile = fileName ?? $"{key}.json";
            var fullPath = Path.Combine(_configPath, configFile);
            
            try
            {
                var json = JsonSerializer.Serialize(config, GetJsonOptions());
                File.WriteAllText(fullPath, json);
                _configs[key] = config!;
                _log.Info($"Saved config: {configFile}");
            }
            catch (Exception ex)
            {
                _log.Exception(ex, $"Error saving {configFile}");
            }
        }
        
        /// <summary>
        /// Reloads a configuration from disk, bypassing cache.
        /// </summary>
        /// <typeparam name="T">The configuration type.</typeparam>
        /// <param name="fileName">Optional custom filename.</param>
        /// <returns>The freshly loaded configuration.</returns>
        public static T ReloadConfig<T>(string? fileName = null) where T : new()
        {
            var key = typeof(T).Name;
            var configFile = fileName ?? $"{key}.json";
            var fullPath = Path.Combine(_configPath, configFile);
            
            // Remove from cache
            _configs.TryRemove(key, out _);
            
            // Force reload
            return GetConfig<T>(configFile);
        }
        
        /// <summary>
        /// Checks if a configuration file exists.
        /// </summary>
        /// <typeparam name="T">The configuration type.</typeparam>
        /// <param name="fileName">Optional custom filename.</param>
        /// <returns>True if the file exists.</returns>
        public static bool ConfigExists<T>(string? fileName = null)
        {
            var configFile = fileName ?? $"{typeof(T).Name}.json";
            var fullPath = Path.Combine(_configPath, configFile);
            return File.Exists(fullPath);
        }
        
        /// <summary>
        /// Clears all cached configurations.
        /// </summary>
        public static void ClearCache()
        {
            _configs.Clear();
            _log.Info("Config cache cleared");
        }
        
        /// <summary>
        /// Gets the JSON serializer options with custom converters.
        /// </summary>
        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new Float3JsonConverter(),
                    new Float2JsonConverter(),
                    new PrefabGuidJsonConverter(),
                    new QuaternionJsonConverter()
                }
            };
        }
    }
}
