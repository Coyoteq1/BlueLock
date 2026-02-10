using System;
using UnityEngine;
using VLifecycle;

namespace VLifecycle.Services.Lifecycle
{
    /// <summary>
    /// Input blocker that briefly blocks game inputs during ZUI interactions in arena zones.
    /// Provides a momentary "shield" to prevent clicks from passing through to the game.
    /// </summary>
    public static class ZUIInputBlocker
    {
        private static bool _shouldBlock = false;
        private static bool _isInitialized = false;
        private static float _unblockTime;

        /// <summary>
        /// Gets whether game inputs should currently be blocked.
        /// </summary>
        public static bool ShouldBlock => _shouldBlock;

        /// <summary>
        /// Initialize the blocker.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            _shouldBlock = false;
            _isInitialized = true;
            
            Plugin.Log.LogInfo("[ZUIInputBlocker] Initialized - starting UNBLOCKED");
        }

        /// <summary>
        /// Momentarily blocks input for a brief period (default 0.1 seconds).
        /// </summary>
        public static void BlockMomentarily(float duration = 0.1f)
        {
            if (!_isInitialized)
            {
                Plugin.Log.LogWarning("[ZUIInputBlocker] BlockMomentarily called before Initialize!");
                return;
            }

            _shouldBlock = true;
            _unblockTime = Time.unscaledTime + duration;
            Plugin.Log.LogDebug($"[ZUIInputBlocker] Momentary input block START ({duration}s)");
        }

        /// <summary>
        /// Check if block should still be active and update state.
        /// Call this in Update().
        /// </summary>
        public static void Update()
        {
            if (_shouldBlock && Time.unscaledTime >= _unblockTime)
            {
                _shouldBlock = false;
                Plugin.Log.LogDebug("[ZUIInputBlocker] Momentary input block END");
            }
        }

        /// <summary>
        /// Immediately restores game inputs.
        /// </summary>
        public static void UnblockImmediately()
        {
            _shouldBlock = false;
            Plugin.Log.LogDebug("[ZUIInputBlocker] Input block CLEARED immediately");
        }
    }
}
