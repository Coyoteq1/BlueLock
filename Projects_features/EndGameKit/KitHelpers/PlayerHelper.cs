using System;
using System.Collections.Generic;
using ProjectM;
using Unity.Collections;
using Unity.Entities;

namespace VAuto.EndGameKit.Helpers
{
    /// <summary>
    /// Helper class for player entity operations.
    /// Provides validation, lookup, and information retrieval for player entities.
    /// </summary>
    public static class PlayerHelper
    {
        /// <summary>
        /// Checks if an entity is a valid player (exists and has PlayerCharacter component).
        /// </summary>
        public static bool IsValidPlayer(EntityManager em, Entity entity)
        {
            if (!em.Exists(entity))
                return false;

            if (!em.HasComponent<PlayerCharacter>(entity))
                return false;

            return true;
        }

        /// <summary>
        /// Gets the player name for an entity.
        /// </summary>
        public static string GetPlayerName(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<PlayerCharacter>(entity))
                return $"Entity_{entity.Index}";

            var characterName = em.GetComponentData<PlayerCharacter>(entity).Name;
            if (string.IsNullOrEmpty(characterName))
                return $"Entity_{entity.Index}";

            return characterName;
        }

        /// <summary>
        /// Checks if a player has an equipped weapon.
        /// </summary>
        public static bool HasEquippedWeapon(EntityManager em, Entity player, out Entity weaponEntity)
        {
            weaponEntity = Entity.Null;

            if (!em.HasComponent<EquippedWeapon>(player))
                return false;

            weaponEntity = em.GetComponentData<EquippedWeapon>(player).WeaponEntity;
            if (!em.Exists(weaponEntity))
                return false;

            return true;
        }

        /// <summary>
        /// Finds a player entity by name (partial match supported).
        /// </summary>
        public static Entity FindPlayerByName(EntityManager em, string name)
        {
            if (string.IsNullOrEmpty(name))
                return Entity.Null;

            var query = em.CreateEntityQuery(typeof(PlayerCharacter));
            var players = query.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var player in players)
                {
                    var characterName = em.GetComponentData<PlayerCharacter>(player).Name;
                    if (!string.IsNullOrEmpty(characterName) && 
                        (characterName.Equals(name, StringComparison.OrdinalIgnoreCase) || 
                         characterName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        return player;
                    }
                }
            }
            finally
            {
                players.Dispose();
            }

            return Entity.Null;
        }

        /// <summary>
        /// Finds a player entity by user ID (steam ID).
        /// </summary>
        public static Entity FindPlayerByUserId(EntityManager em, ulong steamId)
        {
            var query = em.CreateEntityQuery(typeof(PlayerCharacter));
            var players = query.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var player in players)
                {
                    var userData = em.GetComponentData<UserComponent>(player);
                    if (userData.PlatformId == steamId)
                    {
                        return player;
                    }
                }
            }
            finally
            {
                players.Dispose();
            }

            return Entity.Null;
        }

        /// <summary>
        /// Gets all online player entities.
        /// </summary>
        public static List<Entity> GetAllOnlinePlayers(EntityManager em)
        {
            var players = new List<Entity>();

            var query = em.CreateEntityQuery(typeof(PlayerCharacter));
            var entities = query.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var entity in entities)
                {
                    if (em.Exists(entity))
                    {
                        players.Add(entity);
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }

            return players;
        }

        /// <summary>
        /// Gets the user ID (steam ID) for a player.
        /// </summary>
        public static ulong GetUserId(EntityManager em, Entity player)
        {
            if (!em.HasComponent<UserComponent>(player))
                return 0;

            return em.GetComponentData<UserComponent>(player).PlatformId;
        }

        /// <summary>
        /// Gets the player position.
        /// </summary>
        public static Unity.Mathematics.float3 GetPosition(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<LocalTransform>(entity))
                return Unity.Mathematics.float3.zero;

            return em.GetComponentData<LocalTransform>(entity).Position;
        }

        /// <summary>
        /// Checks if a player is in a specific area (distance check).
        /// </summary>
        public static bool IsInArea(EntityManager em, Entity player, Unity.Mathematics.float3 center, float radius)
        {
            var position = GetPosition(em, player);
            return Unity.Mathematics.distance(position, center) <= radius;
        }

        /// <summary>
        /// Gets a summary of player information for logging.
        /// </summary>
        public static string GetPlayerSummary(EntityManager em, Entity player)
        {
            var name = GetPlayerName(em, player);
            var position = GetPosition(em, player);
            var hasWeapon = HasEquippedWeapon(em, player, out var weapon);

            return $"{name} (Entity:{player.Index}, Pos:{position}, Weapon:{hasWeapon})";
        }

        /// <summary>
        /// Checks if a player has a specific component.
        /// </summary>
        public static bool HasComponent<T>(EntityManager em, Entity player) where T : unmanaged
        {
            return em.HasComponent<T>(player);
        }

        /// <summary>
        /// Gets a component value if it exists.
        /// </summary>
        public static T? GetComponent<T>(EntityManager em, Entity player) where T : unmanaged
        {
            if (em.HasComponent<T>(player))
            {
                return em.GetComponentData<T>(player);
            }
            return null;
        }

        /// <summary>
        /// Checks if a player is online (connected to the server).
        /// </summary>
        public static bool IsOnline(EntityManager em, Entity player)
        {
            // A player is considered online if they exist and have a UserComponent
            return em.Exists(player) && em.HasComponent<UserComponent>(player);
        }

        /// <summary>
        /// Gets the player's current health.
        /// </summary>
        public static float GetHealth(EntityManager em, Entity player)
        {
            if (!em.HasComponent<Health>(player))
                return 0;

            return em.GetComponentData<Health>(player).Value;
        }

        /// <summary>
        /// Gets the player's max health.
        /// </summary>
        public static float GetMaxHealth(EntityManager em, Entity player)
        {
            if (!em.HasComponent<Health>(player))
                return 0;

            return em.GetComponentData<Health>(player).MaxHealth;
        }
    }
}
