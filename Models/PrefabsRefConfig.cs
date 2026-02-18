using System;
using System.Collections.Generic;

namespace VAuto.Zone.Models
{
    public sealed class PrefabsRefConfig
    {
        public int SchemaVersion { get; set; } = 2;
        public int? MaxEntries { get; set; }
        public string? Source { get; set; }
        public int? Count { get; set; }
        public List<PrefabChoice> Choices { get; set; } = new();

        /// <summary>
        /// Alias -> prefab token/name. Populated from Prefabsref.json (aliases) and legacy ability_prefabs.json.
        /// </summary>
        public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class PrefabChoice
    {
        public string? Name { get; set; }
        public string? Prefab { get; set; }
        public int PrefabGuid { get; set; }
    }
}
