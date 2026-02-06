using Unity.Entities;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using VAuto.Core;
using VAuto.Core.Lifecycle;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal sealed class VBloodSectionSaver : ISnapshotSectionSaver
    {
        public void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            try
            {
                if (!em.HasBuffer<VBloodUnlockTechBuffer>(character))
                {
                    snapshot.VBloodMode = VBloodSnapshotMode.RepairOnly;
                    snapshot.VBloodUnlocks = new System.Collections.Generic.List<long>();
                    return;
                }

                var buf = em.GetBuffer<VBloodUnlockTechBuffer>(character);
                if (buf.Length == 0)
                {
                    snapshot.VBloodMode = VBloodSnapshotMode.RepairOnly;
                    snapshot.VBloodUnlocks = new System.Collections.Generic.List<long>();
                    return;
                }

                snapshot.VBloodMode = VBloodSnapshotMode.RestoreExact;
                snapshot.VBloodUnlocks = new System.Collections.Generic.List<long>(buf.Length);
                for (int i = 0; i < buf.Length; i++)
                {
                    snapshot.VBloodUnlocks.Add(buf[i].Guid.GuidHash);
                }
            }
            catch
            {
                snapshot.VBloodMode = VBloodSnapshotMode.RepairOnly;
                snapshot.VBloodUnlocks = new System.Collections.Generic.List<long>();
            }
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            if (snapshot.VBloodMode == VBloodSnapshotMode.Ignore)
                return;

            if (snapshot.VBloodMode == VBloodSnapshotMode.RepairOnly)
            {
                RequestRepairRefresh();
                return;
            }

            if (snapshot.VBloodMode == VBloodSnapshotMode.RestoreExact && snapshot.VBloodUnlocks != null)
            {
                try
                {
                    var buf = em.HasBuffer<VBloodUnlockTechBuffer>(character)
                        ? em.GetBuffer<VBloodUnlockTechBuffer>(character)
                        : em.AddBuffer<VBloodUnlockTechBuffer>(character);

                    buf.Clear();

                    foreach (var guidValue in snapshot.VBloodUnlocks)
                    {
                        buf.Add(new VBloodUnlockTechBuffer { Guid = new PrefabGUID((int)guidValue) });
                    }

                    // Ensure any derived progression/state gets rebuilt.
                    RequestRepairRefresh();
                }
                catch
                {
                    // Fallback to additive unlock events if buffer editing is not supported.
                    foreach (var guidValue in snapshot.VBloodUnlocks)
                    {
                        QueueUnlock(em, character, new PrefabGUID((int)guidValue));
                    }

                    RequestRepairRefresh();
                }
            }
        }

        private static void QueueUnlock(EntityManager em, Entity character, PrefabGUID vblood)
        {
            try
            {
                var e = em.CreateEntity(
                    ComponentType.ReadWrite<UnlockVBlood>(),
                    ComponentType.ReadWrite<FromCharacter>());

                em.SetComponentData(e, new UnlockVBlood { VBlood = vblood });
                em.SetComponentData(e, new FromCharacter { Character = character });
            }
            catch
            {
                // swallow
            }
        }

        public void Reset()
        {
            // No internal state to reset for VBlood unlocks
        }

        private static void RequestRepairRefresh()
        {
            try
            {
                var world = VRCore.ServerWorld;
                if (world == null)
                {
                    Plugin.Log.LogWarning("[VBloodSectionSaver] ServerWorld is null, cannot request repair refresh");
                    return;
                }

                var em = world.EntityManager;
                var query = em.CreateEntityQuery(ComponentType.ReadOnly<PendingVbloodRepairRefresh>());
                if (!query.IsEmpty)
                    return;

                em.CreateEntity(ComponentType.ReadWrite<PendingVbloodRepairRefresh>());
            }
            catch
            {
                // ignore
            }
        }
    }
}
