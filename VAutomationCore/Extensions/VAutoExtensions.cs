using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectM;
using VAuto.Core;
using VAuto.Core.Networking;
using Stunlock.Core;

namespace VAuto.Extensions
{
    /// <summary>
    /// Consolidated extension methods for V Rising modding
    /// Auto-applied extension methods for common operations
    /// </summary>
    public static class VAutoExtensions
    {
        #region Entity Extensions

        /// <summary>
        /// Get or add component to entity
        /// </summary>
        public static T GetOrAdd<T>(this Entity entity) where T : IComponentData, new()
        {
            var em = VRCore.EntityManager;
            if (em.HasComponent<T>(entity))
                return em.GetComponentData<T>(entity);

            em.AddComponent<T>(entity);
            return new T();
        }

        /// <summary>
        /// Safely get component
        /// </summary>
        public static T? GetSafe<T>(this Entity entity) where T : IComponentData
        {
            var em = VRCore.EntityManager;
            return em.Exists(entity) && em.HasComponent<T>(entity)
                ? em.GetComponentData<T>(entity)
                : null;
        }

        /// <summary>
        /// Get component or default
        /// </summary>
        public static T GetOrDefault<T>(this Entity entity, T defaultValue = default) where T : IComponentData
        {
            return entity.GetSafe<T>() ?? defaultValue;
        }

        /// <summary>
        /// Set component if it exists
        /// </summary>
        public static void SetIfExists<T>(this Entity entity, T component) where T : IComponentData
        {
            var em = VRCore.EntityManager;
            if (em.Exists(entity) && em.HasComponent<T>(entity))
                em.SetComponentData(entity, component);
        }

        /// <summary>
        /// Check if entity has all specified components
        /// </summary>
        public static bool HasAllComponents(this Entity entity, params Type[] componentTypes)
        {
            var em = VRCore.EntityManager;
            return componentTypes.All(type => em.HasComponent(entity, type));
        }

        /// <summary>
        /// Get all entities with a specific component
        /// </summary>
        public static EntityQuery GetEntitiesWith<T>() where T : IComponentData
        {
            return VRCore.EntityManager.CreateEntityQuery(typeof(T));
        }

        /// <summary>
        /// Get all entities matching multiple component types
        /// </summary>
        public static EntityQuery GetEntitiesWith(params Type[] componentTypes)
        {
            var em = VRCore.EntityManager;
            return em.CreateEntityQuery(componentTypes);
        }

        /// <summary>
        /// Get entity count for a query
        /// </summary>
        public static int GetEntityCount<T>() where T : IComponentData
        {
            var query = VRCore.EntityManager.CreateEntityQuery(typeof(T));
            return query.CalculateEntityCount();
        }

        #endregion

        #region Prefab Extensions

        /// <summary>
        /// Get prefab name from GUID
        /// </summary>
        public static string? GetName(this PrefabGUID guid)
        {
            return Prefabs.GetPrefabGuid(guid.Value.ToString())?.Name;
        }

        /// <summary>
        /// Check if prefab exists in registry
        /// </summary>
        public static bool Exists(this PrefabGUID guid)
        {
            return Prefabs.GetPrefabGuid(guid.ToString()) != null;
        }

        /// <summary>
        /// Spawn prefab at position
        /// </summary>
        public static Entity SpawnAt(this PrefabGUID guid, float3 position)
        {
            var em = VRCore.EntityManager;
            var entity = em.Instantiate(guid);

            if (em.HasComponent<LocalTransform>(entity))
            {
                var transform = em.GetComponentData<LocalTransform>(entity);
                transform.Position = position;
                em.SetComponentData(entity, transform);
            }

            return entity;
        }

        /// <summary>
        /// Spawn prefab with rotation
        /// </summary>
        public static Entity SpawnAt(this PrefabGUID guid, float3 position, quaternion rotation)
        {
            var em = VRCore.EntityManager;
            var entity = em.Instantiate(guid);

            if (em.HasComponent<LocalTransform>(entity))
            {
                var transform = LocalTransform.FromPositionRotationScale(position, rotation, 1f);
                em.SetComponentData(entity, transform);
            }

            return entity;
        }

        /// <summary>
        /// Get prefab GUID from name
        /// </summary>
        public static PrefabGUID? GetGuid(this string prefabName)
        {
            var guid = Prefabs.GetPrefabGuid(prefabName);
            return guid.HasValue ? new PrefabGUID((int)guid.Value) : null;
        }

