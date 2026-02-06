using VampireCommandFramework;
using ProjectM;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VAuto.Core.Components;
using VAuto.Data;
using VAuto.Services.World;

namespace VAuto.Commands.Core
{
    [CommandGroup("spawn", "Spawn commands for glows and prefabs")]
    public static class SpawnCommands
    {
        /// <summary>
        /// Spawn a circular glow zone around the player's position.
        /// Usage: .spawnglow circle <radius> [spacing]
        /// </summary>
        [Command("glow", "Spawn a circular glow zone around your position", adminOnly: true)]
        public static void SpawnGlowCommand(ICommandContext ctx, float radius = 10f, float spacing = 3f)
        {
            try
            {
                // Get player position
                var playerEntity = ctx.SenderUserEntity;
                if (!VRCore.EntityManager.HasComponent<Translation>(playerEntity))
                {
                    ctx.Reply(Plugin.Log, "[Spawn] Error: Could not get player position");
                    return;
                }

                var playerPos = VRCore.EntityManager.GetComponentData<Translation>(playerEntity).Value;
                
                // Generate unique zone name
                var zoneName = $"Glow_{DateTime.UtcNow:HHmmss}";
                
                // Build the glow zone
                GlowZoneService.BuildCircleZone(zoneName, playerPos, radius, spacing);
                
                ctx.Reply(Plugin.Log, $"[Spawn] Created circular glow zone '{zoneName}' at position ({playerPos.x:F1}, {playerPos.y:F1}, {playerPos.z:F1}) with radius {radius}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Spawn] Error spawning glow: {ex.Message}");
            }
        }

