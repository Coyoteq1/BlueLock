using Unity.Entities;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal sealed class SpellbookSectionSaver : ISnapshotSectionSaver
    {
        public bool SlotsChanged { get; private set; }

        public void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // TODO: discover actual spellbook buffers; for now no-op capture.
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Without concrete spellbook buffer types, we skip restore but mark unchanged.
            SlotsChanged = false;
        }
    }
}
