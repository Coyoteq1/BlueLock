using System.Collections.Generic;

namespace VAuto.Zone.Services
{
    public static class ProcessConfigService
    {
        public class ValidationResult
        {
            public bool Success { get; set; } = true;
            public List<string> Errors { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
            public Dictionary<string, object> ConfigData { get; set; } = new();
        }

        public static ValidationResult ValidateAllConfigs(string baseConfigPath)
        {
            return new ValidationResult { Success = true };
        }
    }
}
