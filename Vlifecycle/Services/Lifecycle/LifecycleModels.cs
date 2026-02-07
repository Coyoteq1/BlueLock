using System.Collections.Generic;
using Unity.Entities;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// Represents a lifecycle stage with its actions
    /// </summary>
    public class LifecycleStage
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<LifecycleAction> Actions { get; set; } = new List<LifecycleAction>();
    }

    /// <summary>
    /// Represents a single lifecycle action
    /// </summary>
    public class LifecycleAction
    {
        public string Type { get; set; }
        public string StoreKey { get; set; }
        public string Prefix { get; set; }
        public string BloodType { get; set; }
        public int Quality { get; set; }
        public string ZoneKey { get; set; }
        public string Message { get; set; }
        public string ConfigId { get; set; }
        public string CommandId { get; set; }
    }

    /// <summary>
    /// Context data passed to lifecycle actions
    /// </summary>
    public class LifecycleContext
    {
        public Entity UserEntity { get; set; }
        public Entity CharacterEntity { get; set; }
        public Entity ItemEntity { get; set; }
        public string ZoneId { get; set; }
        public Dictionary<string, object> StoredData { get; set; } = new Dictionary<string, object>();
        public Unity.Mathematics.float3 Position { get; set; }
    }
}
