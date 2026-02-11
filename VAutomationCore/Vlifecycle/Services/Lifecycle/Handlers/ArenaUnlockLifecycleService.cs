using System.Collections.Generic;
using BepInEx.Logging;
using Unity.Entities;
using VAuto.Core.Lifecycle;

namespace VAuto.Core.Lifecycle.Handlers
{
    /// <summary>
    /// Lifecycle service that orchestrates all unlock/reset handlers when player enters or leaves arena.
    /// This service enables full endgame sandbox mode during arena sessions.
    /// </summary>
    public class ArenaUnlockLifecycleService : IArenaLifecycleService
    {
        private readonly List<LifecycleActionHandler> _enterHandlers = new();
        private readonly List<LifecycleActionHandler> _exitHandlers = new();
        private ManualLogSource _log;

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Initialize the arena unlock lifecycle service
        /// </summary>
        public void Initialize(ManualLogSource logger)
        {
            _log = logger;
            
            // ENTRY CHAIN - Execute when player enters arena
            _enterHandlers.Add(new SavePlayerStateHandler());
            _enterHandlers.Add(new ResetCooldownsHandler());
            _enterHandlers.Add(new ClearBuffsHandler());
            _enterHandlers.Add(new PlayerEndgameUnlockHandler());
            _enterHandlers.Add(new UnlockMenusHandler());
            _enterHandlers.Add(new BoostStatsHandler());
            
            // Phase 2-3: Automation Handlers
            _enterHandlers.Add(new AutoVBloodUnlockHandler());
            _enterHandlers.Add(new AutoSpellbookGrantHandler());
            
            _enterHandlers.Add(new ApplyBuffHandler { BuffId = "ArenaReady" });
            _enterHandlers.Add(new TeleportHandler { Position = new Unity.Mathematics.float3(-1000, 5, -500) });
            _enterHandlers.Add(new MessageActionHandler { Message = "Entering Arena — All powers unlocked!" });
            _enterHandlers.Add(new CreateGameplayEventHandler { EventPrefab = "ArenaEnterComplete" });

            // EXIT CHAIN - Execute when player exits arena
            _exitHandlers.Add(new ClearBuffsHandler());
            _exitHandlers.Add(new LockMenusHandler());
            _exitHandlers.Add(new PlayerEndgameLockHandler());
            
            _exitHandlers.Add(new RestorePlayerStateHandler());
            _exitHandlers.Add(new MessageActionHandler { Message = "Exiting Arena — Restoring Progression..." });
            _exitHandlers.Add(new CreateGameplayEventHandler { EventPrefab = "ArenaExitComplete" });

            IsInitialized = true;
            _log?.LogInfo("[ArenaUnlockLifecycleService] Initialized with unlock/reset handler chains");
        }

        /// <summary>
        /// Cleanup the service
        /// </summary>
        public void Cleanup()
        {
            _enterHandlers.Clear();
            _exitHandlers.Clear();
            IsInitialized = false;
            _log?.LogInfo("[ArenaUnlockLifecycleService] Cleaned up");
        }

        /// <summary>
        /// Called when player enters arena - Execute all unlock handlers
        /// </summary>
        public bool OnPlayerEnter(Entity user, Entity character, string arenaId)
        {
            if (!ValidateState())
            {
                _log?.LogWarning("[ArenaUnlockLifecycleService] Service not initialized");
                return false;
            }

            _log?.LogInfo($"[ArenaUnlockLifecycleService] Player entering arena {arenaId}");

            var context = new LifecycleContext
            {
                UserEntity = user,
                CharacterEntity = character,
                ArenaId = arenaId,
                StoredData = new Dictionary<string, object>()
            };

            foreach (var handler in _enterHandlers)
            {
                try
                {
                    var action = new LifecycleAction { ActionType = LifecycleActionType.Custom };
                    handler.Execute(action, context);
                }
                catch (System.Exception ex)
                {
                    _log?.LogError($"[ArenaUnlockLifecycleService] Error in enter handler: {ex.Message}");
                }
            }

            _log?.LogInfo($"[ArenaUnlockLifecycleService] ✅ Arena entry unlocks complete");
            return true;
        }

        /// <summary>
        /// Called when player exits arena - Execute all reset handlers
        /// </summary>
        public bool OnPlayerExit(Entity user, Entity character, string arenaId)
        {
            if (!ValidateState())
            {
                _log?.LogWarning("[ArenaUnlockLifecycleService] Service not initialized");
                return true; // Don't fail exit
            }

            _log?.LogInfo($"[ArenaUnlockLifecycleService] Player exiting arena {arenaId}");

            var context = new LifecycleContext
            {
                UserEntity = user,
                CharacterEntity = character,
                ArenaId = arenaId,
                StoredData = new Dictionary<string, object>()
            };

            foreach (var handler in _exitHandlers)
            {
                try
                {
                    var action = new LifecycleAction { ActionType = LifecycleActionType.Custom };
                    handler.Execute(action, context);
                }
                catch (System.Exception ex)
                {
                    _log?.LogError($"[ArenaUnlockLifecycleService] Error in exit handler: {ex.Message}");
                }
            }

            _log?.LogInfo($"[ArenaUnlockLifecycleService] ✅ Arena exit resets complete");
            return true;
        }

        /// <summary>
        /// Called when arena lifecycle starts
        /// </summary>
        public bool OnArenaStart(string arenaId)
        {
            _log?.LogInfo($"[ArenaUnlockLifecycleService] Arena {arenaId} started");
            return true;
        }

        /// <summary>
        /// Called when arena lifecycle ends
        /// </summary>
        public bool OnArenaEnd(string arenaId)
        {
            _log?.LogInfo($"[ArenaUnlockLifecycleService] Arena {arenaId} ended");
            ArenaTracker.ClearArena(arenaId);
            return true;
        }

        // Unused interface methods
        public bool OnBuildStart(Entity u, string s, string a) => true;
        public bool OnBuildComplete(Entity u, string s, string a) => true;
        public bool OnBuildDestroy(Entity u, string s, string a) => true;

        private bool ValidateState()
        {
            return IsInitialized && _log != null;
        }
    }
}
