using System;

namespace VAutomation.Core.Configuration
{
    /// <summary>
    /// Public interface for VAuto configuration service.
    /// This is the stable contract that third-party mods should reference.
    /// </summary>
    public interface IVAutoConfigService
    {
        /// <summary>
        /// Gets the configuration for a module.
        /// </summary>
        /// <typeparam name="T">The configuration type.</typeparam>
        /// <param name="moduleName">Name of the module (e.g., "Zones", "Traps").</param>
        /// <returns>The configuration object.</returns>
        T GetConfig<T>(string moduleName) where T : class, new();

        /// <summary>
        /// Tries to get the configuration for a module.
        /// </summary>
        /// <typeparam name="T">The configuration type.</typeparam>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="config">The configuration object if found.</param>
        /// <returns>True if configuration was found.</returns>
        bool TryGetConfig<T>(string moduleName, out T config) where T : class, new();

        /// <summary>
        /// Reloads configuration from disk.
        /// </summary>
        /// <param name="moduleName">Name of the module to reload.</param>
        void Reload(string moduleName);

        /// <summary>
        /// Reloads all configurations.
        /// </summary>
        void ReloadAll();
    }

    /// <summary>
    /// Interface for configuration validators.
    /// </summary>
    /// <typeparam name="T">The configuration type to validate.</typeparam>
    public interface IConfigValidator<T> where T : class
    {
        /// <summary>
        /// Validates the configuration. Throws exception if invalid.
        /// </summary>
        void Validate(T config);
    }

    /// <summary>
    /// Interface for configuration migrators.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    public interface IConfigMigrator<T> where T : class
    {
        /// <summary>
        /// Gets the current config version for this configuration type.
        /// </summary>
        string CurrentVersion { get; }

        /// <summary>
        /// Checks if migration is needed.
        /// </summary>
        bool NeedsMigration(T config);

        /// <summary>
        /// Migrates the configuration to the current version.
        /// </summary>
        void Migrate(T config);
    }

    /// <summary>
    /// Interface for listening to configuration reload events.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    public interface IConfigReloadListener<T> where T : class
    {
        /// <summary>
        /// Called when configuration is reloaded.
        /// </summary>
        void OnReloaded(T newConfig);
    }
}
