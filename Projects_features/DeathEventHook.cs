using HarmonyLib;
using Unity.Entities;
using VAuto.Core.Services;

namespace VAuto.Core.Harmony
{
    /// <summary>
    /// Death event hook for tracking player kills.
    /// This is a registration point - actual death detection needs to be done
    /// by systems that have access to ProjectM's internal types.
    /// 
    /// Usage: Call DeathEventHook.RegisterKill(killerId, victimId) when a player dies.
    /// </summary>
    public static class DeathEventHook
    {
        private static bool _initialized;
        private static readonly object _initLock = new object();
        
        /// <summary>
        /// Initialize the death event hook.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;
                _initialized = true;
                
                Plugin.Log.LogInfo("[DeathEventHook] Initialized - ready to track kills");
            }
        }
        
        /// <summary>
        /// Register a kill event.
        /// Call this from any system that detects player deaths.
        /// </summary>
        /// <param name="killerPlatformId">The platform ID of the killer</param>
        /// <param name="victimPlatformId">The platform ID of the victim</param>
        public static void RegisterKill(ulong killerPlatformId, ulong victimPlatformId)
        {
            // Validate inputs
            if (killerPlatformId == 0 || victimPlatformId == 0) return;
            if (killerPlatformId == victimPlatformId) return; // No self-kills
            
            // Forward to trap spawn rules
            TrapSpawnRules.OnPlayerDeath(killerPlatformId, victimPlatformId);
            
            Plugin.Log.LogInfo($"[DeathEventHook] Kill registered: {killerPlatformId} -> {victimPlatformId}");
        }
        
        /// <summary>
        /// Register a death (for tracking victim streaks).
        /// </summary>
        public static void RegisterDeath(ulong victimPlatformId)
        {
            if (victimPlatformId == 0) return;
            
            // Reset the victim's kill streak
            TrapSpawnRules.ResetStreak(victimPlatformId);
            
            Plugin.Log.LogInfo($"[DeathEventHook] Death registered: {victimPlatformId}");
        }
    }
    
    /// <summary>
    /// Placeholder for future death system integration.
    /// This class demonstrates how to hook into Unity ECS systems when types are available.
    /// </summary>
    [HarmonyPatch]
    public static class DeathSystemPatch
    {
        // These methods would be implemented when ProjectM types are available:
        // 
        // [HarmonyPatch(typeof(DamageSystem), "OnDeath")]
        // [HarmonyPostfix]
        // static void OnDeath_Postfix(Entity victim, Entity attacker)
        // {
        //     if (attacker != Entity.Null && victim != Entity.Null)
        //     {
        //         var killerId = GetPlatformId(attacker);
        //         var victimId = GetPlatformId(victim);
        //         if (killerId.HasValue && victimId.HasValue)
        //         {
        //             DeathEventHook.RegisterKill(killerId.Value, victimId.Value);
        //         }
        //     }
        // }
        //
        // private static ulong? GetPlatformId(Entity entity)
        // {
        //     var em = entity.World.EntityManager;
        //     if (em.HasComponent<PlayerCharacter>(entity))
        //     {
        //         var pc = em.GetComponentData<PlayerCharacter>(entity);
        //         var user = em.GetComponentData<User>(pc.UserEntity);
        //         return (ulong)user.PlatformId;
        //     }
        //     return null;
        // }
        
        /// <summary>
        /// Apply harmony patches (call from Plugin.cs when ready).
        /// </summary>
        public static void Patch(HarmonyLib.Harmony harmony)
        {
            // TODO: Uncomment when ProjectM types are available in interop
            // Plugin.Log.LogInfo("[DeathSystemPatch] Harmony patches would be applied here");
        }
    }
}
