using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using VAuto.Core.Lifecycle;
using VAuto.Services.World;
using VAuto.Services.Interfaces;
using static VAuto.MyPluginInfo;

namespace VAuto.Core.Patterns
{
    /// <summary>
    /// Service Manager - Singleton pattern for managing all V Rising services
    /// Provides centralized access to all services with proper lifecycle management
    /// </summary>
    public class ServiceManager : Singleton<ServiceManager>
    {
        private static readonly string _logPrefix = "[ServiceManager]";
        private readonly Dictionary<Type, IService> _services;
        private bool _isInitialized = false;

        public ManualLogSource Log { get; private set; }

        public ServiceManager()
        {
            Log = Plugin.Log;
            _services = new Dictionary<Type, IService>();
        }

        /// <summary>
        /// Initialize all services
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                Log?.LogInfo($"{_logPrefix} Initializing V Rising services...");

                // Initialize core services
                RegisterService<PVPItemLifecycle>();
                RegisterService<GlowZonesService>();

                Log?.LogInfo($"{_logPrefix} Successfully initialized {_services.Count} services");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to initialize services: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Register and initialize a service
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        private void RegisterService<T>() where T : Singleton<T>, IService, new()
        {
            try
            {
                var service = Singleton<T>.Instance;
                service.Initialize();
                _services[typeof(T)] = service;
                
                Log?.LogInfo($"{_logPrefix} Registered service: {typeof(T).Name}");
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to register service {typeof(T).Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get a service by type
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>Service instance or null if not found</returns>
        public T GetService<T>() where T : Singleton<T>, IService, new()
        {
            var serviceType = typeof(T);
            
            if (_services.TryGetValue(serviceType, out var service))
            {
                return service as T;
            }

            // Try to get singleton instance directly
            try
            {
                var singletonService = Singleton<T>.Instance;
                if (((IService)singletonService).IsInitialized)
                {
                    _services[serviceType] = singletonService;
                    return singletonService;
                }
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"{_logPrefix} Service {serviceType.Name} not initialized: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if a service is registered and initialized
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <returns>True if service is available</returns>
        public bool IsServiceAvailable<T>() where T : Singleton<T>, IService, new()
        {
            return GetService<T>() != null;
        }

        /// <summary>
        /// Shutdown all services
        /// </summary>
        public void Shutdown()
        {
            if (!_isInitialized) return;

            try
            {
                Log?.LogInfo($"{_logPrefix} Shutting down {_services.Count} services...");

                foreach (var service in _services.Values.Reverse())
                {
                    try
                    {
                        service.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Log?.LogError($"{_logPrefix} Error shutting down service: {ex.Message}");
                    }
                }

                _services.Clear();
                _isInitialized = false;
                
                Log?.LogInfo($"{_logPrefix} All services shut down successfully");
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Error during shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Get service status information
        /// </summary>
        /// <returns>Dictionary of service statuses</returns>
        public Dictionary<string, bool> GetServiceStatus()
        {
            var status = new Dictionary<string, bool>();
            
            foreach (var kvp in _services)
            {
                status[kvp.Key.Name] = kvp.Value.IsInitialized;
            }

            return status;
        }

        /// <summary>
        /// Reload all service configurations
        /// </summary>
        public void ReloadConfigurations()
        {
            Log?.LogInfo($"{_logPrefix} Reloading service configurations...");

            foreach (var service in _services.Values)
            {
                try
                {
                    // Call reload if service has configuration
                    if (service is PVPItemLifecycle lifecycle)
                    {
                        lifecycle.LoadLifecycleConfig();
                    }
                    else if (service is GlowZonesService zones)
                    {
                        zones.ReloadZonesConfig();
                    }
                }
                catch (Exception ex)
                {
                    Log?.LogError($"{_logPrefix} Failed to reload configuration for {service.GetType().Name}: {ex.Message}");
                }
            }
        }
    }
}
