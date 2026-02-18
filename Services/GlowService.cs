using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VAuto.Zone.Core;

namespace VAuto.Zone.Services
{
    public static class GlowService
    {
        private static readonly int[] _defaultVisibleGlowBuffHashes = LoadDefaultVisibleGlowBuffHashes();

        public static int[] GetValidatedGlowBuffHashes()
        {
            return _defaultVisibleGlowBuffHashes;
        }

        private static int[] LoadDefaultVisibleGlowBuffHashes()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "buffs_numbered.txt");
                if (!File.Exists(path))
                {
                    path = Path.Combine(AppContext.BaseDirectory, "Bluelock", "buffs_numbered.txt");
                }

                if (!File.Exists(path))
                {
                    return Array.Empty<int>();
                }

                var values = new List<int>();
                foreach (var rawLine in File.ReadLines(path))
                {
                    var line = rawLine?.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var dotIndex = line.IndexOf('.');
                    var token = dotIndex >= 0 ? line[(dotIndex + 1)..].Trim() : line;
                    if (int.TryParse(token, out var hash) && hash != 0)
                    {
                        values.Add(hash);
                    }
                }

                return values.Distinct().ToArray();
            }
            catch
            {
                return Array.Empty<int>();
            }
        }

        public static bool TryResolve(string token, out int guidHash)
        {
            guidHash = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var lookup = token.Trim();
            if (int.TryParse(lookup, out var numeric) && numeric != 0)
            {
                guidHash = numeric;
                return true;
            }

            if (PrefabReferenceCatalog.TryResolve(lookup, out var guid))
            {
                guidHash = guid.GuidHash;
                return true;
            }

            return false;
        }
    }
}
