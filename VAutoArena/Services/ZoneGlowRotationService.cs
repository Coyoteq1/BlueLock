using System;
using VAuto.Arena.Services;

namespace VAuto.Arena
{
    internal static class ZoneGlowRotationService
    {
        public static void Tick()
        {
            ZoneGlowBorderService.RotateDueZones();
        }

        public static void RotateAllNow()
        {
            ZoneGlowBorderService.RotateAll();
        }
    }
}
