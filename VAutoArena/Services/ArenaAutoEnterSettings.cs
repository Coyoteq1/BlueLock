namespace VAuto.Arena.Services
{
    internal static class ArenaAutoEnterSettings
    {
        public static bool AutoEnterEnabled { get; private set; } = true;
        public static bool AutoExitEnabled { get; private set; } = true;

        public static void Configure(bool autoEnterEnabled, bool autoExitEnabled)
        {
            AutoEnterEnabled = autoEnterEnabled;
            AutoExitEnabled = autoExitEnabled;
        }
    }
}
