using System.Collections.Generic;
using System.Text.Json.Serialization;
using Unity.Mathematics;

namespace VAuto.Zone.Models
{
    public sealed class TemplateSnapshot
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("entities")]
        public List<TemplateEntityEntry> Entities { get; set; } = new List<TemplateEntityEntry>();
    }

    public sealed class TemplateEntityEntry
    {
        [JsonPropertyName("prefabName")]
        public string PrefabName { get; set; } = string.Empty;

        [JsonPropertyName("prefabGuid")]
        public int PrefabGuid { get; set; }

        [JsonPropertyName("offset")]
        public float3 Offset { get; set; }

        [JsonPropertyName("rotationDegrees")]
        public float RotationDegrees { get; set; }
    }
}
