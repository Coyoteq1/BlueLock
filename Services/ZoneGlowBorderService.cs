using VAuto.Zone;

namespace VAuto.Zone.Services
{
    public static class ZoneGlowBorderService
    {
        public static void ReloadConfigAndRebuild()
        {
            Plugin.ForceGlowRebuild();
        }
    }
}
