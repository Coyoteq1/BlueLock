using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Unity.Entities;
using Unity.Mathematics;

namespace VAuto.Services.Interfaces
{
    /// <summary>
    /// Base interface for all services
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Gets whether the service is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Gets the service logger
        /// </summary>
        ManualLogSource Log { get; }

        /// <summary>
        /// Initializes the service
        /// </summary>
        void Initialize();

        /// <summary>
        /// Cleans up the service
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// Interface for services that manage entities
    /// </summary>
    public interface IEntityService : IService
    {
        /// <summary>
        /// Registers an entity with the service
        /// </summary>
        /// <param name="entity">The entity to register</param>
        /// <returns>True if registration was successful</returns>
        bool RegisterEntity(Entity entity);

        /// <summary>
        /// Unregisters an entity from the service
        /// </summary>
        /// <param name="entity">The entity to unregister</param>
        /// <returns>True if unregistration was successful</returns>
        bool UnregisterEntity(Entity entity);

        /// <summary>
        /// Gets the count of registered entities
        /// </summary>
        int GetEntityCount();
    }

    /// <summary>
    /// Interface for services that manage arena functionality
    /// </summary>
    public interface IArenaService : IEntityService
    {
        /// <summary>
        /// Creates a new arena
        /// </summary>
        /// <param name="arenaId">Unique arena identifier</param>
        /// <param name="center">Arena center position</param>
        /// <param name="radius">Arena radius</param>
        /// <returns>True if arena creation was successful</returns>
        bool CreateArena(string arenaId, float3 center, float radius);

        /// <summary>
        /// Deletes an arena
        /// </summary>
        /// <param name="arenaId">Arena identifier</param>
        /// <returns>True if deletion was successful</returns>
        bool DeleteArena(string arenaId);

        /// <summary>
        /// Gets all arena IDs
        /// </summary>
        /// <returns>List of arena IDs</returns>
        System.Collections.Generic.List<string> GetArenaIds();

        /// <summary>
        /// Gets the count of active arenas
        /// </summary>
        /// <returns>Number of active arenas</returns>
        int GetArenaCount();
    }

    /// <summary>
    /// Interface for services that manage player data
    /// </summary>
    public interface IPlayerService : IService
    {
        /// <summary>
        /// Adds a player to the service
        /// </summary>
        /// <param name="platformId">Player platform ID</param>
        /// <param name="characterName">Player character name</param>
        /// <returns>True if player was added successfully</returns>
        bool AddPlayer(ulong platformId, string characterName);

        /// <summary>
        /// Removes a player from the service
        /// </summary>
        /// <param name="platformId">Player platform ID</param>
        /// <returns>True if player was removed successfully</returns>
        bool RemovePlayer(ulong platformId);

        /// <summary>
        /// Gets all player IDs
        /// </summary>
        /// <returns>List of player IDs</returns>
        System.Collections.Generic.List<ulong> GetPlayerIds();

        /// <summary>
        /// Gets the count of players
        /// </summary>
        /// <returns>Number of players</returns>
        int GetPlayerCount();

        /// <summary>
        /// Checks if a player exists
        /// </summary>
        /// <param name="platformId">Player platform ID</param>
        /// <returns>True if player exists</returns>
        bool PlayerExists(ulong platformId);
    }

    /// <summary>
    /// Interface for services that manage data persistence
    /// </summary>
    public interface IDataPersistenceService : IService
    {
        /// <summary>
        /// Saves data with the specified key
        /// </summary>
        /// <param name="key">Data key</param>
        /// <param name="data">Data to save</param>
        /// <returns>True if save was successful</returns>
        bool SaveData(string key, object data);

        /// <summary>
        /// Loads data with the specified key
        /// </summary>
        /// <param name="key">Data key</param>
        /// <param name="dataType">Type of data to load</param>
        /// <returns>Loaded data or null</returns>
        object LoadData(string key, Type dataType);

