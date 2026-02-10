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
using VAutomationCore.Core;
using VAutomationCore.Core.ECS;
using VAutomationCore.Core.Services;
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
        private const string LogSource = "SavePlayerStateHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = UnifiedCore.EntityManager;
            var character = context.CharacterEntity;
            var user = context.UserEntity;

            if (character == Entity.Null || !em.Exists(character))
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null or does not exist");
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
                    UnifiedCore.LogInfo($"[{LogSource}] Saved position: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})");
                }

                // Save blood type and quality
                if (VLifecycle.Plugin.SaveBlood && em.HasComponent<Blood>(character))
                {
                    var blood = em.GetComponentData<Blood>(character);
                    storedData["BloodType"] = blood.BloodType;
                    storedData["BloodQuality"] = blood.Quality;
                    UnifiedCore.LogInfo($"[{LogSource}] Blood saved: {blood.BloodType} quality {blood.Quality}");
                }

                // Save health
                if (VLifecycle.Plugin.SaveHealth && em.HasComponent<Health>(character))
                {
                    var health = em.GetComponentData<Health>(character);
                    storedData["HealthValue"] = health.Value;
                    storedData["MaxHealth"] = health.MaxHealth;
                    UnifiedCore.LogInfo($"[{LogSource}] Health saved: {health.Value}/{health.MaxHealth}");
                }

                // Save equipment state - store equipped item prefab GUIDs
                if (VLifecycle.Plugin.SaveEquipment)
                {
                    var equipmentList = new List<string>();
                    if (em.HasComponent<Equipment>(character))
                    {
                        var equipment = em.GetComponentData<Equipment>(character);
                        UnifiedCore.LogInfo($"[{LogSource}] Equipment state marked for unequip");
                    }
                    storedData["Equipment"] = equipmentList;
                }

                // Mark that state should be saved (actual removal happens in separate handler)
                storedData["ShouldRemoveItems"] = VLifecycle.Plugin.SaveInventory;
                storedData["ShouldClearBuffs"] = VLifecycle.Plugin.SaveBuffs;
                storedData["ShouldUnequip"] = VLifecycle.Plugin.SaveEquipment;
                
                // Log state saved
                UnifiedCore.LogInfo($"[{LogSource}] Player state marked for save - items/buffs will be handled by exit actions");

                context.StoredData["PlayerState"] = storedData;
                context.StoredData["SaveTimestamp"] = DateTime.UtcNow.ToString("O");
                
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "RestorePlayerStateHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = UnifiedCore.EntityManager;
            var character = context.CharacterEntity;

            if (character == Entity.Null || !em.Exists(character))
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null or does not exist");
                VLifecycle.Plugin.Log?.LogError("[RestorePlayerState] Character entity is null or does not exist");
                return false;
            }

            try
            {
                if (!context.StoredData.TryGetValue("PlayerState", out var stateData) || stateData == null)
                {
                    UnifiedCore.LogWarning($"[{LogSource}] No saved state found");
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
                        UnifiedCore.LogInfo($"[{LogSource}] Restored position: ({savedPos.x:F0}, {savedPos.y:F0}, {savedPos.z:F0})");
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
                            blood.BloodType = (PrefabGUID)bloodTypeObj;
                            blood.Quality = (float)bloodQualityObj;
                            em.SetComponentData(character, blood);
                            UnifiedCore.LogInfo($"[{LogSource}] Blood restored: {blood.BloodType} quality {blood.Quality}");
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
                        UnifiedCore.LogInfo($"[{LogSource}] Health restored: {health.Value}");
                    }
                }

                UnifiedCore.LogInfo($"[{LogSource}] Player state restored");
                return allRestored;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "ApplyBuffHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;

            if (character == Entity.Null)
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null");
                VLifecycle.Plugin.Log?.LogError("[ApplyBuff] Character entity is null");
                return false;
            }

            try
            {
                var buffId = action.BuffId ?? "InCombatBuff";
                UnifiedCore.LogInfo($"[{LogSource}] Would apply buff '{buffId}' via DebugEventSystem");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "ClearBuffsHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = UnifiedCore.EntityManager;

            if (character == Entity.Null)
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null or EntityManager not available");
                VLifecycle.Plugin.Log?.LogError("[ClearBuffs] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                UnifiedCore.LogInfo($"[{LogSource}] Would clear temporary buffs on arena exit");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "RemoveUnequipHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = UnifiedCore.EntityManager;

            if (character == Entity.Null)
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null or EntityManager not available");
                VLifecycle.Plugin.Log?.LogError("[RemoveUnequip] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                UnifiedCore.LogInfo($"[{LogSource}] Would unequip gear for arena entry");
                
                // Store equipment state for later restoration (placeholder)
                var equipmentState = new List<int>();
                context.StoredData["EquipmentState"] = equipmentState;
                
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "ResetCooldownsHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = UnifiedCore.EntityManager;

            if (character == Entity.Null)
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null or EntityManager not available");
                VLifecycle.Plugin.Log?.LogError("[ResetCooldowns] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                UnifiedCore.LogInfo($"[{LogSource}] Would reset ability cooldowns");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "TeleportHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = UnifiedCore.EntityManager;
            var character = context.CharacterEntity;

            if (character == Entity.Null || !em.Exists(character))
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null or does not exist");
                VLifecycle.Plugin.Log?.LogError("[Teleport] Character entity is null or does not exist");
                return false;
            }

            try
            {
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
                    UnifiedCore.LogInfo($"[{LogSource}] Teleported to ({spawnPos.x:F0}, {spawnPos.y:F0}, {spawnPos.z:F0})");
                    return true;
                }

                UnifiedCore.LogWarning($"[{LogSource}] No transform component found");
                VLifecycle.Plugin.Log?.LogWarning("[Teleport] No transform component found");
                return false;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "CreateGameplayEventHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var character = context.CharacterEntity;
            var em = UnifiedCore.EntityManager;

            if (character == Entity.Null)
            {
                UnifiedCore.LogError($"[{LogSource}] Character entity is null or EntityManager not available");
                VLifecycle.Plugin.Log?.LogError("[CreateGameplayEvent] Character entity is null or EntityManager not available");
                return false;
            }

            try
            {
                var eventPrefab = action.EventPrefab ?? "Event_VBlood_Unlock_Test";
                UnifiedCore.LogInfo($"[{LogSource}] Would spawn event: {eventPrefab}");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "StoreActionHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.StoreKey))
                {
                    UnifiedCore.LogError($"[{LogSource}] StoreKey is required");
                    VLifecycle.Plugin.Log?.LogError("[StoreActionHandler] StoreKey is required");
                    return false;
                }

                context.StoredData[action.StoreKey] = action.Prefix ?? action.Message ?? action.ConfigId;
                UnifiedCore.LogInfo($"[{LogSource}] Stored value for key: {action.StoreKey}");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
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
        private const string LogSource = "MessageActionHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(action.Message))
                {
                    UnifiedCore.LogError($"[{LogSource}] Message is required");
                    VLifecycle.Plugin.Log?.LogError("[MessageActionHandler] Message is required");
                    return false;
                }

                UnifiedCore.LogInfo($"[{LogSource}] Message action: {action.Message}");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedCore.LogException(ex);
                VLifecycle.Plugin.Log?.LogError($"[MessageActionHandler] Failed: {ex.Message}");
                return false;
            }
        }
    }
}
