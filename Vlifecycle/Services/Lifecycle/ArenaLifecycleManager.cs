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
    /// This class is loaded by VAutoZone via reflection
    /// </summary>
    public class ArenaLifecycleManager : Singleton<ArenaLifecycleManager>
    {
        private static readonly string _logPrefix = "[ArenaLifecycleManager]";
        
        // Lifecycle stages for arena
        private readonly Dictionary<string, LifecycleStage> _lifecycleStages;
        private readonly Dictionary<string, LifecycleActionHandler> _actionHandlers;
        
        public new ManualLogSource Log { get; private set; }
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
                Actions = new List<LifecycleAction>()
            };

            _lifecycleStages["onExitArenaZone"] = new LifecycleStage
            {
                Name = "onExitArenaZone",
                Description = "Triggered when player exits arena",
                Actions = new List<LifecycleAction>()
            };
        }

        /// <summary>
        /// Initialize action handlers
        /// </summary>
        private void InitializeActionHandlers()
        {
            _actionHandlers["store"] = new StoreActionHandler();
            _actionHandlers["message"] = new MessageActionHandler();
            _actionHandlers["command"] = new CommandActionHandler();
            _actionHandlers["config"] = new ConfigActionHandler();
            _actionHandlers["zone"] = new ZoneActionHandler();
            _actionHandlers["prefix"] = new PrefixActionHandler();
            _actionHandlers["blood"] = new BloodActionHandler();
            _actionHandlers["quality"] = new QualityActionHandler();
        }
    }
}
