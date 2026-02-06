using Unity.Entities;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal sealed class InventorySectionSaver : ISnapshotSectionSaver
    {
        public void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Inventory capture stub; requires deeper ProjectM buffer knowledge.
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Inventory restore stub.
        }
    }
}
