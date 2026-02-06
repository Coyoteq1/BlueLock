namespace VAuto.Core.Lifecycle
{
    public static class LifecycleDebug
    {
        public static bool Enabled { get; private set; }
        public static bool Verbose { get; private set; }

        public static void Set(bool enabled, bool verbose = false)
        {
            Enabled = enabled;
            Verbose = verbose;
        }
    }
}

