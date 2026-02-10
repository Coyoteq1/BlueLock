using System;
using HarmonyLib;
using ProjectM;
using Unity.Entities;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Patches
{
    /// <summary>
    /// Patch for ServerBootstrapSystem to track world initialization state.
    /// Provides events for server startup, world ready, and shutdown.
    /// </summary>
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
    internal static class ServerBootstrapSystemPatch
    {
        public static event EventHandler OnServerStarted;
        public static event EventHandler OnWorldReady;
        public static event EventHandler OnServerShutdown;
        
        private static bool _hasStarted = false;
        private static bool _isReady = false;

        [HarmonyPrefix]
        static void OnUpdatePrefix(ServerBootstrapSystem __instance)
        {
            try
            {
                // Check if server has started (first update)
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    OnServerStarted?.Invoke(__instance, EventArgs.Empty);
                    CoreLogger.LogInfo("Server bootstrap started");
                }

                // Check if world is ready (systems are initialized)
                if (!_isReady && __instance.World.IsCreated)
                {
                    _isReady = true;
                    OnWorldReady?.Invoke(__instance, EventArgs.Empty);
                    CoreLogger.LogInfo("World is ready - all systems initialized");
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in server bootstrap update", ex);
            }
        }

        /// <summary>
        /// Check if the server has completed startup.
        /// </summary>
        public static bool IsServerStarted => _hasStarted;

        /// <summary>
        /// Check if the world is ready and all systems are initialized.
        /// </summary>
        public static bool IsWorldReady => _isReady;
    }

    /// <summary>
    /// Patch for Initialization completion to track when the game world is fully loaded.
    /// </summary>
    [HarmonyPatch(typeof(WorldBootstrapSystem), nameof(WorldBootstrapSystem.Initialize))]
    internal static class WorldBootstrapPatch
    {
        public static event EventHandler OnWorldInitialized;

        [HarmonyPostfix]
        static void InitializePostfix(WorldBootstrapSystem __instance)
        {
            try
            {
                OnWorldInitialized?.Invoke(__instance, EventArgs.Empty);
                CoreLogger.LogInfo("WorldBootstrapSystem.Initialize completed");
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in world bootstrap initialize", ex);
            }
        }
    }
}
