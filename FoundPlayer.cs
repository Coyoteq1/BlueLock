using System;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework;
using VAuto.Core;
using Il2CppInterop.Runtime;

namespace VAuto.Commands.Converters
{
    /// <summary>
    /// Use this type as a parameter to match a player by steamid first, then by name.
    /// The resulting value will be a tuple of UserEntity and CharacterEntity.
    /// </summary>
    /// <param name="UserEntity">The user entity</param>
    /// <param name="CharacterEntity">The character entity</param>
    /// <param name="CharacterName">The character name</param>
    public record FoundPlayer(Entity UserEntity, Entity CharacterEntity, string CharacterName);

    /// <summary>
    /// Use this type as a parameter to match a player by steamid first, then by name but the player must be online
    /// The resulting value will be a tuple of UserEntity and CharacterEntity.
    /// </summary>
    /// <param name="UserEntity">The user entity</param>
    /// <param name="CharacterEntity">The character entity</param>
    /// <param name="CharacterName">The character name</param>
    public record OnlinePlayer(Entity UserEntity, Entity CharacterEntity, string CharacterName);

    internal class FoundPlayerConverter : CommandArgumentConverter<FoundPlayer>
    {
        public override FoundPlayer Parse(ICommandContext ctx, string input)
        {
            var (userEntity, charEntity, charName) = HandleFindPlayerData(ctx, input, requireOnline: false);
            return new FoundPlayer(userEntity, charEntity, charName);
        }

        public static (Entity UserEntity, Entity CharacterEntity, string CharacterName) HandleFindPlayerData(ICommandContext ctx, string input, bool requireOnline)
        {
            Plugin.Log.LogInfo($"[FoundPlayerConverter] Searching for player: {input}");
            
            // Try to parse as Steam ID first
            var isSteamId = ulong.TryParse(input, out var steamId) && steamId != 0;
            
            if (isSteamId)
            {
                Plugin.Log.LogInfo($"[FoundPlayerConverter] Trying Steam ID: {steamId}");
                var userQueryDesc = new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        new ComponentType(Il2CppType.Of<User>(), ComponentType.AccessMode.ReadOnly)
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities
                };
                var userQuery = VRCore.EntityManager.CreateEntityQuery(userQueryDesc);
                var users = userQuery.ToEntityArray(Allocator.Temp);
                
                try
                {
                    foreach (var userEntity in users)
                    {
                        var user = VRCore.EntityManager.GetComponentData<User>(userEntity);
                        if (user.PlatformId == steamId)
                        {
                            if (requireOnline && !user.IsConnected)
                            {
                                throw ctx.Error($"Player with Steam ID {steamId} is not online.");
                            }

                            var charEntity = user.LocalCharacter.GetEntityOnServer();
                            var charName = user.CharacterName.ToString();
                            
                            Plugin.Log.LogInfo($"[FoundPlayerConverter] Found by Steam ID: {charName}");
                            return (userEntity, charEntity, charName);
                        }
                    }
                }
                finally
                {
                    users.Dispose();
                    userQuery.Dispose();
                }
            }

            // Try to find by name
            Plugin.Log.LogInfo($"[FoundPlayerConverter] Trying name: {input}");
            var userQueryDesc2 = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    new ComponentType(Il2CppType.Of<User>(), ComponentType.AccessMode.ReadOnly)
                },
                Options = EntityQueryOptions.IncludeDisabledEntities
            };
            var userQuery2 = VRCore.EntityManager.CreateEntityQuery(userQueryDesc2);
            var users2 = userQuery2.ToEntityArray(Allocator.Temp);
            
            try
            {
                foreach (var userEntity in users2)
                {
                    var user = VRCore.EntityManager.GetComponentData<User>(userEntity);
                    var charName = user.CharacterName.ToString();
                    
                    if (charName.Equals(input, StringComparison.OrdinalIgnoreCase))
                    {
                        if (requireOnline && !user.IsConnected)
                        {
                            throw ctx.Error($"Player '{input}' is not online.");
                        }

                        var charEntity = user.LocalCharacter.GetEntityOnServer();
                        
                        Plugin.Log.LogInfo($"[FoundPlayerConverter] Found by name: {charName}");
                        return (userEntity, charEntity, charName);
                    }
                }
            }
            finally
            {
                users2.Dispose();
                userQuery2.Dispose();
            }

            Plugin.Log.LogWarning($"[FoundPlayerConverter] Player not found: {input}");
            throw ctx.Error($"Player '{input}' not found.");
        }
    }

    internal class OnlinePlayerConverter : CommandArgumentConverter<OnlinePlayer>
    {
        public override OnlinePlayer Parse(ICommandContext ctx, string input)
        {
            var (userEntity, charEntity, charName) = FoundPlayerConverter.HandleFindPlayerData(ctx, input, requireOnline: true);
            return new OnlinePlayer(userEntity, charEntity, charName);
        }
    }
}
