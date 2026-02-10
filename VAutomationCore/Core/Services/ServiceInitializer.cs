using System;
using System.Collections.Generic;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Services
{
    /// <summary>
    /// Centralized service initializer for static-only services.
    /// Provides ordered initialization and validation for all mod services.
    /// </summary>
    public static class ServiceInitializer
    {
        private static readonly List<Action> _initializers = new();
        private static readonly List<Func<bool>> _validators = new();
        private static readonly Dictionary<string, Action> _namedInitializers = new();
        private static readonly Dictionary<string, Func<bool>> _namedValidators = new();
        private static bool _servicesInitialized;
        private static readonly object _lock = new();
        private static CoreLogger _log;
        
        /// <summary>
        /// Initializes the ServiceInitializer with a logger.
        /// Must be called before using RegisterInitializer or InitializeAll.
        /// </summary>
        /// <param name="log">The core logger to use.</param>
        public static void InitializeLogger(CoreLogger log)
        {
            _log = log;
        }
        
        private const string LogSource = "ServiceInitializer";
        
        /// <summary>
        /// Registers an initializer for a service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="initializer">The initialization action.</param>
        public static void RegisterInitializer(string serviceName, Action initializer)
        {
            lock (_lock)
            {
                if (_namedInitializers.ContainsKey(serviceName))
                {
                    _log?.Warning($"Initializer already registered for {serviceName}", LogSource);
                    return;
                }
                
                _namedInitializers[serviceName] = initializer;
                _log?.Debug($"Registered initializer: {serviceName}", LogSource);
            }
        }
        
        /// <summary>
        /// Registers a validator for a service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="validator">The validation function.</param>
        public static void RegisterValidator(string serviceName, Func<bool> validator)
        {
            lock (_lock)
            {
                if (_namedValidators.ContainsKey(serviceName))
                {
                    _log?.Warning($"Validator already registered for {serviceName}", LogSource);
                    return;
                }
                
                _namedValidators[serviceName] = validator;
                _log?.Debug($"Registered validator: {serviceName}", LogSource);
            }
        }
        
        /// <summary>
        /// Initializes all registered services in order.
        /// </summary>
        /// <param name="log">The core logger to use.</param>
        /// <returns>True if all services initialized successfully.</returns>
        public static bool InitializeAll(CoreLogger log)
        {
            _log = log;
            return InitializeAll();
        }
        
        /// <summary>
        /// Initializes all registered services in order.
        /// </summary>
        /// <returns>True if all services initialized successfully.</returns>
        public static bool InitializeAll()
        {
            lock (_lock)
            {
                if (_servicesInitialized)
                {
                    _log?.Warning("Services already initialized", LogSource);
                    return true;
                }
                
                _log?.Info("Initializing all services...", LogSource);
                
                try
                {
                    // Initialize services in order
                    foreach (var kvp in _namedInitializers)
                    {
                        try
                        {
                            _log?.Info($"Initializing service: {kvp.Key}", LogSource);
                            kvp.Value();
                        }
                        catch (Exception ex)
                        {
                            _log?.Exception(ex, $"Error initializing {kvp.Key}");
                            return false;
                        }
                    }
                    
                    // Validate all services
                    foreach (var kvp in _namedValidators)
                    {
                        try
                        {
                            if (!kvp.Value())
                            {
                                _log?.Error($"Service validation failed: {kvp.Key}", LogSource);
                                return false;
                            }
                            
                            _log?.Debug($"Service validated: {kvp.Key}", LogSource);
                        }
                        catch (Exception ex)
                        {
                            _log?.Exception(ex, $"Error validating {kvp.Key}");
                            return false;
                        }
                    }
                    
                    _servicesInitialized = true;
                    _log?.Info("All services initialized successfully", LogSource);
                    return true;
                }
                catch (Exception ex)
                {
                    _log?.Exception(ex, LogSource);
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Initializes a specific service by name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <returns>True if the service initialized successfully.</returns>
        public static bool InitializeService(string serviceName)
        {
            lock (_lock)
            {
                if (_namedInitializers.TryGetValue(serviceName, out var initializer))
                {
                    try
                    {
                        _log?.Info($"Initializing service: {serviceName}", LogSource);
                        initializer();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _log?.Exception(ex, $"Error initializing {serviceName}");
                        return false;
                    }
                }
                
                _log?.Warning($"No initializer found for service: {serviceName}", LogSource);
                return false;
            }
        }
        
        /// <summary>
        /// Validates a specific service by name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <returns>True if the service is valid.</returns>
        public static bool ValidateService(string serviceName)
        {
            lock (_lock)
            {
                if (_namedValidators.TryGetValue(serviceName, out var validator))
                {
                    return validator();
                }
                
                _log?.Warning($"No validator found for service: {serviceName}", LogSource);
                return false;
            }
        }
        
        /// <summary>
        /// Resets all registrations and initialization state.
        /// Use for testing only.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _initializers.Clear();
                _validators.Clear();
                _namedInitializers.Clear();
                _namedValidators.Clear();
                _servicesInitialized = false;
                _log?.Info("ServiceInitializer reset", LogSource);
            }
        }
        
        /// <summary>
        /// Gets the count of registered services.
        /// </summary>
        public static int ServiceCount => _namedInitializers.Count;
        
        /// <summary>
        /// Checks if all services have been initialized.
        /// </summary>
        public static bool IsInitialized => _servicesInitialized;
        
        /// <summary>
        /// Gets a list of all registered service names.
        /// </summary>
        public static IReadOnlyList<string> GetServiceNames()
        {
            lock (_lock)
            {
                return new List<string>(_namedInitializers.Keys);
            }
        }
    }
}