        /// <summary>
        /// Deletes data with the specified key
        /// </summary>
        /// <param name="key">Data key</param>
        /// <returns>True if deletion was successful</returns>
        bool DeleteData(string key);

        /// <summary>
        /// Checks if data exists for the specified key
        /// </summary>
        /// <param name="key">Data key</param>
        /// <returns>True if data exists</returns>
        bool DataExists(string key);
    }

    /// <summary>
    /// Interface for services that manage component data
    /// </summary>
    public interface IComponentService : IService
    {
        /// <summary>
        /// Saves component data for an entity
        /// </summary>
        /// <param name="entity">Entity to save components for</param>
        /// <param name="saveId">Optional save ID</param>
        /// <returns>True if save was successful</returns>
        bool SaveComponents(Entity entity, string saveId = null);

        /// <summary>
        /// Restores component data for an entity
        /// </summary>
        /// <param name="entity">Entity to restore components for</param>
        /// <param name="saveId">Optional save ID</param>
        /// <returns>True if restore was successful</returns>
        bool RestoreComponents(Entity entity, string saveId = null);

        /// <summary>
        /// Deletes saved component data
        /// </summary>
        /// <param name="entity">Entity to delete data for</param>
        /// <returns>True if deletion was successful</returns>
        bool DeleteComponents(Entity entity);
    }

    /// <summary>
    /// Interface for services that manage build functionality
    /// </summary>
    public interface IBuildService : IService
    {
        /// <summary>
        /// Builds a structure
        /// </summary>
        /// <param name="user">User entity</param>
        /// <param name="structureName">Name of structure to build</param>
        /// <param name="position">Position to build at</param>
        /// <param name="rotation">Optional rotation</param>
        /// <returns>True if build was successful</returns>
        bool BuildStructure(Entity user, string structureName, float3 position, quaternion? rotation = null);

        /// <summary>
        /// Removes a structure
        /// </summary>
        /// <param name="structureName">Name of structure to remove</param>
        /// <returns>True if removal was successful</returns>
        bool RemoveStructure(string structureName);

        /// <summary>
        /// Gets available structures
        /// </summary>
        /// <returns>List of available structure names</returns>
        System.Collections.Generic.List<string> GetAvailableStructures();

        /// <summary>
        /// Sets player build permission
        /// </summary>
        /// <param name="platformId">Player platform ID</param>
        /// <param name="canBuild">Whether the player can build</param>
        void SetBuildPermission(ulong platformId, bool canBuild);
    }

    /// <summary>
    /// Interface for services that manage visual effects
    /// </summary>
    public interface IVisualEffectService : IService
    {
        /// <summary>
        /// Creates a visual effect
        /// </summary>
        /// <param name="name">Effect name</param>
        /// <param name="position">Effect position</param>
        /// <param name="type">Effect type</param>
        /// <param name="color">Effect color</param>
        /// <returns>True if creation was successful</returns>
        bool CreateEffect(string name, float3 position, object type, float4 color);

        /// <summary>
        /// Removes a visual effect
        /// </summary>
        /// <param name="name">Effect name</param>
        /// <returns>True if removal was successful</returns>
        bool RemoveEffect(string name);

        /// <summary>
        /// Updates a visual effect
        /// </summary>
        /// <param name="name">Effect name</param>
        /// <param name="position">New position</param>
        /// <param name="color">New color</param>
        /// <returns>True if update was successful</returns>
        bool UpdateEffect(string name, float3? position = null, float4? color = null);

        /// <summary>
        /// Gets active effect count
        /// </summary>
        /// <returns>Number of active effects</returns>
        int GetActiveEffectCount();
    }

    /// <summary>
    /// Interface for services that manage database operations
    /// </summary>
    public interface IDatabaseService : IDataPersistenceService
    {
        /// <summary>
        /// Saves arena state
        /// </summary>
        /// <param name="arenaId">Arena ID</param>
        /// <param name="arenaState">Arena state data</param>
        /// <returns>True if save was successful</returns>
        bool SaveArenaState(string arenaId, object arenaState);

