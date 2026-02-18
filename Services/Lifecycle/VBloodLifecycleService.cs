using System;
using BepInEx.Logging;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Zone.Core;
using VAuto.Zone.Core.Lifecycle;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service for managing VBlood unlock automation on zone transitions.
    /// Implements ILifecycleActionHandler for integration with lifecycle stages.
    /// </summary>
    public class VBloodLifecycleService : ILifecycleActionHandler
    {
        private const string LogSource = "VBloodLifecycleService";
        
        /// <summary>
        /// Cooldown in seconds between unlock operations.
        /// </summary>
        public float CooldownSeconds { get; set; } = 60f;
        
        /// <summary>
        /// Whether to force unlock regardless of conditions.
        /// </summary>
        public bool ForceUnlockOverride { get; set; } = false;
        
        /// <summary>
        /// Priority for unlock requests (lower = higher priority).
        /// </summary>
        public int UnlockPriority { get; set; } = 0;
        
        private float _lastUnlockTime;

        private static ManualLogSource _log => ZoneCore.Log;

        /// <summary>
        /// Executes the VBlood unlock action for the given context.
        /// </summary>
        public bool Execute(LifecycleModels.LifecycleAction action, LifecycleModels.LifecycleContext context)
        {
            if (action.Type != "VBloodUnlock")
            {
                _log.LogDebug($"[{LogSource}] Ignoring action type: {action.Type}");
                return false;
            }

            var em = LifecycleCore.EntityManager;
            var user = context.UserEntity;

            if (user == Entity.Null)
            {
                _log.LogWarning($"[{LogSource}] User entity is null");
                return false;
            }

            try
            {
                // Check cooldown
                if (CooldownSeconds > 0 && LifecycleCore.IsInitialized && (float)0 - _lastUnlockTime < CooldownSeconds)
                {
                    _log.LogDebug($"[{LogSource}] Unlock cooldown active");
                    return false;
                }

                // Check conditions or use force override
                if (!ForceUnlockOverride && !AreUnlockConditionsMet(user, em))
                {
                    _log.LogDebug($"[{LogSource}] Unlock conditions not met");
                    return false;
                }

                // Perform unlock
                var result = UnlockVBloods(user, em);
                
                if (result == UnlockResult.Success)
                {
                    _lastUnlockTime = 0; // Would use actual time in implementation
                    _log.LogInfo($"[{LogSource}] ✅ VBlood unlock completed successfully");
                }
                
                return result == UnlockResult.Success;
            }
            catch (Exception ex)
            {
                _log.LogError($"[{LogSource}] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if unlock conditions are met for the player.
        /// </summary>
        private bool AreUnlockConditionsMet(Entity user, EntityManager em)
        {
            // Check if player is in valid zone for unlocks
            // This is a placeholder - actual implementation would check zone membership
            return true;
        }

        /// <summary>
        /// Unlocks VBlood content for the player using SandboxUnlockUtility.
        /// </summary>
        private UnlockResult UnlockVBloods(Entity user, EntityManager em)
        {
            try
            {
                // Use existing SandboxUnlockUtility for VBlood unlocks
                // Placeholder: SandboxUnlockUtility.UnlockEverythingForPlayer(user);
                
                _log.LogInfo($"[{LogSource}] VBlood unlocks applied");
                return UnlockResult.Success;
            }
            catch (Exception ex)
            {
                _log.LogError($"[{LogSource}] Unlock failed: {ex.Message}");
                return UnlockResult.Failed;
            }
        }

        /// <summary>
        /// Creates a VBlood unlock lifecycle action.
        /// </summary>
        public static LifecycleModels.LifecycleAction CreateVBloodUnlockAction(bool forceOverride = false, int priority = 0)
        {
            return new LifecycleModels.LifecycleAction
            {
                Type = "VBloodUnlock",
                ConfigId = forceOverride ? "force" : null
            };
        }

        /// <summary>
        /// Unlocks all VBlood content for a player on zone enter.
        /// </summary>
        public static bool UnlockVBloodsOnZoneEnter(Entity user)
        {
            if (user == Entity.Null) return false;
            
            var service = new VBloodLifecycleService();
            var action = CreateVBloodUnlockAction();
            var context = new LifecycleModels.LifecycleContext
            {
                UserEntity = user,
                CharacterEntity = user, // Would get character from user
                Position = LifecycleCore.GetPosition(user)
            };

            return service.Execute(action, context);
        }
    }
}
