using System;
using System.IO;
using System.Text.Json;

namespace VAuto.Core.Lifecycle.Snapshots
{
    internal sealed class SnapshotStore
    {
        private readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public bool TryLoad(ulong platformId, string arenaId, out CharacterSnapshot snapshot)
        {
            snapshot = null;
            var path = GetPath(platformId, arenaId);
            if (!File.Exists(path)) return false;
            try
            {
                var json = File.ReadAllText(path);
                snapshot = JsonSerializer.Deserialize<CharacterSnapshot>(json, _options);
                return snapshot != null;
            }
            catch
            {
                return false;
            }
        }

        public void Save(ulong platformId, string arenaId, CharacterSnapshot snapshot)
        {
            var path = GetPath(platformId, arenaId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(snapshot, _options);
            File.WriteAllText(path, json);
        }

        public void Delete(ulong platformId, string arenaId)
        {
            var path = GetPath(platformId, arenaId);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string GetPath(ulong platformId, string arenaId)
        {
            var root = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "Snapshots", platformId.ToString());
            return Path.Combine(root, $"{arenaId}.json");
        }
    }
}
