using System.Text.Json;
using System.Text.Json.Serialization;

namespace VAuto.Core.Json
{
    /// <summary>
    /// Shared JsonSerializerOptions for configs and snapshot-like payloads.
    /// Keep this in VAutomationCore so all plugin projects can reuse it.
    /// </summary>
    public static class VAutoJsonOptions
    {
        public static JsonSerializerOptions CreateDefault()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            options.Converters.Add(new PrefabGuidJsonConverter());

            return options;
        }
    }
}

