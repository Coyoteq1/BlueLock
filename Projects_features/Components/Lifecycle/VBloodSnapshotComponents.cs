using Unity.Entities;
using ProjectM;
using Unity.Collections;
using Stunlock.Core;

namespace VAuto.Core.Components.Lifecycle
{
    // Request to capture current defeated VBlood state
    public struct VBloodSnapshotRequest : IComponentData { }

    // Request to restore previously captured defeated VBlood state
    public struct VBloodRestoreRequest : IComponentData { }

    // Buffer element storing per-boss defeated info
    public struct VBloodDefeatSnapshotElement : IBufferElementData
    {
        public PrefabGUID BossGuid;
        public bool Defeated;
        public float Timestamp;
    }
}
