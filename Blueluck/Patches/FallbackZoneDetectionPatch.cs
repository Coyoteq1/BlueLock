using Blueluck.Services;
using HarmonyLib;
using ProjectM;

namespace Blueluck.Patches
{
    [HarmonyPatch(typeof(BacktraceSystem), nameof(BacktraceSystem.OnUpdate))]
    internal static class FallbackZoneDetectionPatch
    {
        [HarmonyPostfix]
        private static void OnUpdatePostfix()
        {
            FallbackZoneDetectionService.ProcessTick();
        }
    }
}
