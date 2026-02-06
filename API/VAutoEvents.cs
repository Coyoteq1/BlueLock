using System;
using Unity.Entities;

namespace VAuto.API
{
    /// <summary>
    /// Cross-mod event hooks for VAutomationEvents.
    /// </summary>
    public static class VAutoEvents
    {
        public static event Action<Entity, string>? OnPlayerEnteredZone;
        public static event Action<Entity, string>? OnPlayerExitedZone;
        public static event Action<Entity, int>? OnKillStreakMilestone;

        public static void RaisePlayerEnteredZone(Entity player, string zoneName)
        {
            OnPlayerEnteredZone?.Invoke(player, zoneName);
        }

        public static void RaisePlayerExitedZone(Entity player, string zoneName)
        {
            OnPlayerExitedZone?.Invoke(player, zoneName);
        }

        public static void RaiseKillStreakMilestone(Entity player, int streak)
        {
            OnKillStreakMilestone?.Invoke(player, streak);
        }
    }
}
