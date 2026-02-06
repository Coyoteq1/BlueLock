using HarmonyLib;
using ProjectM;

namespace VAuto.Core.Harmony
{
    /// <summary>
    /// Intercepts server shutdown events to inject custom logic.
    /// Allows for graceful shutdown, data saving, and player notifications.
    /// NOTE: This file uses string-based type lookups to avoid compile-time dependency issues.
    /// The actual patching happens at runtime when ProjectM types are available.
    /// </summary>
    [HarmonyPatch]
    public class ServerShutdownInterceptor
    {
        /// <summary>
        /// Initialize the shutdown interceptor.
        /// </summary>
        public static void Initialize()
        {
            Plugin.Log.LogInfo("[ServerShutdownInterceptor] Initialized");
        }
        
        /// <summary>
        /// Prefix method - called before the original method.
        /// </summary>
        static bool Prefix(object __instance)
        {
            // Check if shutdown should be cancelled or modified
            if (ShouldCancelShutdown())
            {
                Plugin.Log.LogInfo("[ServerShutdown] Shutdown cancelled by VAutomationEvents");
                return false; // Skip original method
            }
            
            // Inject custom countdown messages before shutdown
            InjectShutdownMessages(__instance);
            
            // Allow original shutdown to proceed
            return true;
        }
        
        private static bool ShouldCancelShutdown()
        {
            // Check for pending operations that require cancellation
            // For example: ongoing arena matches, player votes, etc.
            return false;
        }
        
        private static void InjectShutdownMessages(object system)
        {
            Plugin.Log.LogInfo("[ServerShutdown] Custom shutdown message injected");
        }
    }
}
