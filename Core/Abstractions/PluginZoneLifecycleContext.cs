using System.Collections.Generic;
using Unity.Entities;
using VAutomationCore.Core.Lifecycle;

namespace VAutomationCore.Abstractions
{
    public class PluginZoneLifecycleContext : IZoneLifecycleContext
    {
        public Entity Player { get; }
        public string ZoneId { get; }
        public EntityManager EntityManager { get; }
        public object? Config { get; set; }
        public Dictionary<string, object> Data { get; } = new();

        public PluginZoneLifecycleContext(Entity player, string zoneId, EntityManager entityManager)
        {
            Player = player;
            ZoneId = zoneId ?? string.Empty;
            EntityManager = entityManager;
        }
    }
}
