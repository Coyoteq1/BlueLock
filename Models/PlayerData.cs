using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using VAuto.Core;

namespace VAuto.Models
{
    /// <summary>
    /// Player data structure for caching player information
    /// </summary>
    public struct PlayerData
    {
        public FixedString64Bytes CharacterName;
        public ulong SteamID;
        public bool IsOnline;
        public Entity UserEntity;
        public Entity CharEntity;

        public PlayerData(FixedString64Bytes characterName, ulong steamId, bool isOnline, Entity userEntity, Entity charEntity)
        {
            CharacterName = characterName;
            SteamID = steamId;
            IsOnline = isOnline;
            UserEntity = userEntity;
            CharEntity = charEntity;
        }

        /// <summary>
        /// Check if player is in any zone
        /// </summary>
        public bool IsInZone()
        {
            return CharEntity != Entity.Null && VRCore.EntityManager.HasComponent<Translation>(CharEntity);
        }

        /// <summary>
        /// Check if player is in specific zone type
        /// </summary>
        public bool IsInZone(string zoneType)
        {
            // Simple implementation - can be extended later
            return IsInZone();
        }

        /// <summary>
        /// Check if player is in PvP arena
        /// </summary>
        public bool IsInPvPArena()
        {
            return IsInZone("pvparena") || IsInZone("arena");
        }

        /// <summary>
        /// Check if player is in main arena
        /// </summary>
        public bool IsInMainArena()
        {
            return IsInZone("mainarena");
        }

        /// <summary>
        /// Check if player is in safe zone
        /// </summary>
        public bool IsInSafeZone()
        {
            return IsInZone("safezone");
        }

        /// <summary>
        /// Check if player is in glow zone
        /// </summary>
        public bool IsInGlowZone()
        {
            return IsInZone("glowzone");
        }

        /// <summary>
        /// Check if player is in any arena
        /// </summary>
        public bool IsInArena()
        {
            return IsInPvPArena() || IsInMainArena();
        }
    }

    /// <summary>
    /// Player class with convenience methods
    /// </summary>
    public class Player
    {
        public string Name;
        public ulong SteamID;
        public bool IsOnline;
        public bool IsAdmin;
        public Entity User;
        public Entity Character;

        public Player(Entity userEntity = default)
        {
            User = userEntity;
            var user = VAuto.Core.VRCore.EntityManager.GetComponentData<User>(userEntity);
            Character = user.LocalCharacter._Entity;
            Name = user.CharacterName.ToString();
            IsOnline = user.IsConnected;
            IsAdmin = user.IsAdmin;
            SteamID = user.PlatformId;
        }

        /// <summary>
        /// Check if player is in any zone
        /// </summary>
        public bool IsInZone()
        {
            var playerData = new PlayerData(Name, SteamID, IsOnline, User, Character);
            return playerData.IsInZone();
        }

        /// <summary>
        /// Check if player is in specific zone type
        /// </summary>
        public bool IsInZone(string zoneType)
        {
            var playerData = new PlayerData(Name, SteamID, IsOnline, User, Character);
            return playerData.IsInZone(zoneType);
        }

        /// <summary>
        /// Check if player is in PvP arena
        /// </summary>
        public bool IsInPvPArena()
        {
            var playerData = new PlayerData(Name, SteamID, IsOnline, User, Character);
            return playerData.IsInPvPArena();
        }

        /// <summary>
        /// Check if player is in main arena
        /// </summary>
        public bool IsInMainArena()
        {
            var playerData = new PlayerData(Name, SteamID, IsOnline, User, Character);
            return playerData.IsInMainArena();
        }

        /// <summary>
        /// Check if player is in safe zone
        /// </summary>
        public bool IsInSafeZone()
        {
            var playerData = new PlayerData(Name, SteamID, IsOnline, User, Character);
            return playerData.IsInSafeZone();
        }

        /// <summary>
        /// Check if player is in glow zone
        /// </summary>
        public bool IsInGlowZone()
        {
            var playerData = new PlayerData(Name, SteamID, IsOnline, User, Character);
            return playerData.IsInGlowZone();
        }

        /// <summary>
        /// Check if player is in any arena
        /// </summary>
        public bool IsInArena()
        {
            var playerData = new PlayerData(Name, SteamID, IsOnline, User, Character);
            return playerData.IsInArena();
        }
    }
}
