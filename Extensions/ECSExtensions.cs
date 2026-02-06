using System;
using Unity.Collections;
using Unity.Entities;
using Il2CppInterop.Runtime;

namespace VAuto.Extensions
{
    /// <summary>
    /// IL2CPP-compatible ECS extension methods for Entity operations
    /// </summary>
    public static class ECSExtensions
    {
        /// <summary>
        /// Safe entity read component (IL2CPP compatible)
        /// </summary>
        public static T Read<T>(this Entity entity, EntityManager em) where T : struct
        {
            try
            {
                if (!em.Exists(entity))
                {
                    Plugin.Log.LogWarning($"[ECSExtensions] Attempting to read from non-existent entity: {entity}");
                    return default;
                }

                if (em.HasComponent<T>(entity))
                {
                    return em.GetComponentData<T>(entity);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ECSExtensions] Error reading component {typeof(T).Name} from entity {entity}: {ex.Message}");
            }
            return default;
        }

        /// <summary>
        /// Safe entity write component using EntityCommandBuffer for thread safety (IL2CPP compatible)
        /// </summary>
        public static void Write<T>(this Entity entity, EntityManager em, T component) where T : struct
        {
            try
            {
                if (!em.Exists(entity))
                {
                    Plugin.Log.LogWarning($"[ECSExtensions] Attempting to write to non-existent entity: {entity}");
                    return;
                }

                var ecb = new EntityCommandBuffer(Allocator.Temp);
                if (em.HasComponent<T>(entity))
                {
                    ecb.SetComponent(entity, component);
                }
                else
                {
                    ecb.AddComponent(entity, component);
                }
                ecb.Playback(em);
                ecb.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ECSExtensions] Error writing component {typeof(T).Name} to entity {entity}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get entities by component type using IL2CPP-safe EntityQueryBuilder
        /// </summary>
        public static NativeArray<Entity> GetEntitiesByComponentType<T>(EntityManager em, bool includeDisabled = false) where T : struct
        {
            EntityQueryOptions options = EntityQueryOptions.Default;
            if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;

            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .AddAll(new(Il2CppType.Of<T>(), ComponentType.AccessMode.ReadWrite));

            var query = em.CreateEntityQuery(ref entityQueryBuilder);
            var entities = query.ToEntityArray(Allocator.Temp);
            entityQueryBuilder.Dispose();
            return entities;
        }

        /// <summary>
        /// Get entities by component types using IL2CPP-safe EntityQueryBuilder
        /// </summary>
        public static NativeArray<Entity> GetEntitiesByComponentTypes<T1, T2>(EntityManager em, bool includeDisabled = false)
            where T1 : struct
            where T2 : struct
        {
            EntityQueryOptions options = EntityQueryOptions.Default;
            if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;

            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .AddAll(new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite))
                .AddAll(new(Il2CppType.Of<T2>(), ComponentType.AccessMode.ReadWrite));

            var query = em.CreateEntityQuery(ref entityQueryBuilder);
            var entities = query.ToEntityArray(Allocator.Temp);
            entityQueryBuilder.Dispose();
            return entities;
        }
    }
}
