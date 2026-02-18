using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Mathematics;

namespace VAuto.Zone
{
    /// <summary>
    /// JSON converter for Unity.Mathematics.quaternion.
    /// Serializes as Euler angles array [x, y, z] in degrees.
    /// </summary>
    public class QuaternionConverter : JsonConverter<quaternion>
    {
        public override quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Quaternion should be an array");
            }

            reader.Read();
            float x = reader.GetSingle();
            reader.Read();
            float y = reader.GetSingle();
            reader.Read();
            float z = reader.GetSingle();
            reader.Read();

            return quaternion.Euler(math.radians(x), math.radians(y), math.radians(z));
        }

        public override void Write(Utf8JsonWriter writer, quaternion value, JsonSerializerOptions options)
        {
            var euler = ToEulerAngles(value);
            
            writer.WriteStartArray();
            writer.WriteNumberValue(Math.Abs(euler.x) <= float.Epsilon ? 0f : euler.x);
            writer.WriteNumberValue(Math.Abs(euler.y) <= float.Epsilon ? 0f : euler.y);
            writer.WriteNumberValue(Math.Abs(euler.z) <= float.Epsilon ? 0f : euler.z);
            writer.WriteEndArray();
        }

        private static float3 ToEulerAngles(quaternion q)
        {
            // Extract Euler angles from quaternion
            var qx = q.value.x;
            var qy = q.value.y;
            var qz = q.value.z;
            var qw = q.value.w;

            var roll = Math.Atan2(2 * (qw * qz + qx * qy), 1 - 2 * (qy * qy + qz * qz));
            var pitch = Math.Asin(2 * (qw * qy - qz * qx));
            var yaw = Math.Atan2(2 * (qw * qx + qy * qz), 1 - 2 * (qx * qx + qy * qy));

            return new float3((float)math.degrees(pitch), (float)math.degrees(yaw), (float)math.degrees(roll));
        }
    }

    /// <summary>
    /// JSON converter for Unity.Mathematics.float3.
    /// Serializes as array [x, y, z].
    /// </summary>
    public class Float3Converter : JsonConverter<float3>
    {
        public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("float3 should be an array");
            }

            reader.Read();
            float x = reader.GetSingle();
            reader.Read();
            float y = reader.GetSingle();
            reader.Read();
            float z = reader.GetSingle();
            reader.Read();

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
    /// JSON converter for Unity.Mathematics.float2.
    /// Serializes as array [x, y].
    /// </summary>
    public class Float2Converter : JsonConverter<float2>
    {
        public override float2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("float2 should be an array");
            }

            reader.Read();
            float x = reader.GetSingle();
            reader.Read();
            float y = reader.GetSingle();
            reader.Read();

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
    /// JSON converter for Unity.Mathematics.int2.
    /// Serializes as array [x, y].
    /// </summary>
    public class Int2Converter : JsonConverter<int2>
    {
        public override int2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("int2 should be an array");
            }

            reader.Read();
            var x = reader.GetInt32();
            reader.Read();
            var y = reader.GetInt32();
            reader.Read();

            return new int2(x, y);
        }

        public override void Write(Utf8JsonWriter writer, int2 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.x);
            writer.WriteNumberValue(value.y);
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// JSON converter options for VAuto Zone serialization.
    /// </summary>
    public static class ZoneJsonOptions
    {
        public static JsonSerializerOptions Default { get; } = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = false
        };

        public static JsonSerializerOptions WithUnityMathConverters { get; } = new JsonSerializerOptions(Default)
        {
            Converters = 
            {
                new QuaternionConverter(),
                new Float3Converter(),
                new Float2Converter(),
                new Int2Converter()
            }
        };
    }
}
