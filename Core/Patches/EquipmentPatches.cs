using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Patches;

namespace VAutomationCore.Core.Patches
{
    /// <summary>
    /// Equipment handling patches for V Rising.
    /// Provides patches for equipment system integration and management.
    /// </summary>
    public static class EquipmentPatches
    {
        private static bool _initialized;
        private static readonly object _initLock = new object();
        private static readonly Dictionary<int, EquipmentInfo> _equipmentInfo = new Dictionary<int, EquipmentInfo>();
        private static readonly Dictionary<int, List<EquipmentModifier>> _equipmentModifiers = new Dictionary<int, List<EquipmentModifier>>();

        #region Equipment Information Structures

        /// <summary>
        /// Equipment information structure.
        /// </summary>
        public class EquipmentInfo
        {
            public int EquipmentId;
            public string Name;
            public string Type;
            public string Category;
            public string Rarity;
            public int Level;
            public float BaseDamage;
            public float BaseDefense;
            public float BaseDurability;
            public string[] AllowedSlots;
            public string[] RequiredSkills;
            public string[] Tags;
            public bool IsEnabled;
            public bool IsCraftable;
            public DateTime AddedTime;
            public Dictionary<string, object> Properties;
        }

        /// <summary>
        /// Equipment modifier structure.
        /// </summary>
        public class EquipmentModifier
        {
            public int ModifierId;
            public int EquipmentId;
            public string Name;
            public string Type;
            public float Value;
            public string[] AffectedStats;
            public bool IsActive;
            public DateTime AppliedTime;
            public Dictionary<string, object> Properties;
        }

        /// <summary>
        /// Equipment statistics for monitoring.
        /// </summary>
        public class EquipmentStatistics
        {
            public int TotalEquipment;
            public int EnabledEquipment;
            public int CraftableEquipment;
            public Dictionary<string, int> EquipmentByCategory;
            public Dictionary<string, int> EquipmentByRarity;
            public Dictionary<string, int> EquipmentByType;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the equipment patches.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;

                try
                {
                    // Load equipment data
                    LoadEquipmentData();

                    _initialized = true;
                    Plugin.Log.LogInfo("[EquipmentPatches] Initialized successfully");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[EquipmentPatches] Initialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if patches are ready.
        /// </summary>
        public static bool IsReady()
        {
            return _initialized;
        }

        #endregion

        #region Equipment Data Loading

        /// <summary>
        /// Load equipment data from game files.
        /// </summary>
        private static void LoadEquipmentData()
        {
            try
            {
                // TODO: Implement actual equipment data loading from game files
                // For now, add some sample equipment
                AddSampleEquipment();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EquipmentPatches] Error loading equipment data: {ex.Message}");
            }
        }

        /// <summary>
        /// Add sample equipment data.
        /// </summary>
        private static void AddSampleEquipment()
        {
            AddEquipment(new EquipmentInfo
            {
                EquipmentId = 1001,
                Name = "Iron Sword",
                Type = "Weapon",
                Category = "Melee",
                Rarity = "Common",
                Level = 1,
                BaseDamage = 15.0f,
                BaseDefense = 0.0f,
                BaseDurability = 100.0f,
                AllowedSlots = new[] { "MainHand" },
                RequiredSkills = new[] { "Swordsmanship" },
                Tags = new[] { "Iron", "Sword", "Melee" },
                IsEnabled = true,
                IsCraftable = true,
                AddedTime = DateTime.UtcNow,
                Properties = new Dictionary<string, object>()
            });

            AddEquipment(new EquipmentInfo
            {
                EquipmentId = 1002,
                Name = "Wooden Shield",
                Type = "Armor",
                Category = "Shield",
                Rarity = "Common",
                Level = 1,
                BaseDamage = 0.0f,
                BaseDefense = 10.0f,
                BaseDurability = 80.0f,
                AllowedSlots = new[] { "OffHand" },
                RequiredSkills = new[] { "Blocking" },
                Tags = new[] { "Wood", "Shield", "Armor" },
                IsEnabled = true,
                IsCraftable = true,
                AddedTime = DateTime.UtcNow,
                Properties = new Dictionary<string, object>()
            });

            Plugin.Log.LogInfo("[EquipmentPatches] Added sample equipment data");
        }

        #endregion

        #region Equipment Management

        /// <summary>
        /// Add equipment information.
        /// </summary>
        /// <param name="equipment""Equipment information</param>
        /// <returns>True if added</returns>
        public static bool AddEquipment(EquipmentInfo equipment)
        {
            if (equipment == null || equipment.EquipmentId <= 0) return false;

            lock (_equipmentInfo)
            {
                if (_equipmentInfo.ContainsKey(equipment.EquipmentId))
                {
                    Plugin.Log.LogWarning($"[EquipmentPatches] Equipment {equipment.EquipmentId} already exists, updating");
                }

                _equipmentInfo[equipment.EquipmentId] = equipment;
                Plugin.Log.LogInfo($"[EquipmentPatches] Added equipment {equipment.EquipmentId}: {equipment.Name}");
                return true;
            }
        }

