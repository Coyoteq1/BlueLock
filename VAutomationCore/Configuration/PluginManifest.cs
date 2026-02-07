using System;

namespace VAuto.Core.Configuration
{
    public enum PluginKey
    {
        Core,
        Arena,
        Lifecycle,
        Announcement,
        Traps,
        Zone
    }

    public sealed class PluginManifest
    {
        public PluginKey Key { get; }
        public string Guid { get; }
        public string Name { get; }
        public string Version { get; }
        public bool EnableHarmony { get; }
        public string HarmonyId { get; }

        public PluginManifest(
            PluginKey key,
            string guid,
            string name,
            string version,
            bool enableHarmony,
            string harmonyId = null)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new ArgumentException("GUID cannot be null or empty.", nameof(guid));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Version cannot be null or empty.", nameof(version));
            }

            Key = key;
            Guid = guid;
            Name = name;
            Version = version;
            EnableHarmony = enableHarmony;
            HarmonyId = string.IsNullOrWhiteSpace(harmonyId) ? guid : harmonyId;
        }

        public override string ToString()
        {
            return $"{Name} ({Guid}) v{Version}";
        }
    }
}
