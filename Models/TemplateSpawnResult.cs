using System;
using System.Collections.Generic;
using Unity.Entities;

namespace VAuto.Zone.Models
{
    public class TemplateSpawnResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }
        public string ZoneId { get; set; }
        public string TemplateType { get; set; }
        public string TemplateName { get; set; }
        public List<Entity> Entities { get; set; } = new();
        public int EntityCount => Entities?.Count ?? 0;
        public DateTime SpawnedAt { get; set; } = DateTime.UtcNow;
    }
}
