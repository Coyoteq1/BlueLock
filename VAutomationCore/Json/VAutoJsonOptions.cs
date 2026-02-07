using System.Text.Json;
using System.Text.Json.Serialization;

namespace VAutomation.Core.Json
{
    /// <summary>
    /// Standard JSON serialization options for VAuto framework.
    /// </summary>
    public static class VAutoJsonOptions
    {
        public static JsonSerializerOptions Default { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            IgnoreReadOnlyProperties = false,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public static JsonSerializerOptions WithConverters(params JsonConverter[] converters)
        {
            var options = new JsonSerializerOptions(Default);
            foreach (var converter in converters)
            {
                options.Converters.Add(converter);
            }
            return options;
        }
    }
}