        /// <summary>
        /// Spawn a box glow zone around the player's position.
        /// Usage: .spawnglow box <width> <length> [spacing]
        /// </summary>
        [Command("glowbox", "Spawn a box glow zone around your position", adminOnly: true)]
        public static void SpawnGlowBoxCommand(ICommandContext ctx, float width = 10f, float length = 10f, float spacing = 3f)
        {
            try
            {
                var playerEntity = ctx.SenderUserEntity;
                if (!VRCore.EntityManager.HasComponent<Translation>(playerEntity))
                {
                    ctx.Reply(Plugin.Log, "[Spawn] Error: Could not get player position");
                    return;
                }

                var playerPos = VRCore.EntityManager.GetComponentData<Translation>(playerEntity).Value;
                
                var zoneName = $"GlowBox_{DateTime.UtcNow:HHmmss}";
                
                GlowZoneService.BuildBoxZone(zoneName, playerPos, new float2(width, length), spacing);
                
                ctx.Reply(Plugin.Log, $"[Spawn] Created box glow zone '{zoneName}' at position ({playerPos.x:F1}, {playerPos.y:F1}, {playerPos.z:F1}) with dimensions {width}x{length}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Spawn] Error spawning glow box: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all glow zones.
        /// Usage: .spawnglow clear
        /// </summary>
        [Command("glowclear", "Clear all glow zones", adminOnly: true)]
        public static void ClearGlowsCommand(ICommandContext ctx)
        {
            try
            {
                GlowZoneService.ClearAll();
                ctx.Reply(Plugin.Log, "[Spawn] Cleared all glow zones");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Spawn] Error clearing glows: {ex.Message}");
            }
        }

        /// <summary>
        /// Spawn a prefab at the player's position.
        /// Usage: .spawn prefabname [amount]
        /// </summary>
        [Command("spawn", "Spawn a prefab at your position", adminOnly: true)]
        public static void SpawnPrefabCommand(ICommandContext ctx, string prefabName, int amount = 1)
        {
            try
            {
                var playerEntity = ctx.SenderUserEntity;
                if (!VRCore.EntityManager.HasComponent<Translation>(playerEntity))
                {
                    ctx.Reply(Plugin.Log, "[Spawn] Error: Could not get player position");
                    return;
                }

                var playerPos = VRCore.EntityManager.GetComponentData<Translation>(playerEntity).Value;

                // Find prefab by name
                var prefabGuid = FindPrefabByName(prefabName);
                if (prefabGuid == null)
                {
                    ctx.Reply(Plugin.Log, $"[Spawn] Error: Prefab '{prefabName}' not found");
                    return;
                }

                var spawned = SpawnPrefab(prefabGuid.Value, playerPos, amount);
                
                ctx.Reply(Plugin.Log, $"[Spawn] Spawned {spawned} instance(s) of '{prefabName}' at your position");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Spawn] Error spawning prefab: {ex.Message}");
            }
        }

        /// <summary>
        /// List available prefabs.
        /// Usage: .spawn list [search]
        /// </summary>
        [Command("spawnlist", "List available prefabs (optional: search filter)", adminOnly: true)]
        public static void ListPrefabsCommand(ICommandContext ctx, string searchFilter = "")
        {
            try
            {
                // Get all prefab field names from Prefabs class
                var prefabNames = typeof(Prefabs).GetFields()
                    .Where(f => f.FieldType == typeof(PrefabGUID))
                    .Select(f => f.Name)
                    .Where(name => string.IsNullOrEmpty(searchFilter) || name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    .Take(20) // Limit output
                    .ToList();

                if (prefabNames.Count == 0)
                {
                    ctx.Reply(Plugin.Log, "[Spawn] No prefabs found matching filter");
                    return;
                }

                ctx.Reply(Plugin.Log, $"[Spawn] Available prefabs (showing {prefabNames.Count}):");
                foreach (var name in prefabNames)
                {
                    ctx.Reply(Plugin.Log, $"  - {name}");
                }
                
                if (prefabNames.Count == 20 && !string.IsNullOrEmpty(searchFilter))
                {
                    ctx.Reply(Plugin.Log, $"  ... (use different filter to see more)");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Spawn] Error listing prefabs: {ex.Message}");
            }
        }

        /// <summary>
        /// Give an item to the player.
        /// Usage: .giveitem itemname [amount]
        /// </summary>
        [Command("giveitem", "Give an item to yourself", adminOnly: true)]
        public static void GiveItemCommand(ICommandContext ctx, string itemName, int amount = 1)
        {
            try
            {
                var playerEntity = ctx.SenderUserEntity;
                var serverGameManager = VRCore.ServerWorld?.GetExistingSystemManaged<ServerGameManager>();
                
                if (serverGameManager == null)
                {
                    ctx.Reply(Plugin.Log, "[GiveItem] Error: ServerGameManager not available");
                    return;
                }

                // Find item prefab
                var itemGuid = FindPrefabByName(itemName);
                if (itemGuid == null)
                {
                    ctx.Reply(Plugin.Log, $"[GiveItem] Error: Item '{itemName}' not found");
                    return;
                }

                var success = serverGameManager.TryGiveItem(playerEntity, itemGuid.Value, amount);
                
                if (success)
                {
                    ctx.Reply(Plugin.Log, $"[GiveItem] Successfully gave {amount}x '{itemName}' to player");
                }
                else
                {
                    ctx.Reply(Plugin.Log, $"[GiveItem] Failed to give item '{itemName}' (inventory full or invalid item)");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[GiveItem] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Give an item to a specific player.
        /// Usage: .giveitemto player itemname [amount]
        /// </summary>
        [Command("giveitemto", "Give an item to another player", adminOnly: true)]
        public static void GiveItemToCommand(ICommandContext ctx, string targetPlayerName, string itemName, int amount = 1)
        {
            try
            {
                // Find target player
                var targetEntity = FindPlayerByName(targetPlayerName);
                if (targetEntity == Entity.Null)
                {
                    ctx.Reply(Plugin.Log, $"[GiveItem] Error: Player '{targetPlayerName}' not found");
                    return;
                }

                var serverGameManager = VRCore.ServerWorld?.GetExistingSystemManaged<ServerGameManager>();
                if (serverGameManager == null)
                {
                    ctx.Reply(Plugin.Log, "[GiveItem] Error: ServerGameManager not available");
                    return;
                }

                // Find item prefab
                var itemGuid = FindPrefabByName(itemName);
                if (itemGuid == null)
                {
                    ctx.Reply(Plugin.Log, $"[GiveItem] Error: Item '{itemName}' not found");
                    return;
                }

                var success = serverGameManager.TryGiveItem(targetEntity, itemGuid.Value, amount);
                
                if (success)
                {
                    ctx.Reply(Plugin.Log, $"[GiveItem] Successfully gave {amount}x '{itemName}' to '{targetPlayerName}'");
                }
                else
                {
                    ctx.Reply(Plugin.Log, $"[GiveItem] Failed to give item to '{targetPlayerName}'");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[GiveItem] Error: {ex.Message}");
            }
        }

        #region Zone Commands

        /// <summary>
        /// List all configured zones.
        /// Usage: .zone list
        /// </summary>
        [CommandGroup("zone", "Zone management commands")]
        public static class ZoneCommands
        {
            [Command("list", "List all configured zones", adminOnly: true)]
            public static void ListZonesCommand(ICommandContext ctx)
            {
                try
                {
                    var zoneTracking = VRCore.ServiceContainer.GetService<VAuto.Services.Systems.ZoneTrackingService>();
                    if (zoneTracking == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] ZoneTrackingService not available");
                        return;
                    }

                    // Access zone boundaries via reflection (since _zoneBoundaries is private)
                    var zoneBoundariesField = typeof(VAuto.Services.Systems.ZoneTrackingService)
                        .GetField("_zoneBoundaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (zoneBoundariesField == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] Could not access zone boundaries");
                        return;
                    }

                    var zoneBoundaries = zoneBoundariesField.GetValue(zoneTracking) as System.Collections.IList;
                    if (zoneBoundaries == null || zoneBoundaries.Count == 0)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] No zones configured");
                        return;
                    }

                    ctx.Reply(Plugin.Log, $"[Zone] Configured zones ({zoneBoundaries.Count}):");
                    
                    foreach (var zone in zoneBoundaries)
                    {
                        var zoneType = zone.GetType().GetProperty("ZoneType")?.GetValue(zone);
                        var zoneId = zone.GetType().GetProperty("ZoneId")?.GetValue(zone);
                        var center = zone.GetType().GetProperty("Center")?.GetValue(zone);
                        var radius = zone.GetType().GetProperty("Radius")?.GetValue(zone);
                        var pvpEnabled = zone.GetType().GetProperty("PvPEnabled")?.GetValue(zone);
                        var safeZone = zone.GetType().GetProperty("SafeZone")?.GetValue(zone);
                        
                        var centerStr = center?.ToString() ?? "unknown";
                        ctx.Reply(Plugin.Log, $"  Zone {zoneId}: {zoneType} | Radius: {radius} | Center: {centerStr} | PvP: {pvpEnabled} | Safe: {safeZone}");
                    }
                }
                catch (Exception ex)
                {
                    ctx.Reply(Plugin.Log, $"[Zone] Error listing zones: {ex.Message}");
                }
            }

            /// <summary>
            /// Set the radius of a zone.
            /// Usage: .zoneset radius <zoneId> <radius>
            /// </summary>
            [Command("setradius", "Set the radius of a zone", adminOnly: true)]
            public static void SetZoneRadiusCommand(ICommandContext ctx, int zoneId, float radius)
            {
                try
                {
                    if (radius <= 0)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] Error: Radius must be positive");
                        return;
                    }

                    var zoneTracking = VRCore.ServiceContainer.GetService<VAuto.Services.Systems.ZoneTrackingService>();
                    if (zoneTracking == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] ZoneTrackingService not available");
                        return;
                    }

                    var zoneBoundariesField = typeof(VAuto.Services.Systems.ZoneTrackingService)
                        .GetField("_zoneBoundaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (zoneBoundariesField == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] Could not access zone boundaries");
                        return;
                    }

                    var zoneBoundaries = zoneBoundariesField.GetValue(zoneTracking) as System.Collections.IList;
                    if (zoneBoundaries == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] No zones configured");
                        return;
                    }

