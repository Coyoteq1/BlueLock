using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using VAutomation.Core.Json;

namespace VAutomation.Core.Configuration
{
    /// <summary>
    /// Centralized configuration service for VAuto framework.
    /// Handles JSON defaults + CFG overrides, validation, migration, caching, and hot reload.
    /// </summary>
    public class VAutoConfigService : IVAutoConfigService
    {
        private readonly Dictionary<string, ConfigRegistration> _registrations = new();
        private readonly Dictionary<string, object> _configCache = new();
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly Dictionary<Type, List<object>> _validators = new();
        private readonly Dictionary<Type, object> _migrators = new();
        private readonly Dictionary<Type, List<object>> _listeners = new();
        
        private readonly string _configRoot;
        private readonly bool _enableHotReload;
        private readonly VAutoLogger _logger;

        public VAutoConfigService(string configRoot = null, bool enableHotReload = true)
        {
            _configRoot = configRoot ?? Path.Combine(BepInEx.Paths.ConfigPath, "VAuto");
            _enableHotReload = enableHotReload;
            _logger = new VAutoLogger("VAutoConfig");
            
            Directory.CreateDirectory(_configRoot);
        }

        /// <summary>
        /// Registers a module configuration.
        /// </summary>
        public void Register<T>(string moduleName, string configVersion = "1.0") where T : class, new()
        {
            var type = typeof(T);
            var jsonPath = Path.Combine(_configRoot, $"VAuto.{moduleName}.json");
            var cfgPath = Path.Combine(_configRoot, $"VAuto.{moduleName}.cfg");

            _registrations[moduleName] = new ConfigRegistration
            {
                ModuleName = moduleName,
                ConfigType = type,
                JsonPath = jsonPath,
                CfgPath = cfgPath,
                ConfigVersion = configVersion
            };

            _validators[type] = new List<object>();
            _migrators[type] = null;
            _listeners[type] = new List<object>();

            _logger.LogInfo($"Registered config for module: {moduleName}");
        }

        /// <summary>
        /// Registers a configuration validator.
        /// </summary>
        public void RegisterValidator<T>(IConfigValidator<T> validator) where T : class
        {
            if (_validators.TryGetValue(typeof(T), out var validators))
            {
                validators.Add(validator);
            }
        }

        /// <summary>
        /// Registers a configuration migrator.
        /// </summary>
        public void RegisterMigrator<T>(IConfigMigrator<T> migrator) where T : class
        {
            _migrators[typeof(T)] = migrator;
        }

        /// <summary>
        /// Registers a reload listener.
        /// </summary>
        public void RegisterReloadListener<T>(IConfigReloadListener<T> listener) where T : class
        {
            if (_listeners.TryGetValue(typeof(T), out var listeners))
            {
                listeners.Add(listener);
            }
        }

        public T GetConfig<T>(string moduleName) where T : class, new()
        {
            if (_configCache.TryGetValue(moduleName, out var cached))
            {
                return (T)cached;
            }

            var config = LoadConfigInternal<T>(moduleName);
            _configCache[moduleName] = config;
            
            if (_enableHotReload)
            {
                SetupWatcher(moduleName);
            }

            return config;
        }

        public bool TryGetConfig<T>(string moduleName, out T config) where T : class, new()
        {
            try
            {
                config = GetConfig<T>(moduleName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Config not found for module {moduleName}: {ex.Message}");
                config = null;
                return false;
            }
        }

        public void Reload(string moduleName)
        {
            if (!_registrations.TryGetValue(moduleName, out var reg))
            {
                _logger.LogWarning($"No registration found for module: {moduleName}");
                return;
            }

            _configCache.Remove(moduleName);
            _logger.LogInfo($"Reloading config for module: {moduleName}");

            // Re-trigger load
            var method = typeof(VAutoConfigService).GetMethod("GetConfig", Type.EmptyTypes);
            var genericMethod = method.MakeGenericMethod(reg.ConfigType);
            genericMethod.Invoke(this, new[] { moduleName });
        }

        public void ReloadAll()
        {
            _configCache.Clear();
            _logger.LogInfo("Reloading all configurations");

            foreach (var moduleName in _registrations.Keys)
            {
                Reload(moduleName);
            }
        }

        private T LoadConfigInternal<T>(string moduleName) where T : class, new()
        {
            var reg = _registrations[moduleName];

            // 1. Load JSON defaults
            var config = LoadJsonDefaults<T>(reg.JsonPath);

            // 2. Apply CFG overrides
            ApplyCfgOverrides(config, reg.CfgPath);

            // 3. Validate
            ValidateConfig(config);

            // 4. Migrate if needed
            MigrateIfNeeded(config, reg.ConfigVersion);

            return config;
        }

        private T LoadJsonDefaults<T>(string path) where T : class, new()
        {
            if (!File.Exists(path))
            {
                _logger.LogInfo($"Creating default config at: {path}");
                var defaultObj = new T();
                SaveJson(path, defaultObj);
                return defaultObj;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, VAutoJsonOptions.Default)
                       ?? throw new InvalidOperationException("Deserialized config is null");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load JSON config: {ex.Message}");
                _logger.LogWarning("Using default configuration");
                return new T();
            }
        }

