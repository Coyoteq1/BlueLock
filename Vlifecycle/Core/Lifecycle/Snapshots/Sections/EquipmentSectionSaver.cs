using Unity.Entities;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal sealed class EquipmentSectionSaver : ISnapshotSectionSaver
    {
        public void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Equipment capture stub.
        }

        public void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em)
        {
            // Equipment restore stub.
        }
    }
}
