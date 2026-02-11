using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VampireCommandFramework;
using VAuto.Zone.Core;
using VAuto.Zone;
using VAutomationCore;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.ECS;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Commands for unlocking VBlood Tech_Collection prefabs directly.
    /// Allows testing unlocks by parsing VBloodUnlockTechBuffer from VBlood boss entities.
    /// </summary>
    public static class VBloodUnlockCommands
    {
        private static readonly string LogPrefix = "[VBloodUnlock]";

        /// <summary>
        /// Unlock VBlood Tech_Collection prefab by parsing VBloodUnlockTechBuffer from VBlood entities.
        /// Usage: .unlockprefab [vbloodName]
        /// </summary>
        [Command("unlockprefab", description: "Unlock VBlood Tech_Collection prefab for testing", adminOnly: true)]
        public static void UnlockPrefabCommand(ChatCommandContext ctx, string vbloodName = null)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                var user = ctx.User;
                var playerName = user.CharacterName.ToString();
                
                ZoneCore.LogInfo($"{LogPrefix} command by {playerName}, vbloodName: {vbloodName ?? "all"}");
                
                // Find all VBlood entities with VBloodUnlockTechBuffer
                var vbloodQuery = em.CreateEntityQuery(ComponentType.ReadOnly<VBloodUnlockTechBuffer>());
                var vbloodEntities = vbloodQuery.ToEntityArray(Allocator.Temp);
                
                if (vbloodEntities.Length == 0)
                {
                    ctx.Reply("<color=#FF0000>[VBloodUnlock] No VBlood entities found with VBloodUnlockTechBuffer.</color>");
                    vbloodEntities.Dispose();
                    return;
                }
                
                ZoneCore.LogInfo($"Found {vbloodEntities.Length} VBlood entities");
                
                var unlockedCount = 0;
                var errors = new List<string>();
                
                foreach (var entity in vbloodEntities)
                {
                    try
                    {
                        // Get entity name/description (using entity index as fallback)
                        var entityName = $"Entity_{entity.Index}";
                        
                        // Filter by vbloodName if specified
                        if (!string.IsNullOrEmpty(vbloodName) && 
                            !entityName.Contains(vbloodName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        
                        // Read the VBloodUnlockTechBuffer
                        var buffer = em.GetBuffer<VBloodUnlockTechBuffer>(entity);
                        
                        if (buffer.Length == 0)
                        {
                            ZoneCore.LogWarning($"Entity {entityName} has empty buffer");
                            continue;
                        }
                        
                        ZoneCore.LogInfo($"Processing {entityName} with {buffer.Length} tech buffer entries");
                        
                        foreach (var entry in buffer)
                        {
                            // Try to get PrefabGuid from buffer entry
                            var prefabGuid = new PrefabGUID();
                            
                            // Try to access the prefab GUID property
                            var type = entry.GetType();
                            var guidProperty = type.GetProperty("PrefabGuid");
                            if (guidProperty != null)
                            {
                                prefabGuid = (PrefabGUID)guidProperty.GetValue(entry);
                            }
                            
                            ZoneCore.LogInfo($"Entry PrefabGuid: {prefabGuid}");
                            
                            // Try to resolve the prefab GUID to get the Tech_Collection name
                            var prefabName = ResolvePrefabName(prefabGuid);
                            if (prefabName != null)
                            {
                                ZoneCore.LogInfo($"Resolved prefab: {prefabName}");
                                
                                // Here we would actually unlock the prefab for the player
                                // For testing purposes, just log it
                                UnlockTechCollectionForPlayer(user, prefabGuid);
                                unlockedCount++;
                            }
                            else
                            {
                                ZoneCore.LogWarning($"Could not resolve PrefabGuid: {prefabGuid}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var entityDebug = em.Exists(entity) ? "unknown" : "invalid";
                        ZoneCore.LogError($"Error processing entity: {ex.Message}");
                        errors.Add($"Error processing entity: {ex.Message}");
                    }
                }
                
                vbloodEntities.Dispose();
                
                // Send summary to player
                if (unlockedCount > 0)
                {
                    var message = $"<color=#00FF00>[VBloodUnlock] Successfully processed {unlockedCount} Tech_Collection prefab(s).</color>";
                    if (!string.IsNullOrEmpty(vbloodName))
                    {
                        message += $" Filter: {vbloodName}";
                    }
                    ctx.Reply(message);
                }
                else
                {
                    ctx.Reply("<color=#FF0000>[VBloodUnlock] No matching VBlood entities found.</color>");
                }
                
                if (errors.Count > 0)
                {
                    ZoneCore.LogWarning($"Errors: {string.Join(", ", errors)}");
                }
                
                ZoneCore.LogInfo($"Command completed: {unlockedCount} unlocks, {errors.Count} errors");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"Error: {ex.Message}");
                ctx.Reply("<color=#FF0000>[VBloodUnlock] An error occurred processing your command.</color>");
            }
        }
        
        private static Dictionary<int, string> _prefabIndex;

        /// <summary>
        /// Resolve a PrefabGuid to a prefab name using the PrefabIndex.json file.
        /// </summary>
        private static string ResolvePrefabName(PrefabGUID prefabGuid)
        {
            try
            {
                // Lazy-load the index once
                if (_prefabIndex == null)
                {
                    var jsonPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "PrefabIndex.json");
                    if (System.IO.File.Exists(jsonPath))
                    {
                        var json = System.IO.File.ReadAllText(jsonPath);
                        _prefabIndex = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
                    }
                    else
                    {
                        ZoneCore.LogWarning($"{LogPrefix} PrefabIndex.json not found.");
                        _prefabIndex = new Dictionary<int, string>();
                    }
                }

                // Lookup the GUID hash
                if (_prefabIndex.TryGetValue(prefabGuid.GuidHash, out var name))
                {
                    return name;
                }

                // Fallback to using GUID hash directly
                return $"Prefab_{prefabGuid.GuidHash}";
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"{LogPrefix} Failed to resolve prefab name: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Unlock the Tech_Collection prefab for the player using the game's API.
        /// Note: This is a placeholder - actual tech unlock requires ServerGameManager.GrantTech
        /// which may not be accessible in this context.
        /// </summary>
        private static void UnlockTechCollectionForPlayer(User user, PrefabGUID techCollectionGuid)
        {
            try
            {
                // The actual tech unlock would use:
                // var unlockEvent = new GrantTechEvent { UserIndex = user.Index, TechPrefabGuid = techCollectionGuid };
                // VRCore.SERVER.GetExistingSystem<ServerGameManager>().GrantTech(unlockEvent);
                
                // For now, just log the unlock intent
                var playerName = user.CharacterName.ToString();
                var prefabName = ResolvePrefabName(techCollectionGuid);
                ZoneCore.LogInfo($"{LogPrefix} Would unlock Tech_Collection {prefabName ?? techCollectionGuid.ToString()} for player {playerName}");
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"{LogPrefix} Could not unlock tech collection: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a tech collection is already unlocked for the player.
        /// Note: Placeholder - actual implementation depends on server API availability.
        /// </summary>
        private static bool IsTechCollectionUnlocked(ulong steamId, int techCollectionPrefabId)
        {
            // Placeholder - actual implementation would check player tech state via server API
            // This requires access to PlayerTechState or similar server-side component
            _ = steamId; // Suppress unused warning for placeholder
            _ = techCollectionPrefabId;
            return false;
        }
        
        /// <summary>
        /// List all available Tech_Collection prefabs from VBlood entities.
        /// Usage: .unlockprefab list
        /// </summary>
        [Command("unlockprefab", "list", description: "List all available Tech_Collection prefabs", adminOnly: true)]
        public static void UnlockPrefabListCommand(ChatCommandContext ctx)
        {
            try
            {
                var em = ZoneCore.EntityManager;
                
                var vbloodQuery = em.CreateEntityQuery(ComponentType.ReadOnly<VBloodUnlockTechBuffer>());
                var vbloodEntities = vbloodQuery.ToEntityArray(Allocator.Temp);
                
                if (vbloodEntities.Length == 0)
                {
                    ctx.Reply("<color=#FF0000>[VBloodUnlock] No VBlood entities found.</color>");
                    vbloodEntities.Dispose();
                    return;
                }
                
                var message = "<color=#FFD700>[VBloodUnlock] Available Tech_Collections:</color>\n";
                var index = 0;
                
                foreach (var entity in vbloodEntities)
                {
                    var entityName = $"Entity_{entity.Index}";
                    var buffer = em.GetBuffer<VBloodUnlockTechBuffer>(entity);
                    
                    foreach (var entry in buffer)
                    {
                        // Try to get PrefabGuid from buffer entry
                        var prefabGuid = new PrefabGUID();
                        
                        var type = entry.GetType();
                        var guidProperty = type.GetProperty("PrefabGuid");
                        if (guidProperty != null)
                        {
                            prefabGuid = (PrefabGUID)guidProperty.GetValue(entry);
                        }
                        
                        var prefabName = ResolvePrefabName(prefabGuid);
                        message += $"<color=#00FFFF>{index++}. {entityName}</color> -> ";
                        message += $"<color=#00FF00>{prefabName ?? "Unknown"}</color>\n";
                    }
                }
                
                vbloodEntities.Dispose();
                
                // Limit message length
                if (message.Length > 1500)
                {
                    message = message.Substring(0, 1500) + "\n<color=#FF0000>... (truncated)</color>";
                }
                
                ctx.Reply(message);
                ZoneCore.LogInfo($"{LogPrefix} Listed {index} Tech_Collection prefabs");
            }
            catch (Exception ex)
            {
                ZoneCore.LogError($"{LogPrefix} List error: {ex.Message}");
                ctx.Reply("<color=#FF0000>[VBloodUnlock] An error occurred listing prefabs.</color>");
            }
        }
    }
}
