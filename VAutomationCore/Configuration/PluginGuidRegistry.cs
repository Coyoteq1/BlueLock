using System;
using System.Collections.Generic;

namespace VAuto.Core.Configuration
{
    public static class PluginGuidRegistry
    {
        public const string Version = "1.0.0";

        public const string CoreGuid = "gg.vautomation.core";
        public const string CoreName = "VAutomationCore";

        public const string ArenaGuid = "gg.vautomation.arena";
        public const string ArenaName = "VAutoArena";

        public const string LifecycleGuid = "gg.vautomation.lifecycle";
        public const string LifecycleName = "Vlifecycle";

        public const string AnnouncementGuid = "gg.vautomation.announce";
        public const string AnnouncementName = "VAutoannounce";

        public const string TrapsGuid = "gg.vautomation.traps";
        public const string TrapsName = "VAutoTraps";

        private static readonly IReadOnlyDictionary<PluginKey, PluginManifest> Manifests =
            new Dictionary<PluginKey, PluginManifest>
            {
                {
                    PluginKey.Core,
                    new PluginManifest(PluginKey.Core, CoreGuid, CoreName, Version, enableHarmony: true)
                },
                {
                    PluginKey.Arena,
                    new PluginManifest(PluginKey.Arena, ArenaGuid, ArenaName, Version, enableHarmony: true)
                },
                {
                    PluginKey.Lifecycle,
                    new PluginManifest(PluginKey.Lifecycle, LifecycleGuid, LifecycleName, Version, enableHarmony: true)
                },
                {
                    PluginKey.Announcement,
                    new PluginManifest(PluginKey.Announcement, AnnouncementGuid, AnnouncementName, Version, enableHarmony: true)
                },
                {
                    PluginKey.Traps,
                    new PluginManifest(PluginKey.Traps, TrapsGuid, TrapsName, Version, enableHarmony: true)
                }
            };

        public static IEnumerable<PluginManifest> All => Manifests.Values;

        public static PluginManifest Core => Get(PluginKey.Core);
        public static PluginManifest Arena => Get(PluginKey.Arena);
        public static PluginManifest Lifecycle => Get(PluginKey.Lifecycle);
        public static PluginManifest Announcement => Get(PluginKey.Announcement);
        public static PluginManifest Traps => Get(PluginKey.Traps);

        public static PluginManifest Get(PluginKey key)
        {
            if (Manifests.TryGetValue(key, out var manifest))
            {
                return manifest;
            }

            throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown plugin key.");
        }

        public static bool TryGet(PluginKey key, out PluginManifest manifest)
        {
            return Manifests.TryGetValue(key, out manifest!);
        }
    }
}
