using System;
using HarmonyLib;
using Unity.Entities;
using VAutomationCore.Core.ECS.Components;
using VAutomationCore.Services;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Patches
{
    /// <summary>
    /// Patch-first hooks for zone lifecycle entry/exit and component sync.
    /// </summary>
    [HarmonyPatch]
    internal static class ZoneLifecyclePatch
    {
        public static Func<string, string> NormalizeZoneId;
        public static Func<Entity, string, bool> OnBeforeEnter;
        public static Action<Entity, string> OnAfterEnter;
        public static Func<Entity, string, bool> OnBeforeExit;
        public static Action<Entity, string> OnAfterExit;
        public static Func<EcsPlayerZoneState, EcsPlayerZoneState> OnBeforeComponentSync;

        private static string Normalize(string zoneId)
        {
            if (NormalizeZoneId != null)
            {
                return NormalizeZoneId(zoneId);
            }

            return zoneId ?? string.Empty;
        }

        [HarmonyPatch(typeof(ZoneEventBridge), nameof(ZoneEventBridge.PublishPlayerEntered))]
        [HarmonyPrefix]
        private static bool PublishPlayerEnteredPrefix(Entity player, ref string zoneId)
        {
            try
            {
                zoneId = Normalize(zoneId);
                if (OnBeforeEnter == null)
                {
                    return true;
                }

                return OnBeforeEnter(player, zoneId);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in ZoneLifecyclePatch.PublishPlayerEnteredPrefix", ex);
                return true; // Allow continuation on error
            }
        }

        [HarmonyPatch(typeof(ZoneEventBridge), nameof(ZoneEventBridge.PublishPlayerEntered))]
        [HarmonyPostfix]
        private static void PublishPlayerEnteredPostfix(Entity player, string zoneId)
        {
            try
            {
                var normalized = Normalize(zoneId);
                OnAfterEnter?.Invoke(player, normalized);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in ZoneLifecyclePatch.PublishPlayerEnteredPostfix", ex);
            }
        }

        [HarmonyPatch(typeof(ZoneEventBridge), nameof(ZoneEventBridge.PublishPlayerExited))]
        [HarmonyPrefix]
        private static bool PublishPlayerExitedPrefix(Entity player, ref string zoneId)
        {
            try
            {
                zoneId = Normalize(zoneId);
                if (OnBeforeExit == null)
                {
                    return true;
                }

                return OnBeforeExit(player, zoneId);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in ZoneLifecyclePatch.PublishPlayerExitedPrefix", ex);
                return true; // Allow continuation on error
            }
        }

        [HarmonyPatch(typeof(ZoneEventBridge), nameof(ZoneEventBridge.PublishPlayerExited))]
        [HarmonyPostfix]
        private static void PublishPlayerExitedPostfix(Entity player, string zoneId)
        {
            try
            {
                var normalized = Normalize(zoneId);
                OnAfterExit?.Invoke(player, normalized);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in ZoneLifecyclePatch.PublishPlayerExitedPostfix", ex);
            }
        }

        [HarmonyPatch(typeof(ZoneEventBridge), nameof(ZoneEventBridge.UpdateFromComponentState))]
        [HarmonyPrefix]
        private static void UpdateFromComponentStatePrefix(ref EcsPlayerZoneState state)
        {
            try
            {
                if (OnBeforeComponentSync == null)
                {
                    return;
                }

                state = OnBeforeComponentSync(state);
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error in ZoneLifecyclePatch.UpdateFromComponentStatePrefix", ex);
            }
        }
    }
}
