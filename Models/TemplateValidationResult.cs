using System.Collections.Generic;

namespace VAuto.Zone.Models
{
    public class TemplateValidationResult
    {
        public bool IsValid { get; set; } = true;
        public int TotalEntities { get; set; }
        public List<string> MissingPrefabs { get; set; } = new();
    }
}
