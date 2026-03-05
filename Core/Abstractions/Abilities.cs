using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using VAutomationCore.Core;

// Custom components for ability system
public struct AbilityLevel
{
    public int Level;
}

public struct AbilityLifeTime
{
    public float Duration;
    public float Elapsed;
}

namespace VAutomationCore.Abstractions
{
    /// <summary>
    /// Centralized ability management service for VAutomationCore.
    /// Provides methods to grant, remove, check, and manage abilities on entities.
    /// </summary>
    public static class Abilities
    {
        /// <summary>
        /// Callback delegate for ability-related events.
        /// </summary>
        public delegate void AbilityEvent(Entity abilityEntity, PrefabGUID abilityPrefab);

        /// <summary>
        /// Event fired when an ability is granted to an entity.
        /// </summary>
        public static event AbilityEvent OnAbilityGranted;

        /// <summary>
        /// Event fired when an ability is removed from an entity.
        /// </summary>
        public static event AbilityEvent OnAbilityRemoved;

        /// <summary>
        /// Grants an ability to the target entity.
        /// </summary>
        /// <param name="userEntity">The user/caster entity</param>
        /// <param name="targetEntity">The target entity to receive the ability</param>
        /// <param name="abilityPrefab">The ability prefab GUID to grant</param>
        /// <param name="level">Optional ability level (default: 1)</param>
        /// <returns>True if ability was granted successfully, false otherwise</returns>
        public static bool GrantAbility(Entity userEntity, Entity targetEntity, PrefabGUID abilityPrefab, int level = 1)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(userEntity) || !em.Exists(targetEntity) || abilityPrefab == PrefabGUID.Empty)
                {
                    return false;
                }

                // Check if already has ability
                if (HasAbility(targetEntity, abilityPrefab))
                {
                    // Update level if already has ability
                    return SetAbilityLevel(targetEntity, abilityPrefab, level);
                }

                // Grant the ability through debug system
                var debugSystem = UnifiedCore.Server.GetExistingSystemManaged<DebugEventsSystem>();
                debugSystem.ApplyBuff(
                    new FromCharacter { User = userEntity, Character = targetEntity },
                    new ApplyBuffDebugEvent { BuffPrefabGUID = abilityPrefab });

                // Fire event
                OnAbilityGranted?.Invoke(targetEntity, abilityPrefab);

                // Set ability level if specified
                if (level > 1)
                {
                    SetAbilityLevel(targetEntity, abilityPrefab, level);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes an ability from the target entity.
        /// </summary>
        /// <param name="targetEntity">The target entity</param>
        /// <param name="abilityPrefab">The ability prefab GUID to remove</param>
        /// <returns>True if ability was removed successfully, false otherwise</returns>
        public static bool RemoveAbility(Entity targetEntity, PrefabGUID abilityPrefab)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(targetEntity) || abilityPrefab == PrefabGUID.Empty)
                {
                    return false;
                }

                if (!HasAbility(targetEntity, abilityPrefab))
                {
                    return true; // Already doesn't have it, consider it a success
                }

                // Remove using buff removal (abilities are implemented as buffs)
                if (BuffUtility.TryGetBuff(em, targetEntity, abilityPrefab, out var buffEntity))
                {
                    DestroyUtility.Destroy(em, buffEntity, DestroyDebugReason.TryRemoveBuff);
                }

