using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Services;
using VAutomationCore.Services;

namespace VAutomationCore.Core.Api
{
    /// <summary>
    /// Alias map for runtime entities and values used by flow execution.
    /// </summary>
    public sealed class EntityMap
    {
        private readonly Dictionary<string, Entity> _map = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _map.Count;

        /// <summary>
        /// Maps an entity to an alias.
        /// </summary>
        public void SetEntity(string alias, Entity entity)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }
            _map[alias.Trim()] = entity;
        }

        /// <summary>
        /// Maps a string value to an alias.
        /// </summary>
        public void SetString(string alias, string value)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }
            _values[alias.Trim()] = value ?? string.Empty;
        }

        /// <summary>
        /// Maps an integer value to an alias.
        /// </summary>
        public void SetInt(string alias, int value)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }
            _values[alias.Trim()] = value;
        }

        /// <summary>
        /// Maps a float3 value to an alias.
        /// </summary>
        public void SetFloat3(string alias, float3 value)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }
            _values[alias.Trim()] = value;
        }

        /// <summary>
        /// Tries to get a string value by alias.
        /// </summary>
        public bool TryGetString(string alias, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            if (_values.TryGetValue(alias.Trim(), out var obj) && obj is string s)
            {
                value = s;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get an integer value by alias.
        /// </summary>
        public bool TryGetInt(string alias, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            if (_values.TryGetValue(alias.Trim(), out var obj) && obj is int i)
            {
                value = i;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get a float3 value by alias.
        /// </summary>
        public bool TryGetFloat3(string alias, out float3 value)
        {
            value = float3.zero;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            if (_values.TryGetValue(alias.Trim(), out var obj) && obj is float3 f)
            {
                value = f;
                return true;
            }

            return false;
        }

        public bool Map(string alias, Entity entity, bool replace = true)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            var key = alias.Trim();
            if (_map.ContainsKey(key))
            {
                if (!replace)
                {
                    return false;
                }

                _map[key] = entity;
                return true;
            }

            _map.Add(key, entity);
            return true;
        }

        public bool TryGet(string alias, out Entity entity)
        {
            entity = Entity.Null;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            return _map.TryGetValue(alias.Trim(), out entity);
        }

        public bool Remove(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            return _map.Remove(alias.Trim());
        }

        public void Clear()
        {
            _map.Clear();
            _values.Clear();
        }

        public IReadOnlyDictionary<string, Entity> Snapshot()
        {
            return new Dictionary<string, Entity>(_map, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Convenience mapping from platform ID to User entity.
        /// </summary>
        public bool TryMapUserByPlatformId(string alias, ulong platformId, bool replace = true)
        {
            if (!GameActionService.TryFindUserEntityByPlatformId(platformId, out var userEntity))
            {
                return false;
            }

            return Map(alias, userEntity, replace);
        }
    }
}
