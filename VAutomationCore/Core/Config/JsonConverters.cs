using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;
using ProjectM;
using Stunlock.Core;

namespace VAutomationCore.Core.Config
{
    /// <summary>
    /// JSON converter for Unity.Mathematics.float3 type.
    /// Serializes as an array [x, y, z].
    /// </summary>
    public class Float3JsonConverter : JsonConverter<float3>
    {
        public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected StartArray token, got {reader.TokenType}");
            }

            float x = 0, y = 0, z = 0;
            int index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.Number)
                {
                    switch (index)
                    {
                        case 0: x = (float)reader.GetDouble(); break;
                        case 1: y = (float)reader.GetDouble(); break;
                        case 2: z = (float)reader.GetDouble(); break;
                    }
                    index++;
                }
            }

            return new float3(x, y, z);
        }

        public override void Write(Utf8JsonWriter writer, float3 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.x);
            writer.WriteNumberValue(value.y);
            writer.WriteNumberValue(value.z);
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// JSON converter for Unity.Mathematics.float2 type.
    /// Serializes as an array [x, y].
    /// </summary>
    public class Float2JsonConverter : JsonConverter<float2>
    {
        public override float2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected StartArray token, got {reader.TokenType}");
            }

            float x = 0, y = 0;
            int index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.Number)
                {
                    switch (index)
                    {
                        case 0: x = (float)reader.GetDouble(); break;
                        case 1: y = (float)reader.GetDouble(); break;
                    }
                    index++;
                }
            }

            return new float2(x, y);
        }

        public override void Write(Utf8JsonWriter writer, float2 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.x);
            writer.WriteNumberValue(value.y);
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// JSON converter for Unity.Mathematics.quaternion type.
    /// Serializes as an array [x, y, z, w].
    /// </summary>
    public class QuaternionJsonConverter : JsonConverter<quaternion>
    {
        public override quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected StartArray token, got {reader.TokenType}");
            }

            float x = 0, y = 0, z = 0, w = 1;
            int index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.Number)
                {
                    switch (index)
                    {
                        case 0: x = (float)reader.GetDouble(); break;
                        case 1: y = (float)reader.GetDouble(); break;
                        case 2: z = (float)reader.GetDouble(); break;
                        case 3: w = (float)reader.GetDouble(); break;
                    }
                    index++;
                }
            }

            return new quaternion(x, y, z, w);
        }

        public override void Write(Utf8JsonWriter writer, quaternion value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.value.x);
            writer.WriteNumberValue(value.value.y);
            writer.WriteNumberValue(value.value.z);
            writer.WriteNumberValue(value.value.w);
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// JSON converter for ProjectM.PrefabGUID type.
    /// Serializes as a GUID string or object with Id and Name properties.
    /// </summary>
    public class PrefabGuidJsonConverter : JsonConverter<PrefabGUID>
    {
        public override PrefabGUID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                // Simple GUID string format - parse as int hash
                var guidString = reader.GetString();
                if (int.TryParse(guidString, out var hash))
                {
                    return new PrefabGUID(hash);
                }
                // Try legacy Guid format for backwards compatibility
                // WARNING: GetHashCode() is NOT stable - this may produce wrong prefabs
                // Consider using known prefab GUID values directly instead
                if (Guid.TryParse(guidString, out var legacyGuid))
                {
                    // Keep this for backward compatibility
                    // In production, prefabs should be referenced by their actual GuidHash value
                    // Log at your own risk - JsonConverters may be used before ZoneCore is initialized
                    return new PrefabGUID(legacyGuid.GetHashCode());
                }
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Object format with Id (GuidHash)
                int hash = 0;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propertyName = reader.GetString();
                        reader.Read();

                        if (propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                            propertyName.Equals("GuidHash", StringComparison.OrdinalIgnoreCase))
                        {
                            hash = reader.GetInt32();
                        }
                    }
                }

                return new PrefabGUID(hash);
            }

            throw new JsonException($"Unable to parse PrefabGUID from token type {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, PrefabGUID value, JsonSerializerOptions options)
        {
            // Serialize as just the GuidHash for simplicity
            writer.WriteStartObject();
            writer.WriteNumber("GuidHash", value.GuidHash);
            writer.WriteEndObject();
        }
    }
}
