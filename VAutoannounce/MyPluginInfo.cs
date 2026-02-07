using System.Reflection;

namespace VAuto
{   
    //do not change this
    public static class MyPluginInfo
    {
        public const string GUID = "gg.coyote.VAutoannounce";
        public const string NAME = "VAuto Announcement";
        public const string VERSION = "1.0.0";

        public static class Announcement
        {
            public static readonly string Name = "VAuto Announcement";
        }

        public static class Manifest
        {
            public const string Name = "VAuto Announcement";
            public const string Version = "1.0.0";
            public const bool EnableHarmony = true;
            public const string HarmonyId = "gg.coyote.VAutoannounce";
        }
    }
}