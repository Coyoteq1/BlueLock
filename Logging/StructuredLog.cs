using System;
using Unity.Entities;

namespace VAuto.Core.Logging
{
    /// <summary>
    /// Structured logging helpers for ECS diagnostics.
    /// </summary>
    public static class StructuredLog
    {
        public static void SystemInit(string systemName)
        {
            VAutoLogger.LogInfo(systemName, "Initialized");
        }

        public static void SystemWarning(string systemName, string message)
        {
            VAutoLogger.LogWarning(systemName, message);
        }

        public static void SystemError(string systemName, string message, Exception? ex = null)
        {
            VAutoLogger.LogError(systemName, message, ex);
        }

        public static void EntityQuery(string systemName, EntityQuery query)
        {
            var count = query.CalculateEntityCount();
            VAutoLogger.LogDebug(systemName, $"[QUERY] {count} entities matched");
        }

        public static void ComponentMutation(Entity entity, string componentName, string operation)
        {
            VAutoLogger.LogDebug("ECS", $"[{operation}] {componentName}", entity);
        }
    }
}