                OnAbilityRemoved?.Invoke(targetEntity, abilityPrefab);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the target entity has a specific ability.
        /// </summary>
        /// <param name="targetEntity">The target entity to check</param>
        /// <param name="abilityPrefab">The ability prefab GUID to check for</param>
        /// <returns>True if the entity has the ability, false otherwise</returns>
        public static bool HasAbility(Entity targetEntity, PrefabGUID abilityPrefab)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(targetEntity) || abilityPrefab == PrefabGUID.Empty)
                {
                    return false;
                }

                return BuffUtility.TryGetBuff(em, targetEntity, abilityPrefab, out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all abilities on an entity.
        /// </summary>
        /// <param name="targetEntity">The target entity</param>
        /// <returns>List of ability prefab GUIDs on the entity</returns>
        public static List<PrefabGUID> GetAbilities(Entity targetEntity)
        {
            var abilities = new List<PrefabGUID>();
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(targetEntity))
                {
                    return abilities;
                }

                if (!em.HasBuffer<BuffBuffer>(targetEntity))
                {
                    return abilities;
                }

                var buffs = em.GetBuffer<BuffBuffer>(targetEntity);
                foreach (var buff in buffs)
                {
                    abilities.Add(buff.PrefabGuid);
                }
            }
            catch
            {
                // Return empty list on error
            }
            return abilities;
        }

        /// <summary>
        /// Sets the level of an existing ability on an entity.
        /// </summary>
        /// <param name="targetEntity">The target entity</param>
        /// <param name="abilityPrefab">The ability prefab GUID</param>
        /// <param name="level">The new level to set</param>
        /// <returns>True if level was set successfully, false otherwise</returns>
        public static bool SetAbilityLevel(Entity targetEntity, PrefabGUID abilityPrefab, int level)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(targetEntity) || abilityPrefab == PrefabGUID.Empty || level < 1)
                {
                    return false;
                }

                if (!BuffUtility.TryGetBuff(em, targetEntity, abilityPrefab, out var abilityEntity))
                {
                    return false;
                }

                // Check if entity has level component and set it
                // This is game-specific and may need adjustment based on actual ability implementation
if (em.HasComponent<AbilityLevel>(abilityEntity))
{
    var levelComponent = em.GetComponentData<AbilityLevel>(abilityEntity);
    levelComponent.Level = level;
    em.SetComponentData(abilityEntity, levelComponent);
}

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes all abilities from an entity.
        /// </summary>
        /// <param name="targetEntity">The target entity</param>
        /// <returns>Number of abilities removed</returns>
        public static int RemoveAllAbilities(Entity targetEntity)
        {
            var removed = 0;
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(targetEntity))
                {
                    return 0;
                }

                if (!em.HasBuffer<BuffBuffer>(targetEntity))
                {
                    return 0;
                }

                var buffs = em.GetBuffer<BuffBuffer>(targetEntity);
                var toRemove = new List<PrefabGUID>();
                
                foreach (var buff in buffs)
                {
                    toRemove.Add(buff.PrefabGuid);
                }

                foreach (var abilityPrefab in toRemove)
                {
                    if (RemoveAbility(targetEntity, abilityPrefab))
                    {
                        removed++;
                    }
                }
            }
            catch
            {
                // Return count on error
            }
            return removed;
        }

        /// <summary>
        /// Checks if an ability is on cooldown for the target entity.
        /// </summary>
        /// <param name="targetEntity">The target entity</param>
        /// <param name="abilityPrefab">The ability prefab GUID</param>
        /// <returns>True if on cooldown, false otherwise</returns>
        public static bool IsOnCooldown(Entity targetEntity, PrefabGUID abilityPrefab)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(targetEntity) || abilityPrefab == PrefabGUID.Empty)
                {
                    return false;
                }

                // Check for cooldown component - this is game-specific
                if (BuffUtility.TryGetBuff(em, targetEntity, abilityPrefab, out var buffEntity))
                {
if (em.HasComponent<AbilityLifeTime>(buffEntity))
{
    var lifetime = em.GetComponentData<AbilityLifeTime>(buffEntity);
    // If elapsed < duration, it's still active
    return lifetime.Elapsed < lifetime.Duration;
}
                }
            }
            catch
            {
                // Return false on error
            }
            return false;
        }

        /// <summary>
        /// Gets the remaining duration for an ability (if it's time-based).
        /// </summary>
        /// <param name="targetEntity">The target entity</param>
        /// <param name="abilityPrefab">The ability prefab GUID</param>
        /// <returns>Remaining duration in seconds, or -1 if not found</returns>
        public static float GetRemainingDuration(Entity targetEntity, PrefabGUID abilityPrefab)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(targetEntity) || abilityPrefab == PrefabGUID.Empty)
                {
                    return -1;
                }

                if (BuffUtility.TryGetBuff(em, targetEntity, abilityPrefab, out var buffEntity))
                {
if (em.HasComponent<AbilityLifeTime>(buffEntity))
{
    var lifetime = em.GetComponentData<AbilityLifeTime>(buffEntity);
    return lifetime.Duration - lifetime.Elapsed;
}
                }
            }
            catch
            {
                // Return -1 on error
            }
            return -1;
        }
    }
}