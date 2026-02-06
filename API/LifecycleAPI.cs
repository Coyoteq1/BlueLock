using Unity.Entities;

namespace VAuto.API
{
    /// <summary>
    /// Public API for lifecycle-related actions.
    /// </summary>
    public static class LifecycleAPI
    {
        public static bool IsInLifecycleZone(Entity player)
        {
            // TODO: wire to lifecycle ECS components.
            return false;
        }

        public static bool EnterArena(Entity player, string arenaName)
        {
            // TODO: route through lifecycle services or ECS requests.
            return false;
        }
    }
}
