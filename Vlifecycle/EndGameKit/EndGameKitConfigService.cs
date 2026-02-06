using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VAuto.EndGameKit.Configuration;

namespace VAuto.EndGameKit
{
    /// <summary>
    /// Service responsible for configuration management and hot-reload.
    /// Monitors configuration file changes and triggers reloads.
    /// </summary>
    public class EndGameKitConfigService
    {
        private string _configPath;
        private DateTime _lastWriteTime;
        private float _checkInterval = 5.0f;
        private float _checkTimer;
        private bool _initialized;

        /// <summary>
        /// Creates a new EndGameKitConfigService instance.
        /// </summary>
        /// <param name="configPath">Path to the configuration file.</param>
        public EndGameKitConfigService(string configPath)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        }

        /// <summary>
        /// Attempts to load the configuration from the file.
        /// </summary>
        /// <param name="profiles">The loaded kit profiles.</param>
        /// <returns>True if loading succeeded.</returns>
        public bool TryLoad(out List<EndGameKitProfile> profiles)
        {
            profiles = new List<EndGameKitProfile>();

            if (!File.Exists(_configPath))
            {
                Plugin.Log.LogWarning($"[EndGameKitConfigService] Config file not found: {_configPath}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<KitConfiguration>(json, VAutoJsonOptions.Default);

                if (config == null || config.Profiles == null)
                {
                    Plugin.Log.LogWarning("[EndGameKitConfigService] Invalid configuration format");
                    return false;
                }

                profiles = config.Profiles;
                _lastWriteTime = File.GetLastWriteTime(_configPath);
                _initialized = true;

                Plugin.Log.LogInfo($"[EndGameKitConfigService] Loaded {profiles.Count} kit profiles");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[EndGameKitConfigService] Failed to load config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the configuration file has changed and reloads if necessary.
        /// </summary>
        /// <param name="profiles">The reloaded kit profiles if changed.</param>
        /// <returns>True if the config was reloaded.</returns>
        public bool TryReloadIfChanged(out List<EndGameKitProfile> profiles)
        {
            profiles = new List<EndGameKitProfile>();

            if (!_initialized || string.IsNullOrEmpty(_configPath))
                return false;

            try
            {
                if (File.Exists(_configPath))
                {
                    var currentWriteTime = File.GetLastWriteTime(_configPath);
                    if (currentWriteTime > _lastWriteTime)
                    {
                        Plugin.Log.LogInfo("[EndGameKitConfigService] Detected config change, reloading...");

                        _lastWriteTime = currentWriteTime;
                        return TryLoad(out profiles);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[EndGameKitConfigService] Config check failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Gets the current configuration path.
        /// </summary>
        public string ConfigPath => _configPath;

        /// <summary>
        /// Gets whether the service is initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Sets the hot-reload check interval.
        /// </summary>
        /// <param name="intervalSeconds">Check interval in seconds.</param>
        public void SetCheckInterval(float intervalSeconds)
        {
            _checkInterval = Math.Max(1.0f, intervalSeconds);
        }

        /// <summary>
        /// Forces an immediate config reload check.
        /// </summary>
        public void ForceCheck()
        {
            _checkTimer = _checkInterval; // Trigger check on next Update
        }
    }
}
