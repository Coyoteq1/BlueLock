using ProjectM;
using Unity.Entities;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal sealed class BuffGlowSectionSaver : ISnapshotSectionSaver
    {
        public void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            if (!em.HasBuffer<BuffBuffer>(character)) return;
            var buffs = em.GetBuffer<BuffBuffer>(character);
            for (int i = 0; i < buffs.Length; i++)
            {
                var buff = buffs[i];
                if (buff.Entity == Entity.Null) continue;
                if (!em.HasComponent<HideOutsideVision>(buff.Entity)) continue;
                snapshot.Buffs.Add(buff.PrefabGuid.GuidHash);
            }
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Best-effort: restoring glows requires a dedicated GlowService not present here.
            // We keep data for future use but do not reapply to avoid compile/runtime errors.
        }

        public void Reset()
        {
            // No internal state to reset for buff glows
        }
    }
}
