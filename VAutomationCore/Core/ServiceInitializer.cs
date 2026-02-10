using System;
using System.Collections.Generic;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core
{
    /// <summary>
    /// Provides ordered service registration and initialization for VAutomation mods.
    /// Ensures dependencies are initialized in the correct order before use.
    /// </summary>
    public static class ServiceInitializer
    {
        private static readonly Dictionary<string, bool> _initializedServices = new();
        private static readonly object _lock = new();

        /// <summary>
        /// Initializes a service with dependencies. Throws if dependencies are not satisfied.
        /// </summary>
        /// <param name="serviceName">Name of the service being initialized.</param>
        /// <param name="initializer">The initialization function that performs actual setup.</param>
        /// <param name="dependencies">Optional list of service names that must be initialized first.</param>
        public static void Initialize(string serviceName, Action initializer, IEnumerable<string> dependencies = null)
        {
            lock (_lock)
            {
                if (_initializedServices.ContainsKey(serviceName))
                {
                    UnifiedCore.LogWarning($"Service '{serviceName}' already initialized, skipping");
                    return;
                }

                // Check dependencies
                if (dependencies != null)
                {
                    foreach (var dep in dependencies)
                    {
                        if (!_initializedServices.ContainsKey(dep))
                        {
                            throw new InvalidOperationException(
                                $"Service '{serviceName}' depends on '{dep}' which has not been initialized");
                        }
                    }
                }

                try
                {
                    initializer();
                    _initializedServices[serviceName] = true;
                    UnifiedCore.LogInfo($"Service '{serviceName}' initialized successfully");
                }
                catch (Exception ex)
                {
                    UnifiedCore.LogException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Checks if a service has been initialized.
        /// </summary>
        public static bool IsInitialized(string serviceName)
        {
            lock (_lock)
            {
                return _initializedServices.ContainsKey(serviceName);
            }
        }

        /// <summary>
        /// Resets all initialized services (for testing purposes).
        /// </summary>
        public static void ResetAll()
        {
            lock (_lock)
            {
                _initializedServices.Clear();
            }
        }
    }
}
