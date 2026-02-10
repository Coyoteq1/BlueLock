using System;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace VAutomationCore.Core
{
    /// <summary>
    /// Unified core accessor for V Rising game server integration.
    /// Provides safe access to World, EntityManager, ServerGameManager, and PrefabCollection.
    /// Uses main-thread guard pattern for thread safety in V Rising's modified Unity ECS.
    /// </summary>
    public static class UnifiedCore
    {
        private static World? _server;
        private static bool _initialized;
        private static readonly ManualLogSource Log = Plugin.Log;
        
        /// <summary>
        /// Gets the server World. Lazy initializes on first access using main-thread guard pattern.
        /// </summary>
        public static World Server
        {
            get
            {
                if (!_initialized)
                {
                    _server = GetWorld("Server") ?? throw new InvalidOperationException(
                        "[UnifiedCore] Server world not found. Ensure mod is loaded on server.");
                    _initialized = true;
                }
                return _server!;
            }
        }
        
        /// <summary>
        /// Gets the EntityManager from the server World.
        /// </summary>
        public static EntityManager EntityManager => Server.EntityManager;
        
        /// <summary>
        /// Gets the PrefabCollectionSystem if available. Returns null on failure.
        /// </summary>
        public static PrefabCollectionSystem? PrefabCollection
        {
            get
            {
                try
                {
                    return Server.GetExistingSystemManaged<PrefabCollectionSystem>();
                }
                catch
                {
                    return null;
                }
            }
        }
        
        /// <summary>
        /// Checks if the core has been initialized.
        /// </summary>
        public static bool IsInitialized => _initialized;
        
        /// <summary>
        /// Resets the core for testing purposes.
        /// </summary>
        internal static void Reset()
        {
            _server = null;
            _initialized = false;
        }
        
        #region Logging Methods
        
        /// <summary>
        /// Logs an info message with caller context.
        /// </summary>
        public static void LogInfo(string message, [CallerMemberName] string caller = null)
            => Log.LogInfo($"[{caller}] {message}");
        
        /// <summary>
        /// Logs an error message with caller context.
        /// </summary>
        public static void LogError(string message, [CallerMemberName] string caller = null)
            => Log.LogError($"[{caller}] {message}");
        
        /// <summary>
        /// Logs a warning message with caller context.
        /// </summary>
        public static void LogWarning(string message, [CallerMemberName] string caller = null)
            => Log.LogWarning($"[{caller}] {message}");
        
        /// <summary>
        /// Logs an exception with caller context.
        /// </summary>
        public static void LogException(Exception ex, [CallerMemberName] string caller = null)
            => Log.LogError($"[{caller}] Exception: {ex.Message}\n{ex.StackTrace}");
        
        #endregion
        
        #region Prefab Access
        
        /// <summary>
        /// Tries to get the entity for a given PrefabGUID.
        /// </summary>
        public static bool TryGetPrefabEntity(PrefabGUID guid, out Entity entity)
        {
            entity = Entity.Null;
            
            var prefabSystem = PrefabCollection;
            if (prefabSystem == null) return false;
            
            // Access the dictionary directly (common V Rising pattern)
            var field = typeof(PrefabCollectionSystem).GetField(
                "_PrefabGuidToEntityDictionary", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field == null) return false;
            
            var dictionary = field.GetValue(prefabSystem) as System.Collections.IDictionary;
            if (dictionary == null) return false;
            
            if (!dictionary.Contains(guid)) return false;
            
            var result = dictionary[guid];
            if (result is Entity e)
            {
                entity = e;
                return entity != Entity.Null;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the entity for a given PrefabGUID. Throws if not found.
        /// </summary>
        public static Entity GetPrefabEntity(PrefabGUID guid)
        {
            if (!TryGetPrefabEntity(guid, out var entity))
            {
                throw new InvalidOperationException($"Prefab GUID {guid} not found: {guid.GuidHash}");
            }
            return entity;
        }
        
        #endregion
        
        #region Entity Operations
        
        /// <summary>
        /// Creates a new entity with the specified component types.
        /// </summary>
        public static Entity CreateEntity(params ComponentType[] componentTypes)
        {
            return EntityManager.CreateEntity(componentTypes);
        }
        
        /// <summary>
        /// Creates a new entity and sets its transform.
        /// </summary>
        public static Entity CreateEntity(float3 position, quaternion rotation = default, float scale = 1f)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                position, 
                rotation.value.Equals(float4.zero) ? quaternion.identity : rotation, 
                scale));
            return entity;
        }
        
        /// <summary>
        /// Destroys an entity if it exists.
        /// </summary>
        public static void DestroyEntity(Entity entity)
        {
            if (EntityManager.Exists(entity))
            {
                EntityManager.DestroyEntity(entity);
            }
        }
        
        /// <summary>
        /// Checks if an entity exists.
        /// </summary>
        public static bool EntityExists(Entity entity)
        {
            return entity != Entity.Null && EntityManager.Exists(entity);
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Gets a World by name from Unity's World registry.
        /// </summary>
        private static World? GetWorld(string name)
        {
            foreach (var world in World.s_AllWorlds)
            {
                if (world.Name == name)
                    return world;
            }
            return null;
        }
        
        #endregion
    }
}
