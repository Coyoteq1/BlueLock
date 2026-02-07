using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VAutoTraps;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Service for spawning and managing reward chests.
    /// Chests are spawned at waypoints for kill streak rewards.
    /// Only players with sufficient kill streak can open them.
    /// </summary>
    public static class ChestSpawnService
    {
        private static readonly Dictionary<float3, ChestData> _chests = new();
        private static readonly object _lock = new object();
        private static bool _initialized;
        
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Plugin.Log.LogInfo("[ChestSpawnService] Initialized - chest spawning ready");
        }
        
        /// <summary>
        /// Spawn a chest at the specified position.
        /// </summary>
        public static EntityReference SpawnChest(float3 position, ulong spawnedBy, ChestRewardType type)
        {
            lock (_lock)
            {
                var chest = new ChestData
                {
                    Position = position,
                    SpawnedByPlatformId = spawnedBy,
                    ChestType = type,
                    SpawnedTime = DateTime.UtcNow,
                    Contents = GetContentsForType(type),
                    IsLocked = true,
                    LooterPlatformIds = new List<ulong>()
                };
                
                _chests[position] = chest;
                
                Plugin.Log.LogInfo($"[ChestSpawnService] Spawned {type} chest at {position}");
                Plugin.Log.LogInfo($"[ChestSpawnService]   Contents: {chest.Contents}");
                
                return new EntityReference { Position = position };
            }
        }
        
        /// <summary>
        /// Attempt to loot a chest at the player's position.
        /// </summary>
        public static LootResult AttemptLoot(float3 playerPosition, ulong playerId)
        {
            lock (_lock)
            {
                foreach (var kvp in _chests)
                {
                    var chestPos = kvp.Key;
                    var chest = kvp.Value;
                    
                    // Check distance
                    if (math.distance(playerPosition, chestPos) <= 2f)
                    {
                        // Check if already looted by this player
                        if (chest.LooterPlatformIds.Contains(playerId))
                        {
                            return new LootResult
                            {
                                Success = false,
                                Message = "You already looted this chest!"
                            };
                        }
                        
                        // Add player to looters
                        chest.LooterPlatformIds.Add(playerId);
                        _chests[chestPos] = chest;
                        
                        Plugin.Log.LogInfo($"[ChestSpawnService] Player {playerId} looted chest at {chestPos}");
                        
                        return new LootResult
                        {
                            Success = true,
                            LootType = chest.Contents,
                            Contents = chest.Contents,
                            Message = $"You received: {chest.Contents}"
                        };
                    }
                }
                
                return new LootResult
                {
                    Success = false,
                    Message = "No chest nearby"
                };
            }
        }
        
        /// <summary>
        /// Check if player can access chest at position.
        /// </summary>
        public static bool CanAccessChest(float3 playerPosition, int playerKillStreak)
        {
            lock (_lock)
            {
                foreach (var kvp in _chests)
                {
                    if (math.distance(playerPosition, kvp.Key) <= 2f)
                    {
                        // Check if player has sufficient streak
                        return playerKillStreak >= TrapSpawnRules.Config.KillThreshold;
                    }
                }
                return false;
            }
        }
        
        /// <summary>
        /// Remove nearest chest within radius.
        /// </summary>
        public static bool RemoveNearestChest(float3 position, float maxRadius = 5f)
        {
            lock (_lock)
            {
                foreach (var kvp in _chests)
                {
                    if (math.distance(position, kvp.Key) <= maxRadius)
                    {
                        _chests.Remove(kvp.Key);
                        Plugin.Log.LogInfo($"[ChestSpawnService] Removed chest at {kvp.Key}");
                        return true;
                    }
                }
                return false;
            }
        }
        
        /// <summary>
        /// Remove chest at exact position.
        /// </summary>
        public static bool RemoveChest(float3 position)
        {
            lock (_lock)
            {
                return _chests.Remove(position);
            }
        }
        
        /// <summary>
        /// Get all active chests.
        /// </summary>
        public static Dictionary<float3, ChestData> GetAllChests()
        {
            lock (_lock)
            {
                return new Dictionary<float3, ChestData>(_chests);
            }
        }
        
        /// <summary>
        /// Get chest count.
        /// </summary>
        public static int GetChestCount()
        {
            lock (_lock)
            {
                return _chests.Count;
            }
        }
        
        /// <summary>
        /// Clear all chests.
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                _chests.Clear();
                Plugin.Log.LogInfo("[ChestSpawnService] All chests cleared");
            }
        }
        
        /// <summary>
        /// Get contents string for chest type.
        /// </summary>
        private static string GetContentsForType(ChestRewardType type)
        {
            return type switch
            {
                ChestRewardType.Normal => "5x Greater Blood Essence, 2x Scroll of Conflict",
                ChestRewardType.Rare => "10x Greater Blood Essence, 5x Scroll of Conflict, 1x Sacred Blood",
                ChestRewardType.Epic => "20x Greater Blood Essence, 10x Scroll of Conflict, 3x Sacred Blood, 1x Gem Dust",
                ChestRewardType.Legendary => "50x Greater Blood Essence, 20x Scroll of Conflict, 5x Sacred Blood, 3x Gem Dust, 1x Golden Egg",
                _ => "Basic loot"
            };
        }
    }
    
    /// <summary>
    /// Chest data structure.
    /// </summary>
    public struct ChestData
    {
        public float3 Position;
        public ulong SpawnedByPlatformId;
        public ChestRewardType ChestType;
        public DateTime SpawnedTime;
        public string Contents;
        public bool IsLocked;
        public List<ulong> LooterPlatformIds;
    }
    
    /// <summary>
    /// Loot attempt result.
    /// </summary>
    public struct LootResult
    {
        public bool Success;
        public string LootType;
        public string Contents;
        public string Message;
    }
    
    /// <summary>
    /// Entity reference for chest.
    /// </summary>
    public struct EntityReference
    {
        public float3 Position;
    }
}
