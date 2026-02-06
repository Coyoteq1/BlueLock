using Unity.Entities;

namespace VAuto.Core.Components.Lifecycle
{
    public struct RepairRequest : IComponentData
    {
        public int Threshold;
        public int Priority;
        public float RequestedAt;
    }
}
