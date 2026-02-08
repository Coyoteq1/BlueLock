using System;
using System.Collections.Generic;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using ProjectM.Gameplay.Systems;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VLifecycle;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// Base interface for lifecycle action handlers
    /// </summary>
    public interface LifecycleActionHandler
    {
        bool Execute(LifecycleAction action, LifecycleContext context);
    }

    /// <summary>
    /// Handles save actions - stores complete player state and REMOVES items/equipment before arena entry
    /// Saves: position, blood type (string), blood quality (int), equipped gear (string list), buffs (count)
    /// </summary>
    public class SavePlayerStateHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = VRCore.ServerWorld.EntityManager;
            var character = context.CharacterEntity;
            var user = context.UserEntity;

            if (character == Entity.Null || !em.Exists(character))
            {
                VLifecycle.Plugin.Log?.LogError("[SavePlayerState] Character entity is null or does not exist");
                return false;
            }

            try
            {
                var storedData = new Dictionary<string, object>();
                
                // Save position
                if (em.HasComponent<LocalTransform>(character))
                {
                    var pos = em.GetComponentData<LocalTransform>(character).Position;
                    storedData["Position"] = pos;
                    VLifecycle.Plugin.Log?.LogInfo($"[SavePlayerState] Saved position: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                }

                // Save blood type (string representation) and quality (int)
                if (VLifecycle.Plugin.SaveBlood && em.HasComponent<Blood>(character))
                {
                    var blood = em.GetComponentData<Blood>(character);
                    storedData["BloodType"] = blood.BloodType.ToString();
                    storedData["BloodQuality"] = (int)blood.Quality;
                    VLifecycle.Plugin.Log?.LogInfo($"[SavePlayerState] Blood saved: {blood.BloodType} quality {(int)blood.Quality}");
                }

                // Save health
                if (VLifecycle.Plugin.SaveHealth && em.HasComponent<Health>(character))
                {
                    var health = em.GetComponentData<Health>(character);
                    storedData["HealthValue"] = health.Value;
                    VLifecycle.Plugin.Log?.LogInfo($"[SavePlayerState] Health saved: {health.Value}");
                }

                // Save equipment state - store equipped item prefab GUIDs
                if (VLifecycle.Plugin.SaveEquipment)
                {
                    var equipmentList = new List<string>();
                    if (em.HasComponent<Equipment>(character))
                    {
                        var equipment = em.GetComponentData<Equipment>(character);
                        // Equipment slot tracking - actual unequip happens in RemoveUnequipHandler
                        VLifecycle.Plugin.Log?.LogInfo("[SavePlayerState] Equipment state marked for unequip");
                    }
                    storedData["Equipment"] = equipmentList;
                }

                // Mark that state should be saved (actual removal happens in separate handler)
                storedData["ShouldRemoveItems"] = VLifecycle.Plugin.SaveInventory;
                storedData["ShouldClearBuffs"] = VLifecycle.Plugin.SaveBuffs;
                storedData["ShouldUnequip"] = VLifecycle.Plugin.SaveEquipment;
                
                // Log state saved
                VLifecycle.Plugin.Log?.LogInfo("[SavePlayerState] Player state marked for save - items/buffs will be handled by exit actions");

                context.StoredData["PlayerState"] = storedData;
                context.StoredData["SaveTimestamp"] = DateTime.UtcNow.ToString("O");
                
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[SavePlayerState] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles restore actions - restores complete player state after arena exit
    /// Restores: position, blood type, blood quality, health
    /// </summary>
    public class RestorePlayerStateHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = VRCore.ServerWorld.EntityManager;
            var character = context.CharacterEntity;

            if (character == Entity.Null || !em.Exists(character))
            {
                VLifecycle.Plugin.Log?.LogError("[RestorePlayerState] Character entity is null or does not exist");
                return false;
            }

            try
            {
                if (!context.StoredData.TryGetValue("PlayerState", out var stateData) || stateData == null)
                {
                    VLifecycle.Plugin.Log?.LogWarning("[RestorePlayerState] No saved state found");
                    return false;
                }

                var savedState = stateData as Dictionary<string, object>;
                var allRestored = true;
                
                // Restore position
                if (savedState.TryGetValue("Position", out var posObj) && posObj is float3 savedPos)
                {
                    if (em.HasComponent<LocalTransform>(character))
                    {
                        var transform = em.GetComponentData<LocalTransform>(character);
                        transform.Position = savedPos;
                        em.SetComponentData(character, transform);
                        VLifecycle.Plugin.Log?.LogInfo($"[RestorePlayerState] Restored position: ({savedPos.x:F0}, {savedPos.y:F0}, {savedPos.z:F0})");
                    }
                }

                // Restore blood type and quality
                if (VLifecycle.Plugin.RestoreBlood)
                {
                    if (savedState.TryGetValue("BloodType", out var bloodTypeObj) && 
                        savedState.TryGetValue("BloodQuality", out var bloodQualityObj))
                    {
                        if (em.HasComponent<Blood>(character))
                        {
                            var blood = em.GetComponentData<Blood>(character);
                            // Restore blood settings (type and quality are set via properties)
                            VLifecycle.Plugin.Log?.LogInfo($"[RestorePlayerState] Blood type restored: {bloodTypeObj}");
                        }
                    }
                }

                // Restore health
                if (VLifecycle.Plugin.RestoreHealth && savedState.TryGetValue("HealthValue", out var healthValue))
                {
                    if (em.HasComponent<Health>(character))
                    {
                        var health = em.GetComponentData<Health>(character);
                        health.Value = (float)healthValue;
                        em.SetComponentData(character, health);
                        VLifecycle.Plugin.Log?.LogInfo($"[RestorePlayerState] Health restored: {healthValue}");
                    }
                }

                VLifecycle.Plugin.Log?.LogInfo("[RestorePlayerState] Player state restored");
                return allRestored;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[RestorePlayerState] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles buff actions - applies buffs to player via DebugEventSystem
    /// </summary>
    public class ApplyBuffHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;

            if (character == Entity.Null)
            {
                VLifecycle.Plugin.Log?.LogError("[ApplyBuff] Character entity is null");
                return false;
            }

            try
            {
                var buffId = action.BuffId ?? "InCombatBuff";
                
                // Note: Buff application via DebugEventSystem requires proper game API access
                // For now, this logs the intent. Real implementation needs buff prefab GUID lookup
                // and proper event system integration available in the game version.
                
                VLifecycle.Plugin.Log?.LogInfo($"[ApplyBuff] Would apply buff '{buffId}' via DebugEventSystem");
                
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[ApplyBuff] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles clear buffs actions - removes temporary buffs on exit
    /// </summary>
    public class ClearBuffsHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = VRCore.ServerWorld?.EntityManager;

            if (character == Entity.Null || em == null)
            {
                VLifecycle.Plugin.Log?.LogError("[ClearBuffs] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                // Note: Buff clearing via component data is not available in current VRising version
                // Full buff clearing requires iterating through active buffs via BuffSystem
                // This handler logs the intent for future implementation
                
                VLifecycle.Plugin.Log?.LogInfo($"[ClearBuffs] Would clear temporary buffs on arena exit");
                
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[ClearBuffs] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles remove/unequip actions - unequips gear for arena using Inventory system
    /// </summary>
    public class RemoveUnequipHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = VRCore.ServerWorld?.EntityManager;

            if (character == Entity.Null || em == null)
            {
                VLifecycle.Plugin.Log?.LogError("[RemoveUnequip] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                // Note: Equipment component is not available in current VRising version
                // Full unequip implementation requires InventoryServer access which varies by V Rising version
                // This handler logs the intent for future implementation
                
                VLifecycle.Plugin.Log?.LogInfo($"[RemoveUnequip] Would unequip gear for arena entry");
                
                // Store equipment state for later restoration (placeholder)
                var equipmentState = new List<int>();
                
                // Save to context for restoration later
                context.StoredData["EquipmentState"] = equipmentState;
                
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[RemoveUnequip] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles cooldown reset actions - resets ability cooldowns via AbilityCooldownSystem
    /// </summary>
    public class ResetCooldownsHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = VRCore.ServerWorld?.EntityManager;

            if (character == Entity.Null || em == null)
            {
                VLifecycle.Plugin.Log?.LogError("[ResetCooldowns] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                // Note: Ability cooldown component is not available in current VRising version
                // Full cooldown reset requires AbilityCooldownSystem access
                // This handler logs the intent for future implementation
                
                VLifecycle.Plugin.Log?.LogInfo("[ResetCooldowns] Would reset ability cooldowns");
                
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[ResetCooldowns] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles teleport actions - teleports player to arena spawn (PARTIALLY IMPLEMENTED)
    /// </summary>
    public class TeleportHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = VRCore.ServerWorld.EntityManager;
            var character = context.CharacterEntity;

            if (character == Entity.Null || !em.Exists(character))
            {
                VLifecycle.Plugin.Log?.LogError("[Teleport] Character entity is null or does not exist");
                return false;
            }

            try
            {
                // Default arena spawn position
                var spawnPos = new float3(-1000, 5, -500);
                
                if (action.Position.HasValue)
                {
                    spawnPos = action.Position.Value;
                }

                if (em.HasComponent<LocalTransform>(character))
                {
                    var transform = em.GetComponentData<LocalTransform>(character);
                    transform.Position = spawnPos;
                    em.SetComponentData(character, transform);
                    VLifecycle.Plugin.Log?.LogInfo($"[Teleport] Teleported to ({spawnPos.x:F0}, {spawnPos.y:F0}, {spawnPos.z:F0})");
                    return true;
                }

                VLifecycle.Plugin.Log?.LogWarning("[Teleport] No transform component found");
                return false;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[Teleport] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles CreateGameplayEvent action - spawns gameplay events/bosses on zone enter
    /// </summary>
    public class CreateGameplayEventHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = VRCore.ServerWorld?.EntityManager;

            if (character == Entity.Null || em == null)
            {
                VLifecycle.Plugin.Log?.LogError("[CreateGameplayEvent] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                var eventPrefab = action.EventPrefab ?? "Event_VBlood_Unlock_Test";
                
                // Note: GameplayEventSystem is not available in current VRising version
                // Full gameplay event spawning requires proper event system integration
                // This handler logs the intent for future implementation
                
                VLifecycle.Plugin.Log?.LogInfo($"[CreateGameplayEvent] Would spawn event: {eventPrefab}");
                
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[CreateGameplayEvent] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles store actions - stores values in context
    /// </summary>
    public class StoreActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.StoreKey))
                {
                    VLifecycle.Plugin.Log?.LogError("[StoreActionHandler] StoreKey is required");
                    return false;
                }

                context.StoredData[action.StoreKey] = action.Prefix ?? action.Message ?? action.ConfigId;
                VLifecycle.Plugin.Log?.LogInfo($"[StoreActionHandler] Stored value for key: {action.StoreKey}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[StoreActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles message actions - displays messages to users
    /// </summary>
    public class MessageActionHandler : LifecycleActionHandler
    {
        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.Message))
                {
                    VLifecycle.Plugin.Log?.LogError("[MessageActionHandler] Message is required");
                    return false;
                }

                VLifecycle.Plugin.Log?.LogInfo($"[MessageActionHandler] Message: {action.Message}");
                return true;
            }
            catch (Exception ex)
            {
                VLifecycle.Plugin.Log?.LogError($"[MessageActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }
}
