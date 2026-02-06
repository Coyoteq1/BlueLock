using Unity.Entities;
using VAuto.Services.Systems;

namespace VAuto.Core
{
    /// <summary>
    /// Service manager for IL2CPP-compatible ECS-style services.
    /// Initializes and updates all tracking services.
    /// </summary>
    public class ECSServiceManager
    {
        private readonly Plugin _plugin;
        private readonly EntityManager _entityManager;
        
        // IL2CPP-compatible services
        private ZoneTrackingService _zoneTrackingService;
        private AutomationProcessingService _automationProcessingService;
        private PortalInterceptService _portalInterceptService;
        
        private bool _initialized = false;

        public ECSServiceManager(Plugin plugin, EntityManager entityManager)
        {
            _plugin = plugin;
            _entityManager = entityManager;
        }

        /// <summary>
        /// Initialize all ECS-style services
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            Plugin.Log.LogInfo("[ECSServiceManager] Initializing IL2CPP-compatible ECS services...");

            // Create and initialize services
            _zoneTrackingService = new ZoneTrackingService(_plugin, _entityManager);
            _zoneTrackingService.Initialize();

            _automationProcessingService = new AutomationProcessingService(_plugin, _entityManager);
            _automationProcessingService.Initialize();

            _portalInterceptService = new PortalInterceptService(_plugin, _entityManager);
            _portalInterceptService.Initialize();

            Plugin.Log.LogInfo("[ECSServiceManager] All services initialized successfully");
        }

        /// <summary>
        /// Update all services (called from Plugin.Update)
        /// </summary>
        public void Update()
        {
            if (!_initialized)
                return;

            _zoneTrackingService?.Update();
            _automationProcessingService?.Update();
        }

        /// <summary>
        /// Get zone tracking service
        /// </summary>
        public ZoneTrackingService ZoneTracking => _zoneTrackingService;

        /// <summary>
        /// Get automation processing service
        /// </summary>
        public AutomationProcessingService AutomationProcessing => _automationProcessingService;

        /// <summary>
        /// Get portal intercept service
        /// </summary>
        public PortalInterceptService PortalIntercept => _portalInterceptService;

        /// <summary>
        /// Cleanup for shutdown
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized)
                return;

            Plugin.Log.LogInfo("[ECSServiceManager] Shutting down services...");
                
            _initialized = false;
        }
    }
}
