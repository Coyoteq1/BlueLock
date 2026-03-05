using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Api
{
    /// <summary>
    /// Registry for tracking module IDs and preventing collisions.
    /// Implements Rule 21: Never Share Module IDs Across Mods
    /// </summary>
    public static class ModuleIdRegistry
    {
        private static readonly ConcurrentDictionary<string, ModuleRegistration> RegisteredModules = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object RegistrationSync = new();
        private static bool _initialized;
        
        private static CoreLogger _logger;

        /// <summary>
        /// Known module IDs that should not collide.
        /// </summary>
        public static class KnownModules
        {
            public const string Blueluck = "blueluck.zones";

            [Obsolete("Use Blueluck.")]
            public const string BlueLock = Blueluck;
            public const string VAutomationCore = "vautomationcore";
            
            /// <summary>
            /// Reserved key prefixes per module for flow ownership.
            /// </summary>
            public static readonly Dictionary<string, string[]> ReservedKeyPrefixes = new(StringComparer.OrdinalIgnoreCase)
            {
                [Blueluck] = new[] { "zone.enter.", "zone.exit.", "A1.", "B1.", "T3.", "ZoneDefault." }
            };
        }

        /// <summary>
        /// Represents a registered module.
        /// </summary>
        public class ModuleRegistration
        {
            public string ModuleId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Version { get; set; } = "1.0.0";
            public DateTime RegisteredAt { get; set; }
            public bool IsCore { get; set; }
        }

        /// <summary>
        /// Initializes the module registry with logging.
        /// </summary>
        public static void Initialize(CoreLogger logger)
        {
            if (_initialized)
            {
                return;
            }

            _logger = logger;
            
            // Register VAutomationCore as the base module
            RegisterModule(KnownModules.VAutomationCore, "VAutomationCore", "1.0.1", isCore: true);
            
            _initialized = true;
            _logger?.LogInfo($"[ModuleIdRegistry] Initialized with {RegisteredModules.Count} registered modules");
        }

        /// <summary>
        /// Registers a module ID. Returns false if the module ID is already registered by another module.
        /// </summary>
        /// <param name="moduleId">The unique module identifier</param>
        /// <param name="displayName">Human-readable display name</param>
        /// <param name="version">Module version</param>
        /// <param name="isCore">Whether this is a core module</param>
        /// <returns>True if registration succeeded, false if collision detected</returns>
        public static bool RegisterModule(string moduleId, string displayName, string version = "1.0.0", bool isCore = false)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                _logger?.LogWarning("[ModuleIdRegistry] Cannot register module with empty ID");
                return false;
            }

            var registration = new ModuleRegistration
            {
                ModuleId = moduleId.Trim(),
                DisplayName = displayName ?? moduleId,
                Version = version ?? "1.0.0",
                RegisteredAt = DateTime.UtcNow,
                IsCore = isCore
            };

            lock (RegistrationSync)
            {
                if (RegisteredModules.TryGetValue(moduleId, out var existing))
                {
                    // Allow re-registration of same module
                    if (string.Equals(existing.DisplayName, registration.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogDebug($"[ModuleIdRegistry] Module '{moduleId}' already registered, updating version to {version}");
                        RegisteredModules[moduleId] = registration;
                        return true;
                    }

                    // Collision detected
                    _logger?.LogError($"[ModuleIdRegistry] MODULE ID COLLISION: '{moduleId}' already registered by '{existing.DisplayName}', cannot register '{displayName}'");
                    return false;
                }

                RegisteredModules[moduleId] = registration;
                _logger?.LogInfo($"[ModuleIdRegistry] Registered module: {moduleId} ({displayName}) v{version}");
                return true;
            }
        }

        /// <summary>
        /// Unregisters a module by ID.
        /// </summary>
        /// <param name="moduleId">The module ID to unregister</param>
        /// <returns>True if unregistered successfully</returns>
        public static bool UnregisterModule(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return false;
            }

            lock (RegistrationSync)
            {
                // Don't allow unregistering core modules
                if (RegisteredModules.TryGetValue(moduleId, out var existing) && existing.IsCore)
                {
                    _logger?.LogWarning($"[ModuleIdRegistry] Cannot unregister core module: {moduleId}");
                    return false;
                }

                var removed = RegisteredModules.TryRemove(moduleId, out _);
                if (removed)
                {
                    _logger?.LogInfo($"[ModuleIdRegistry] Unregistered module: {moduleId}");
                }
                return removed;
            }
        }

        /// <summary>
        /// Checks if a module ID is registered.
        /// </summary>
        /// <param name="moduleId">The module ID to check</param>
        /// <returns>True if registered</returns>
        public static bool IsRegistered(string moduleId)
        {
            return !string.IsNullOrWhiteSpace(moduleId) && RegisteredModules.ContainsKey(moduleId);
        }

        /// <summary>
        /// Gets a module registration by ID.
        /// </summary>
        /// <param name="moduleId">The module ID</param>
        /// <param name="registration">The registration if found</param>
        /// <returns>True if found</returns>
        public static bool TryGetRegistration(string moduleId, out ModuleRegistration registration)
        {
            return RegisteredModules.TryGetValue(moduleId ?? string.Empty, out registration);
        }

        /// <summary>
        /// Gets all registered module IDs.
        /// </summary>
        /// <returns>List of registered module IDs</returns>
        public static IReadOnlyList<string> GetRegisteredModuleIds()
        {
            return RegisteredModules.Keys.ToList();
        }

        /// <summary>
        /// Validates that a flow key doesn't conflict with another module's reserved keys.
        /// Rule 30: Registry Ownership Matrix
        /// </summary>
        /// <param name="moduleId">The owning module ID</param>
        /// <param name="flowKey">The flow key to validate</param>
        /// <returns>ValidationResult indicating if the key is valid</returns>
        public static ValidationResult ValidateFlowKeyOwnership(string moduleId, string flowKey)
        {
            if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(flowKey))
            {
                return new ValidationResult(false, "Module ID and flow key are required");
            }

            // Check if another module owns this key
            if (FlowService.FlowModuleOwners.TryGetValue(flowKey, out var owner) && 
                !string.Equals(owner, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(false, $"Flow key '{flowKey}' is already owned by '{owner}'");
            }

            // Check reserved prefixes
            foreach (var kvp in KnownModules.ReservedKeyPrefixes)
            {
                if (string.Equals(kvp.Key, moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip own prefixes
                }

                foreach (var prefix in kvp.Value)
                {
                    if (flowKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if another module has claimed this
                        if (!string.Equals(kvp.Key, moduleId, StringComparison.OrdinalIgnoreCase))
                        {
                            return new ValidationResult(false, $"Flow key '{flowKey}' matches reserved prefix '{prefix}' from module '{kvp.Key}'");
                        }
                    }
                }
            }

            return new ValidationResult(true, "Flow key is valid");
        }

        /// <summary>
        /// Logs all registered modules and their ownership.
        /// </summary>
        public static void LogRegistryState()
        {
            _logger?.LogInfo($"[ModuleIdRegistry] Registry State ({RegisteredModules.Count} modules):");
            foreach (var kvp in RegisteredModules)
            {
                _logger?.LogInfo($"  - {kvp.Key}: {kvp.Value.DisplayName} v{kvp.Value.Version} (Core: {kvp.Value.IsCore})");
            }

            // Log flow ownership
            var ownerGroups = FlowService.FlowModuleOwners
                .GroupBy(kvp => kvp.Value)
                .OrderBy(g => g.Key);
            
            _logger?.LogInfo($"[ModuleIdRegistry] Flow Ownership:");
            foreach (var group in ownerGroups)
            {
                _logger?.LogInfo($"  {group.Key}: {group.Count()} flows");
            }
        }

        /// <summary>
        /// Result of a validation operation.
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; }
            public string Message { get; }

            public ValidationResult(bool isValid, string message)
            {
                IsValid = isValid;
                Message = message;
            }
        }
    }
}
