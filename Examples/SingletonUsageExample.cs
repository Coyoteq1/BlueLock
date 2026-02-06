using System;
using VAuto.Core.Lifecycle;
using VAuto.Services.Zones;
using VAuto.Core.Patterns;

namespace VAuto.Examples
{
    /// <summary>
    /// Example usage of Singleton pattern with V Rising services
    /// </summary>
    public static class SingletonUsageExample
    {
        /// <summary>
        /// Example of using singleton services
        /// </summary>
        public static void ExampleUsage()
        {
            // Initialize service manager
            var serviceManager = ServiceManager.Instance;
            serviceManager.Initialize();

            // Get singleton instances directly
            var lifecycleService = PVPItemLifecycle.Instance;
            var zonesService = GlowZonesService.Instance;

            // Use services
            if (lifecycleService.IsInitialized)
            {
                lifecycleService.LoadLifecycleConfig();
                Console.WriteLine("PVP Item Lifecycle service loaded configuration");
            }

            if (zonesService.IsInitialized)
            {
                zonesService.LoadZonesConfig();
                Console.WriteLine("Glow Zones service loaded configuration");
            }

            // Get services through ServiceManager
            var lifecycleFromManager = serviceManager.GetService<PVPItemLifecycle>();
            var zonesFromManager = serviceManager.GetService<GlowZonesService>();

            // Check service availability
            bool lifecycleAvailable = serviceManager.IsServiceAvailable<PVPItemLifecycle>();
            bool zonesAvailable = serviceManager.IsServiceAvailable<GlowZonesService>();

            Console.WriteLine($"Services available - Lifecycle: {lifecycleAvailable}, Zones: {zonesAvailable}");

            // Get service status
            var serviceStatus = serviceManager.GetServiceStatus();
            Console.WriteLine($"Service Status: {serviceStatus.Count} services registered");

            // Reload configurations
            serviceManager.ReloadConfigurations();

            // Shutdown when done
            serviceManager.Shutdown();
        }

        /// <summary>
        /// Direct singleton access without ServiceManager
        /// </summary>
        public static void DirectSingletonAccess()
        {
            // Direct access to singleton instances
            var lifecycle = PVPItemLifecycle.Instance;
            var zones = GlowZonesService.Instance;

            // Initialize if needed
            if (!lifecycle.IsInitialized)
            {
                lifecycle.Initialize();
            }

            if (!zones.IsInitialized)
            {
                zones.Initialize();
            }

            // Use the services
            lifecycle.SaveLifecycleConfig();
            zones.SaveZonesConfig();

            Console.WriteLine("Services used directly via singleton pattern");
        }
    }
}
