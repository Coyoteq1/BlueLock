using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VAuto.Core.Services
{
    internal static class SimpleToml
    {
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

        public static string SerializeTrapConfig(TrapSystemConfig cfg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# VAutoTraps killstreak/trap config (TOML)");
            sb.AppendLine();
            sb.AppendLine("[metadata]");
            sb.AppendLine("name = \"VAutoTraps Config\"");
            sb.AppendLine("version = \"1.0.0\"");
            sb.AppendLine("updatedAt = \"2026-02-06\"");
            sb.AppendLine();
            sb.AppendLine("[core]");
            sb.AppendLine($"killThreshold = {cfg.KillThreshold}");
            sb.AppendLine($"chestsPerSpawn = {cfg.ChestsPerSpawn}");
            sb.AppendLine($"containerGlowRadius = {Float(cfg.ContainerGlowRadius)}");
            sb.AppendLine($"containerPrefabId = {cfg.ContainerPrefabId}");
            sb.AppendLine($"waypointTrapThreshold = {cfg.WaypointTrapThreshold}");
            sb.AppendLine($"waypointTrapGlowRadius = {Float(cfg.WaypointTrapGlowRadius)}");
            sb.AppendLine($"notificationEnabled = {(cfg.NotificationEnabled ? "true" : "false")}");
            sb.AppendLine($"trapDamageAmount = {Float(cfg.TrapDamageAmount)}");
            sb.AppendLine($"trapDuration = {Float(cfg.TrapDuration)}");
            sb.AppendLine("containerGlowColor = [" + Float(cfg.ContainerGlowColor.x) + ", " + Float(cfg.ContainerGlowColor.y) + ", " + Float(cfg.ContainerGlowColor.z) + "]");
            sb.AppendLine("waypointTrapGlowColor = [" + Float(cfg.WaypointTrapGlowColor.x) + ", " + Float(cfg.WaypointTrapGlowColor.y) + ", " + Float(cfg.WaypointTrapGlowColor.z) + "]");
            sb.AppendLine();
            sb.AppendLine("[dependencies]");
            sb.AppendLine("requiresVcf = true");
            sb.AppendLine();
            sb.AppendLine("[optionalFeatures]");
            sb.AppendLine("enableWaypointTraps = true");
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

        private static string Float(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

