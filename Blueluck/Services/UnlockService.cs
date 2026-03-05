using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Services.Interfaces;
using VAutomationCore.Core;

namespace Blueluck.Services
{
    /// <summary>
    /// ECS-based service for unlocking progression content using DebugEvent system.
    /// Provides actual unlocking through V Rising's DebugEventsSystem.
    /// </summary>
    public class UnlockService : IService
    {
        private static readonly ManualLogSource _log = Logger.CreateLogSource("Blueluck.Unlock");
        
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log => _log;

        private DebugEventsSystem? _debugEventsSystem;
        private EntityQuery _playerQuery;
        private EntityQuery _userQuery;
        private EntityQuery _playerCharacterQuery;

        // Unlock method cache for performance
        private readonly Dictionary<UnlockType, string[]> _unlockMethods = new();

        public enum UnlockType
        {
            Research,
            VBlood,
            Achievement,
            Spells,
            All
        }

        public void Initialize()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                _log.LogError("[Unlock] World not available");
                return;
            }

            // Get DebugEventsSystem
            _debugEventsSystem = world.GetExistingSystemManaged<DebugEventsSystem>();
            if (_debugEventsSystem == null)
            {
                _log.LogError("[Unlock] DebugEventsSystem not available");
                return;
            }

            // Setup ECS queries
            var em = world.EntityManager;
            _playerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<LocalToWorld>());
            _userQuery = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
            _playerCharacterQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerCharacter>());

            // Cache unlock methods
            CacheUnlockMethods();

            IsInitialized = true;
            _log.LogInfo("[Unlock] Initialized with DebugEvent system");
        }

        public void Cleanup()
        {
            _debugEventsSystem = null;
            _unlockMethods.Clear();
            IsInitialized = false;
            _log.LogInfo("[Unlock] Cleaned up");
        }

        /// <summary>
        /// Unlocks all progression content for a player.
        /// </summary>
        public bool UnlockAll(Entity player)
        {
            if (!IsInitialized || _debugEventsSystem == null)
            {
                _log.LogWarning("[Unlock] Service not initialized or DebugEventsSystem unavailable");
                return false;
            }

            if (!TryGetFromCharacter(player, out var fromCharacter))
            {
                _log.LogWarning($"[Unlock] Failed to get FromCharacter for player {player.Index}");
                return false;
            }

            var success = true;
            var debugSystemType = _debugEventsSystem.GetType();

            // Unlock Research
            success &= TryInvokeUnlock(debugSystemType, UnlockType.Research, fromCharacter);

            // Unlock VBloods
            success &= TryInvokeUnlock(debugSystemType, UnlockType.VBlood, fromCharacter);

            // Unlock Achievements
            success &= TryInvokeUnlock(debugSystemType, UnlockType.Achievement, fromCharacter);

            // Unlock Spells/Abilities
            success &= TryInvokeUnlock(debugSystemType, UnlockType.Spells, fromCharacter);

            if (success)
            {
                _log.LogInfo($"[Unlock] Successfully unlocked all content for player {player.Index}");
            }
            else
            {
                _log.LogWarning($"[Unlock] Partial unlock failure for player {player.Index}");
            }

            return success;
        }

        /// <summary>
        /// Unlocks specific progression type for a player.
        /// </summary>
        public bool UnlockProgressionType(Entity player, UnlockType unlockType)
        {
            if (!IsInitialized || _debugEventsSystem == null)
            {
                _log.LogWarning("[Unlock] Service not initialized or DebugEventsSystem unavailable");
                return false;
            }

            if (!TryGetFromCharacter(player, out var fromCharacter))
            {
                _log.LogWarning($"[Unlock] Failed to get FromCharacter for player {player.Index}");
                return false;
            }

            var debugSystemType = _debugEventsSystem.GetType();
            var success = TryInvokeUnlock(debugSystemType, unlockType, fromCharacter);

            if (success)
            {
                _log.LogInfo($"[Unlock] Successfully unlocked {unlockType} for player {player.Index}");
            }
            else
            {
                _log.LogWarning($"[Unlock] Failed to unlock {unlockType} for player {player.Index}");
            }

            return success;
        }

        /// <summary>
        /// Applies a specific buff using DebugEvent system.
        /// </summary>
        public bool ApplyBuff(Entity player, PrefabGUID buffPrefab, float duration = 0f)
        {
            if (!IsInitialized || _debugEventsSystem == null)
            {
                _log.LogWarning("[Unlock] Service not initialized or DebugEventsSystem unavailable");
                return false;
            }

            if (!TryGetFromCharacter(player, out var fromCharacter))
            {
                _log.LogWarning($"[Unlock] Failed to get FromCharacter for player {player.Index}");
                return false;
            }

            try
            {
                var buffEvent = new ApplyBuffDebugEvent
                {
                    BuffPrefabGUID = buffPrefab
                };

                _debugEventsSystem.ApplyBuff(fromCharacter, buffEvent);
                _log.LogInfo($"[Unlock] Applied buff {buffPrefab.GetHashCode()} to player {player.Index}");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError($"[Unlock] Failed to apply buff {buffPrefab.GetHashCode()}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a specific buff using DebugEvent system.
        /// Note: RemoveBuff may not be available in all versions.
        /// </summary>
        public bool RemoveBuff(Entity player, PrefabGUID buffPrefab)
        {
            if (!IsInitialized || _debugEventsSystem == null)
            {
                _log.LogWarning("[Unlock] Service not initialized or DebugEventsSystem unavailable");
                return false;
            }

            if (!TryGetFromCharacter(player, out var fromCharacter))
            {
                _log.LogWarning($"[Unlock] Failed to get FromCharacter for player {player.Index}");
                return false;
            }

            try
            {
                // Try to use RemoveBuff if available
                var debugSystemType = _debugEventsSystem.GetType();
                var removeBuffMethod = debugSystemType.GetMethod("RemoveBuff", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (removeBuffMethod != null)
                {
                    var buffEvent = new object(); // RemoveBuffDebugEvent if it exists
                    removeBuffMethod.Invoke(_debugEventsSystem, new object[] { fromCharacter, buffEvent });
                    _log.LogInfo($"[Unlock] Removed buff {buffPrefab.GetHashCode()} from player {player.Index}");
                    return true;
                }
                else
                {
                    _log.LogWarning($"[Unlock] RemoveBuff method not available in this version");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[Unlock] Failed to remove buff {buffPrefab.GetHashCode()}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets FromCharacter structure for a player entity.
        /// </summary>
        private bool TryGetFromCharacter(Entity player, out FromCharacter fromCharacter)
        {
            fromCharacter = default;

            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                var em = world.EntityManager;

                if (!em.Exists(player) || !em.HasComponent<PlayerCharacter>(player))
                {
                    return false;
                }

                var playerCharacter = em.GetComponentData<PlayerCharacter>(player);
                if (!em.Exists(playerCharacter.UserEntity) || !em.HasComponent<User>(playerCharacter.UserEntity))
                {
                    return false;
                }

                fromCharacter = new FromCharacter
                {
                    User = playerCharacter.UserEntity,
                    Character = player
                };

                return true;
            }
            catch (Exception ex)
            {
                _log.LogError($"[Unlock] Error creating FromCharacter: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Caches unlock method names for each unlock type.
        /// </summary>
        private void CacheUnlockMethods()
        {
            _unlockMethods[UnlockType.Research] = new[]
            {
                "UnlockAllResearch", "TriggerUnlockAllResearch"
            };

            _unlockMethods[UnlockType.VBlood] = new[]
            {
                "UnlockAllVBloods", "UnlockAllVBlood", "TriggerUnlockAllVBlood"
            };

            _unlockMethods[UnlockType.Achievement] = new[]
            {
                "CompleteAllAchievements", "TriggerCompleteAllAchievements"
            };

            _unlockMethods[UnlockType.Spells] = new[]
            {
                "UnlockAllSpells", "UnlockAllAbilities", "TriggerUnlockAllSpells", "TriggerUnlockAllAbilities",
                "UnlockAllSpellSchools", "UnlockAllMagic", "UnlockAllPowers", "CompleteAllSpells"
            };

            _unlockMethods[UnlockType.All] = _unlockMethods.Values.SelectMany(x => x).ToArray();
        }

        /// <summary>
        /// Invokes unlock methods on DebugEventsSystem.
        /// </summary>
        private bool TryInvokeUnlock(Type systemType, UnlockType unlockType, FromCharacter fromCharacter)
        {
            if (!_unlockMethods.TryGetValue(unlockType, out var methodNames))
            {
                return false;
            }

            var methods = systemType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var methodName in methodNames)
            {
                foreach (var method in methods.Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal)))
                {
                    if (TryInvokeUnlockMethod(method, fromCharacter))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Invokes a specific unlock method.
        /// </summary>
        private bool TryInvokeUnlockMethod(MethodInfo method, FromCharacter fromCharacter)
        {
            var parameters = method.GetParameters();

            try
            {
                if (parameters.Length == 0)
                {
                    method.Invoke(_debugEventsSystem, Array.Empty<object>());
                    return true;
                }

                if (parameters.Length == 1)
                {
                    if (parameters[0].ParameterType == typeof(FromCharacter))
                    {
                        method.Invoke(_debugEventsSystem, new object[] { fromCharacter });
                        return true;
                    }

                    if (parameters[0].ParameterType == typeof(Entity) && fromCharacter.User != Entity.Null)
                    {
                        method.Invoke(_debugEventsSystem, new object[] { fromCharacter.User });
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug($"[Unlock] Method {method.Name} failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Checks if the DebugEventsSystem is available.
        /// </summary>
        public bool IsDebugEventsSystemAvailable()
        {
            return _debugEventsSystem != null && IsInitialized;
        }

        /// <summary>
        /// Gets available unlock method names for a specific type.
        /// </summary>
        public string[] GetAvailableMethods(UnlockType unlockType)
        {
            return _unlockMethods.TryGetValue(unlockType, out var methods) ? methods : Array.Empty<string>();
        }
    }
}
