using System;
using System.Reflection;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using VampireCommandFramework;
using Unity.Mathematics;
using VAuto.Core.Lifecycle;

namespace VLifecycle.Services.Lifecycle
{
    /// <summary>
    /// Handles user connection/disconnection events for cleanup and lifecycle management.
    /// </summary>
    public static class ConnectionEventPatches
    {
        private static bool _isInitialized = false;
        private static Harmony _harmony;
        private static readonly string _logPrefix = "[ConnectionEventPatches]";
        
        // Cache the NetEndPointToUserIndex field for performance
        private static FieldInfo _netEndPointToUserIndexField;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // Try to get the internal field via reflection
            try {
                var bootstrapType = typeof(ServerBootstrapSystem);
                _netEndPointToUserIndexField = bootstrapType.GetField("_NetEndPointToUserIndex", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (_netEndPointToUserIndexField != null)
                {
                    Plugin.Log.LogInfo($"{_logPrefix} Found _NetEndPointToUserIndex field via reflection");
                }
                else
                {
                    Plugin.Log.LogWarning($"{_logPrefix} _NetEndPointToUserIndex field not found - connection events will be skipped");
                }
            } catch (Exception ex) {
                Plugin.Log.LogWarning($"{_logPrefix} Failed to get _NetEndPointToUserIndex: {ex.Message}");
            }

            _harmony = new Harmony("gg.coyote.Vlifecycle.ConnectionEvents");
            _harmony.PatchAll(typeof(ConnectionEventPatches));
            _isInitialized = true;

            Plugin.Log.LogInfo($"{_logPrefix} Initialized");
        }

        public static void Dispose()
        {
            _harmony?.UnpatchSelf();
            _isInitialized = false;
        }

        /// <summary>
        /// Handles player connection events.
        /// </summary>
        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
        public static class OnUserConnectedPatch
        {
            [HarmonyPostfix]
            public static void Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
            {
                try
                {
                    var userIndex = GetUserIndex(__instance, netConnectionId);
                    if (userIndex.HasValue)
                    {
                        ArenaLifecycleManager.Instance.OnPlayerConnected(userIndex.Value);
                    }
                    else
                    {
                        Plugin.Log.LogDebug($"{_logPrefix} OnUserConnected: could not resolve user index for {netConnectionId}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"{_logPrefix} OnUserConnected Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles player disconnection events.
        /// </summary>
        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserDisconnected))]
        public static class OnUserDisconnectedPatch
        {
            [HarmonyPostfix]
            public static void Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
            {
                try
                {
                    var userIndex = GetUserIndex(__instance, netConnectionId);
                    if (userIndex.HasValue)
                    {
                        ArenaLifecycleManager.Instance.OnPlayerDisconnected(userIndex.Value);
                    }
                    else
                    {
                        Plugin.Log.LogDebug($"{_logPrefix} OnUserDisconnected: could not resolve user index for {netConnectionId}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"{_logPrefix} OnUserDisconnected Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Try to get user index from NetConnectionId using reflection.
        /// Returns null if the field is not available.
        /// </summary>
        private static int? GetUserIndex(ServerBootstrapSystem instance, NetConnectionId connectionId)
        {
            if (_netEndPointToUserIndexField == null)
                return null;

            try
            {
                var dictionary = _netEndPointToUserIndexField.GetValue(instance) as System.Collections.IDictionary;
                if (dictionary != null && dictionary.Contains(connectionId))
                {
                    return (int)dictionary[connectionId];
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"{_logPrefix} Failed to get user index: {ex.Message}");
            }
            return null;
        }
    }
}
