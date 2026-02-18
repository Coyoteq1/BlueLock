using System.Collections.Generic;
using Unity.Mathematics;

namespace VAuto.Zone.Models
{
    /// <summary>
    /// Root DTO for a zone structure file (formerly schematic).
    /// </summary>
    public class ZoneStructureData
    {
        public List<ZoneStructureEntity> Entities { get; set; } = new();
    }

    public class ZoneStructureEntity
    {
        public int PrefabId { get; set; }
        public string PrefabName { get; set; }
        public float3 Position { get; set; }
        public float3 Rotation { get; set; }
        public int? TeamHeart { get; set; }
        public List<int> Dependencies { get; set; } = new();
    }
}
