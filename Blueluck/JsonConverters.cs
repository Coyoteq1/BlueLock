using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;

namespace Blueluck
{
    /// <summary>
    /// JSON converters for Blueluck configuration.
    /// </summary>
    public class Float3Converter : JsonConverter<float3>
    {
        public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected StartArray token");

            float x = 0, y = 0, z = 0;
            int index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (index == 0)
                    x = (float)reader.GetDouble();
                else if (index == 1)
                    y = (float)reader.GetDouble();
                else if (index == 2)
                    z = (float)reader.GetDouble();

                index++;
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

    public class QuaternionConverter : JsonConverter<quaternion>
    {
        public override quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected StartArray token");

            float x = 0, y = 0, z = 0, w = 1;
            int index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (index == 0)
                    x = (float)reader.GetDouble();
                else if (index == 1)
                    y = (float)reader.GetDouble();
                else if (index == 2)
                    z = (float)reader.GetDouble();
                else if (index == 3)
                    w = (float)reader.GetDouble();

                index++;
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
    /// JSON options for Blueluck configuration.
    /// </summary>
    public static class BlueluckJsonOptions
    {
        private static JsonSerializerOptions _options;

        public static JsonSerializerOptions Options
        {
            get
            {
                if (_options == null)
                {
                    _options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        Converters =
                        {
                            new Float3Converter(),
                            new QuaternionConverter()
                        }
                    };
                }
                return _options;
            }
        }
    }
}
