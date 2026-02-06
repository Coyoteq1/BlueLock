using Unity.Entities;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal sealed class JewelSocketSectionSaver : ISnapshotSectionSaver
    {
        public void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Jewel socket capture stub.
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Jewel socket restore stub.
        }
    }
}
