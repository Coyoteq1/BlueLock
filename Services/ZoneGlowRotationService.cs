using System;
using VAuto.Zone.Services;

namespace VAuto.Zone.Services
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
