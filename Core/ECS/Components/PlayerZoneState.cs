using Unity.Entities;

namespace VAutomationCore.Core.ECS.Components
{
    public struct PlayerZoneState : IComponentData
    {
        public int CurrentZoneHash;
    }
}