using Unity.Entities;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using VAuto.Core;

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
                    snapshot.VBloodUnlocks = new System.Collections.Generic.List<int>();
                    return;
                }

                var buf = em.GetBuffer<VBloodUnlockTechBuffer>(character);
                if (buf.Length == 0)
                {
                    snapshot.VBloodMode = VBloodSnapshotMode.RepairOnly;
                    snapshot.VBloodUnlocks = new System.Collections.Generic.List<int>();
                    return;
                }

                snapshot.VBloodMode = VBloodSnapshotMode.RestoreExact;
                snapshot.VBloodUnlocks = new System.Collections.Generic.List<int>(buf.Length);
                for (int i = 0; i < buf.Length; i++)
                {
                    snapshot.VBloodUnlocks.Add(buf[i].Guid.GuidHash);
                }
            }
            catch
            {
                snapshot.VBloodMode = VBloodSnapshotMode.RepairOnly;
                snapshot.VBloodUnlocks = new System.Collections.Generic.List<int>();
            }
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            if (snapshot.VBloodMode == VBloodSnapshotMode.Ignore)
                return;

            if (snapshot.VBloodMode == VBloodSnapshotMode.RepairOnly)
            {
                TryRepairVbloodProgression();
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
                        buf.Add(new VBloodUnlockTechBuffer { Guid = new PrefabGUID(guidValue) });
                    }

                    // Ensure any derived progression/state gets rebuilt.
                    TryRepairVbloodProgression();
                }
                catch
                {
                    // Fallback to additive unlock events if buffer editing is not supported.
                    foreach (var guidValue in snapshot.VBloodUnlocks)
                    {
                        QueueUnlock(em, character, new PrefabGUID(guidValue));
                    }

                    TryRepairVbloodProgression();
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

        private static void TryRepairVbloodProgression()
        {
            try
            {
                VRCore.Initialize();
                var world = VRCore.ServerWorld;
                if (world == null)
                    return;

                // RepairVBloodProgressionSystem is an unmanaged ISystem. Drive a one-shot update so
                // derived progression/state gets rebuilt after we mutate the unlock buffer.
                var handle = world.GetExistingSystem<RepairVBloodProgressionSystem>();
                if (handle == default)
                    handle = world.GetOrCreateSystem<RepairVBloodProgressionSystem>();

                ref var state = ref world.Unmanaged.GetExistingSystemState<RepairVBloodProgressionSystem>();
                ref var sys = ref world.Unmanaged.GetUnsafeSystemRef<RepairVBloodProgressionSystem>(handle);
                sys.OnUpdate(ref state);
            }
            catch
            {
                // ignore
            }
        }
    }
}
