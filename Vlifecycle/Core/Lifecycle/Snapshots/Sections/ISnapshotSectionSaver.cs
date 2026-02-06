using Unity.Entities;

namespace VAuto.Core.Lifecycle.Snapshots.Sections
{
    internal interface ISnapshotSectionSaver
    {
        void Capture(Entity character, CharacterSnapshot snapshot, EntityManager em);
        void Restore(Entity character, CharacterSnapshot snapshot, EntityManager em);
        void Reset();
    }
}
