using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stunlock.Core;

namespace VAuto.Core.Json
{
    /// <summary>
    /// Serializes PrefabGUID as an int (GuidHash), and deserializes from:
    /// - number: -123
    /// - string: "-123" or "Prefab_Name"
    /// - object: { "guidHash": -123 } / { "GuidHash": -123 }
    /// </summary>
    public sealed class PrefabGuidJsonConverter : JsonConverter<PrefabGUID>
    {
        public override PrefabGUID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    return new PrefabGUID(reader.GetInt32());
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (string.IsNullOrWhiteSpace(s))
                        return default;

                    if (int.TryParse(s, out var i))
                        return new PrefabGUID(i);

                    // Allow config to use prefab names.
                    if (PrefabGuidConverter.TryGetGuid(s, out var guid))
                        return guid;

                    return default;
                }

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    var root = doc.RootElement;

                    if (TryReadGuidHash(root, "guidHash", out var gh) || TryReadGuidHash(root, "GuidHash", out gh))
                        return new PrefabGUID(gh);

                    // As a last resort, accept { "name": "Prefab_Name" }
                    if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name) && PrefabGuidConverter.TryGetGuid(name, out var guid))
                            return guid;
                    }

                    return default;
                }
            }
            catch
            {
                // Intentionally swallow: configs should be resilient.
            }

            // Consume token if it's something unexpected.
            reader.Skip();
            return default;
        }

        public override void Write(Utf8JsonWriter writer, PrefabGUID value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.GuidHash);
        }

        private static bool TryReadGuidHash(JsonElement root, string property, out int guidHash)
        {
            guidHash = default;
            if (!root.TryGetProperty(property, out var el))
                return false;

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out guidHash))
                return true;

            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out guidHash))
                return true;

            return false;
        }
    }
}

