using Unity.Entities;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal sealed class SpellbookSectionSaver : ISnapshotSectionSaver
    {
        public bool RequiresRebuild { get; private set; }

        public void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            Reset();
            // TODO: discover actual spellbook buffers; until then we treat spellbook state as needing manual review.
            RequiresRebuild = true;
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Without concrete spellbook buffers, we always ask vanilla to rebuild when the player opens the menu.
            RequiresRebuild = true;
        }

        public void Reset()
        {
            RequiresRebuild = false;
        }
    }
}