        private void ApplyCfgOverrides<T>(T config, string cfgPath) where T : class
        {
            if (!File.Exists(cfgPath))
            {
                return;
            }

            try
            {
                var parsed = CfgParser.Parse(cfgPath);

                foreach (var section in parsed)
                {
                    ApplySectionOverrides(config, section.Key, section.Value);
                }

                _logger.LogDebug($"Applied CFG overrides from: {cfgPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to parse CFG overrides: {ex.Message}");
            }
        }

        private void ApplySectionOverrides(object config, string section, Dictionary<string, string> values)
        {
            var sectionProp = config.GetType().GetProperty(section, BindingFlags.Public | BindingFlags.Instance);
            if (sectionProp == null || !sectionProp.CanRead || !sectionProp.CanWrite)
            {
                return;
            }

            var sectionObj = sectionProp.GetValue(config);
            if (sectionObj == null)
            {
                // Create section object if it doesn't exist
                sectionObj = Activator.CreateInstance(sectionProp.PropertyType);
                sectionProp.SetValue(config, sectionObj);
            }

            foreach (var pair in values)
            {
                var prop = sectionObj.GetType().GetProperty(pair.Key, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite)
                {
                    continue;
                }

                try
                {
                    var converted = ConvertValue(prop.PropertyType, pair.Value);
                    prop.SetValue(sectionObj, converted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to set config property {section}.{pair.Key}: {ex.Message}");
                }
            }
        }

        private object ConvertValue(Type type, string value)
        {
            if (type == typeof(string))
                return value;
            
            if (type == typeof(int))
                return int.Parse(value);
            
            if (type == typeof(float))
                return float.Parse(value);
            
            if (type == typeof(double))
                return double.Parse(value);
            
            if (type == typeof(bool))
                return bool.Parse(value);
            
            if (type.IsEnum)
                return Enum.Parse(type, value);
            
            return value;
        }

        private void ValidateConfig<T>(T config) where T : class
        {
            var type = typeof(T);
            
            if (_validators.TryGetValue(type, out var validators))
            {
                foreach (var validator in validators)
                {
                    ((IConfigValidator<T>)validator).Validate(config);
                }
            }
        }

        private void MigrateIfNeeded<T>(T config, string currentVersion) where T : class
        {
            var type = typeof(T);
            
            if (_migrators.TryGetValue(type, out var migrator) && migrator != null)
            {
                var typedMigrator = (IConfigMigrator<T>)migrator;
                
                if (typedMigrator.NeedsMigration(config))
                {
                    _logger.LogInfo($"Migrating config from version {typedMigrator.CurrentVersion} to {currentVersion}");
                    typedMigrator.Migrate(config);
                    
                    // Save migrated config
                    var reg = _registrations.First(r => r.Value.ConfigType == type).Value;
                    SaveJson(reg.JsonPath, config);
                }
            }
        }

        private void SetupWatcher(string moduleName)
        {
            if (_watchers.ContainsKey(moduleName))
            {
                return;
            }

            var reg = _registrations[moduleName];
            var directory = Path.GetDirectoryName(reg.JsonPath) ?? _configRoot;

            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                var watcher = new FileSystemWatcher(directory, $"VAuto.{moduleName}.*")
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                watcher.Changed += (_, args) =>
                {
                    if (args.Name.Contains(".json") || args.Name.Contains(".cfg"))
                    {
                        _logger.LogInfo($"Config change detected for {moduleName}, reloading...");
                        Reload(moduleName);
                        
                        // Notify listeners
                        NotifyListeners(moduleName);
                    }
                };

                _watchers[moduleName] = watcher;
                _logger.LogDebug($"File watcher setup for module: {moduleName}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to setup file watcher for {moduleName}: {ex.Message}");
            }
        }

        private void NotifyListeners(string moduleName)
        {
            var reg = _registrations[moduleName];
            var type = reg.ConfigType;
            var config = _configCache.TryGetValue(moduleName, out var cached) ? cached : null;

            if (config == null)
            {
                return;
            }

            if (_listeners.TryGetValue(type, out var listeners))
            {
                var notifyMethod = typeof(IConfigReloadListener<>).MakeGenericType(type)
                    .GetMethod("OnReloaded");

                foreach (var listener in listeners)
                {
                    try
                    {
                        notifyMethod?.Invoke(listener, new[] { config });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error notifying listener: {ex.Message}");
                    }
                }
            }
        }

        private void SaveJson<T>(string path, T config)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save config: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Internal registration data.
    /// </summary>
    internal class ConfigRegistration
    {
        public string ModuleName { get; set; }
        public Type ConfigType { get; set; }
        public string JsonPath { get; set; }
        public string CfgPath { get; set; }
        public string ConfigVersion { get; set; }
    }

    /// <summary>
    /// Simple INI-style CFG parser.
    /// </summary>
    public static class CfgParser
    {
        public static Dictionary<string, Dictionary<string, string>> Parse(string path)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            result[""] = currentSection;

            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                {
                    continue;
                }

                // Check for section header
                if (trimmed.StartsWith("[") && trimmed.Contains("]"))
                {
                    var sectionName = trimmed.Substring(1, trimmed.IndexOf(']') - 1).Trim();
                    if (!result.ContainsKey(sectionName))
                    {
                        currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        result[sectionName] = currentSection;
                    }
                    else
                    {
                        currentSection = result[sectionName];
                    }
                    continue;
                }

                // Check for key=value
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = trimmed.Substring(0, eqIndex).Trim();
                    var value = trimmed.Substring(eqIndex + 1).Trim();
                    currentSection[key] = value;
                }
            }

            return result;
        }
    }
}
