using System;
using Unity.Collections;
using Unity.Entities;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;

namespace VAuto.Core
{
    /// <summary>
    /// Enhanced ECS helper using KindredArenas patterns for V Rising IL2CPP environment.
    /// Provides flexible entity queries with proper IL2CPP component type handling.
    /// </summary>
    public static class ECSHelper
    {
        /// <summary>
        /// Gets entities by two component types with flexible query options.
        /// </summary>
        public static NativeArray<Entity> GetEntitiesByComponentTypes<T1, T2>(
            bool includeAll = false, 
            bool includeDisabled = false, 
            bool includeSpawn = false, 
            bool includePrefab = false, 
            bool includeDestroyed = false)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            EntityQueryOptions options = EntityQueryOptions.Default;
            if (includeAll) options |= EntityQueryOptions.IncludeAll;
            if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;
            if (includeSpawn) options |= EntityQueryOptions.IncludeSpawnTag;
            if (includePrefab) options |= EntityQueryOptions.IncludePrefab;
            if (includeDestroyed) options |= EntityQueryOptions.IncludeDestroyTag;

            EntityQueryDesc queryDesc = new()
            {
                All = new ComponentType[] { 
                    new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite), 
                    new(Il2CppType.Of<T2>(), ComponentType.AccessMode.ReadWrite) 
                },
                Options = options
            };

            var entityManager = VRCore.EntityManager;
            var query = entityManager.CreateEntityQuery(queryDesc);
            var entities = query.ToEntityArray(Allocator.Temp);
            query.Dispose();
            return entities;
        }

        /// <summary>
        /// Gets entities by a single component type with flexible query options.
        /// </summary>
        public static NativeArray<Entity> GetEntitiesByComponentType<T1>(
            bool includeAll = false, 
            bool includeDisabled = false, 
            bool includeSpawn = false, 
            bool includePrefab = false, 
            bool includeDestroyed = false)
            where T1 : unmanaged
        {
            EntityQueryOptions options = EntityQueryOptions.Default;
            if (includeAll) options |= EntityQueryOptions.IncludeAll;
            if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;
            if (includeSpawn) options |= EntityQueryOptions.IncludeSpawnTag;
            if (includePrefab) options |= EntityQueryOptions.IncludePrefab;
            if (includeDestroyed) options |= EntityQueryOptions.IncludeDestroyTag;

            EntityQueryDesc queryDesc = new()
            {
                All = new ComponentType[] { 
                    new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite) 
                },
                Options = options
            };

            var entityManager = VRCore.EntityManager;
            var query = entityManager.CreateEntityQuery(queryDesc);
            var entities = query.ToEntityArray(Allocator.Temp);
            query.Dispose();
            return entities;
        }

        /// <summary>
        /// Gets entities by component type with read-only access mode.
        /// </summary>
        public static NativeArray<Entity> GetEntitiesByComponentTypeReadOnly<T1>(
            bool includeAll = false, 
            bool includeDisabled = false, 
            bool includeSpawn = false, 
            bool includePrefab = false, 
            bool includeDestroyed = false)
            where T1 : unmanaged
        {
            EntityQueryOptions options = EntityQueryOptions.Default;
            if (includeAll) options |= EntityQueryOptions.IncludeAll;
            if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;
            if (includeSpawn) options |= EntityQueryOptions.IncludeSpawnTag;
            if (includePrefab) options |= EntityQueryOptions.IncludePrefab;
            if (includeDestroyed) options |= EntityQueryOptions.IncludeDestroyTag;

            EntityQueryDesc queryDesc = new()
            {
                All = new ComponentType[] { 
                    new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadOnly) 
                },
                Options = options
            };

            var entityManager = VRCore.EntityManager;
            var query = entityManager.CreateEntityQuery(queryDesc);
            var entities = query.ToEntityArray(Allocator.Temp);
            query.Dispose();
            return entities;
        }

        /// <summary>
        /// Creates a simple entity query for common use cases.
        /// </summary>
        public static EntityQuery CreateEntityQuery<T1>(ComponentType.AccessMode accessMode = ComponentType.AccessMode.ReadWrite)
            where T1 : unmanaged
        {
            var entityManager = VRCore.EntityManager;
            return entityManager.CreateEntityQuery(
                new ComponentType(Il2CppType.Of<T1>(), accessMode)
            );
        }

        /// <summary>
        /// Creates a simple entity query for two component types.
        /// </summary>
        public static EntityQuery CreateEntityQuery<T1, T2>(
            ComponentType.AccessMode accessMode1 = ComponentType.AccessMode.ReadWrite,
            ComponentType.AccessMode accessMode2 = ComponentType.AccessMode.ReadWrite)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            var entityManager = VRCore.EntityManager;
            return entityManager.CreateEntityQuery(
                new ComponentType(Il2CppType.Of<T1>(), accessMode1),
                new ComponentType(Il2CppType.Of<T2>(), accessMode2)
            );
        }

        /// <summary>
        /// Safely sends a system message to a client.
        /// </summary>
        public static void SendSystemMessageToClient(EntityManager entityManager, User user, string message)
        {
            var msg = new FixedString512Bytes(message);
            ServerChatUtils.SendSystemMessageToClient(entityManager, user, ref msg);
        }

        /// <summary>
        /// Safely sends a system message to a client using VRCore.
        /// </summary>
        public static void SendSystemMessageToClient(User user, string message)
        {
            var entityManager = VRCore.EntityManager;
            SendSystemMessageToClient(entityManager, user, message);
        }

        /// <summary>
        /// Checks if an entity has a specific component.
        /// </summary>
        public static bool HasComponent<T>(Entity entity) where T : unmanaged
        {
            var entityManager = VRCore.EntityManager;
            return entityManager.HasComponent<T>(entity);
        }

        /// <summary>
        /// Gets component data from an entity safely.
        /// </summary>
        public static bool TryGetComponent<T>(Entity entity, out T component) where T : unmanaged
        {
            var entityManager = VRCore.EntityManager;
            return entityManager.TryGetComponent<T>(entity, out component);
        }

        /// <summary>
        /// Disposes a NativeArray safely with error handling.
        /// </summary>
        public static void SafeDispose<T>(ref NativeArray<T> array) where T : unmanaged
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }
    }
}
