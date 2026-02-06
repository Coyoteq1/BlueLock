using System;
using Unity.Collections;
using Unity.Entities;
using Il2CppInterop.Runtime;

namespace VAuto.Core
{
    /// <summary>
    /// Helper methods for EntityQuery creation (1.1 compatible)
    /// </summary>
    public static class EntityQueryHelper
    {
        /// <summary>
        /// Create EntityQuery using EntityQueryBuilder with Allocator.Temp (1.1 compatible)
        /// </summary>
        public static EntityQuery CreateQuery<T1>(EntityQueryOptions options = EntityQueryOptions.Default) where T1 : struct
        {
            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .AddAll(new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite))
                .WithOptions(options);
            return VAutoCore.EntityManager.CreateEntityQuery(ref entityQueryBuilder);
        }

        /// <summary>
        /// Create EntityQuery using EntityQueryBuilder with Allocator.Temp for multiple component types
        /// </summary>
        public static EntityQuery CreateQuery<T1, T2>(EntityQueryOptions options = EntityQueryOptions.Default) 
            where T1 : struct 
            where T2 : struct
        {
            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .AddAll(new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite))
                .AddAll(new(Il2CppType.Of<T2>(), ComponentType.AccessMode.ReadWrite))
                .WithOptions(options);
            return VAutoCore.EntityManager.CreateEntityQuery(ref entityQueryBuilder);
        }

        /// <summary>
        /// Create EntityQuery using EntityQueryBuilder with Allocator.Temp for three component types
        /// </summary>
        public static EntityQuery CreateQuery<T1, T2, T3>(EntityQueryOptions options = EntityQueryOptions.Default) 
            where T1 : struct 
            where T2 : struct
            where T3 : struct
        {
            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .AddAll(new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite))
                .AddAll(new(Il2CppType.Of<T2>(), ComponentType.AccessMode.ReadWrite))
                .AddAll(new(Il2CppType.Of<T3>(), ComponentType.AccessMode.ReadWrite))
                .WithOptions(options);
            return VAutoCore.EntityManager.CreateEntityQuery(ref entityQueryBuilder);
        }
    }
}
