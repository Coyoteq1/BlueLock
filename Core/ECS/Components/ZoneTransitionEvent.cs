using Unity.Entities;

namespace VAutomationCore.Core.ECS.Components
{
    public struct ZoneTransitionEvent : IComponentData
    {
        public Entity Player;
        public int OldZoneHash;
        public int NewZoneHash;
    }
}