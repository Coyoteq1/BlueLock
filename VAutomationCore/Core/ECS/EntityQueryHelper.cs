using System;
using Unity.Collections;
using Unity.Entities;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.ECS
{
    /// <summary>
    /// Helper methods for EntityQuery operations.
    /// Provides safe synchronous query operations compatible with V Rising's modified Unity ECS.
    /// Note: Avoid async/await in ECS code - use sync processing instead.
    /// </summary>
    public static class EntityQueryHelper
    {
        private static readonly CoreLogger _log = new CoreLogger("EntityQueryHelper");
        
        /// <summary>
        /// Creates a query with All component types.
        /// </summary>
        public static EntityQuery CreateQuery(EntityManager em, params ComponentType[] allTypes)
        {
            return em.CreateEntityQuery(allTypes);
        }
        
        /// <summary>
        /// Creates a query with All/Any/None component constraints.
        /// </summary>
        public static EntityQuery CreateQuery(
            EntityManager em,
            ComponentType[] allTypes,
            ComponentType[]? anyTypes = null,
            ComponentType[]? noneTypes = null)
        {
            // Use EntityQueryDesc for complex queries
            var desc = new EntityQueryDesc
            {
                All = allTypes,
                Any = anyTypes,
                None = noneTypes
            };
            
            return em.CreateEntityQuery(desc);
        }
        
        /// <summary>
        /// Processes entities synchronously with a handler action.
        /// </summary>
        public static void ProcessEntities(
            EntityQuery query,
            Action<Entity> handler)
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            
            try
            {
                foreach (var entity in entities)
                {
                    if (entity != Entity.Null)
                    {
                        handler(entity);
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
        
        /// <summary>
        /// Processes entities with component data synchronously.
        /// </summary>
        public static void ProcessEntities<T>(
            EntityQuery query,
            Action<Entity, T> handler) where T : struct
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            var components = query.ToComponentDataArray<T>(Allocator.Temp);
            
            try
            {
                int count = Math.Min(entities.Length, components.Length);
                for (int i = 0; i < count; i++)
                {
                    if (entities[i] != Entity.Null)
                    {
                        handler(entities[i], components[i]);
                    }
                }
            }
            finally
            {
                entities.Dispose();
                components.Dispose();
            }
        }
        
        /// <summary>
        /// Processes entities in batches synchronously.
        /// Useful for large entity counts to allow frame updates.
        /// </summary>
        public static void ProcessEntitiesBatched<T>(
            EntityQuery query,
            Action<Entity, T> handler,
            int batchSize = 100) where T : struct
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            var components = query.ToComponentDataArray<T>(Allocator.Temp);
            
            try
            {
                int count = Math.Min(entities.Length, components.Length);
                int processed = 0;
                
                for (int i = 0; i < count; i++)
                {
                    if (entities[i] != Entity.Null)
                    {
                        handler(entities[i], components[i]);
                    }
                    
                    processed++;
                    
                    // Yield every batchSize entities to avoid frame drops
                    if (processed >= batchSize)
                    {
                        processed = 0;
                        // Could add FrameCounter.Yield here if needed
                    }
                }
            }
            finally
            {
                entities.Dispose();
                components.Dispose();
            }
        }
        
        /// <summary>
        /// Gets entity count for a query.
        /// </summary>
        public static int GetEntityCount(EntityQuery query)
        {
            return query.CalculateEntityCount();
        }
        
        /// <summary>
        /// Checks if any entities match the query.
        /// </summary>
        public static bool HasEntities(EntityQuery query)
        {
            return query.CalculateEntityCount() > 0;
        }
    }
}
