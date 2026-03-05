using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using VAutomationCore.Core;

namespace VAutomationCore.Abstractions
{
    public static class Buffs
    {
        public delegate void BuffCreated(Entity buffEntity);

        public static bool AddBuff(Entity userEntity, Entity targetEntity, PrefabGUID buffPrefab, float duration = 0f, bool immortal = false)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (!em.Exists(userEntity) || !em.Exists(targetEntity) || buffPrefab == PrefabGUID.Empty)
                {
                    return false;
                }

                if (BuffUtility.TryGetBuff(em, targetEntity, buffPrefab, out _))
                {
                    return false;
                }

                var debugSystem = UnifiedCore.Server.GetExistingSystemManaged<DebugEventsSystem>();
                debugSystem.ApplyBuff(
                    new FromCharacter { User = userEntity, Character = targetEntity },
                    new ApplyBuffDebugEvent { BuffPrefabGUID = buffPrefab });

                if (!BuffUtility.TryGetBuff(em, targetEntity, buffPrefab, out var buffEntity))
                {
                    return false;
                }

                if (duration > 0f)
                {
                    if (!em.HasComponent<LifeTime>(buffEntity))
                    {
                        em.AddComponent<LifeTime>(buffEntity);
                    }

                    var lifeTime = em.GetComponentData<LifeTime>(buffEntity);
                    lifeTime.Duration = duration;
                    lifeTime.EndAction = LifeTimeEndAction.Destroy;
                    em.SetComponentData(buffEntity, lifeTime);
                }
                else if (duration == -1f && em.HasComponent<LifeTime>(buffEntity))
                {
                    var lifeTime = em.GetComponentData<LifeTime>(buffEntity);
                    lifeTime.Duration = 0f;
                    lifeTime.EndAction = LifeTimeEndAction.None;
                    em.SetComponentData(buffEntity, lifeTime);
                }

                if (immortal && !em.HasComponent<Buff_Persists_Through_Death>(buffEntity))
                {
                    em.AddComponent<Buff_Persists_Through_Death>(buffEntity);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void RemoveBuff(Entity targetEntity, PrefabGUID buffPrefab)
        {
            try
            {
                var em = UnifiedCore.EntityManager;
                if (BuffUtility.TryGetBuff(em, targetEntity, buffPrefab, out var buffEntity))
                {
                    DestroyUtility.Destroy(em, buffEntity, DestroyDebugReason.TryRemoveBuff);
                }
            }
            catch
            {
            }
        }

        public static void RemoveAndAddBuff(Entity userEntity, Entity targetEntity, PrefabGUID buffPrefab, float duration = -1f, BuffCreated callback = null)
        {
            if (buffPrefab == PrefabGUID.Empty)
            {
                return;
            }

            RemoveBuff(targetEntity, buffPrefab);
            if (!AddBuff(userEntity, targetEntity, buffPrefab, duration, immortal: true))
            {
                return;
            }

            if (callback == null)
            {
                return;
            }

            try
            {
                if (BuffUtility.TryGetBuff(UnifiedCore.EntityManager, targetEntity, buffPrefab, out var buffEntity))
                {
                    callback(buffEntity);
                }
            }
            catch
            {
            }
        }
    }
}
