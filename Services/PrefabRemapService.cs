using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Optional prefab name remaps (KindredSchematics-style) to recover from non-instantiable/incorrect TM_ names.
    /// File format: BadPrefabName=GoodPrefabName (one per line), comments with '#'.
    /// </summary>
    public static class PrefabRemapService
    {
        private static readonly object Lock = new();
        private static Dictionary<string, string> _remaps = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        private static string RemapsPath =>
            Path.Combine(Paths.ConfigPath, "VAuto.Arena", "prefabRemaps.txt");

        private static IReadOnlyDictionary<string, string> DefaultRemaps => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common KindredSchematics table parent remaps (non-instantiable -> instantiable parent prefab).
            ["TM_Castle_ObjectDecor_Table_3x3_Cabal01"] = "TM_Castle_Module_Parent_RoundTable_3x3_Cabal01",
            ["TM_Castle_ObjectDecor_Table_10x6_Gothic01"] = "TM_Castle_Module_Parent_RectangularTable_10x6_Gothic01",
        };

        public static void Reload()
        {
            var next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var path = RemapsPath;
                if (!File.Exists(path))
                {
                    foreach (var kv in DefaultRemaps)
                    {
                        next[kv.Key] = kv.Value;
                    }
                }
                else
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var p = trimmed.Split('=', 2);
                        if (p.Length != 2)
                        {
                            continue;
                        }

                        var bad = p[0].Trim();
                        var good = p[1].Trim();
                        if (string.IsNullOrWhiteSpace(bad) || string.IsNullOrWhiteSpace(good))
                        {
                            continue;
                        }

                        next[bad] = good;
                    }
                }
            }
            catch
            {
                // Ignore parse/IO errors; remaps are optional.
            }

            lock (Lock)
            {
                _remaps = next;
                _loaded = true;
            }
        }

        public static IReadOnlyDictionary<string, string> GetMappings()
        {
            EnsureLoaded();
            lock (Lock)
            {
                return new Dictionary<string, string>(_remaps, StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void ClearMappings(bool persist = true)
        {
            EnsureLoaded();
            lock (Lock)
            {
                _remaps.Clear();
            }

            if (persist)
            {
                SaveMappings();
            }
        }

        public static void AddMapping(string badPrefabName, string goodPrefabName, bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(badPrefabName) || string.IsNullOrWhiteSpace(goodPrefabName))
            {
                return;
            }

            EnsureLoaded();
            lock (Lock)
            {
                _remaps[badPrefabName.Trim()] = goodPrefabName.Trim();
            }

            if (persist)
            {
                SaveMappings();
            }
        }

        public static bool RemoveMapping(string badPrefabName, bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(badPrefabName))
            {
                return false;
            }

            EnsureLoaded();
            var removed = false;
            lock (Lock)
            {
                removed = _remaps.Remove(badPrefabName.Trim());
            }

            if (removed && persist)
            {
                SaveMappings();
            }

            return removed;
        }

        private static void SaveMappings()
        {
            try
            {
                var path = RemapsPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                Dictionary<string, string> snapshot;
                lock (Lock)
                {
                    snapshot = new Dictionary<string, string>(_remaps, StringComparer.OrdinalIgnoreCase);
                }

                using var sw = new StreamWriter(path);
                sw.WriteLine("# Prefab remaps: BadPrefabName=GoodPrefabName");
                foreach (var kv in snapshot)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                    {
                        continue;
                    }

                    sw.WriteLine($"{kv.Key}={kv.Value}");
                }
            }
            catch
            {
                // Ignore IO errors; remaps are optional.
            }
        }

        public static bool TryRemap(string prefabName, out string remappedPrefabName)
        {
            remappedPrefabName = string.Empty;
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            EnsureLoaded();

            if (_remaps.TryGetValue(prefabName.Trim(), out var good) && !string.IsNullOrWhiteSpace(good))
            {
                remappedPrefabName = good.Trim();
                return !string.Equals(prefabName, remappedPrefabName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            lock (Lock)
            {
                if (_loaded)
                {
                    return;
                }
            }

            Reload();
        }
    }
}
