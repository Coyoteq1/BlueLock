using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Shared;
using Unity.Collections;
using Unity.Entities;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Disables durability loss for equipped items while player is inside a zone.
    /// Restores original durability settings when player exits the zone.
    /// </summary>
    public static class ZoneNoDurabilityService
    {
        private struct TrackedDurability
        {
            public float Value;
            public float LossFactor;
        }

        private static readonly Dictionary<Entity, Dictionary<Entity, TrackedDurability>> _trackedByPlayer = new();

        public static void StartTracking(Entity player, EntityManager em)
        {
            try
            {
                if (player == Entity.Null || !em.Exists(player) || !em.HasComponent<Equipment>(player))
                {
                    return;
                }

                if (!_trackedByPlayer.TryGetValue(player, out var trackedItems))
                {
                    trackedItems = new Dictionary<Entity, TrackedDurability>();
                    _trackedByPlayer[player] = trackedItems;
                }

                ApplyNoDurabilityToCurrentEquipment(player, em, trackedItems);
            }
            catch
            {
                // Best-effort only.
            }
        }

        public static void StopTracking(Entity player, EntityManager em)
        {
            try
            {
                if (!_trackedByPlayer.TryGetValue(player, out var trackedItems))
                {
                    return;
                }

                foreach (var pair in trackedItems)
                {
                    RestoreItemDurability(pair.Key, pair.Value, em);
                }

                _trackedByPlayer.Remove(player);
            }
            catch
            {
                // Best-effort only.
            }
        }

        public static void Tick(EntityManager em)
        {
            try
            {
                if (_trackedByPlayer.Count == 0)
                {
                    return;
                }

                var players = new List<Entity>(_trackedByPlayer.Keys);
                foreach (var player in players)
                {
                    if (!em.Exists(player) || !em.HasComponent<Equipment>(player))
                    {
                        StopTracking(player, em);
                        continue;
                    }

                    var trackedItems = _trackedByPlayer[player];
                    var currentEquipped = GetEquippedItems(player, em);

                    // Restore and remove items that are no longer equipped.
                    var trackedKeys = new List<Entity>(trackedItems.Keys);
                    foreach (var item in trackedKeys)
                    {
                        if (!currentEquipped.Contains(item))
                        {
                            RestoreItemDurability(item, trackedItems[item], em);
                            trackedItems.Remove(item);
                        }
                    }

                    // Apply no-durability for newly equipped items.
                    foreach (var item in currentEquipped)
                    {
                        if (!trackedItems.ContainsKey(item))
                        {
                            TrySetNoDurability(item, em, out var state);
                            if (state.HasValue)
                            {
                                trackedItems[item] = state.Value;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static void ApplyNoDurabilityToCurrentEquipment(Entity player, EntityManager em, Dictionary<Entity, TrackedDurability> trackedItems)
        {
            var equippedItems = GetEquippedItems(player, em);
            foreach (var item in equippedItems)
            {
                if (trackedItems.ContainsKey(item))
                {
                    continue;
                }

                TrySetNoDurability(item, em, out var state);
                if (state.HasValue)
                {
                    trackedItems[item] = state.Value;
                }
            }
        }

        private static HashSet<Entity> GetEquippedItems(Entity player, EntityManager em)
        {
            var result = new HashSet<Entity>();
            if (!em.Exists(player) || !em.HasComponent<Equipment>(player))
            {
                return result;
            }

            var equipment = em.GetComponentData<Equipment>(player);
            var equipped = new NativeList<Entity>(Allocator.Temp);
            try
            {
                equipment.GetAllEquipmentEntities(equipped);
                for (var i = 0; i < equipped.Length; i++)
                {
                    var item = equipped[i];
                    if (item != Entity.Null)
                    {
                        result.Add(item);
                    }
                }
            }
            finally
            {
                equipped.Dispose();
            }

            return result;
        }

        private static void TrySetNoDurability(Entity item, EntityManager em, out TrackedDurability? tracked)
        {
            tracked = null;
            if (!em.Exists(item) || !em.HasComponent<Durability>(item))
            {
                return;
            }

            var durability = em.GetComponentData<Durability>(item);
            tracked = new TrackedDurability
            {
                Value = durability.Value,
                LossFactor = durability.TakeDamageDurabilityLossFactor
            };

            durability.Value = durability.MaxDurability;
            durability.TakeDamageDurabilityLossFactor = 0f;
            em.SetComponentData(item, durability);
        }

        private static void RestoreItemDurability(Entity item, TrackedDurability state, EntityManager em)
        {
            if (!em.Exists(item) || !em.HasComponent<Durability>(item))
            {
                return;
            }

            var durability = em.GetComponentData<Durability>(item);
            durability.Value = state.Value;
            durability.TakeDamageDurabilityLossFactor = state.LossFactor;
            em.SetComponentData(item, durability);
        }
    }
}
