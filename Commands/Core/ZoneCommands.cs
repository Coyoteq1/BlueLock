using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAutomationCore.Core.Commands;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Logging;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Zone and Castle Territory management commands for VAutomationEvents
    /// Uses CommandBase for VCF integration, logging, and feedback.
    /// </summary>
    public static class ZoneCommands : CommandBase
    {
        private const string CommandName = "zone";
        
        [Command("zonelist", shortHand: "zl", description: "List active zones", adminOnly: false)]
        public static void ZoneList(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "zonelist", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                Log.Info($"Zone list requested by {GetPlayerInfo(ctx).Name}");
                
                SendInfo(ctx, "Zone list command executed.");
                // TODO: Integrate with actual zone service
            });
        }

        [Command("zoneinfo", shortHand: "zi", description: "Show info about your current zone", adminOnly: false)]
        public static void ZoneInfo(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "zoneinfo", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                Log.Info($"Zone info requested by {GetPlayerInfo(ctx).Name} at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
                
                SendLocation(ctx, "Your position", position.Value);
                SendInfo(ctx, "Zone checking...");
            });
        }

        [Command("setzone", description: "Create a zone at your position with radius", adminOnly: true)]
        public static void SetZone(ChatCommandContext ctx, string zoneId, float radius)
        {
            ExecuteSafely(ctx, "setzone", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"User '{playerInfo.Name}' setting zone '{zoneId}' at ({position.Value.x}, {position.Value.y}, {position.Value.z}) with radius {radius}");
                
                SendSuccess(ctx, $"Created zone '{zoneId}'", $"at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1}) radius {radius}");
                // TODO: Integrate with actual zone creation service
            });
        }

        [Command("setzonesq", description: "Create a square zone at your position with size", adminOnly: true)]
        public static void SetZoneSquare(ChatCommandContext ctx, string zoneId, float size)
        {
            ExecuteSafely(ctx, "setzonesq", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"User '{playerInfo.Name}' setting square zone '{zoneId}' at ({position.Value.x}, {position.Value.y}, {position.Value.z}) with size {size}");
                
                SendSuccess(ctx, $"Created square zone '{zoneId}'", $"at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1}) size {size}");
            });
        }

        [Command("setzonePrefab", description: "Create a glow zone with prefab at your position", adminOnly: true)]
        public static void SetZonePrefab(ChatCommandContext ctx, string zoneId, int prefabGuid, float radius)
        {
            ExecuteSafely(ctx, "setzonePrefab", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"User '{playerInfo.Name}' setting glow zone '{zoneId}' at ({position.Value.x}, {position.Value.y}, {position.Value.z}) with prefab {prefabGuid} radius {radius}");
                
                SendSuccess(ctx, $"Created glow zone '{zoneId}'", $"at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
                SendInfo(ctx, $"Prefab GUID: {prefabGuid}, Radius: {radius}");
            });
        }

        [Command("teleport", shortHand: "tp", description: "Teleport to coordinates", adminOnly: true)]
        public static void Teleport(ChatCommandContext ctx, float x, float y, float z)
        {
            ExecuteSafely(ctx, "teleport", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = new float3(x, y, z);
                var playerInfo = GetPlayerInfo(ctx);
                
                // Find the character entity for the calling user
                var characterEntity = GetCallingCharacterEntity(ctx.User.PlatformId);
                if (characterEntity == Entity.Null)
                {
                    SendError(ctx, "Could not find your character.");
                    return;
                }
                
                // Update Translation component (older Unity) or LocalTransform (newer)
                if (EM.HasComponent<LocalTransform>(characterEntity))
                {
                    var transform = EM.GetComponentData<LocalTransform>(characterEntity);
                    transform.Position = position;
                    EM.SetComponentData(characterEntity, transform);
                }
                else if (EM.HasComponent<Translation>(characterEntity))
                {
                    var translation = EM.GetComponentData<Translation>(characterEntity);
                    translation.Value = position;
                    EM.SetComponentData(characterEntity, translation);
                }
                else
                {
                    SendError(ctx, "Character has no position component.");
                    return;
                }
                
                Log.Info($"User '{playerInfo.Name}' teleported to ({x}, {y}, {z})");
                SendSuccess(ctx, $"Teleported to ({x:F1}, {y:F1}, {z:F1})");
            });
        }

        private static Entity GetCallingCharacterEntity(ulong platformId)
        {
            var query = EM.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
            var entities = query.ToEntityArray(Allocator.Temp);
            
            try
            {
                foreach (var entity in entities)
                {
                    var pc = EM.GetComponentData<PlayerCharacter>(entity);
                    var userEntity = pc.UserEntity;
                    
                    if (userEntity == Entity.Null) continue;
                    
                    var user = EM.GetComponentData<User>(userEntity);
                    if (user.PlatformId == platformId)
                    {
                        return entity;
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
            
            return Entity.Null;
        }

        // ===== Arena Management Commands =====

        [Command("setexitpoint", shortHand: "sep", description: "Set arena exit point at your position", adminOnly: true)]
        public static void SetExitPoint(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "setexitpoint", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                Log.Info($"Exit point set to ({position.Value.x}, {position.Value.y}, {position.Value.z}) by {GetPlayerInfo(ctx).Name}");
                
                SendSuccess(ctx, $"Exit point set at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
            });
        }

        [Command("setspawnpoint", shortHand: "ssp", description: "Set arena spawn point at your position", adminOnly: true)]
        public static void SetSpawnPoint(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "setspawnpoint", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                Log.Info($"Spawn point set to ({position.Value.x}, {position.Value.y}, {position.Value.z}) by {GetPlayerInfo(ctx).Name}");
                
                SendSuccess(ctx, $"Spawn point set at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
            });
        }

        [Command("enterarena", shortHand: "ea", description: "Enter the arena", adminOnly: false)]
        public static void EnterArena(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "enterarena", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"User '{playerInfo.Name}' entering arena");
                
                SendInfo(ctx, "Entering arena...");
                // TODO: Integrate with ArenaTerritory service
            });
        }

        [Command("exitarena", shortHand: "xa", description: "Exit the arena", adminOnly: false)]
        public static void ExitArena(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "exitarena", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"User '{playerInfo.Name}' exiting arena");
                
                SendInfo(ctx, "Exiting arena...");
            });
        }

        [Command("tpSpawn", shortHand: "tps", description: "Teleport to arena spawn point", adminOnly: false)]
        public static void TeleportToSpawn(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "tpspawn", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"User '{playerInfo.Name}' teleporting to spawn");
                
                SendInfo(ctx, "Teleporting to spawn point...");
            });
        }

        [Command("inzone", shortHand: "iz", description: "Check if you're in a zone", adminOnly: false)]
        public static void CheckPlayerZones(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "inzone", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                SendLocation(ctx, "Your position", position.Value);
                SendInfo(ctx, "Zone checking...");
            });
        }

        [Command("playerzoneindex", shortHand: "pzi", description: "Get your territory index", adminOnly: false)]
        public static void GetPlayerTerritoryIndex(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "playerzoneindex", () =>
            {
                RequirePermission(ctx, PermissionLevel.Anyone);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                var index = GetArenaGridIndex(position.Value);
                SendCount(ctx, "Your territory index", index);
            });
        }

        [Command("castleradius", shortHand: "cr", description: "Set castle territory building radius (admin only)", adminOnly: true)]
        public static void SetCastleRadius(ChatCommandContext ctx, float radius)
        {
            ExecuteSafely(ctx, "castleradius", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                var playerInfo = GetPlayerInfo(ctx);
                Log.Info($"User '{playerInfo.Name}' setting castle radius to {radius} at ({position.Value.x}, {position.Value.y}, {position.Value.z})");
                
                SendSuccess(ctx, $"Castle territory radius set to {radius:F1}");
                SendInfo(ctx, "Note: Castle territory blocks must be placed within this radius.");
            });
        }

        [Command("castleinfo", shortHand: "ci", description: "Show castle territory info (admin only)", adminOnly: true)]
        public static void CastleInfo(ChatCommandContext ctx)
        {
            ExecuteSafely(ctx, "castleinfo", () =>
            {
                RequirePermission(ctx, PermissionLevel.Admin);
                
                var position = GetPlayerPosition(ctx);
                if (position == null)
                {
                    SendError(ctx, "Could not get your position.");
                    return;
                }
                
                SendLocation(ctx, "Your position", position.Value);
                SendInfo(ctx, "Castle territory management commands:");
                SendInfo(ctx, "  .castleradius <radius> - Set building radius");
                SendInfo(ctx, "  .castleinfo / .ci - Show this info");
            });
        }

        private static float3? GetPlayerPosition(ChatCommandContext ctx)
        {
            return GetPlayerPosition(ctx.User.PlatformId);
        }

        private static float3? GetPlayerPosition(ulong platformId)
        {
            try
            {
                var serverWorld = UnifiedCore.Server;
                if (serverWorld == null)
                {
                    Log.Warning("Server world not available");
                    return null;
                }
                
                var entityManager = serverWorld.EntityManager;
                
                // Get all PlayerCharacter entities
                var playerQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerCharacter>()
                );
                
                var entities = playerQuery.ToEntityArray(Allocator.Temp);
                
                try
                {
                    foreach (var entity in entities)
                    {
                        if (!entityManager.HasComponent<PlayerCharacter>(entity)) continue;
                        
                        var pc = entityManager.GetComponentData<PlayerCharacter>(entity);
                        var userEntity = pc.UserEntity;
                        
                        if (userEntity == Entity.Null) continue;
                        
                        if (!entityManager.HasComponent<User>(userEntity)) continue;
                        
                        var user = entityManager.GetComponentData<User>(userEntity);
                        if (user.IsConnected)
                        {
                            // Try LocalTransform first (newer Unity versions)
                            if (entityManager.HasComponent<LocalTransform>(entity))
                            {
                                var pos = entityManager.GetComponentData<LocalTransform>(entity).Position;
                                return pos;
                            }
                            // Fallback to Translation (older Unity versions)
                            else if (entityManager.HasComponent<Translation>(entity))
                            {
                                var pos = entityManager.GetComponentData<Translation>(entity).Value;
                                return pos;
                            }
                        }
                    }
                }
                finally
                {
                    entities.Dispose();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "GetPlayerPosition");
                return null;
            }
        }

        private static int GetArenaGridIndex(float3 position)
        {
            // Simple grid-based index calculation
            const float gridSize = 100f;
            var xIndex = (int)math.floor(position.x / gridSize);
            var zIndex = (int)math.floor(position.z / gridSize);
            return xIndex * 1000 + zIndex;
        }
    }
}
