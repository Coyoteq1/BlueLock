using System;
using Unity.Entities;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Api;

namespace VAutomationCore.Core.Utilities
{
    /// <summary>
    /// Utility class for automation tasks, particularly for executing VCF commands.
    /// </summary>
    public static class AutomationUtils
    {
        private static readonly CoreLogger _log = new CoreLogger("AutomationUtils");

        /// <summary>
        /// Attempts to execute a VCF command.
        /// </summary>
        /// <param name="entityManager">The entity manager to use for command execution.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>True if the command was executed successfully, false otherwise.</returns>
        public static bool TryExecVcf(EntityManager entityManager, string command)
        {
            try
            {
                if (string.IsNullOrEmpty(command))
                {
                    _log.Error("Command is null or empty");
                    return false;
                }

                if (!UnifiedCore.IsInitialized || entityManager == default)
                {
                    _log.Error("UnifiedCore is not initialized or EntityManager is invalid");
                    return false;
                }

                // Reflection-based best effort call into VCF internals.
                var registryType = Type.GetType("VampireCommandFramework.CommandRegistry, VampireCommandFramework");
                if (registryType == null)
                {
                    _log.Warning("VCF CommandRegistry type not found");
                    return false;
                }

                foreach (var method in registryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
                {
                    if (!method.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) &&
                        !method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        method.Invoke(null, new object[] { command });
                        return true;
                    }
                }

                _log.Warning("No compatible VCF execution method found via reflection");
                return false;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to execute VCF command '{command}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes multiple VCF commands in sequence.
        /// </summary>
        /// <param name="entityManager">The entity manager to use for command execution.</param>
        /// <param name="commands">The commands to execute.</param>
        /// <returns>True if all commands were executed successfully, false otherwise.</returns>
        public static bool ExecMany(EntityManager entityManager, params string[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                _log.Error("No commands provided to ExecMany");
                return false;
            }

            bool allSuccessful = true;
            
            foreach (var command in commands)
            {
                if (!string.IsNullOrEmpty(command))
                {
                    if (!TryExecVcf(entityManager, command.Trim()))
                    {
                        allSuccessful = false;
                        _log.Error($"Failed to execute command: {command}");
                    }
                }
            }

            return allSuccessful;
        }
    }
}
