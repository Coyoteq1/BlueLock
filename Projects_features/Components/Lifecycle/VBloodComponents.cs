using Unity.Entities;

namespace VAuto.Core.Components.Lifecycle
{
    public enum VBloodUnlockType : byte { All, ZoneSpecific, Progressive }

    public struct VBloodUnlockRequest : IComponentData
    {
        public VBloodUnlockType Type;
        public float RequestedAt;
    }
}
