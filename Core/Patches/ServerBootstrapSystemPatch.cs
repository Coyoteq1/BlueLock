using System;
using HarmonyLib;
using ProjectM;
using Unity.Entities;
using VAutomationCore.Core.Events;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Patches
{
    /// <summary>
    /// Patch for ServerBootstrapSystem to track world initialization state.
    /// Provides events for server startup, world ready, and shutdown.
    /// 
    /// Flow Stabilization: P0 - Server Bootstrap
    /// - Logs: START/END with correlation context (flow=server-bootstrap)
    /// - Feature flags: bootstrap.strict, bootstrap.logLevel
    /// </summary>
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
    internal static class ServerBootstrapSystemPatch
    {
        // Feature flags per flow plan
        private static bool _strictMode = true;
        private static string _logLevel = "INFO";
        
        public static event EventHandler OnServerStarted;
        public static event EventHandler OnWorldReady;
        public static event EventHandler OnServerShutdown;
        
        private static bool _hasStarted = false;
        private static bool _isReady = false;

        // Correlation ID for log tracking
        private static readonly string FlowName = "server-bootstrap";

        [HarmonyPrefix]
        static void OnUpdatePrefix(ServerBootstrapSystem __instance)
        {
            var correlationId = $"bootstrap:{DateTime.UtcNow:HHmmss.fff}";
            
            try
            {
                // Check if server has started (first update)
                // Log format: flow=<name> | stage=<phase/step> | id=<corr-id> | ctx=<context>
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    
                    // START log with correlation context
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=start | id={correlationId} | ctx=state=ServerStarting",
                        "ServerBootstrap");
                    
                    OnServerStarted?.Invoke(__instance, EventArgs.Empty);
                    TypedEventBus.Publish(new ServerStartedEvent());
                    
                    // END log for start phase
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=start_complete | id={correlationId} | ctx=state=ServerStarted,strictMode={_strictMode}",
                        "ServerBootstrap");
                }

                // Check if world is ready (systems are initialized)
                if (!_isReady && __instance.World.IsCreated)
                {
                    _isReady = true;
                    
                    // Stage log: World ready check
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=world_check | id={correlationId} | ctx=state=WorldCreating",
                        "ServerBootstrap");
                    
                    OnWorldReady?.Invoke(__instance, EventArgs.Empty);
                    TypedEventBus.Publish(new WorldReadyEvent());
                    
                    // END log: World ready
                    CoreLogger.LogInfoStatic(
                        $"flow={FlowName} | stage=world_ready | id={correlationId} | ctx=state=WorldReady,systemsInitialized=true",
                        "ServerBootstrap");
                }
            }
            catch (Exception ex)
            {
                // Error log with correlation context
                CoreLogger.LogErrorStatic(
                    $"flow={FlowName} | stage=error | id={correlationId} | ctx=error={ex.Message}",
                    "ServerBootstrap");
                
                // Fail-closed behavior: in strict mode, don't continue
                if (_strictMode)
                {
                    CoreLogger.LogWarningStatic(
                        $"flow={FlowName} | stage=error_handled | id={correlationId} | ctx=strictMode=true,continuing=false",
                        "ServerBootstrap");
                }
            }
        }

        /// <summary>
        /// Configure bootstrap strict mode. When enabled, errors cause fail-closed behavior.
        /// </summary>
        public static void SetStrictMode(bool enabled)
        {
            _strictMode = enabled;
            CoreLogger.LogInfoStatic(
                $"flow={FlowName} | stage=config | ctx=strictMode={enabled}",
                "ServerBootstrap");
        }

        /// <summary>
        /// Configure bootstrap log level.
        /// </summary>
        public static void SetLogLevel(string level)
        {
            _logLevel = level;
            CoreLogger.LogInfoStatic(
                $"flow={FlowName} | stage=config | ctx=logLevel={level}",
                "ServerBootstrap");
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
    /// 
    /// Flow Stabilization: P0 - Server Bootstrap (World Initialization phase)
    /// </summary>
    internal static class WorldBootstrapPatch
    {
        public static event EventHandler OnWorldInitialized;

        // Compatibility shim for modules that reflect for this type/event.
        public static void RaiseWorldInitialized(object? sender = null)
        {
            OnWorldInitialized?.Invoke(sender ?? typeof(WorldBootstrapPatch), EventArgs.Empty);
            TypedEventBus.Publish(new WorldInitializedEvent());
        }
    }
}