                    bool found = false;
                    foreach (var zone in zoneBoundaries)
                    {
                        var currentZoneId = zone.GetType().GetProperty("ZoneId")?.GetValue(zone);
                        if (currentZoneId is int id && id == zoneId)
                        {
                            zone.GetType().GetProperty("Radius")?.SetValue(zone, radius);
                            found = true;
                            ctx.Reply(Plugin.Log, $"[Zone] Set Zone {zoneId} radius to {radius}");
                            
                            // Rebuild glow if zone has glow
                            var zoneType = zone.GetType().GetProperty("ZoneType")?.GetValue(zone);
                            var center = zone.GetType().GetProperty("Center")?.GetValue(zone);
                            if (center is float3 centerPos)
                            {
                                var glowName = $"ZoneGlow_{zoneId}";
                                GlowZoneService.ClearZone(glowName);
                                GlowZoneService.BuildCircleZone(glowName, centerPos, radius);
                                ctx.Reply(Plugin.Log, $"[Zone] Rebuilt glow for Zone {zoneId}");
                            }
                            break;
                        }
                    }

                    if (!found)
                    {
                        ctx.Reply(Plugin.Log, $"[Zone] Error: Zone {zoneId} not found");
                    }
                }
                catch (Exception ex)
                {
                    ctx.Reply(Plugin.Log, $"[Zone] Error setting radius: {ex.Message}");
                }
            }

            /// <summary>
            /// Add glow to a zone at the player's position.
            /// Usage: .zone glow <zoneId>
            /// </summary>
            [Command("glow", "Add glow to a zone at your position", adminOnly: true)]
            public static void ZoneGlowCommand(ICommandContext ctx, int zoneId)
            {
                try
                {
                    var playerEntity = ctx.SenderUserEntity;
                    if (!VRCore.EntityManager.HasComponent<Translation>(playerEntity))
                    {
                        ctx.Reply(Plugin.Log, "[Zone] Error: Could not get player position");
                        return;
                    }

                    var playerPos = VRCore.EntityManager.GetComponentData<Translation>(playerEntity).Value;

                    var zoneTracking = VRCore.ServiceContainer.GetService<VAuto.Services.Systems.ZoneTrackingService>();
                    if (zoneTracking == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] ZoneTrackingService not available");
                        return;
                    }

                    var zoneBoundariesField = typeof(VAuto.Services.Systems.ZoneTrackingService)
                        .GetField("_zoneBoundaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (zoneBoundariesField == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] Could not access zone boundaries");
                        return;
                    }

                    var zoneBoundaries = zoneBoundariesField.GetValue(zoneTracking) as System.Collections.IList;
                    if (zoneBoundaries == null)
                    {
                        ctx.Reply(Plugin.Log, "[Zone] No zones configured");
                        return;
                    }

                    bool found = false;
                    foreach (var zone in zoneBoundaries)
                    {
                        var currentZoneId = zone.GetType().GetProperty("ZoneId")?.GetValue(zone);
                        if (currentZoneId is int id && id == zoneId)
                        {
                            var radius = zone.GetType().GetProperty("Radius")?.GetValue(zone);
                            var radiusValue = radius is float r ? r : 30f;
                            
                            // Update zone center to player's position
                            zone.GetType().GetProperty("Center")?.SetValue(zone, playerPos);
                            
                            // Build glow at player's position
                            var glowName = $"ZoneGlow_{zoneId}";
                            GlowZoneService.BuildCircleZone(glowName, playerPos, radiusValue);
                            
                            found = true;
                            ctx.Reply(Plugin.Log, $"[Zone] Added glow to Zone {zoneId} at your position ({playerPos.x:F1}, {playerPos.y:F1}, {playerPos.z:F1}) with radius {radiusValue}");
                            break;
                        }
                    }

                    if (!found)
                    {
                        ctx.Reply(Plugin.Log, $"[Zone] Error: Zone {zoneId} not found");
                    }
                }
                catch (Exception ex)
                {
                    ctx.Reply(Plugin.Log, $"[Zone] Error adding glow: {ex.Message}");
                }
            }

            /// <summary>
            /// Clear glow from a zone.
            /// Usage: .zone clearglow <zoneId>
            /// </summary>
            [Command("clearglow", "Clear glow from a zone", adminOnly: true)]
            public static void ZoneClearGlowCommand(ICommandContext ctx, int zoneId)
            {
                try
                {
                    var glowName = $"ZoneGlow_{zoneId}";
                    GlowZoneService.ClearZone(glowName);
                    ctx.Reply(Plugin.Log, $"[Zone] Cleared glow from Zone {zoneId}");
                }
                catch (Exception ex)
                {
                    ctx.Reply(Plugin.Log, $"[Zone] Error clearing glow: {ex.Message}");
                }
            }
        }

        #endregion

        #region Helper Methods

        private static PrefabGUID? FindPrefabByName(string name)
        {
            // Try exact match first
            var field = typeof(Prefabs).GetField(name);
            if (field != null && field.FieldType == typeof(PrefabGUID))
            {
                return (PrefabGUID)field.GetValue(null)!;
            }

            // Try case-insensitive match
            field = typeof(Prefabs).GetFields()
                .FirstOrDefault(f => f.FieldType == typeof(PrefabGUID) && 
                    f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            
            if (field != null)
            {
                return (PrefabGUID)field.GetValue(null)!;
            }

            return null;
        }

        private static Entity FindPlayerByName(string playerName)
        {
            var entityManager = VRCore.EntityManager;
            
            // Get all player entities with User component
            var userQuery = entityManager.CreateEntityQuery(typeof(User));
            var users = userQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            try
            {
                foreach (var userEntity in users)
                {
                    var user = entityManager.GetComponentData<User>(userEntity);
                    if (user.CharacterName?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // Get the character entity
                        if (entityManager.HasComponent<Character>(userEntity))
                        {
                            return userEntity;
                        }
                    }
                }
            }
            finally
            {
                users.Dispose();
            }
            
            return Entity.Null;
        }

        private static int SpawnPrefab(PrefabGUID prefabGuid, float3 position, int amount)
        {
            var entityManager = VRCore.EntityManager;
            var prefabCollectionSystem = VRCore.ServerWorld?.GetExistingSystemManaged<PrefabCollectionSystem>();
            
            if (prefabCollectionSystem == null || 
                !prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(prefabGuid, out var prefabEntity))
            {
                return 0;
            }

            var spawned = 0;
            for (int i = 0; i < amount; i++)
            {
                // Offset each spawn slightly
                var offset = new float3(
                    (i % 3 - 1) * 0.5f,
                    0,
                    (i / 3 - 1) * 0.5f
                );
                
                var entity = entityManager.Instantiate(prefabEntity);
                if (entity != Entity.Null)
                {
                    entityManager.SetComponentData(entity, new Translation 
                    { 
                        Value = position + offset 
                    });
                    spawned++;
                }
            }
            
            return spawned;
        }

        #endregion
    }
}


