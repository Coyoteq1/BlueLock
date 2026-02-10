using System;
using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using VAutomationCore.Core;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Patches
{
    /// <summary>
    /// Patch for DeathEventListenerSystem to track death events.
    /// Provides events for kills, deaths, and VBlood consumption.
    /// </summary>
    [HarmonyPatch]
    internal static class DeathEventSystemPatch
    {
        public static event EventHandler<DeathEventArgs> OnDeathEvent;
        
        public class DeathEventArgs : EventArgs
        {
            public Entity Killer { get; set; }
            public Entity Victim { get; set; }
            public StatChangeReason Reason { get; set; }
            public bool IsPlayerKill { get; set; }
            public bool IsVBlood { get; set; }
        }

        [HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
        [HarmonyPostfix]
        static unsafe void OnUpdatePostfix(DeathEventListenerSystem __instance)
        {
            if (!CoreLogger.IsInitialized) return;

            try
            {
                using var deathEvents = __instance._DeathEventQuery.ToComponentDataArrayAccessor<DeathEvent>(Allocator.Temp);
                var killerLookup = __instance.GetComponentLookup<Killer>(true);
                var vBloodLookup = __instance.GetComponentLookup<VBloodConsumeSource>(true);

                for (int i = 0; i < deathEvents.Length; i++)
                {
                    var deathEvent = deathEvents[i];
                    
                    var args = new DeathEventArgs
                    {
                        Killer = deathEvent.Killer,
                        Victim = deathEvent.Died,
                        Reason = deathEvent.StatChangeReason,
                        IsPlayerKill = killerLookup.HasComponent(deathEvent.Killer),
                        IsVBlood = vBloodLookup.HasComponent(deathEvent.Died)
                    };

                    OnDeathEvent?.Invoke(__instance, args);
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error processing death events", ex);
            }
        }
    }
}
