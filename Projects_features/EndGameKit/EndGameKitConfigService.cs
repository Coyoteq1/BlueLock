using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectM;
using Unity.Entities;
using VAuto.EndGameKit.Configuration;

namespace VAuto.EndGameKit
{
    /// <summary>
    /// Service responsible for configuration management and hot-reload.
    /// Monitors configuration file changes and triggers reloads.
    /// </summary>
    public class EndGameKitConfigService
    {
        private readonly EntityManager _entityManager;
        private string _configPath;
        private DateTime _lastWriteTime;
        private float _checkInterval;
        private float _checkTimer;
        private bool _initialized;

        /// <summary>
        /// Creates a new EndGameKitConfigService instance.
        /// </summary>
        /// <param name="entityManager">Entity manager for related operations.</param>
        public EndGameKitConfigService(EntityManager entityManager)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _checkInterval = 5.0f;
        }

        /// <summary>
        /// Initializes the config service with a configuration file path.
        /// </summary>
        public void Initialize(string configPath)
        {
            if (_initialized)
                return;

            _configPath = configPath;
            _initialized = true;

            Plugin.Log.LogInfo($"[EndGameKitConfigService] Initialized with config: {_configPath}");
        }

        /// <summary>
        /// Updates the config check timer and triggers reload if needed.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <returns>True if config was reloaded.</returns>
        public bool Update(float deltaTime)
        {
            if (!_initialized || string.IsNullOrEmpty(_configPath))
                return false;

            _checkTimer += deltaTime;
            if (_checkTimer < _checkInterval)
                return false;

            _checkTimer = 0f;

            try
            {
                if (File.Exists(_configPath))
                {
                    var currentWriteTime = File.GetLastWriteTime(_configPath);
                    if (currentWriteTime > _lastWriteTime)
                    {
                        Plugin.Log.LogInfo("[EndGameKitConfigService] Detected config change, reloading...");
                        
                        _lastWriteTime = currentWriteTime;
                        return true;
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

    /// <summary>
    /// Factory for creating EndGameKitSystem with proper initialization.
    /// </summary>
    public static class EndGameKitSystemFactory
    {
        private static EndGameKitSystem? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Creates or gets the singleton EndGameKitSystem instance.
        /// </summary>
        /// <param name="entityManager">Entity manager for the system.</param>
        /// <param name="configPath">Optional config path override.</param>
        /// <returns>The EndGameKitSystem instance.</returns>
        public static EndGameKitSystem Create(EntityManager entityManager, string? configPath = null)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new EndGameKitSystem(entityManager);
                        
                        var path = configPath ?? Path.Combine(Paths.ConfigPath, "EndGameKit.json");
                        _instance.Initialize(path);
                    }
                }
            }

            return _instance;
        }

        /// <summary>
        /// Gets the singleton instance, throws if not created.
        /// </summary>
        public static EndGameKitSystem GetInstance()
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("EndGameKitSystem has not been created. Call Create() first.");
            }
            return _instance;
        }

        /// <summary>
        /// Checks if the system has been created.
        /// </summary>
        public static bool IsCreated()
        {
            return _instance != null;
        }

        /// <summary>
        /// Shuts down and disposes the singleton instance.
        /// </summary>
        public static void Shutdown()
        {
            if (_instance != null)
            {
                _instance.Shutdown();
                _instance = null;
            }
        }
    }
}
