using System;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;

namespace VAuto.Extensions
{
    /// <summary>
    /// JSON converters for various types
    /// </summary>
    public class JsonConverters
    {
        /// <summary>
        /// Converts float3 to string representation
        /// </summary>
        public static string Float3ToString(float3 value)
        {
            return $"{value.x.ToString(CultureInfo.InvariantCulture)},{value.y.ToString(CultureInfo.InvariantCulture)},{value.z.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Parses string to float3
        /// </summary>
        public static float3 ParseFloat3(string value)
        {
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

        /// <summary>
        /// Converts Vector2 to string representation
        /// </summary>
        public static string Vector2ToString(Vector2 value)
        {
            return $"{value.X.ToString(CultureInfo.InvariantCulture)},{value.Y.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Parses string to Vector2
        /// </summary>
        public static Vector2 ParseVector2(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Vector2.Zero;

            var parts = value.Split(',');
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    return new Vector2(x, y);
                }
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// Converts Vector3 to string representation
        /// </summary>
        public static string Vector3ToString(Vector3 value)
        {
            return $"{value.X.ToString(CultureInfo.InvariantCulture)},{value.Y.ToString(CultureInfo.InvariantCulture)},{value.Z.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Parses string to Vector3
        /// </summary>
        public static Vector3 ParseVector3(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Vector3.Zero;

            var parts = value.Split(',');
            if (parts.Length == 3)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    return new Vector3(x, y, z);
                }
            }

            return Vector3.Zero;
        }
    }

    /// <summary>
    /// JSON converter for float3
    /// </summary>
    public class Float3Converter : JsonConverter<float3>
    {
        public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return JsonConverters.ParseFloat3(value);
        }

        public override void Write(Utf8JsonWriter writer, float3 value, JsonSerializerOptions options)
        {
            var stringValue = JsonConverters.Float3ToString(value);
            writer.WriteStringValue(stringValue);
        }
    }

    /// <summary>
    /// JSON converter for float2
    /// </summary>
    public class Float2Converter : JsonConverter<float2>
    {
        public override float2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return float2.zero;

            var parts = value.Split(',');
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    return new float2(x, y);
                }
            }

            return float2.zero;
        }

        public override void Write(Utf8JsonWriter writer, float2 value, JsonSerializerOptions options)
        {
            var stringValue = $"{value.x.ToString(CultureInfo.InvariantCulture)},{value.y.ToString(CultureInfo.InvariantCulture)}";
            writer.WriteStringValue(stringValue);
        }
    }

    /// <summary>
    /// JSON converter for Vector3
    /// </summary>
    public class JSON3Converter : JsonConverter<Vector3>
    {
        private readonly int _precision;
        private readonly bool _useCompressed;

        public JSON3Converter(int precision = 2, bool useCompressed = false)
        {
            _precision = precision;
            _useCompressed = useCompressed;
        }

        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return JsonConverters.ParseVector3(value);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            var format = _useCompressed ? "F0" : $"F{_precision}";
            var stringValue = $"{value.X.ToString(format, CultureInfo.InvariantCulture)},{value.Y.ToString(format, CultureInfo.InvariantCulture)},{value.Z.ToString(format, CultureInfo.InvariantCulture)}";
            writer.WriteStringValue(stringValue);
        }
    }

    /// <summary>
    /// JSON converter for Vector2
    /// </summary>
    public class JSON2Converter : JsonConverter<Vector2>
    {
        private readonly int _precision;
        private readonly bool _useCompressed;

        public JSON2Converter(int precision = 2, bool useCompressed = false)
        {
            _precision = precision;
            _useCompressed = useCompressed;
        }

        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return JsonConverters.ParseVector2(value);
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            var format = _useCompressed ? "F0" : $"F{_precision}";
            var stringValue = $"{value.X.ToString(format, CultureInfo.InvariantCulture)},{value.Y.ToString(format, CultureInfo.InvariantCulture)}";
            writer.WriteStringValue(stringValue);
        }
    }

    /// <summary>
    /// JSON converter for AASD (placeholder)
    /// </summary>
    public class AASDJsonConverter : JsonConverter<string>
    {
        private readonly bool _includeType;
        private readonly int _maxLength;

        public AASDJsonConverter(bool includeType = true, int maxLength = 2000)
        {
            _includeType = includeType;
            _maxLength = maxLength;
        }

        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value != null && value.Length > _maxLength)
                value = value.Substring(0, _maxLength);
            
            writer.WriteStringValue(value);
        }
    }
}
