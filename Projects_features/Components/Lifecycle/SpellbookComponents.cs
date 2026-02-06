using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Stunlock.Core;

namespace VAuto.Core.Components.Lifecycle
{
    /// <summary>
    /// Buffer element to store original spellbook abilities before temporary modification.
    /// </summary>
    public struct SpellbookSnapshotElement : IBufferElementData
    {
        public int SlotIndex;
        public PrefabGUID AbilityPrefab;
    }

    /// <summary>
    /// Request component to open spellbook on zone enter.
    /// </summary>
    public struct SpellbookOpenRequest : IComponentData
    {
        public Entity ZoneEntity;
    }

    /// <summary>
    /// Request component to restore original spellbook on zone exit.
    /// </summary>
    public struct SpellbookRestoreRequest : IComponentData
    {
        public Entity ZoneEntity;
    }
}