        /// <summary>
        /// Loads arena state
        /// </summary>
        /// <param name="arenaId">Arena ID</param>
        /// <returns>Loaded arena state or null</returns>
        object LoadArenaState(string arenaId);

        /// <summary>
        /// Saves player data
        /// </summary>
        /// <param name="platformId">Player platform ID</param>
        /// <param name="playerData">Player data</param>
        /// <returns>True if save was successful</returns>
        bool SavePlayerData(ulong platformId, object playerData);

        /// <summary>
        /// Loads player data
        /// </summary>
        /// <param name="platformId">Player platform ID</param>
        /// <returns>Loaded player data or null</returns>
        object LoadPlayerData(ulong platformId);
    }

    /// <summary>
    /// Interface for health monitoring of services
    /// </summary>
    public interface IServiceHealthMonitor
    {
        /// <summary>
        /// Gets service health status
        /// </summary>
        /// <returns>Health status information</returns>
        ServiceHealthStatus GetHealthStatus();

        /// <summary>
        /// Gets service performance metrics
        /// </summary>
        /// <returns>Performance metrics</returns>
        ServicePerformanceMetrics GetPerformanceMetrics();

        /// <summary>
        /// Gets service error count
        /// </summary>
        /// <returns>Number of errors</returns>
        int GetErrorCount();

        /// <summary>
        /// Gets last error message
        /// </summary>
        /// <returns>Last error message or null</returns>
        string GetLastError();
    }

    /// <summary>
    /// Service health status information
    /// </summary>
    public class ServiceHealthStatus
    {
        public string ServiceName { get; set; }
        public bool IsHealthy { get; set; }
        public string Status { get; set; }
        public DateTime LastCheck { get; set; }
        public System.Collections.Generic.List<string> Issues { get; set; } = new();
    }

    /// <summary>
    /// Service performance metrics
    /// </summary>
    public class ServicePerformanceMetrics
    {
        public string ServiceName { get; set; }
        public double AverageResponseTime { get; set; }
        public int RequestsPerSecond { get; set; }
        public long MemoryUsage { get; set; }
        public int ActiveOperations { get; set; }
        public DateTime MeasuredAt { get; set; }
    }

    /// <summary>
    /// Additional service interfaces for dependency injection
    /// </summary>

    /// <summary>
    /// Lifecycle management service interface
    /// </summary>
    public interface ILifecycleService : IService
    {
        bool EnterArena(Entity user, Entity character);
        bool ExitArena(Entity user, Entity character);
        bool IsPlayerInArena(ulong platformId);
    }

    /// <summary>
    /// Zone management service interface
    /// </summary>
    public interface IZoneService : IService
    {
        bool IsInZone(Entity user, string zoneName);
        void EnterZone(Entity user, string zoneName);
        void ExitZone(Entity user, string zoneName);
        bool IsInArena(float3 position);
        float GetDistanceToCenter(float3 position);
        bool IsInTransitionZone(float3 position);
        float3 Center { get; set; }
        float Radius { get; set; }
        float3 SpawnPoint { get; set; }
        float ExitRadius { get; set; }
        void SetArenaZone(float3 center, float radius);
        void SetSpawn(float3 spawn);
        bool CreateZone(string name, float3 center, float radius);
    }

    /// <summary>
    /// Healing service interface
    /// </summary>
    public interface IHealingService : IService
    {
        void ApplyHeal(Entity character);
        void HealPlayer(Entity user, float amount);
        void SetHealth(Entity user, float health);
    }

    /// <summary>
    /// Teleport service interface
    /// </summary>
    public interface ITeleportService : IService
    {
        bool Teleport(Entity characterEntity, float3 position);
        void TeleportTo(Entity user, float3 position);
        bool CanTeleport(Entity user, float3 position);
    }
}
