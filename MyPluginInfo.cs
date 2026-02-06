using VAuto.Core.Configuration;

namespace VAuto
{
    public static class MyPluginInfo
    {
        public const string Version = PluginGuidRegistry.Version;

        public static class Arena
        {
            public const string Guid = PluginGuidRegistry.ArenaGuid;
            public const string Name = PluginGuidRegistry.ArenaName;
            public const string Version = PluginGuidRegistry.Version;
            public static PluginManifest Manifest => PluginGuidRegistry.Arena;
        }

        public static class Lifecycle
        {
            public const string Guid = PluginGuidRegistry.LifecycleGuid;
            public const string Name = PluginGuidRegistry.LifecycleName;
            public const string Version = PluginGuidRegistry.Version;
            public static PluginManifest Manifest => PluginGuidRegistry.Lifecycle;
        }

        public static class Announcement
        {
            public const string Guid = PluginGuidRegistry.AnnouncementGuid;
            public const string Name = PluginGuidRegistry.AnnouncementName;
            public const string Version = PluginGuidRegistry.Version;
            public static PluginManifest Manifest => PluginGuidRegistry.Announcement;
        }

        public static class Traps
        {
            public const string Guid = PluginGuidRegistry.TrapsGuid;
            public const string Name = PluginGuidRegistry.TrapsName;
            public const string Version = PluginGuidRegistry.Version;
            public static PluginManifest Manifest => PluginGuidRegistry.Traps;
        }

        public static class Core
        {
            public const string Guid = PluginGuidRegistry.CoreGuid;
            public const string Name = PluginGuidRegistry.CoreName;
            public const string Version = PluginGuidRegistry.Version;
            public static PluginManifest Manifest => PluginGuidRegistry.Core;
        }
    }
}
