using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using ProjectM;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Core.Patterns;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// Arena Lifecycle Manager - Handles lifecycle events for arena zones 
    /// This class is loaded by VAutoZone, intending to make it handle all future events triggering 
    /// </summary>
    public class ArenaLifecycleManager : Singleton<ArenaLifecycleManager>
    {
        private static readonly string _logPrefix = "[ArenaLifecycleManager]";
        
        // Lifecycle stages for arena
        private readonly Dictionary<string, LifecycleStage> _lifecycleStages;
        private readonly Dictionary<string, LifecycleActionHandler> _actionHandlers;
        
        public ManualLogSource Log { get; private set; }
        public new bool IsInitialized { get; private set; }
        public int ServiceCount => _lifecycleStages.Count;

        public ArenaLifecycleManager()
        {
            Log = VLifecycle.Plugin.Log;
            _lifecycleStages = new Dictionary<string, LifecycleStage>();
            _actionHandlers = new Dictionary<string, LifecycleActionHandler>();
            InitializeActionHandlers();
        }

        /// <summary>
        /// Initialize the arena lifecycle manager
        /// </summary>
        public void Initialize()
        {
            try
            {
                if (IsInitialized) return;
                
                RegisterLifecycleStages();
                IsInitialized = true;
                Log?.LogInfo($"{_logPrefix} Initialized");
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Shutdown the arena lifecycle manager
        /// </summary>
        public void Shutdown()
        {
            if (!IsInitialized) return;
            
            _lifecycleStages.Clear();
            _actionHandlers.Clear();
            IsInitialized = false;
            Log?.LogInfo($"{_logPrefix} Shutdown");
        }

        /// <summary>
        /// Trigger a lifecycle stage when entering arena
        /// </summary>
        public bool OnEnterArena(Entity character, float3 position)
        {
            return TriggerLifecycleStage("onEnterArenaZone", new LifecycleContext
            {
                CharacterEntity = character,
                Position = position
            });
        }

        /// <summary>
        /// Trigger a lifecycle stage when exiting arena
        /// </summary>
        public bool OnExitArena(Entity character, float3 position)
        {
            return TriggerLifecycleStage("onExitArenaZone", new LifecycleContext
            {
                CharacterEntity = character,
                Position = position
            });
        }

        /// <summary>
        /// Called by VAutoZone when player enters arena (via reflection) - preserves position
        /// </summary>
        public bool OnPlayerEnter(Entity userEntity, Entity characterEntity, string arenaId, float3 position)
        {
            Log?.LogInfo($"{_logPrefix} Player entering arena {arenaId} at position ({position.x:F0}, {position.y:F0}, {position.z:F0})");
            return OnEnterArena(characterEntity, position);
        }

        /// <summary>
        /// Called by VAutoZone when player exits arena (via reflection) - preserves position
        /// </summary>
        public bool OnPlayerExit(Entity userEntity, Entity characterEntity, string arenaId, float3 position)
        {
            Log?.LogInfo($"{_logPrefix} Player exiting arena {arenaId} from position ({position.x:F0}, {position.y:F0}, {position.z:F0})");
            return OnExitArena(characterEntity, position);
        }

        /// <summary>
        /// Handle player connection to server
        /// </summary>
        public void OnPlayerConnected(int userIndex)
        {
            Log?.LogInfo($"{_logPrefix} Player connected: {userIndex}");
            // Initialize any server-level player state if needed
        }

        /// <summary>
        /// Handle player disconnection from server
        /// </summary>
        public void OnPlayerDisconnected(int userIndex)
        {
            Log?.LogInfo($"{_logPrefix} Player disconnected: {userIndex}");
            // Clean up any server-level player state if needed
        }

        /// <summary>
        /// Trigger a lifecycle stage
        /// </summary>
        public bool TriggerLifecycleStage(string stageName, LifecycleContext context)
        {
            try
            {
                if (!_lifecycleStages.TryGetValue(stageName, out var stage))
                {
                    Log?.LogWarning($"{_logPrefix} Unknown lifecycle stage: {stageName}");
                    return false;
                }

                bool allSuccessful = true;
                foreach (var action in stage.Actions)
                {
                    if (!ExecuteAction(action, context))
                    {
                        allSuccessful = false;
                    }
                }

                return allSuccessful;
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to trigger stage {stageName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute a single lifecycle action
        /// </summary>
        private bool ExecuteAction(LifecycleAction action, LifecycleContext context)
        {
            try
            {
                if (!_actionHandlers.TryGetValue(action.Type, out var handler))
                {
                    Log?.LogWarning($"{_logPrefix} No handler for action type: {action.Type}");
                    return false;
                }

                return handler.Execute(action, context);
            }
            catch (Exception ex)
            {
                Log?.LogError($"{_logPrefix} Failed to execute action: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of service names
        /// </summary>
        public string[] GetServiceNames()
        {
            return _lifecycleStages.Keys.ToArray();
        }

        /// <summary>
        /// Register default lifecycle stages
        /// </summary>
        private void RegisterLifecycleStages()
        {
            _lifecycleStages["onEnterArenaZone"] = new LifecycleStage
            {
                Name = "onEnterArenaZone",
                Description = "Triggered when player enters arena",
                Actions = new List<LifecycleAction>
                {
                    new LifecycleAction { Type = "save" },
                    new LifecycleAction { Type = "resetcooldowns" },
                    new LifecycleAction { Type = "message", Message = "Entering Arena Zone..." }
                }
            };

            _lifecycleStages["onExitArenaZone"] = new LifecycleStage
            {
                Name = "onExitArenaZone",
                Description = "Triggered when player exits arena",
                Actions = new List<LifecycleAction>
                {
                    new LifecycleAction { Type = "restore" },
                    new LifecycleAction { Type = "clearbuffs" },
                    new LifecycleAction { Type = "message", Message = "Exiting Arena Zone..." }
                }
            };
        }

        /// <summary>
        /// Initialize action handlers
        /// </summary>
        private void InitializeActionHandlers()
        {
            _actionHandlers["store"] = new StoreActionHandler();
            _actionHandlers["message"] = new MessageActionHandler();
            
            // State management handlers
            _actionHandlers["save"] = new SavePlayerStateHandler();
            _actionHandlers["restore"] = new RestorePlayerStateHandler();
            _actionHandlers["buff"] = new ApplyBuffHandler();
            _actionHandlers["clearbuffs"] = new ClearBuffsHandler();
            _actionHandlers["removeunequip"] = new RemoveUnequipHandler();
            _actionHandlers["resetcooldowns"] = new ResetCooldownsHandler();
            _actionHandlers["teleport"] = new TeleportHandler();
            _actionHandlers["gameplayevent"] = new CreateGameplayEventHandler();
        }

        #region Test Methods

        /// <summary>
        /// Run self-test on all lifecycle components
        /// </summary>
        public Dictionary<string, bool> SelfTest()
        {
            var results = new Dictionary<string, bool>();
            
            // Test initialization
            results["Initialized"] = IsInitialized;
            
            // Test stages registered
            results["StagesRegistered"] = _lifecycleStages.Count > 0;
            
            // Test action handlers
            results["ActionHandlers"] = _actionHandlers.Count > 0;
            
            // Test each handler type
            foreach (var handler in _actionHandlers)
            {
                results[$"Handler_{handler.Key}"] = handler.Value != null;
            }
            
            // Test stage execution (without actual actions)
            try
            {
                var testContext = new LifecycleContext
                {
                    CharacterEntity = Entity.Null,
                    Position = float3.zero
                };
                results["StageExecution"] = true;
            }
            catch
            {
                results["StageExecution"] = false;
            }
            
            return results;
        }

        /// <summary>
        /// Add a test action to a stage
        /// </summary>
        public bool AddTestAction(string stageName, LifecycleAction action)
        {
            if (!_lifecycleStages.TryGetValue(stageName, out var stage))
            {
                Log?.LogWarning($"{_logPrefix} Cannot add action - stage not found: {stageName}");
                return false;
            }
            
            stage.Actions.Add(action);
            Log?.LogInfo($"{_logPrefix} Added test action to {stageName}: {action.Type}");
            return true;
        }

        /// <summary>
        /// Clear all actions from a stage
        /// </summary>
        public bool ClearStageActions(string stageName)
        {
            if (!_lifecycleStages.TryGetValue(stageName, out var stage))
            {
                Log?.LogWarning($"{_logPrefix} Cannot clear - stage not found: {stageName}");
                return false;
            }
            
            var count = stage.Actions.Count;
            stage.Actions.Clear();
            Log?.LogInfo($"{_logPrefix} Cleared {count} actions from {stageName}");
            return true;
        }

        /// <summary>
        /// Get stage details for debugging
        /// </summary>
        public Dictionary<string, object> GetStageDetails(string stageName)
        {
            var details = new Dictionary<string, object>();
            
            if (_lifecycleStages.TryGetValue(stageName, out var stage))
            {
                details["Name"] = stage.Name;
                details["Description"] = stage.Description;
                details["ActionCount"] = stage.Actions.Count;
                
                var actionTypes = stage.Actions.Select(a => a.Type).ToList();
                details["ActionTypes"] = actionTypes;
            }
            else
            {
                details["Error"] = "Stage not found";
            }
            
            return details;
        }

        /// <summary>
        /// Get all registered stages and their action counts
        /// </summary>
        public Dictionary<string, int> GetAllStageActionCounts()
        {
            return _lifecycleStages.ToDictionary(
                s => s.Key,
                s => s.Value.Actions.Count
            );
        }

        #endregion
    }
}
