using System;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.ECS
{
    /// <summary>
    /// Extension methods for Entity and EntityManager operations.
    /// Provides safe, typed access to common ECS operations.
    /// </summary>
    public static class EntityExtensions
    {
        private static readonly CoreLogger _log = new CoreLogger("EntityExtensions");
        
        #region EntityManager Extensions
        
        /// <summary>
        /// Checks if an entity has a component of type T.
        /// </summary>
        public static bool HasComponent<T>(this EntityManager em, Entity entity) where T : struct
        {
            if (entity == Entity.Null) return false;
            
            try
            {
                return em.HasComponent<T>(entity);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Gets a component from an entity. Returns default if not found.
        /// </summary>
        public static T GetComponent<T>(this EntityManager em, Entity entity) where T : struct
        {
            if (entity == Entity.Null) return default;
            
            try
            {
                return em.GetComponentData<T>(entity);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
                return default;
            }
        }
        
        /// <summary>
        /// Sets a component on an entity safely.
        /// </summary>
        public static void SetComponent<T>(this EntityManager em, Entity entity, T data) where T : struct
        {
            if (entity == Entity.Null) return;
            
            try
            {
                em.SetComponentData(entity, data);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }
        
        /// <summary>
        /// Checks if an entity exists.
        /// </summary>
        public static bool Exists(this EntityManager em, Entity entity)
        {
            return entity != Entity.Null && em.Exists(entity);
        }
        
        /// <summary>
        /// Gets or adds a component to an entity.
        /// </summary>
        public static T GetOrAddComponent<T>(this EntityManager em, Entity entity) where T : struct
        {
            if (!em.HasComponent<T>(entity))
            {
                em.AddComponent<T>(entity);
            }
            return em.GetComponentData<T>(entity);
        }
        
        #endregion
        
        #region Entity Extensions
        
        /// <summary>
        /// Checks if an entity has a component of type T.
        /// </summary>
        public static bool Has<T>(this Entity entity) where T : struct
        {
            if (entity == Entity.Null) return false;
            return UnifiedCore.EntityManager.HasComponent<T>(entity);
        }
        
        /// <summary>
        /// Gets a component from an entity.
        /// </summary>
        public static T Get<T>(this Entity entity) where T : struct
        {
            return UnifiedCore.EntityManager.GetComponent<T>(entity);
        }
        
        /// <summary>
        /// Sets a component on an entity.
        /// </summary>
        public static void Set<T>(this Entity entity, T data) where T : struct
        {
            UnifiedCore.EntityManager.SetComponent(entity, data);
        }
        
        /// <summary>
        /// Gets the position component from an entity.
        /// </summary>
        public static float3 GetPosition(this Entity entity)
        {
            if (entity == Entity.Null) return float3.zero;
            
            try
            {
                var transform = UnifiedCore.EntityManager.GetComponentData<LocalTransform>(entity);
                return transform.Position;
            }
            catch
            {
                return float3.zero;
            }
        }
        
        /// <summary>
        /// Sets the position component on an entity.
        /// </summary>
        public static void SetPosition(this Entity entity, float3 position)
        {
            if (entity == Entity.Null) return;
            
            try
            {
                var transform = UnifiedCore.EntityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = position;
                UnifiedCore.EntityManager.SetComponentData(entity, transform);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }
        
        /// <summary>
        /// Gets the rotation component from an entity.
        /// </summary>
        public static quaternion GetRotation(this Entity entity)
        {
            if (entity == Entity.Null) return quaternion.identity;
            
            try
            {
                var transform = UnifiedCore.EntityManager.GetComponentData<LocalTransform>(entity);
                return transform.Rotation;
            }
            catch
            {
                return quaternion.identity;
            }
        }
        
        /// <summary>
        /// Sets the rotation component on an entity.
        /// </summary>
        public static void SetRotation(this Entity entity, quaternion rotation)
        {
            if (entity == Entity.Null) return;
            
            try
            {
                var transform = UnifiedCore.EntityManager.GetComponentData<LocalTransform>(entity);
                transform.Rotation = rotation;
                UnifiedCore.EntityManager.SetComponentData(entity, transform);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }
        
        /// <summary>
        /// Gets the scale component from an entity.
        /// </summary>
        public static float GetScale(this Entity entity)
        {
            if (entity == Entity.Null) return 1f;
            
            try
            {
                var transform = UnifiedCore.EntityManager.GetComponentData<LocalTransform>(entity);
                return transform.Scale;
            }
            catch
            {
                return 1f;
            }
        }
        
        /// <summary>
        /// Sets the scale component on an entity.
        /// </summary>
        public static void SetScale(this Entity entity, float scale)
        {
            if (entity == Entity.Null) return;
            
            try
            {
                var transform = UnifiedCore.EntityManager.GetComponentData<LocalTransform>(entity);
                transform.Scale = scale;
                UnifiedCore.EntityManager.SetComponentData(entity, transform);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }
        
        #endregion
        
        #region Transform Helpers
        
        /// <summary>
        /// Creates a LocalTransform from position, rotation, and scale.
        /// </summary>
        public static LocalTransform CreateTransform(
            float3 position = default,
            quaternion rotation = default,
            float scale = 1f)
        {
            return LocalTransform.FromPositionRotationScale(
                position,
                rotation.value.Equals(float4.zero) ? quaternion.identity : rotation,
                scale);
        }
        
        /// <summary>
        /// Calculates the distance between two entities.
        /// </summary>
        public static float DistanceTo(this Entity entity, Entity other)
        {
            var pos1 = entity.GetPosition();
            var pos2 = other.GetPosition();
            return math.distance(pos1, pos2);
        }
        
        /// <summary>
        /// Calculates the squared distance between two entities.
        /// </summary>
        public static float DistanceSquaredTo(this Entity entity, Entity other)
        {
            var pos1 = entity.GetPosition();
            var pos2 = other.GetPosition();
            return math.distancesq(pos1, pos2);
        }
        
        #endregion
    }
}
