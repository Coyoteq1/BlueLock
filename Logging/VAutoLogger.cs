using System;
using Unity.Entities;

namespace VAuto.Core.Logging
{
    /// <summary>
    /// Centralized logging system for VAutomationEvents
    /// </summary>
    public static class VAutoLogger
    {
        private const string LOG_PREFIX = "[VAuto]";
        
        public static void LogInfo(string component, string message, Entity? entity = null)
        {
            var entityInfo = entity.HasValue ? $" [Entity:{entity.Value.Index}]" : "";
            Plugin.Log?.LogInfo($"{LOG_PREFIX}[{component}]{entityInfo} {message}");
        }
        
        public static void LogWarning(string component, string message, Entity? entity = null)
        {
            var entityInfo = entity.HasValue ? $" [Entity:{entity.Value.Index}]" : "";
            Plugin.Log?.LogWarning($"{LOG_PREFIX}[{component}]{entityInfo} {message}");
        }
        
        public static void LogError(string component, string message, Exception? ex = null, Entity? entity = null)
        {
            var entityInfo = entity.HasValue ? $" [Entity:{entity.Value.Index}]" : "";
            var errorInfo = ex != null ? $" Exception: {ex.Message}" : "";
            Plugin.Log?.LogError($"{LOG_PREFIX}[{component}]{entityInfo} {message}{errorInfo}");
        }
        
        public static void LogDebug(string component, string message, Entity? entity = null)
        {
            #if DEBUG
            var entityInfo = entity.HasValue ? $" [Entity:{entity.Value.Index}]" : "";
            Plugin.Log?.LogInfo($"{LOG_PREFIX}[DEBUG][{component}]{entityInfo} {message}");
            #endif
        }
    }
}
