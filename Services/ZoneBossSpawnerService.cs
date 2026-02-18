using Unity.Entities;

namespace VAuto.Zone.Services
{
    public static class ZoneBossSpawnerService
    {
        public static void Initialize()
        {
            // No-op stub for boss spawner initialization.
        }

        public static bool TryHandlePlayerEnter(Entity player, string zoneId, out string message)
        {
            message = string.Empty;
            return false;
        }

        public static void HandlePlayerExit(Entity player, string zoneId)
        {
            // Stub implementation for boss despawn on exit
        }
    }
}
