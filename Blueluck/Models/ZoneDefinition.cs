using System;
using System.Text.Json.Serialization;
using System.Text.Json;
using Unity.Mathematics;

namespace Blueluck.Models
{
    /// <summary>
    /// Base zone definition containing common zone properties.
    /// </summary>
    public class ZoneDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("hash")]
        public int Hash { get; set; }

        [JsonPropertyName("center")]
        public float[] Center { get; set; } = new float[3];

        [JsonPropertyName("entryRadius")]
        public float EntryRadius { get; set; } = 50f;

        [JsonPropertyName("exitRadius")]
        public float ExitRadius { get; set; } = 60f;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 0;

        [JsonPropertyName("flowOnEnter")]
        public string FlowOnEnter { get; set; } = string.Empty;

        [JsonPropertyName("flowOnExit")]
        public string FlowOnExit { get; set; } = string.Empty;

        [JsonPropertyName("onEnter")]
        public ZoneEventConfig? OnEnter { get; set; }

        [JsonPropertyName("onExit")]
        public ZoneEventConfig? OnExit { get; set; }

        [JsonPropertyName("kitOnEnter")]
        public string? KitOnEnter { get; set; }

        [JsonPropertyName("kitOnExit")]
        public string? KitOnExit { get; set; }

        [JsonPropertyName("abilitySet")]
        public string? AbilitySet { get; set; }

        [JsonPropertyName("borderVisual")]
        public BorderVisualConfig? BorderVisual { get; set; }

        /// <summary>
        /// Gets the center as float3 for ECS operations.
        /// </summary>
        public float3 GetCenterFloat3()
        {
            if (Center == null || Center.Length < 3)
                return float3.zero;
            return new float3(Center[0], Center[1], Center[2]);
        }

        /// <summary>
        /// Gets squared entry radius for distance comparisons.
        /// </summary>
        public float EntryRadiusSq => EntryRadius * EntryRadius;

        /// <summary>
        /// Gets squared exit radius for distance comparisons.
        /// </summary>
        public float ExitRadiusSq => ExitRadius * ExitRadius;

        /// <summary>
        /// Gets random position within zone boundaries.
        /// </summary>
        public float3 GetRandomPosition()
        {
            if (Center == null || Center.Length < 3)
                return float3.zero;
            
            var center = new float3(Center[0], Center[1], Center[2]);
            var radius = EntryRadius * 0.9f; // Stay within 90% of zone radius
            
            // Generate random position within circle
            var angle = UnityEngine.Random.Range(0f, UnityEngine.Mathf.PI * 2f);
            var distance = UnityEngine.Random.Range(0f, radius);
            
            return center + new float3(
                math.cos(angle) * distance,
                0f,
                math.sin(angle) * distance
            );
        }

        /// <summary>
        /// Gets random position inside zone bounds using exact float3 coordinates.
        /// </summary>
        public float3 GetRandomPositionInside()
        {
            if (Center == null || Center.Length < 3)
                return float3.zero;
            
            var center = new float3(Center[0], Center[1], Center[2]);
            var minBounds = center - new float3(EntryRadius, 0f, EntryRadius);
            var maxBounds = center + new float3(EntryRadius, 0f, EntryRadius);
            
            var random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, int.MaxValue));
            
            return new float3(
                random.NextFloat(minBounds.x, maxBounds.x),
                center.y,
                random.NextFloat(minBounds.z, maxBounds.z)
            );
        }

        /// <summary>
        /// Gets minimum bounds of the zone.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public float3 MinBounds => GetCenterFloat3() - new float3(EntryRadius, 0f, EntryRadius);

        /// <summary>
        /// Gets maximum bounds of the zone.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public float3 MaxBounds => GetCenterFloat3() + new float3(EntryRadius, 0f, EntryRadius);
    }

    /// <summary>
    /// Optional visual effect applied near the zone border. Implemented as a server-side buff on the player.
    /// </summary>
    public class BorderVisualConfig
    {
        /// <summary>
        /// Logical effect name. If BuffPrefabs is not provided, this is mapped to known prefab name tokens.
        /// </summary>
        [JsonPropertyName("effect")]
        public string Effect { get; set; } = string.Empty;

        /// <summary>
        /// Explicit buff prefab tokens by intensity tier (1..IntensityMax). Preferred because it avoids
        /// assumptions about naming conventions. If provided, length should be >= IntensityMax.
        /// </summary>
        [JsonPropertyName("buffPrefabs")]
        public string[]? BuffPrefabs { get; set; }

        /// <summary>
        /// Maximum intensity tiers. Default 3.
        /// </summary>
        [JsonPropertyName("intensityMax")]
        public int IntensityMax { get; set; } = 3;

        /// <summary>
        /// Distance from the zone radius where the effect becomes active (world units).
        /// </summary>
        [JsonPropertyName("range")]
        public float Range { get; set; } = 10f;

        /// <summary>
        /// If true, remove the buff when exiting the border range or leaving the zone.
        /// </summary>
        [JsonPropertyName("removeOnExit")]
        public bool RemoveOnExit { get; set; } = true;

        /// <summary>
        /// Optional override priority for overlapping zones. When omitted, ZoneDefinition.Priority is used.
        /// </summary>
        [JsonPropertyName("priority")]
        public int? Priority { get; set; }
    }

    /// <summary>
    /// Configuration for zone enter/exit events.
    /// </summary>
    public class ZoneEventConfig
    {
        [JsonPropertyName("broadcast")]
        public string Broadcast { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detection configuration for zone system.
    /// </summary>
    public class ZoneDetectionConfig
    {
        [JsonPropertyName("checkIntervalMs")]
        public int CheckIntervalMs { get; set; } = 500;

        [JsonPropertyName("positionThreshold")]
        public float PositionThreshold { get; set; } = 1.0f;
    }

    /// <summary>
    /// Root configuration containing all zones and settings.
    /// </summary>
    public class ZonesConfig
    {
        [JsonPropertyName("zones")]
        public ZoneDefinition[] Zones { get; set; } = Array.Empty<ZoneDefinition>();

        [JsonPropertyName("detection")]
        public ZoneDetectionConfig? Detection { get; set; }

        [JsonPropertyName("flows")]
        public FlowConfig? Flows { get; set; }

        [JsonPropertyName("abilities")]
        public AbilityConfig? Abilities { get; set; }
    }

    /// <summary>
    /// Polymorphic deserialization for zone definitions based on the "type" discriminator.
    /// System.Text.Json requires an explicit converter for this pattern.
    /// </summary>
    public sealed class ZoneDefinitionJsonConverter : JsonConverter<ZoneDefinition>
    {
        public override ZoneDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            var raw = root.GetRawText();

            if (string.Equals(type, "ArenaZone", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<ArenaZoneConfig>(raw, options) ?? new ArenaZoneConfig();
            }

            if (string.Equals(type, "BossZone", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Deserialize<BossZoneConfig>(raw, options) ?? new BossZoneConfig();
            }

            // Fall back to base definition (avoid recursion by using options without this converter).
            var baseOptions = CreateBaseOptions(options);
            return JsonSerializer.Deserialize<ZoneDefinition>(raw, baseOptions) ?? new ZoneDefinition();
        }

        public override void Write(Utf8JsonWriter writer, ZoneDefinition value, JsonSerializerOptions options)
        {
            if (value is ArenaZoneConfig arena)
            {
                JsonSerializer.Serialize(writer, arena, options);
                return;
            }

            if (value is BossZoneConfig boss)
            {
                JsonSerializer.Serialize(writer, boss, options);
                return;
            }

            JsonSerializer.Serialize(writer, value, CreateBaseOptions(options));
        }

        private static JsonSerializerOptions CreateBaseOptions(JsonSerializerOptions options)
        {
            // Clone without the zone converter to avoid recursion when writing/reading base types.
            var cloned = new JsonSerializerOptions(options);
            for (var i = cloned.Converters.Count - 1; i >= 0; i--)
            {
                if (cloned.Converters[i] is ZoneDefinitionJsonConverter)
                {
                    cloned.Converters.RemoveAt(i);
                }
            }
            return cloned;
        }
    }


    /// <summary>
    /// Flow configuration for zone transitions.
    /// </summary>
    public class FlowConfig
    {
        [JsonPropertyName("flows")]
        public Dictionary<string, FlowAction[]> Flows { get; set; } = new();
    }

    /// <summary>
    /// Single flow action configuration.
    /// </summary>
    public class FlowAction
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object? Value { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("prefab")]
        public string? Prefab { get; set; }

        [JsonPropertyName("slots")]
        public string[]? Slots { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("qty")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("position")]
        public float[]? Position { get; set; }

        [JsonPropertyName("random")]
        public bool Random { get; set; } = false;

        [JsonPropertyName("randomInZone")]
        public bool RandomInZone { get; set; } = false;

        [JsonPropertyName("vfxPrefab")]
        public string? VfxPrefab { get; set; }

        [JsonPropertyName("radius")]
        public float Radius { get; set; } = 50f;

        [JsonPropertyName("segments")]
        public int Segments { get; set; } = 24;

        [JsonPropertyName("buffPrefab")]
        public string? BuffPrefab { get; set; }

        [JsonPropertyName("duration")]
        public float Duration { get; set; } = -1f;
    }

    /// <summary>
    /// Ability configuration for zone-specific hotbar slots.
    /// </summary>
    public class AbilityConfig
    {
        [JsonPropertyName("abilities")]
        public Dictionary<string, Dictionary<string, int>> Abilities { get; set; } = new();

        [JsonPropertyName("aliases")]
        public Dictionary<string, int> Aliases { get; set; } = new();
    }
}
