using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Unity.Mathematics;

namespace VAuto.Zone.Services
{
    public enum ArenaZoneShape
    {
        Circle,
        Square
    }

    public sealed class ArenaZoneDef
    {
        public string Name { get; set; } = "";
        public float3 Center { get; set; }
        public float Radius { get; set; }
        public float2 Size { get; set; }
        public ArenaZoneShape Shape { get; set; } = ArenaZoneShape.Circle;
        public bool LifecycleEnabled { get; set; } = true;
        public bool IsArenaZone { get; set; }
        public string HolderName { get; set; } = string.Empty;
    }

    internal static class ArenaZoneConfigLoader
    {
        public static bool TryLoadZones(string configPath, out List<ArenaZoneDef> zones, out string error)
        {
            zones = new List<ArenaZoneDef>();
            error = string.Empty;

            if (!File.Exists(configPath))
            {
                error = $"Config not found: {configPath}";
                return false;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("zones", out var zonesEl) || zonesEl.ValueKind != JsonValueKind.Array)
                {
                    error = "Invalid config: missing 'zones' array.";
                    return false;
                }

                foreach (var zoneEl in zonesEl.EnumerateArray())
                {
                    if (!TryParseZone(zoneEl, out var zone, out var zoneError))
                    {
                        error = zoneError;
                        return false;
                    }
                    zones.Add(zone);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to parse config: {ex.Message}";
                return false;
            }
        }

        private static bool TryParseZone(JsonElement zoneEl, out ArenaZoneDef zone, out string error)
        {
            zone = new ArenaZoneDef();
            error = string.Empty;

            if (zoneEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            {
                zone.Name = nameEl.GetString() ?? "";
            }

            if (!TryGetFloat3(zoneEl, "center", out var center))
            {
                error = "Zone missing valid 'center' [x,y,z].";
                return false;
            }
            zone.Center = center;

            if (zoneEl.TryGetProperty("isArenaZone", out var arenaEl) &&
                (arenaEl.ValueKind == JsonValueKind.True || arenaEl.ValueKind == JsonValueKind.False))
            {
                zone.IsArenaZone = arenaEl.GetBoolean();
            }

            if (zoneEl.TryGetProperty("holderName", out var holderEl) && holderEl.ValueKind == JsonValueKind.String)
            {
                zone.HolderName = holderEl.GetString() ?? string.Empty;
            }

            if (zoneEl.TryGetProperty("radius", out var radiusEl) && radiusEl.ValueKind == JsonValueKind.Number)
            {
                zone.Shape = ArenaZoneShape.Circle;
                zone.Radius = radiusEl.GetSingle();
                if (zone.Radius <= 0)
                {
                    error = "Zone radius must be > 0.";
                    return false;
                }
                return true;
            }

            if (zoneEl.TryGetProperty("size", out var sizeEl) && sizeEl.ValueKind == JsonValueKind.Array)
            {
                if (!TryGetFloat2(sizeEl, out var size))
                {
                    error = "Zone size must be [x,z].";
                    return false;
                }

                zone.Shape = ArenaZoneShape.Square;
                zone.Size = size;
                if (zone.Size.x <= 0 || zone.Size.y <= 0)
                {
                    error = "Zone size must be > 0.";
                    return false;
                }
                return true;
            }

            error = "Zone must include 'radius' (circle) or 'size' (square).";
            return false;
        }

        private static bool TryGetFloat3(JsonElement parent, string property, out float3 value)
        {
            value = float3.zero;
            if (!parent.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Array)
                return false;

            return TryGetFloat3(el, out value);
        }

        private static bool TryGetFloat3(JsonElement el, out float3 value)
        {
            value = float3.zero;
            var arr = new List<float>(3);
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number) return false;
                arr.Add(item.GetSingle());
            }
            if (arr.Count != 3) return false;

            value = new float3(arr[0], arr[1], arr[2]);
            return true;
        }

        private static bool TryGetFloat2(JsonElement el, out float2 value)
        {
            value = float2.zero;
            var arr = new List<float>(2);
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number) return false;
                arr.Add(item.GetSingle());
            }
            if (arr.Count != 2) return false;

            value = new float2(arr[0], arr[1]);
            return true;
        }
    }
}