        /// <summary>
        /// Search prefabs by name
        /// </summary>
        public static IEnumerable<string> SearchPrefabs(this string query)
        {
            // Simplified search - would integrate with PrefabRegistry
            return Prefabs.GetAllPrefabNames()
                .Where(name => name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(50);
        }

        #endregion

        #region Serialization Extensions

        /// <summary>
        /// Serialize to JSON
        /// </summary>
        public static string ToJson(this object obj)
        {
            return StateSerializer.Instance.Serialize(obj);
        }

        /// <summary>
        /// Deserialize from JSON
        /// </summary>
        public static T? FromJson<T>(this string json)
        {
            return StateSerializer.Instance.Deserialize<T>(json);
        }

        /// <summary>
        /// Serialize to UTF-8 bytes
        /// </summary>
        public static byte[] ToJsonUtf8(this object obj)
        {
            return StateSerializer.Instance.SerializeToUtf8(obj);
        }

        /// <summary>
        /// Convert to wire message
        /// </summary>
        public static WireMessage ToWire(this object obj, string type)
        {
            return WireMessage.Create(type, obj);
        }

        /// <summary>
        /// Convert object to dictionary
        /// </summary>
        public static Dictionary<string, object> ToDictionary(this object obj)
        {
            return StateSerializer.Instance.Deserialize<Dictionary<string, object>>(obj.ToJson()) ?? new();
        }

        #endregion

        #region Dictionary Extensions

        /// <summary>
        /// Get string value from dictionary
        /// </summary>
        public static string GetString(this Dictionary<string, object> dict, string key, string defaultValue = "")
        {
            if (dict.TryGetValue(key, out var val))
                return val.ToString() ?? defaultValue;
            return defaultValue;
        }

        /// <summary>
        /// Get int value from dictionary
        /// </summary>
        public static int GetInt(this Dictionary<string, object> dict, string key, int defaultValue = 0)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is int i) return i;
                if (int.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// Get bool value from dictionary
        /// </summary>
        public static bool GetBool(this Dictionary<string, object> dict, string key, bool defaultValue = false)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is bool b) return b;
                if (bool.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// Get float value from dictionary
        /// </summary>
        public static float GetFloat(this Dictionary<string, object> dict, string key, float defaultValue = 0f)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is float f) return f;
                if (val is double d) return (float)d;
                if (float.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// Get typed object from dictionary
        /// </summary>
        public static T? GetObject<T>(this Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var val)) return default;
            var json = val.ToJson();
            return StateSerializer.Instance.Deserialize<T>(json);
        }

        /// <summary>
        /// Get or default from dictionary
        /// </summary>
        public static T GetOrDefault<T>(this Dictionary<string, object> dict, string key, T defaultValue)
        {
            return dict.GetObject<T>(key) ?? defaultValue;
        }

        /// <summary>
        /// Add value if not exists
        /// </summary>
        public static void AddIfNotExists<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (!dict.ContainsKey(key))
                dict.Add(key, value);
        }

        /// <summary>
        /// Get or add value
        /// </summary>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict.TryGetValue(key, out var existing))
                return existing;
            dict.Add(key, value);
            return value;
        }

        #endregion

        #region Float3 Extensions

        /// <summary>
        /// Convert float3 to string
        /// </summary>
        public static string ToPositionString(this float3 value)
        {
            return $"{value.x:F2}, {value.y:F2}, {value.z:F2}";
        }

        /// <summary>
        /// Calculate distance between two positions
        /// </summary>
        public static float DistanceTo(this float3 from, float3 to)
        {
            return math.distance(from, to);
        }

        /// <summary>
        /// Check if position is within radius
        /// </summary>
        public static bool IsInRadius(this float3 position, float3 center, float radius)
        {
            return math.distance(position, center) <= radius;
        }

        /// <summary>
        /// Get direction to target
        /// </summary>
        public static float3 DirectionTo(this float3 from, float3 to)
        {
            return math.normalize(to - from);
        }

        #endregion

        #region String Extensions

        /// <summary>
        /// Repeat string n times
        /// </summary>
        public static string Repeat(this string str, int count)
        {
            if (string.IsNullOrEmpty(str) || count <= 0)
                return string.Empty;
            return string.Concat(System.Linq.Enumerable.Repeat(str, count));
        }

        /// <summary>
        /// Truncate string to max length
        /// </summary>
        public static string Truncate(this string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Length <= maxLength ? str : str[..maxLength];
        }

        /// <summary>
        /// Convert to title case
        /// </summary>
        public static string ToTitleCase(this string str)
        {
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
        }

        #endregion

        #region Collection Extensions

        /// <summary>
        /// Convert enumerable to comma-separated string
        /// </summary>
        public static string JoinString<T>(this IEnumerable<T> collection, string separator = ", ")
        {
            return string.Join(separator, collection);
        }

        /// <summary>
        /// Safe list access with default
        /// </summary>
        public static T GetOrDefault<T>(this IList<T> list, int index, T defaultValue = default)
        {
            return index >= 0 && index < list.Count ? list[index] : defaultValue;
        }

        /// <summary>
        /// Shuffle collection
        /// </summary>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection)
        {
            return collection.OrderBy(_ => Guid.NewGuid());
        }

        #endregion

        #region DateTime Extensions

        /// <summary>
        /// Format as relative time
        /// </summary>
        public static string ToRelativeTime(this DateTime dt)
        {
            var now = DateTime.UtcNow;
            var diff = now - dt;

            if (diff.TotalSeconds < 60)
                return "just now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}d ago";

            return dt.ToString("MMM dd");
        }

        /// <summary>
        /// Get Unix timestamp
        /// </summary>
        public static long ToUnixTime(this DateTime dt)
        {
            return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }

        #endregion

        #region Exception Extensions

        /// <summary>
        /// Get full exception message with inner exceptions
        /// </summary>
        public static string GetFullMessage(this Exception ex)
        {
            var messages = new List<string>();
            var current = ex;

            while (current != null)
            {
                messages.Add(current.Message);
                current = current.InnerException;
            }

            return string.Join(" -> ", messages);
        }

        #endregion
    }
}
