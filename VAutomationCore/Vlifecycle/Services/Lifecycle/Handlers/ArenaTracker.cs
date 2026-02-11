using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Unity.Entities;
using VAuto.Core;

namespace VAuto.Core.Lifecycle.Handlers
{
    /// <summary>
    /// Tracks arena state including pending bosses and player arena status.
    /// Used by patch handlers to determine if player is currently in arena.
    /// </summary>
    public static class ArenaTracker
    {
        private static readonly List<Entity> _pendingBosses = new();
        private static readonly HashSet<string> _activeArenas = new();
        private static ManualLogSource _log;

        public static bool IsAnyPlayerInArena => _activeArenas.Count > 0;

        /// <summary>
        /// Initialize the tracker
        /// </summary>
        public static void Initialize(ManualLogSource logger)
        {
            _log = logger;
            _pendingBosses.Clear();
            _activeArenas.Clear();
            _log?.LogInfo("[ArenaTracker] Initialized");
        }

        /// <summary>
        /// Cleanup the tracker
        /// </summary>
        public static void Cleanup()
        {
            _pendingBosses.Clear();
            _activeArenas.Clear();
            _log?.LogInfo("[ArenaTracker] Cleaned up");
        }

        /// <summary>
        /// Register a pending boss spawn
        /// </summary>
        public static void AddPendingBoss(Entity bossEntity)
        {
            if (!_pendingBosses.Contains(bossEntity))
            {
                _pendingBosses.Add(bossEntity);
                _log?.LogDebug($"[ArenaTracker] Added pending boss");
            }
        }

        /// <summary>
        /// Remove a boss from pending list
        /// </summary>
        public static void RemovePendingBoss(Entity bossEntity)
        {
            _pendingBosses.Remove(bossEntity);
        }

        /// <summary>
        /// Clear all pending bosses
        /// </summary>
        public static void ClearPendingBosses()
        {
            _pendingBosses.Clear();
            _log?.LogInfo("[ArenaTracker] Cleared all pending bosses");
        }

        /// <summary>
        /// Mark an arena as active
        /// </summary>
        public static void RegisterArena(string arenaId)
        {
            _activeArenas.Add(arenaId);
            _log?.LogInfo($"[ArenaTracker] Arena registered: {arenaId}");
        }

        /// <summary>
        /// Unregister an arena
        /// </summary>
        public static void UnregisterArena(string arenaId)
        {
            _activeArenas.Remove(arenaId);
            _log?.LogInfo($"[ArenaTracker] Arena unregistered: {arenaId}");
        }

        /// <summary>
        /// Clear a specific arena and its pending bosses
        /// </summary>
        public static void ClearArena(string arenaId)
        {
            _activeArenas.Remove(arenaId);
            ClearPendingBosses();
            _log?.LogInfo($"[ArenaTracker] Arena cleared: {arenaId}");
        }

        /// <summary>
        /// Get all pending boss entities
        /// </summary>
        public static IReadOnlyList<Entity> GetPendingBosses()
        {
            return _pendingBosses.ToList().AsReadOnly();
        }

        /// <summary>
        /// Check if an entity is a pending boss
        /// </summary>
        public static bool IsPendingBoss(Entity entity)
        {
            return _pendingBosses.Contains(entity);
        }
    }
}
