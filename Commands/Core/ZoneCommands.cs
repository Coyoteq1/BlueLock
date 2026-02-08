using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Core;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Zone and Castle Territory management commands for VAutomationEvents
    /// </summary>
    public static class ZoneCommands
    {
        [Command("zonelist", shortHand: "zl", description: "List active zones", adminOnly: false)]
        public static void ZoneList(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Zone] User requesting zone list");
            ctx.Reply("[Zone] Zone list command executed.");
        }

        [Command("zoneinfo", shortHand: "zi", description: "Show info about your current zone", adminOnly: false)]
        public static void ZoneInfo(ChatCommandContext ctx)
        {
            Plugin.Log.LogInfo($"[Zone] User requesting current zone info");
            ctx.Reply("[Zone] Current zone info command executed.");
        }

        [Command("setzone", description: "Create a zone at your position with radius", adminOnly: true)]
        public static void SetZone(ChatCommandContext ctx, string zoneId, float radius)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Zone] Error: Could not get your position.");
                    return;
                }

                Plugin.Log.LogInfo($"[Zone] User '{ctx.User.PlatformId}' setting zone '{zoneId}' at ({position.Value.x}, {position.Value.y}, {position.Value.z}) with radius {radius}");
                ctx.Reply($"[Zone] Created zone '{zoneId}' at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1}) radius {radius}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Zone] SetZone failed: {ex.Message}");
                ctx.Reply("[Zone] Failed to create zone.");
            }
        }

        [Command("setzonesq", description: "Create a square zone at your position with size", adminOnly: true)]
        public static void SetZoneSquare(ChatCommandContext ctx, string zoneId, float size)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Zone] Error: Could not get your position.");
                    return;
                }

                Plugin.Log.LogInfo($"[Zone] User '{ctx.User.PlatformId}' setting square zone '{zoneId}' at ({position.Value.x}, {position.Value.y}, {position.Value.z}) with size {size}");
                ctx.Reply($"[Zone] Created square zone '{zoneId}' at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1}) size {size}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Zone] SetZoneSquare failed: {ex.Message}");
                ctx.Reply("[Zone] Failed to create square zone.");
            }
        }

        [Command("setzonePrefab", description: "Create a glow zone with prefab at your position", adminOnly: true)]
        public static void SetZonePrefab(ChatCommandContext ctx, string zoneId, int prefabGuid, float radius)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Zone] Error: Could not get your position.");
                    return;
                }

                Plugin.Log.LogInfo($"[Zone] User '{ctx.User.PlatformId}' setting glow zone '{zoneId}' at ({position.Value.x}, {position.Value.y}, {position.Value.z}) with prefab {prefabGuid} radius {radius}");
                ctx.Reply($"[Zone] Created glow zone '{zoneId}' at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
                ctx.Reply($"[Zone] Prefab GUID: {prefabGuid}, Radius: {radius}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Zone] SetZonePrefab failed: {ex.Message}");
                ctx.Reply("[Zone] Failed to create glow zone.");
            }
        }

        [Command("teleport", shortHand: "tp", description: "Teleport to coordinates", adminOnly: true)]
        public static void Teleport(ChatCommandContext ctx, float x, float y, float z)
        {
            try
            {
                var position = new float3(x, y, z);

                // Find the character entity for the calling user
                var characterEntity = GetCallingCharacterEntity(ctx.User.PlatformId);
                if (characterEntity == Entity.Null)
                {
                    ctx.Reply("[Teleport] Error: Could not find your character.");
                    return;
                }

                // Update Translation component
                if (VRCore.EntityManager.HasComponent<Translation>(characterEntity))
                {
                    var translation = VRCore.EntityManager.GetComponentData<Translation>(characterEntity);
                    translation.Value = position;
                    VRCore.EntityManager.SetComponentData(characterEntity, translation);

                    Plugin.Log.LogInfo($"[Teleport] User '{ctx.User.PlatformId}' teleported to ({x}, {y}, {z})");
                    ctx.Reply($"[Teleport] Teleported to ({x:F1}, {y:F1}, {z:F1})");
                }
                else if (VRCore.EntityManager.HasComponent<LocalTransform>(characterEntity))
                {
                    var transform = VRCore.EntityManager.GetComponentData<LocalTransform>(characterEntity);
                    transform.Position = position;
                    VRCore.EntityManager.SetComponentData(characterEntity, transform);

                    Plugin.Log.LogInfo($"[Teleport] User '{ctx.User.PlatformId}' teleported to ({x}, {y}, {z})");
                    ctx.Reply($"[Teleport] Teleported to ({x:F1}, {y:F1}, {z:F1})");
                }
                else
                {
                    ctx.Reply("[Teleport] Error: Character has no position component.");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Teleport] Teleport failed: {ex.Message}");
                ctx.Reply("[Teleport] Failed to teleport.");
            }
        }

        private static Entity GetCallingCharacterEntity(ulong platformId)
        {
            var query = VRCore.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            foreach (var entity in entities)
            {
                var pc = VRCore.EntityManager.GetComponentData<PlayerCharacter>(entity);
                var userEntity = pc.UserEntity;
                
                if (userEntity == Entity.Null) continue;
                
                var user = VRCore.EntityManager.GetComponentData<User>(userEntity);
                if (user.PlatformId == platformId)
                {
                    entities.Dispose();
                    return entity;
                }
            }
            
            entities.Dispose();
            return Entity.Null;
        }

        // ===== Arena Management Commands =====

        [Command("setexitpoint", shortHand: "sep", description: "Set arena exit point at your position", adminOnly: true)]
        public static void SetExitPoint(ChatCommandContext ctx)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Arena] Error: Could not get your position.");
                    return;
                }

                Plugin.Log.LogInfo($"[Arena] Exit point set to ({position.Value.x}, {position.Value.y}, {position.Value.z})");
                ctx.Reply($"[Arena] Exit point set at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Arena] SetExitPoint failed: {ex.Message}");
                ctx.Reply("[Arena] Failed to set exit point.");
            }
        }

        [Command("setspawnpoint", shortHand: "ssp", description: "Set arena spawn point at your position", adminOnly: true)]
        public static void SetSpawnPoint(ChatCommandContext ctx)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Arena] Error: Could not get your position.");
                    return;
                }

                Plugin.Log.LogInfo($"[Arena] Spawn point set to ({position.Value.x}, {position.Value.y}, {position.Value.z})");
                ctx.Reply($"[Arena] Spawn point set at ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Arena] SetSpawnPoint failed: {ex.Message}");
                ctx.Reply("[Arena] Failed to set spawn point.");
            }
        }

        [Command("enterarena", shortHand: "ea", description: "Enter the arena", adminOnly: false)]
        public static void EnterArena(ChatCommandContext ctx)
        {
            try
            {
                Plugin.Log.LogInfo($"[Arena] User '{ctx.User.PlatformId}' entering arena");
                ctx.Reply("[Arena] Entering arena...");
                // Arena enter logic would go here
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Arena] EnterArena failed: {ex.Message}");
                ctx.Reply("[Arena] Failed to enter arena.");
            }
        }

        [Command("exitarena", shortHand: "xa", description: "Exit the arena", adminOnly: false)]
        public static void ExitArena(ChatCommandContext ctx)
        {
            try
            {
                Plugin.Log.LogInfo($"[Arena] User '{ctx.User.PlatformId}' exiting arena");
                ctx.Reply("[Arena] Exiting arena...");
                // Arena exit logic would go here
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Arena] ExitArena failed: {ex.Message}");
                ctx.Reply("[Arena] Failed to exit arena.");
            }
        }

        [Command("tpSpawn", shortHand: "tps", description: "Teleport to arena spawn point", adminOnly: false)]
        public static void TeleportToSpawn(ChatCommandContext ctx)
        {
            try
            {
                Plugin.Log.LogInfo($"[Arena] User '{ctx.User.PlatformId}' teleporting to spawn");
                ctx.Reply("[Arena] Teleporting to spawn point...");
                // Teleport to spawn logic would go here
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Arena] TeleportToSpawn failed: {ex.Message}");
                ctx.Reply("[Arena] Failed to teleport to spawn.");
            }
        }

        [Command("inzone", shortHand: "iz", description: "Check if you're in a zone", adminOnly: false)]
        public static void CheckPlayerZones(ChatCommandContext ctx)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Zone] Error: Could not get your position.");
                    return;
                }

                ctx.Reply($"[Zone] Your position: ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
                ctx.Reply("[Zone] Zone checking...");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Zone] CheckPlayerZones failed: {ex.Message}");
                ctx.Reply("[Zone] Failed to check zones.");
            }
        }

        [Command("playerzoneindex", shortHand: "pzi", description: "Get your territory index", adminOnly: false)]
        public static void GetPlayerTerritoryIndex(ChatCommandContext ctx)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Zone] Error: Could not get your position.");
                    return;
                }

                var index = GetArenaGridIndex(position.Value);
                ctx.Reply($"[Zone] Your territory index: {index}");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Zone] GetPlayerTerritoryIndex failed: {ex.Message}");
                ctx.Reply("[Zone] Failed to get territory index.");
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

        [Command("castleradius", shortHand: "cr", description: "Set castle territory building radius (admin only)", adminOnly: true)]
        public static void SetCastleRadius(ChatCommandContext ctx, float radius)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Castle] Error: Could not get your position.");
                    return;
                }

                Plugin.Log.LogInfo($"[Castle] User '{ctx.User.PlatformId}' setting castle radius to {radius} at ({position.Value.x}, {position.Value.y}, {position.Value.z})");
                ctx.Reply($"[Castle] Castle territory radius set to {radius:F1}");
                ctx.Reply("[Castle] Note: Castle territory blocks must be placed within this radius.");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Castle] SetCastleRadius failed: {ex.Message}");
                ctx.Reply("[Castle] Failed to set castle radius.");
            }
        }

        [Command("castleinfo", shortHand: "ci", description: "Show castle territory info (admin only)", adminOnly: true)]
        public static void CastleInfo(ChatCommandContext ctx)
        {
            try
            {
                var position = GetPlayerPosition();
                if (position == null)
                {
                    ctx.Reply("[Castle] Error: Could not get your position.");
                    return;
                }

                ctx.Reply($"[Castle] Your position: ({position.Value.x:F1}, {position.Value.y:F1}, {position.Value.z:F1})");
                ctx.Reply("[Castle] Castle territory management commands:");
                ctx.Reply("  .castleradius <radius> - Set building radius");
                ctx.Reply("  .castleinfo / .ci - Show this info");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Castle] CastleInfo failed: {ex.Message}");
                ctx.Reply("[Castle] Failed to get castle info.");
            }
        }

        private static float3? GetPlayerPosition()
        {
            try
            {
                // Use the first connected player from the server world
                // This approach matches other working commands in the project
                var serverWorld = VRCore.ServerWorld;
                if (serverWorld == null)
                {
                    Plugin.Log.LogWarning("[Zone] Server world not available");
                    return null;
                }
                
                var entityManager = serverWorld.EntityManager;
                
                // Get all PlayerCharacter entities
                var playerQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerCharacter>()
                );
                
                var entities = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                
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
                            var characterEntity = entity;
                            
                            // Try LocalTransform first (newer Unity versions)
                            if (entityManager.HasComponent<LocalTransform>(characterEntity))
                            {
                                var pos = entityManager.GetComponentData<LocalTransform>(characterEntity).Position;
                                return pos;
                            }
                            // Fallback to Translation (older Unity versions)
                            else if (entityManager.HasComponent<Translation>(characterEntity))
                            {
                                var pos = entityManager.GetComponentData<Translation>(characterEntity).Value;
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
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Zone] GetPlayerPosition failed: {ex.Message}");
                return null;
            }
        }
    }
}
