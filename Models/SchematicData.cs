using System.Collections.Generic;
using Unity.Mathematics;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Root DTO for a Kindred-style schematic file.
    /// </summary>
    public class SchematicData
    {
        public List<SchematicEntity> Entities { get; set; } = new();
    }

    public class SchematicEntity
    {
        public int PrefabId { get; set; }
        public string PrefabName { get; set; }
        public float3 Position { get; set; }
        public float3 Rotation { get; set; }
        public int? TeamHeart { get; set; }
        public List<int> Dependencies { get; set; } = new();
    }
}
