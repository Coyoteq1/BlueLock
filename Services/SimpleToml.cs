using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VAuto.Zone.Services
{
    internal static class SimpleToml
    {
        // Minimal TOML subset parser for this mod's configs:
        // - key = value
        // - value: string, int, float, bool, array
        // - table: [table] and nested [a.b]
        // - inline comments (# ...) are allowed and stripped
        public static Dictionary<string, object> Parse(string toml)
        {
            var root = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> current = root;

            var lines = toml.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var raw = StripComment(lines[lineIndex]).Trim();
                if (raw.Length == 0) continue;

                if (raw.StartsWith("[") && raw.EndsWith("]"))
                {
                    var header = raw.Substring(1, raw.Length - 2).Trim();
                    current = GetOrCreateTable(root, header);
                    continue;
                }

                var eq = raw.IndexOf('=');
                if (eq <= 0) throw new FormatException($"Invalid TOML line {lineIndex + 1}: '{lines[lineIndex]}'");

                var key = raw.Substring(0, eq).Trim();
                var valueText = raw.Substring(eq + 1).Trim();
                current[key] = ParseValue(valueText);
            }

            return root;
        }

        public static string SerializeTerritory(
            string zoneId,
            float[] center,
            float radius,
            int regionType,
            float blockSize,
            string glowPrefab,
            float glowSpacingMeters,
            float glowCornerRadius,
            bool spawnGlowInCorners,
            bool enableGlowBorder)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# VAutoZone territory config (TOML)");
            sb.AppendLine();
            sb.AppendLine("[metadata]");
            sb.AppendLine("name = \"VAutoZone Territory\"");
            sb.AppendLine("version = \"2.0.0\"");
            sb.AppendLine("updatedAt = \"2026-02-07\"");
            sb.AppendLine();
            sb.AppendLine("[core]");
            sb.AppendLine("id = " + Quote(zoneId));
            sb.AppendLine("center = [" + Float(center[0]) + ", " + Float(center[1]) + ", " + Float(center[2]) + "]");
            sb.AppendLine("radius = " + Float(radius));
            sb.AppendLine("regionType = " + regionType.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("blockSize = " + Float(blockSize));
            if (!string.IsNullOrWhiteSpace(glowPrefab))
                sb.AppendLine("glowPrefab = " + Quote(glowPrefab));
            if (glowSpacingMeters > 0)
                sb.AppendLine("glowSpacing = " + Float(glowSpacingMeters));
            if (glowCornerRadius > 0)
                sb.AppendLine("glowCornerRadius = " + Float(glowCornerRadius));
            sb.AppendLine();
            sb.AppendLine("[dependencies]");
            sb.AppendLine("requiresVcf = true");
            sb.AppendLine();
            sb.AppendLine("[optionalFeatures]");
            sb.AppendLine("enableGlowBorder = " + (enableGlowBorder ? "true" : "false"));
            sb.AppendLine("spawnGlowInCorners = " + (spawnGlowInCorners ? "true" : "false"));
            return sb.ToString();
        }

        public static string SerializeGlowPrefabs(string defaultPrefab, Dictionary<string, long> prefabs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# VAutoArena glow prefab config (TOML)");
            sb.AppendLine();
            sb.AppendLine("[metadata]");
            sb.AppendLine("name = \"VAutoArena Glow Prefabs\"");
            sb.AppendLine("version = \"1.0.0\"");
            sb.AppendLine("updatedAt = \"2026-02-06\"");
            sb.AppendLine();
            sb.AppendLine("[core]");
            sb.AppendLine("defaultPrefab = " + Quote(defaultPrefab));
            sb.AppendLine();
            sb.AppendLine("[prefabs]");
            foreach (var kvp in prefabs)
            {
                sb.AppendLine($"{EscapeKey(kvp.Key)} = {kvp.Value.ToString(CultureInfo.InvariantCulture)}");
            }
            return sb.ToString();
        }

        private static Dictionary<string, object> GetOrCreateTable(Dictionary<string, object> root, string dotted)
        {
            var parts = dotted.Split('.');
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.Length == 0) throw new FormatException($"Invalid table header: [{dotted}]");

                if (!current.TryGetValue(p, out var existing) || existing is not Dictionary<string, object> dict)
                {
                    dict = new Dictionary<string, object>(StringComparer.Ordinal);
                    current[p] = dict;
                }
                current = dict;
            }
            return current;
        }

        private static object ParseValue(string valueText)
        {
            if (valueText.StartsWith("\"") && valueText.EndsWith("\""))
            {
                return valueText.Substring(1, valueText.Length - 2);
            }

            if (valueText.StartsWith("[") && valueText.EndsWith("]"))
            {
                var inner = valueText.Substring(1, valueText.Length - 2).Trim();
                if (inner.Length == 0) return Array.Empty<object>();
                var parts = SplitArray(inner);
                var arr = new object[parts.Count];
                for (int i = 0; i < parts.Count; i++)
                {
                    arr[i] = ParseValue(parts[i].Trim());
                }
                return arr;
            }

            if (valueText.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (valueText.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

            if (int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iVal)) return iVal;
            if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var fVal)) return fVal;

            throw new FormatException($"Unsupported TOML value: '{valueText}'");
        }

        private static List<string> SplitArray(string inner)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool inString = false;
            for (int i = 0; i < inner.Length; i++)
            {
                var c = inner[i];
                if (c == '"' && (i == 0 || inner[i - 1] != '\\')) inString = !inString;
                if (!inString && c == ',')
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                sb.Append(c);
            }
            parts.Add(sb.ToString());
            return parts;
        }

        private static string StripComment(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"' && (i == 0 || line[i - 1] != '\\')) inString = !inString;
                if (!inString && c == '#') return line.Substring(0, i);
            }
            return line;
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Float(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeKey(string key)
        {
            // For non-identifier keys (like prefab names), TOML requires quoted keys.
            for (int i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                {
                    return Quote(key);
                }
            }
            return key;
        }
    }
}
