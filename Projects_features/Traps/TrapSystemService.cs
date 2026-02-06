using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core.Components;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Comprehensive trap system service managing:
    /// - Kill streak tracking
    /// - Chest spawn system (5-kill streak reward)
    /// - Container trap system (defensive traps on chests)
    /// - Waypoint expansion trap system (10-kill streak requirement)
    /// </summary>
    public class TrapSystemService
    {
        private static TrapSystemService _instance;
        private static readonly object _lock = new object();
        
        private EntityManager _entityManager;
        private TrapSystemConfig _config;
        private readonly string _configPath;
        private bool _initialized;
        
        // Event delegates for other systems
        public delegate void KillEventHandler(Entity player, int newStreak);
        public delegate void ChestSpawnEventHandler(Entity chest, Entity owner);
        public delegate void TrapTriggerEventHandler(Entity trap, Entity intruder, float3 position);
        public delegate void NotificationEventHandler(Entity player, string message, MessageType type);
        
        public event KillEventHandler OnKill;
        public event ChestSpawnEventHandler OnChestSpawn;
        public event TrapTriggerEventHandler OnTrapTriggered;
        public event NotificationEventHandler OnNotification;
        
        /// <summary>
        /// Gets singleton instance.
        /// </summary>
        public static TrapSystemService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new TrapSystemService();
                        }
                    }
                }
                return _instance;
            }
        }
        
        private TrapSystemService()
        {
            _configPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "trap_system_config.json");
        }
        
        /// <summary>
        /// Initializes the trap system service.
        /// </summary>
        public bool Initialize(EntityManager entityManager)
        {
            try
            {
                if (_initialized)
                {
                    Plugin.Log.LogInfo("[TrapSystem] Already initialized");
                    return true;
                }
                
                _entityManager = entityManager;
                Plugin.Log.LogInfo("[TrapSystem] Initializing...");
                
                LoadConfiguration();
                CreateDefaultConfig();
                
                _initialized = true;
                Plugin.Log.LogInfo("[TrapSystem] Initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[TrapSystem] Initialization failed: {ex.Message}");
                return false;
            }
        }
        
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<TrapSystemConfig>(json) ?? new TrapSystemConfig();
                    Plugin.Log.LogInfo("[TrapSystem] Loaded configuration");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[TrapSystem] Failed to load config: {ex.Message}");
                _config = new TrapSystemConfig();
            }
        }
        
        private void CreateDefaultConfig()
        {
            if (_config == null)
            {
                _config = new TrapSystemConfig
                {
                    version = "1.0",
                    chestSpawn = new ChestSpawnConfig
                    {
                        enabled = true,
                        requiredStreak = 5,
                        chestsPerKill = 2,
                        chestPrefabId = 1001,
                        chestLifetime = 300f,
                        spawnRadius = 50f,
                        regions = new List<ChestSpawnRegionConfig>
                        {
                            new ChestSpawnRegionConfig
                            {
                                regionId = "region_1",
                                regionName = "Primary Arena",
                                waypoints = new List<WaypointConfig>
                                {
                                    new WaypointConfig { waypointIndex = 0, position = new float3(100, 0, 100), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 1, position = new float3(200, 0, 200), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 2, position = new float3(300, 0, 300), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 3, position = new float3(400, 0, 400), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 4, position = new float3(500, 0, 500), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 5, position = new float3(600, 0, 600), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 6, position = new float3(700, 0, 700), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 7, position = new float3(800, 0, 800), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 8, position = new float3(900, 0, 900), castleLevel = 15 },
                                    new WaypointConfig { waypointIndex = 9, position = new float3(1000, 0, 1000), castleLevel = 15 },
                                }
                            }
                        }
                    },
                    containerTrap = new ContainerTrapConfig
                    {
                        enabled = true,
                        abilityPrefabId = 2001,
                        damagePerTick = 50f,
                        tickRate = 0.5f,
                        duration = 10f,
                        detectionRadius = 10f,
                        maxTriggers = 3,
                        cooldownSeconds = 60f,
                        glow = new GlowConfig { enabled = true, r = 1f, g = 0f, b = 0f, intensity = 2f },
                        particlePrefabId = 0
                    },
                    waypointTrap = new WaypointTrapConfig
                    {
                        enabled = true,
                        requiredStreak = 10,
                        waypoints = new List<WaypointTrapData>
                        {
                            new WaypointTrapData { index = 0, position = new float3(100, 0, 100), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 1, position = new float3(200, 0, 200), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 2, position = new float3(300, 0, 300), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 3, position = new float3(400, 0, 400), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 4, position = new float3(500, 0, 500), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 5, position = new float3(600, 0, 600), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 6, position = new float3(700, 0, 700), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 7, position = new float3(800, 0, 800), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 8, position = new float3(900, 0, 900), castleLevel = 15, isActive = true },
                            new WaypointTrapData { index = 9, position = new float3(1000, 0, 1000), castleLevel = 15, isActive = true },
                        },
                        damageEntityPrefabId = 3001,
                        visualPrefabId = 3002,
                        damageAmount = 100f,
                        attackSpeed = 2f,
                        effectRadius = 15f,
                        effectDuration = 15f,
                        glow = new GlowConfig { enabled = true, r = 0f, g = 1f, b = 1f, intensity = 3f }
                    }
                };
                
                SaveConfiguration();
                Plugin.Log.LogInfo("[TrapSystem] Created default configuration");
            }
        }
        
        public void SaveConfiguration()
        {
            try
            {
                var configDir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configPath, json);
                
                Plugin.Log.LogInfo($"[TrapSystem] Configuration saved to {_configPath}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[TrapSystem] Failed to save config: {ex.Message}");
            }
        }
        
        #region Kill Streak Methods
        
        /// <summary>
        /// Records a player kill and updates streak.
        /// </summary>
        public void RecordKill(Entity player, string playerName)
        {
            var tracker = GetOrCreateKillStreakTracker(player);
            tracker.CurrentStreak++;
            tracker.TotalKills++;
            tracker.LastKillTime = Time.ElapsedTime;
            
            _entityManager.SetComponentData(player, tracker);
            
            Plugin.Log.LogInfo($"[TrapSystem] {playerName} kill streak: {tracker.CurrentStreak}");
            
            // Check for chest spawn eligibility
            if (_config.chestSpawn.enabled && 
                tracker.CurrentStreak >= _config.chestSpawn.requiredStreak &&
                tracker.ChestsSpawned < _config.chestSpawn.chestsPerKill)
            {
                SpawnChestsForPlayer(player, playerName);
                tracker.ChestsSpawned++;
                _entityManager.SetComponentData(player, tracker);
            }
            
            OnKill?.Invoke(player, tracker.CurrentStreak);
        }
        
        /// <summary>
        /// Resets a player's kill streak.
        /// </summary>
        public void ResetStreak(Entity player)
        {
            if (_entityManager.HasComponent<KillStreakTracker>(player))
            {
                var tracker = _entityManager.GetComponentData<KillStreakTracker>(player);
                if (Time.ElapsedTime - tracker.LastKillTime > tracker.StreakResetTime)
                {
                    tracker.CurrentStreak = 0;
                    _entityManager.SetComponentData(player, tracker);
                    Plugin.Log.LogInfo($"[TrapSystem] Player streak reset");
                }
            }
        }
        
        private KillStreakTracker GetOrCreateKillStreakTracker(Entity player)
        {
            if (!_entityManager.HasComponent<KillStreakTracker>(player))
            {
                _entityManager.AddComponentData(player, new KillStreakTracker
                {
                    CurrentStreak = 0,
                    TotalKills = 0,
                    LastKillTime = 0,
                    ChestsClaimed = 0,
                    ChestsSpawned = 0,
                    StreakResetTime = 60f
                });
            }
            return _entityManager.GetComponentData<KillStreakTracker>(player);
        }
        
        #endregion
        
        #region Chest Spawn Methods
        
        private void SpawnChestsForPlayer(Entity player, string playerName)
        {
            if (_config.chestSpawn.regions == null || _config.chestSpawn.regions.Count == 0)
            {
                Plugin.Log.LogWarning("[TrapSystem] No chest spawn regions configured");
                return;
            }
            
            var region = _config.chestSpawn.regions[0]; // Use first region for now
            var now = Time.ElapsedTime;
            var random = new Random((int)(now * 1000));
            
            int chestsToSpawn = math.min(_config.chestSpawn.chestsPerKill - GetChestsSpawned(player), 2);
            
            for (int i = 0; i < chestsToSpawn && i < region.waypoints.Count; i++)
            {
                var waypoint = region.waypoints[i];
                var offset = waypoint.chestSpawnOffsets != null && waypoint.chestSpawnOffsets.Count > 0
                    ? waypoint.chestSpawnOffsets[random.NextInt(waypoint.chestSpawnOffsets.Count)]
                    : new float3(random.NextFloat(-_config.chestSpawn.spawnRadius, _config.chestSpawn.spawnRadius), 0, random.NextFloat(-_config.chestSpawn.spawnRadius, _config.chestSpawn.spawnRadius));
                
                var spawnPosition = waypoint.position + offset;
                SpawnChest(spawnPosition, player, playerName, waypoint.waypointIndex);
            }
        }
        
        private void SpawnChest(float3 position, Entity owner, string ownerName, int waypointIndex)
        {
            var chestEntity = _entityManager.CreateEntity();
            
            _entityManager.AddComponentData(chestEntity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            
            _entityManager.AddComponentData(chestEntity, new KillStreakChest
            {
                RequiredStreak = _config.chestSpawn.requiredStreak,
                IsClaimed = false,
                OwnerEntity = owner,
                OwnerName = new FixedString64Bytes(ownerName),
                SpawnTime = Time.ElapsedTime,
                ExpirationTime = Time.ElapsedTime + _config.chestSpawn.chestLifetime,
                WaypointIndex = waypointIndex
            });
            
            // Add trap if container trap is enabled
            if (_config.containerTrap.enabled)
            {
                _entityManager.AddComponentData(chestEntity, new ContainerTrap
                {
                    OwnerEntity = owner,
                    OwnerName = new FixedString64Bytes(ownerName),
                    IsArmed = true,
                    IsTriggered = false,
                    MaxTriggers = _config.containerTrap.maxTriggers,
                    TriggerCount = 0,
                    CooldownSeconds = _config.containerTrap.cooldownSeconds,
                    LastTriggerTime = 0,
                    DetectionRadius = _config.containerTrap.detectionRadius
                });
                
                _entityManager.AddComponentData(chestEntity, new TrapAbility
                {
                    AbilityPrefabId = _config.containerTrap.abilityPrefabId,
                    SpawnOffset = new float3(0, 2, 0),
                    DamagePerTick = _config.containerTrap.damagePerTick,
                    TickRate = _config.containerTrap.tickRate,
                    Duration = _config.containerTrap.duration,
                    HasGlow = _config.containerTrap.glow?.enabled ?? true,
                    GlowColor = new float3(_config.containerTrap.glow?.r ?? 1f, _config.containerTrap.glow?.g ?? 0f, _config.containerTrap.glow?.b ?? 0f),
                    GlowIntensity = _config.containerTrap.glow?.intensity ?? 2f,
                    ParticlePrefabId = _config.containerTrap.particlePrefabId
                });
            }
            
            Plugin.Log.LogInfo($"[TrapSystem] Spawned chest at {position} for {ownerName}");
            OnChestSpawn?.Invoke(chestEntity, owner);
        }
        
        private int GetChestsSpawned(Entity player)
        {
            if (_entityManager.HasComponent<KillStreakTracker>(player))
            {
                return _entityManager.GetComponentData<KillStreakTracker>(player).ChestsSpawned;
            }
            return 0;
        }
        
        #endregion
        
        #region Container Trap Methods
        
        /// <summary>
        /// Called when a container is opened. Checks for trap and triggers if unauthorized.
        /// </summary>
        public void OnContainerOpened(Entity containerEntity, Entity playerEntity, string playerName)
        {
            if (!_entityManager.HasComponent<ContainerTrap>(containerEntity))
            {
                return;
            }
            
            var trap = _entityManager.GetComponentData<ContainerTrap>(containerEntity);
            
            // Check if trap is armed
            if (!trap.IsArmed)
            {
                Plugin.Log.LogDebug("[TrapSystem] Trap is not armed");
                return;
            }
            
            // Check cooldown
            if (Time.ElapsedTime - trap.LastTriggerTime < trap.CooldownSeconds)
            {
                Plugin.Log.LogDebug("[TrapSystem] Trap is on cooldown");
                return;
            }
            
            // Check max triggers
            if (trap.MaxTriggers > 0 && trap.TriggerCount >= trap.MaxTriggers)
            {
                Plugin.Log.LogInfo("[TrapSystem] Trap has reached max triggers");
                trap.IsArmed = false;
                _entityManager.SetComponentData(containerEntity, trap);
                return;
            }
            
            // Check if opener is the owner
            bool isOwner = trap.OwnerEntity == playerEntity;
            
            // Check if player has required streak
            int playerStreak = 0;
            if (_entityManager.HasComponent<KillStreakTracker>(playerEntity))
            {
                playerStreak = _entityManager.GetComponentData<KillStreakTracker>(playerEntity).CurrentStreak;
            }
            
            bool hasRequiredStreak = playerStreak >= _config.chestSpawn.requiredStreak;
            
            // Trigger trap if not owner and doesn't have required streak
            if (!isOwner && !hasRequiredStreak)
            {
                TriggerTrap(containerEntity, playerEntity, playerName, trap);
            }
            
            // Update trigger count
            trap.TriggerCount++;
            trap.LastTriggerTime = Time.ElapsedTime;
            _entityManager.SetComponentData(containerEntity, trap);
        }
        
        private void TriggerTrap(Entity containerEntity, Entity intruderEntity, string intruderName, ContainerTrap trap)
        {
            trap.IsTriggered = true;
            
            var position = float3.zero;
            if (_entityManager.HasComponent<LocalTransform>(containerEntity))
            {
                position = _entityManager.GetComponentData<LocalTransform>(containerEntity).Position;
            }
            
            // Spawn the ability
            if (_entityManager.HasComponent<TrapAbility>(containerEntity))
            {
                var ability = _entityManager.GetComponentData<TrapAbility>(containerEntity);
                SpawnTrapAbility(containerEntity, ability, position);
            }
            
            // Log trigger event
            if (_entityManager.HasComponent<TrapTriggerLog>(containerEntity))
            {
                var logBuffer = _entityManager.GetBuffer<TrapTriggerLog>(containerEntity);
                logBuffer.Add(new TrapTriggerLog
                {
                    Timestamp = Time.ElapsedTime,
                    IntruderName = new FixedString64Bytes(intruderName),
                    TriggerPosition = position,
                    TrapSprung = true
                });
            }
            
            // Notify trap owner
            if (trap.OwnerEntity != Entity.Null && _entityManager.Exists(trap.OwnerEntity))
            {
                var message = $"[TRAP ALERT] {intruderName} triggered your trap at position {position}!";
                SendNotification(trap.OwnerEntity, message, MessageType.TrapTriggered);
            }
            
            Plugin.Log.LogInfo($"[TrapSystem] Trap triggered by {intruderName} at {position}");
            OnTrapTriggered?.Invoke(containerEntity, intruderEntity, position);
        }
        
        private void SpawnTrapAbility(Entity trapEntity, TrapAbility ability, float3 position)
        {
            var spawnPosition = position + ability.SpawnOffset;
            
            // Create ability entity
            var abilityEntity = _entityManager.CreateEntity();
            
            _entityManager.AddComponentData(abilityEntity, new LocalTransform
            {
                Position = spawnPosition,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            
            // Add glow effect if enabled
            if (ability.HasGlow)
            {
                // Glow would be added here - depends on your glow system
                Plugin.Log.LogInfo($"[TrapSystem] Spawning glowing ability at {spawnPosition} with color {ability.GlowColor}");
            }
            
            Plugin.Log.LogInfo($"[TrapSystem] Spawned trap ability at {spawnPosition}");
        }
        
        #endregion
        
        #region Waypoint Trap Methods
        
        /// <summary>
        /// Checks if a player triggered a waypoint trap.
        /// </summary>
        public void CheckWaypointTrap(Entity player, string playerName, float3 playerPosition)
        {
            if (!_config.waypointTrap.enabled)
            {
                return;
            }
            
            // Get player streak
            int playerStreak = 0;
            if (_entityManager.HasComponent<KillStreakTracker>(player))
            {
                playerStreak = _entityManager.GetComponentData<KillStreakTracker>(player).CurrentStreak;
            }
            
            // Check if player meets requirement
            if (playerStreak < _config.waypointTrap.requiredStreak)
            {
                return;
            }
            
            // Check each waypoint
            foreach (var waypoint in _config.waypointTrap.waypoints)
            {
                if (!waypoint.isActive)
                {
                    continue;
                }
                
                var distance = math.distance(playerPosition, waypoint.position);
                
                if (distance <= _config.waypointTrap.effectRadius)
                {
                    // Check if player has triggered this waypoint before (use player state)
                    Plugin.Log.LogInfo($"[TrapSystem] Player {playerName} triggered waypoint trap {waypoint.index}");
                    
                    // Trigger the trap
                    SpawnWaypointTrapEffect(waypoint.position, player, playerName);
                }
            }
        }
        
        private void SpawnWaypointTrapEffect(float3 position, Entity player, string playerName)
        {
            var trapEntity = _entityManager.CreateEntity();
            
            _entityManager.AddComponentData(trapEntity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            
            var glow = _config.waypointTrap.glow;
            Plugin.Log.LogInfo($"[TrapSystem] Waypoint trap activated at {position} - Color: {glow?.r},{glow?.g},{glow?.b}");
        }
        
        #endregion
        
        #region Notification Methods
        
        private void SendNotification(Entity player, string message, MessageType type)
        {
            if (!_entityManager.HasComponent<PlayerMessageBuffer>(player))
            {
                _entityManager.AddComponent<PlayerMessageBuffer>(player);
            }
            
            var buffer = _entityManager.GetBuffer<PlayerMessageBuffer>(player);
            buffer.Add(new PlayerMessageBuffer
            {
                Message = new FixedString128Bytes(message),
                Duration = 10f,
                Priority = (int)type,
                Type = type,
                CreatedAt = Time.ElapsedTime
            });
            
            OnNotification?.Invoke(player, message, type);
        }
        
        #endregion
    }
}
