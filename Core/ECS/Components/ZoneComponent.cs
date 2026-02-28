using Unity.Entities;
using Unity.Mathematics;

namespace VAutomationCore.Core.ECS.Components
{
    public struct ZoneComponent : IComponentData
    {
        public int ZoneHash;
        public float3 Center;
        public float EntryRadius;
        public float ExitRadius;
        public float EntryRadiusSq;
        public float ExitRadiusSq;
        public int FlowIdHash;
    }
}