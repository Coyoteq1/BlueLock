using VAuto.Zone;

namespace VAuto.Zone.Services
{
    public static class ZoneGlowBorderService
    {
        public static void QueueRebuild(string reason = "service", bool bypassCooldown = false)
        {
            Plugin.QueueZoneBorderRebuild(reason, bypassCooldown);
        }

        public static void RebuildNow(string reason = "service-manual")
        {
            Plugin.RebuildZoneBordersNow(reason);
        }

        public static void RotateNow()
        {
            Plugin.RotateZoneBorderGlowNow();
        }

        public static void ClearNow()
        {
            Plugin.ClearZoneBordersNow();
        }

        public static void ReloadConfigAndRebuild()
        {
            Plugin.RebuildZoneBordersNow("config-reload");
        }
    }
}