        /// <summary>
        /// Remove equipment information.
        /// </summary>
        /// <param name="equipmentId""Equipment ID</param>
        /// <returns>True if removed</returns>
        public static bool RemoveEquipment(int equipmentId)
        {
            lock (_equipmentInfo)
            {
                if (_equipmentInfo.Remove(equipmentId))
                {
                    // Remove associated modifiers
                    if (_equipmentModifiers.ContainsKey(equipmentId))
                    {
                        _equipmentModifiers.Remove(equipmentId);
                    }

                    Plugin.Log.LogInfo($"[EquipmentPatches] Removed equipment {equipmentId}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get equipment information.
        /// </summary>
        /// <param name="equipmentId""Equipment ID</param>
        /// <returns>Equipment info or null</returns>
        public static EquipmentInfo? GetEquipment(int equipmentId)
        {
            lock (_equipmentInfo)
            {
                if (_equipmentInfo.TryGetValue(equipmentId, out var equipment))
                    return equipment;
            }
            return null;
        }

        /// <summary>
        /// Get all equipment.
        /// </summary>
        /// <returns>List of equipment</returns>
        public static List<EquipmentInfo> GetAllEquipment()
        {
            lock (_equipmentInfo)
            {
                return _equipmentInfo.Values.ToList();
            }
        }

        /// <summary>
        /// Get equipment by category.
        /// </summary>
        /// <param name="category""Category name</param>
        /// <returns>List of equipment</returns>
        public static List<EquipmentInfo> GetEquipmentByCategory(string category)
        {
            lock (_equipmentInfo)
            {
                return _equipmentInfo.Values
                    .Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Get equipment by type.
        /// </summary>
        /// <param name="type""Type name</param>
        /// <returns>List of equipment</returns>
        public static List<EquipmentInfo> GetEquipmentByType(string type)
        {
            lock (_equipmentInfo)
            {
                return _equipmentInfo.Values
                    .Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Get equipment by rarity.
        /// </summary>
        /// <param name="rarity""Rarity name</param>
        /// <returns>List of equipment</returns>
        public static List<EquipmentInfo> GetEquipmentByRarity(string rarity)
        {
            lock (_equipmentInfo)
            {
                return _equipmentInfo.Values
                    .Where(e => e.Rarity.Equals(rarity, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        #endregion

        #region Equipment Modifiers

        /// <summary>
        /// Add equipment modifier.
        /// </summary>
        /// <param name="modifier""Modifier information</param>
        /// <returns>True if added</returns>
        public static bool AddModifier(EquipmentModifier modifier)
        {
            if (modifier == null || modifier.ModifierId <= 0 || modifier.EquipmentId <= 0) return false;

            lock (_equipmentModifiers)
            {
                if (!_equipmentModifiers.ContainsKey(modifier.EquipmentId))
                {
                    _equipmentModifiers[modifier.EquipmentId] = new List<EquipmentModifier>();
                }

                _equipmentModifiers[modifier.EquipmentId].Add(modifier);
                Plugin.Log.LogInfo($"[EquipmentPatches] Added modifier {modifier.ModifierId} to equipment {modifier.EquipmentId}");
                return true;
            }
        }

        /// <summary>
        /// Remove equipment modifier.
        /// </summary>
        /// <param name="modifierId""Modifier ID</param>
        /// <returns>True if removed</returns>
        public static bool RemoveModifier(int modifierId)
        {
            lock (_equipmentModifiers)
            {
                foreach (var kvp in _equipmentModifiers)
                {
                    var modifier = kvp.Value.FirstOrDefault(m => m.ModifierId == modifierId);
                    if (modifier != null)
                    {
                        kvp.Value.Remove(modifier);
                        Plugin.Log.LogInfo($"[EquipmentPatches] Removed modifier {modifierId}");
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Get equipment modifiers.
        /// </summary>
        /// <param name="equipmentId""Equipment ID</param>
        /// <returns>List of modifiers</returns>
        public static List<EquipmentModifier> GetModifiers(int equipmentId)
        {
            lock (_equipmentModifiers)
            {
                if (_equipmentModifiers.TryGetValue(equipmentId, out var modifiers))
                    return modifiers.ToList();
            }
            return new List<EquipmentModifier>();
        }

        /// <summary>
        /// Get active modifiers for equipment.
        /// </summary>
        /// <param name="equipmentId""Equipment ID</param>
        /// <returns>List of active modifiers</returns>
        public static List<EquipmentModifier> GetActiveModifiers(int equipmentId)
        {
            lock (_equipmentModifiers)
            {
                if (_equipmentModifiers.TryGetValue(equipmentId, out var modifiers))
                    return modifiers.Where(m => m.IsActive).ToList();
            }
            return new List<EquipmentModifier>();
        }

        #endregion

        #region Equipment Effects

        /// <summary>
        /// Apply equipment effects to an entity.
        /// </summary>
        /// <param name="entity""Entity to apply effects to</param>
        /// <param name="equipmentId""Equipment ID</param>
        public static void ApplyEquipmentEffects(Entity entity, int equipmentId)
        {
            if (!IsReady() || entity == Entity.Null) return;

            try
            {
                var equipment = GetEquipment(equipmentId);
                if (equipment == null) return;

                // Apply base stats
                ApplyBaseStats(entity, equipment);

                // Apply modifiers
                var modifiers = GetActiveModifiers(equipmentId);
                foreach (var modifier in modifiers)
                {
                    ApplyModifierEffects(entity, modifier);
                }

                Plugin.Log.LogDebug($"[EquipmentPatches] Applied equipment {equipmentId} effects to entity {entity}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EquipmentPatches] Error applying equipment effects: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove equipment effects from an entity.
        /// </summary>
        /// <param name="entity""Entity to remove effects from</param>
        /// <param name="equipmentId""Equipment ID</param>
        public static void RemoveEquipmentEffects(Entity entity, int equipmentId)
        {
            if (!IsReady() || entity == Entity.Null) return;

            try
            {
                // Remove base stats
                RemoveBaseStats(entity, equipmentId);

                // Remove modifiers
                var modifiers = GetActiveModifiers(equipmentId);
                foreach (var modifier in modifiers)
                {
                    RemoveModifierEffects(entity, modifier);
                }

                Plugin.Log.LogDebug($"[EquipmentPatches] Removed equipment {equipmentId} effects from entity {entity}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EquipmentPatches] Error removing equipment effects: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get equipment statistics.
        /// </summary>
        /// <returns>Equipment statistics</returns>
        public static EquipmentStatistics GetStatistics()
        {
            lock (_equipmentInfo)
            {
                return new EquipmentStatistics
                {
                    TotalEquipment = _equipmentInfo.Count,
                    EnabledEquipment = _equipmentInfo.Count(e => e.Value.IsEnabled),
                    CraftableEquipment = _equipmentInfo.Count(e => e.Value.IsCraftable),
                    EquipmentByCategory = _equipmentInfo.Values
                        .GroupBy(e => e.Category)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    EquipmentByRarity = _equipmentInfo.Values
                        .GroupBy(e => e.Rarity)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    EquipmentByType = _equipmentInfo.Values
                        .GroupBy(e => e.Type)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
        }

        /// <summary>
        /// Check if equipment is craftable.
        /// </summary>
        /// <param name="equipmentId""Equipment ID</param>
        /// <returns>True if craftable</returns>
        public static bool IsCraftable(int equipmentId)
        {
            var equipment = GetEquipment(equipmentId);
            return equipment?.IsCraftable ?? false;
        }

        /// <summary>
        /// Get equipment requirements.
        /// </summary>
        /// <param name="equipmentId""Equipment ID</param>
        /// <returns>List of requirements</returns>
        public static List<string> GetRequirements(int equipmentId)
        {
            var equipment = GetEquipment(equipmentId);
            var requirements = new List<string>();

            if (equipment != null)
            {
                if (equipment.RequiredSkills != null)
                {
                    requirements.AddRange(equipment.RequiredSkills);
                }

                // TODO: Add other requirements (materials, level, etc.)
            }

            return requirements;
        }

        #endregion

        #region Private Methods

        private static void ApplyBaseStats(Entity entity, EquipmentInfo equipment)
        {
            // TODO: Implement actual stat application
            Plugin.Log.LogDebug($"[EquipmentPatches] Applying base stats for {equipment.Name} to entity {entity}");
        }

        private static void RemoveBaseStats(Entity entity, int equipmentId)
        {
            // TODO: Implement actual stat removal
            Plugin.Log.LogDebug($"[EquipmentPatches] Removing base stats for equipment {equipmentId} from entity {entity}");
        }

        private static void ApplyModifierEffects(Entity entity, EquipmentModifier modifier)
        {
            // TODO: Implement actual modifier application
            Plugin.Log.LogDebug($"[EquipmentPatches] Applying modifier {modifier.Name} to entity {entity}");
        }

        private static void RemoveModifierEffects(Entity entity, EquipmentModifier modifier)
        {
            // TODO: Implement actual modifier removal
            Plugin.Log.LogDebug($"[EquipmentPatches] Removing modifier {modifier.Name} from entity {entity}");
        }

        #endregion
    }
}