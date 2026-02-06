using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;

namespace VAuto.Core.Networking
{
    /// <summary>
    /// State serializer for network transmission
    /// </summary>
    public class StateSerializer
    {
        private static StateSerializer? _instance;
        public static StateSerializer Instance => _instance ??= new StateSerializer();

        private readonly JsonSerializerOptions _options;

        private StateSerializer()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters =
                {
                    new Float3JsonConverter(),
                    new PrefabGuidJsonConverter(),
                    new DictionaryJsonConverter()
                }
            };
        }

        public string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        public T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        public byte[] SerializeToUtf8<T>(T obj)
        {
            return JsonSerializer.SerializeToUtf8Bytes(obj, _options);
        }

        public T? DeserializeFromUtf8<T>(byte[] utf8Bytes)
        {
            return JsonSerializer.Deserialize<T>(utf8Bytes, _options);
        }

        public T? DeserializeFromUtf8<T>(ReadOnlySpan<byte> utf8Bytes)
        {
            return JsonSerializer.Deserialize<T>(utf8Bytes, _options);
        }
    }

    /// <summary>
    /// JSON converter for float3
    /// </summary>
    public class Float3JsonConverter : JsonConverter<float3>
    {
        public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return float3.zero;

            var parts = value.Split(',');
            if (parts.Length == 3)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    return new float3(x, y, z);
                }
            }

            return float3.zero;
        }

        public override void Write(Utf8JsonWriter writer, float3 value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"{value.x.ToString(CultureInfo.InvariantCulture)},{value.y.ToString(CultureInfo.InvariantCulture)},{value.z.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>
    /// JSON converter for PrefabGUID
    /// </summary>
    public class PrefabGuidJsonConverter : JsonConverter<PrefabGUID>
    {
        public override PrefabGUID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TryGetInt64(out long value))
                return new PrefabGUID((int)value);

            var str = reader.GetString();
            if (int.TryParse(str, out int guidValue))
                return new PrefabGUID((int)guidValue);

            return default;
        }

        public override void Write(Utf8JsonWriter writer, PrefabGUID value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.Value);
        }
    }

    /// <summary>
    /// JSON converter for dictionaries
    /// </summary>
    public class DictionaryJsonConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dict = new Dictionary<string, object>();

            if (reader.TokenType != JsonTokenType.StartObject)
                return dict;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var key = reader.GetString()!;
                    reader.Read();

                    dict[key] = ReadValue(ref reader, options);
                }
            }

            return dict;
        }

        private object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString()!,
                JsonTokenType.Number => reader.TryGetInt32(out int i) ? i : reader.GetDouble(),
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.StartObject => Read(ref reader, typeof(Dictionary<string, object>), options),
                JsonTokenType.StartArray => JsonSerializer.Deserialize<object[]>(ref reader, options) ?? new object[0],
                _ => null!
            };
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                WriteValue(writer, kvp.Value, options);
            }
            writer.WriteEndObject();
        }

        private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case double d:
                    writer.WriteNumberValue(d);
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case Dictionary<string, object> dict:
                    Write(writer, dict, options);
                    break;
                case object[] arr:
                    writer.WriteStartArray();
                    foreach (var item in arr)
                        WriteValue(writer, item, options);
                    writer.WriteEndArray();
                    break;
                default:
                    writer.WriteStringValue(value.ToString());
                    break;
            }
        }
    }

    /// <summary>
    /// Extension methods for serialization
    /// </summary>
    public static class SerializationExtensions
    {
        public static string ToJson(this object obj)
        {
            return StateSerializer.Instance.Serialize(obj);
        }

        public static T? FromJson<T>(this string json)
        {
            return StateSerializer.Instance.Deserialize<T>(json);
        }

        public static byte[] ToJsonUtf8(this object obj)
        {
            return StateSerializer.Instance.SerializeToUtf8(obj);
        }

        public static WireMessage ToWireMessage(this object obj, string type)
        {
            return WireMessage.Create(type, obj);
        }

        public static Dictionary<string, object> ToDictionary(this object obj)
        {
            return StateSerializer.Instance.Deserialize<Dictionary<string, object>>(obj.ToJson()) ?? new();
        }

        public static string GetString(this Dictionary<string, object> dict, string key, string defaultValue = "")
        {
            return dict.TryGetValue(key, out var val) ? val.ToString() ?? defaultValue : defaultValue;
        }

        public static T? GetObject<T>(this Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var val)) return default;
            var json = val.ToJson();
            return StateSerializer.Instance.Deserialize<T>(json);
        }
    }
}
