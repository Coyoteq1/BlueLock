using System;
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

        public static void Initialize()
        {
            if (_isInitialized) return;

            _harmony = new Harmony("gg.coyote.Vlifecycle.ConnectionEvents");
            _harmony.PatchAll(typeof(ConnectionEventPatches));
            _isInitialized = true;

            Plugin.Log.LogInfo("[Vlifecycle] ConnectionEventPatches initialized");
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
                    var userIndex = __instance._NetEndPointToUserIndex[netConnectionId];
                    ArenaLifecycleManager.Instance.OnPlayerConnected(userIndex);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[OnUserConnectedPatch] Error: {ex.Message}");
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
                    var userIndex = __instance._NetEndPointToUserIndex[netConnectionId];
                    ArenaLifecycleManager.Instance.OnPlayerDisconnected(userIndex);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[OnUserDisconnectedPatch] Error: {ex.Message}");
                }
            }
        }

    }
}
