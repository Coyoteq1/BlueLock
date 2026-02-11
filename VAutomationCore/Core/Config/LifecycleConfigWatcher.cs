using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using VAuto.Core.Lifecycle;
using Microsoft.Extensions.Logging;

namespace VAuto.Core.Config
{
    /// <summary>
    /// Hot-reload configuration watcher using FileSystemWatcher pattern.
    /// Monitors config file changes and automatically reloads configuration.
    /// </summary>
    public class LifecycleConfigWatcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly string _configPath;
        private readonly ILogger<LifecycleConfigWatcher> _log;
        private bool _pendingReload;

        /// <summary>
        /// Event fired when configuration is reloaded.
        /// </summary>
        public event Action<ZoneLifecycleConfig> OnConfigReloaded;

        public LifecycleConfigWatcher(string configPath, ILogger<LifecycleConfigWatcher> log)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            
            var directory = Path.GetDirectoryName(configPath);
            var fileName = Path.GetFileName(configPath);
            
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                _log.LogError($"[LifecycleConfigWatcher] Directory not found: {directory}");
                throw new DirectoryNotFoundException($"Configuration directory not found: {directory}");
            }

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = false
            };
            
            _watcher.Changed += OnConfigChanged;
            _watcher.Deleted += OnConfigChanged;
            _watcher.Renamed += OnConfigChanged;
            
            _debounceTimer = new Timer(ProcessReload, null, Timeout.Infinite, Timeout.Infinite);
            
            _log.LogInformation($"[LifecycleConfigWatcher] Watching: {configPath}");
        }

        /// <summary>
        /// Starts monitoring the configuration file.
        /// </summary>
        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
            _log.LogDebug("[LifecycleConfigWatcher] File watching started");
        }

        /// <summary>
        /// Stops monitoring the configuration file.
        /// </summary>
        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
            _log.LogDebug("[LifecycleConfigWatcher] File watching stopped");
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            // Prevent rapid successive triggers
            if (_pendingReload) return;
            _pendingReload = true;
            
            // Debounce: wait 2 seconds before reloading to allow file write to complete
            _debounceTimer.Change(2000, Timeout.Infinite);
            
            _log.LogDebug($"[LifecycleConfigWatcher] Config change detected: {e.ChangeType} ({e.Name})");
        }

        private void ProcessReload(object state)
        {
            try
            {
                // Check if file still exists
                if (!File.Exists(_configPath))
                {
                    _log.LogWarning($"[LifecycleConfigWatcher] Config file deleted: {_configPath}");
                    return;
                }

                // Read and parse configuration
                var json = File.ReadAllText(_configPath);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    _log.LogWarning("[LifecycleConfigWatcher] Config file is empty");
                    return;
                }

                var config = JsonConvert.DeserializeObject<ZoneLifecycleConfig>(json);
                
                if (config == null)
                {
                    _log.LogWarning("[LifecycleConfigWatcher] Deserialized config is null");
                    return;
                }

                // Fire reloaded event
                OnConfigReloaded?.Invoke(config);
                
                _log.LogInformation("[LifecycleConfigWatcher] Configuration reloaded successfully");
            }
            catch (JsonException ex)
            {
                _log.LogError($"[LifecycleConfigWatcher] Invalid JSON in config: {ex.Message}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[LifecycleConfigWatcher] Failed to reload configuration: {ex.Message}");
            }
            finally
            {
                _pendingReload = false;
            }
        }

        /// <summary>
        /// Manually trigger a configuration reload.
        /// </summary>
        public void Reload()
        {
            _log.LogInformation("[LifecycleConfigWatcher] Manual reload triggered");
            ProcessReload(null);
        }

        /// <summary>
        /// Dispose resources used by the config watcher.
        /// </summary>
        public void Dispose()
        {
            Stop();
            _watcher.Dispose();
            _debounceTimer.Dispose();
            _log.LogInformation("[LifecycleConfigWatcher] Disposed");
        }
    }
}
