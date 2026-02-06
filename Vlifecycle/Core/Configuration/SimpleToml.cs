using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VAuto.Core.Configuration
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

                // Array of tables: [[a.b]]
                if (raw.StartsWith("[[") && raw.EndsWith("]]"))
                {
                    var header = raw.Substring(2, raw.Length - 4).Trim();
                    current = AppendArrayTable(root, header);
                    continue;
                }

                // Table: [a.b]
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

        public static string SerializePvpItem(Dictionary<string, object> model)
        {
            // Minimal serializer used for migration. The "pvp_item.toml" committed to repo is the canonical default.
            var sb = new StringBuilder();
            sb.AppendLine("# Vlifecycle PVP item config (TOML)");
            foreach (var stageName in new[] { "onUse", "onEnterArenaZone", "onExitArenaZone" })
            {
                if (!model.TryGetValue(stageName, out var stageObj) || stageObj is not Dictionary<string, object> stage)
                    continue;

                if (!stage.TryGetValue("actions", out var actionsObj) || actionsObj is not List<Dictionary<string, object>> actions)
                    continue;

                sb.AppendLine();
                sb.AppendLine($"[{stageName}]");
                foreach (var action in actions)
                {
                    sb.AppendLine();
                    sb.AppendLine($"[[{stageName}.actions]]");
                    foreach (var kvp in action)
                    {
                        sb.AppendLine($"{kvp.Key} = {SerializeValue(kvp.Value)}");
                    }
                }
            }

            return sb.ToString();
        }

        private static Dictionary<string, object> AppendArrayTable(Dictionary<string, object> root, string dotted)
        {
            var parts = dotted.Split('.');
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.Length == 0) throw new FormatException($"Invalid table header: [[{dotted}]]");

                if (i == parts.Length - 1)
                {
                    if (!current.TryGetValue(p, out var existing) || existing is not List<Dictionary<string, object>> list)
                    {
                        list = new List<Dictionary<string, object>>();
                        current[p] = list;
                    }

                    var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                    list.Add(dict);
                    return dict;
                }

                if (!current.TryGetValue(p, out var nextObj) || nextObj is not Dictionary<string, object> next)
                {
                    next = new Dictionary<string, object>(StringComparer.Ordinal);
                    current[p] = next;
                }
                current = next;
            }

            throw new FormatException($"Invalid array table header: [[{dotted}]]");
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
            if (valueText.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;

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
            if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var dVal)) return dVal;

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

        private static string SerializeValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return Quote(s);
            if (value is bool b) return b ? "true" : "false";
            if (value is int i) return i.ToString(CultureInfo.InvariantCulture);
            if (value is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (value is float f) return f.ToString("0.###", CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString("0.###", CultureInfo.InvariantCulture);

            return Quote(value.ToString() ?? "");
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}

