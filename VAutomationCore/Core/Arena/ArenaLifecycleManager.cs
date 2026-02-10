using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Logging;
using VAutomationCore.Models;
using VAutomationCore.Services;

namespace VAutomationCore.Core.Arena
{
    /// <summary>
    /// Manages arena lifecycle events and state.
    /// </summary>
    public class ArenaLifecycleManager
    {
        private readonly CoreLogger _logger;

        public ArenaLifecycleManager(CoreLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Handles player entering an arena zone.
        /// </summary>
        public void OnPlayerEntered(Entity player, string zoneId)
        {
            _logger?.Info($"Player entered arena: {zoneId}");
            ZoneEventBridge.PublishPlayerEntered(player, zoneId);
        }

        /// <summary>
        /// Handles player exiting an arena zone.
        /// </summary>
        public void OnPlayerExited(Entity player, string zoneId)
        {
            _logger?.Info($"Player exited arena: {zoneId}");
            ZoneEventBridge.PublishPlayerExited(player, zoneId);
        }

        /// <summary>
        /// Initializes the arena lifecycle manager.
        /// </summary>
        public void Initialize()
        {
            _logger?.Info("ArenaLifecycleManager initialized");
        }

        /// <summary>
        /// Shuts down the arena lifecycle manager.
        /// </summary>
        public void Shutdown()
        {
            _logger?.Info("ArenaLifecycleManager shutdown");
        }
    }
}
