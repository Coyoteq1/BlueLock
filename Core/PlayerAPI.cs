using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using VRisingServerApiPlugin.attributes.methods;
using VRisingServerApiPlugin.attributes.parameters;
using VRisingServerApiPlugin.endpoints.players;
using ZUtility.Utils;
using VAutomationCore.Core.ECS;

namespace ZUtility.API
{
    public class PlayerAPI
    {
        // Cache the EntityQuery for better performance - avoid recreating on each request
        private static EntityQuery? _cachedUserQuery;

        private static EntityQuery GetUserQuery(EntityManager em)
        {
            _cachedUserQuery ??= em.CreateEntityQuery(ComponentType.ReadOnly<User>());
            return _cachedUserQuery;
        }

        [HttpGet("/test")]
        public object Test()
        {
            return new { status = "ok", message = "API is working!" };
        }

        [HttpGet("/players")]
        public PlayersListResponse GetAllPlayers()
        {
            var em = Core.EntityManager;
            var query = GetUserQuery(em);
            
            var playersList = new List<ApiPlayerDetails>();

            // Use EntityQueryHelper for safe processing - handles disposal automatically
            EntityQueryHelper.ProcessEntities<User>(query, (entity, user) =>
            {
                var details = new ApiPlayerDetails(
                    userIndex: user.Index,
                    characterName: user.CharacterName.IsEmpty ? null : user.CharacterName.ToString(),
                    steamID: user.PlatformId.ToString(),
                    clanId: "",
                    gearLevel: 0,
                    lastValidPositionX: 0f,
                    lastValidPositionY: 0f,
                    timeLastConnected: user.TimeLastConnected,
                    isBot: user.IsBot,
                    isAdmin: user.IsAdmin,
                    isConnected: user.IsConnected,
                    stats: null,
                    gears: null
                );

                playersList.Add(details);
            });

            return new PlayersListResponse(playersList);
        }

        [HttpGet("/player/(?<id>[0-9]+)")]
        public object GetPlayerDetails([UrlParam("id")] int userIndex)
        {
            var em = Core.EntityManager;
            var query = GetUserQuery(em);
            
            ApiPlayerDetails? foundPlayer = null;

            // Query already guarantees User component - no need for redundant HasComponent check
            EntityQueryHelper.ProcessEntities<User>(query, (entity, user) =>
            {
                if (user.Index == userIndex)
                {
                    foundPlayer = new ApiPlayerDetails(
                        userIndex: user.Index,
                        characterName: user.CharacterName.IsEmpty ? null : user.CharacterName.ToString(),
                        steamID: user.PlatformId.ToString(),
                        clanId: "",
                        gearLevel: 0,
                        lastValidPositionX: 0f,
                        lastValidPositionY: 0f,
                        timeLastConnected: user.TimeLastConnected,
                        isBot: user.IsBot,
                        isAdmin: user.IsAdmin,
                        isConnected: user.IsConnected,
                        stats: null,
                        gears: null
                    );
                }
            });

            if (foundPlayer.HasValue)
            {
                return new PlayerApiResponse(foundPlayer.Value);
            }

            return new { error = "Player not found", userIndex = userIndex, message = $"No player found with Index {userIndex}" };
        }
    }

    public class PlayersListResponse
    {
        public List<ApiPlayerDetails> Players { get; set; }
        public int Count { get; set; }

        public PlayersListResponse(List<ApiPlayerDetails> players)
        {
            Players = players;
            Count = players.Count;
        }
    }
}
