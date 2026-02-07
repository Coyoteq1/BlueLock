using System;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto;

namespace VAuto.Announcement
{
    [BepInPlugin(MyPluginInfo.GUID, MyPluginInfo.NAME, MyPluginInfo.VERSION)]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.0")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        #region Logging
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.NAME);
        public new static ManualLogSource Log => _staticLog;
        public static ManualLogSource Logger => _staticLog;
        #endregion

        #region Harmony
        private Harmony? _harmony;
        public static Plugin Instance { get; private set; }
        #endregion

        #region CFG Configuration Entries
        // General
        public static ConfigEntry<bool> GeneralEnabled;
        public static ConfigEntry<string> LogLevel;

        // Announcement System
        public static ConfigEntry<bool> AnnouncementsEnabled;
        public static ConfigEntry<bool> EventAnnouncements;
        public static ConfigEntry<bool> KillAnnouncements;
        public static ConfigEntry<bool> AchievementAnnouncements;
        public static ConfigEntry<bool> PvPAnnouncements;

        // Message Settings
        public static ConfigEntry<bool> ShowTimestamps;
        public static ConfigEntry<string> MessagePrefix;
        public static ConfigEntry<string> MessageSuffix;
        public static ConfigEntry<bool> UseColors;
        public static ConfigEntry<string> DefaultColor;

        // Event Announcements
        public static ConfigEntry<bool> BossSpawnAnnouncement;
        public static ConfigEntry<bool> VBloodKillAnnouncement;
        public static ConfigEntry<bool> CastleHeartAnnouncement;

        // Kill Feed
        public static ConfigEntry<bool> KillFeedEnabled;
        public static ConfigEntry<bool> KillFeedShowLocations;
        public static ConfigEntry<int> KillFeedDuration;

        // Formatting
        public static ConfigEntry<bool> UseRichFormatting;
        public static ConfigEntry<bool> ShortenLargeNumbers;

        // Debug
        public static ConfigEntry<bool> DebugMode;
        public static ConfigEntry<bool> HotReloadEnabled;
        #endregion

        #region JSON Configuration
        private static string _configPath;
        private static AnnouncementJsonConfig _jsonConfig;
        private static DateTime _lastConfigCheck;
        private static System.Timers.Timer _hotReloadTimer;
        #endregion

        public override void Load()
        {
            Instance = this;
            Log.LogInfo($"[{MyPluginInfo.NAME}] Loading v{MyPluginInfo.VERSION}...");

            try
            {
                // Initialize configuration path
                _configPath = Path.Combine(Paths.ConfigPath, "VAuto.Announcement.json");

                // Bind CFG configuration
                BindConfiguration();

                // Load JSON configuration
                LoadJsonConfiguration();

                // Check if enabled
                if (GeneralEnabled != null && !GeneralEnabled.Value)
                {
                    Log.LogInfo("[VAutoannounce] Disabled via config.");
                    return;
                }

                // Register commands
                CommandRegistry.RegisterAll();

                // Start hot-reload monitoring if enabled
                if (HotReloadEnabled?.Value == true)
                {
                    StartHotReloadMonitoring();
                }

                Log.LogInfo($"[{MyPluginInfo.NAME}] Loaded successfully.");
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
            }
        }

        private void BindConfiguration()
        {
            var configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAuto.Announcement.cfg"), true);

            // General
            GeneralEnabled = configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Announcement plugin");
            LogLevel = configFile.Bind("General", "LogLevel", "Info", "Log level (Debug, Info, Warning, Error)");

            // Announcement System
            AnnouncementsEnabled = configFile.Bind("Announcements", "Enabled", true, "Enable announcement system");
            EventAnnouncements = configFile.Bind("Announcements", "EventAnnouncements", true, "Enable event announcements");
            KillAnnouncements = configFile.Bind("Announcements", "KillAnnouncements", true, "Enable kill announcements");
            AchievementAnnouncements = configFile.Bind("Announcements", "AchievementAnnouncements", true, "Enable achievement announcements");
            PvPAnnouncements = configFile.Bind("Announcements", "PvPAnnouncements", true, "Enable PvP announcements");

            // Message Settings
            ShowTimestamps = configFile.Bind("Messages", "ShowTimestamps", true, "Show timestamps in messages");
            MessagePrefix = configFile.Bind("Messages", "Prefix", "[VAuto] ", "Message prefix");
            MessageSuffix = configFile.Bind("Messages", "Suffix", "", "Message suffix");
            UseColors = configFile.Bind("Messages", "UseColors", true, "Use colors in messages");
            DefaultColor = configFile.Bind("Messages", "DefaultColor", "gold", "Default message color");

            // Event Announcements
            BossSpawnAnnouncement = configFile.Bind("Events", "BossSpawn", true, "Announce boss spawns");
            VBloodKillAnnouncement = configFile.Bind("Events", "VBloodKill", true, "Announce VBlood kills");
            CastleHeartAnnouncement = configFile.Bind("Events", "CastleHeart", true, "Announce castle heart events");

            // Kill Feed
            KillFeedEnabled = configFile.Bind("KillFeed", "Enabled", true, "Enable kill feed");
            KillFeedShowLocations = configFile.Bind("KillFeed", "ShowLocations", true, "Show kill locations in feed");
            KillFeedDuration = configFile.Bind("KillFeed", "Duration", 30, "Kill feed duration in seconds");

            // Formatting
            UseRichFormatting = configFile.Bind("Formatting", "UseRichFormatting", true, "Use rich text formatting");
            ShortenLargeNumbers = configFile.Bind("Formatting", "ShortenNumbers", true, "Shorten large numbers");

            // Debug
            DebugMode = configFile.Bind("Debug", "DebugMode", false, "Enable debug mode");
            HotReloadEnabled = configFile.Bind("Debug", "HotReload", true, "Enable hot-reload of configuration");
        }

        private void LoadJsonConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var jsonContent = File.ReadAllText(_configPath);
                    _jsonConfig = JsonSerializer.Deserialize<AnnouncementJsonConfig>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                    Log.LogInfo($"[VAutoannounce] Loaded JSON configuration from {_configPath}");
                }
                else
                {
                    _jsonConfig = new AnnouncementJsonConfig();
                    SaveJsonConfiguration();
                    Log.LogInfo($"[VAutoannounce] Created new JSON configuration at {_configPath}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[VAutoannounce] Failed to load JSON configuration: {ex.Message}");
                _jsonConfig = new AnnouncementJsonConfig();
            }
        }

        private void SaveJsonConfiguration()
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(_jsonConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(_configPath, jsonContent);
                Log.LogInfo($"[VAutoannounce] Saved JSON configuration to {_configPath}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[VAutoannounce] Failed to save JSON configuration: {ex.Message}");
            }
        }

        private void StartHotReloadMonitoring()
        {
            _lastConfigCheck = DateTime.UtcNow;
            _hotReloadTimer = new System.Timers.Timer(5000);
            _hotReloadTimer.Elapsed += (_, _) => CheckForConfigChanges();
            _hotReloadTimer.Start();
            Log.LogInfo("[VAutoannounce] Hot-reload monitoring started.");
        }

        private void CheckForConfigChanges()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var lastModified = File.GetLastWriteTime(_configPath);
                    if (lastModified > _lastConfigCheck)
                    {
                        _lastConfigCheck = lastModified;
                        LoadJsonConfiguration();
                        Log.LogInfo("[VAutoannounce] Configuration hot-reloaded successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[VAutoannounce] Error checking configuration changes: {ex.Message}");
            }
        }

        #region Public Configuration Accessors
        public static bool IsEnabled => GeneralEnabled?.Value ?? true;
        public static bool AnnouncementsActive => AnnouncementsEnabled?.Value ?? true;
        public static bool EventsActive => EventAnnouncements?.Value ?? true;
        public static bool KillsActive => KillAnnouncements?.Value ?? true;
        public static bool AchievementsActive => AchievementAnnouncements?.Value ?? true;
        public static bool PvPActive => PvPAnnouncements?.Value ?? true;
        public static bool TimestampsEnabled => ShowTimestamps?.Value ?? true;
        public static string MsgPrefix => MessagePrefix?.Value ?? "[VAuto] ";
        public static string MsgSuffix => MessageSuffix?.Value ?? "";
        public static bool ColorsEnabled => UseColors?.Value ?? true;
        public static string DefaultMsgColor => DefaultColor?.Value ?? "gold";
        public static bool BossSpawnAnnounce => BossSpawnAnnouncement?.Value ?? true;
        public static bool VBloodKillAnnounce => VBloodKillAnnouncement?.Value ?? true;
        public static bool CastleHeartAnnounce => CastleHeartAnnouncement?.Value ?? true;
        public static bool KillFeedActive => KillFeedEnabled?.Value ?? true;
        public static bool KillFeedLocations => KillFeedShowLocations?.Value ?? true;
        public static int KillFeedSeconds => KillFeedDuration?.Value ?? 30;
        public static bool RichFormatting => UseRichFormatting?.Value ?? true;
        public static bool ShortenNumbers => ShortenLargeNumbers?.Value ?? true;
        public static bool DebugModeEnabled => DebugMode?.Value ?? false;
        #endregion

        public override bool Unload()
        {
            try
            {
                _harmony?.UnpatchSelf();
                _hotReloadTimer?.Dispose();
                _hotReloadTimer = null;
                Log.LogInfo("[VAutoannounce] Unloaded.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
                return false;
            }
        }
    }

    #region JSON Configuration Classes
    public class AnnouncementJsonConfig
    {
        public AnnouncementConfigSection Announcement { get; set; } = new();
    }

    public class AnnouncementConfigSection
    {
        public bool Enabled { get; set; } = true;
        public AnnouncementsConfig Announcements { get; set; } = new();
        public MessagesConfig Messages { get; set; } = new();
        public EventsConfig Events { get; set; } = new();
        public KillFeedConfig KillFeed { get; set; } = new();
        public FormattingConfig Formatting { get; set; } = new();
    }

    public class AnnouncementsConfig
    {
        public bool Enabled { get; set; } = true;
        public bool EventAnnouncements { get; set; } = true;
        public bool KillAnnouncements { get; set; } = true;
        public bool AchievementAnnouncements { get; set; } = true;
        public bool PvPAnnouncements { get; set; } = true;
    }

    public class MessagesConfig
    {
        public bool ShowTimestamps { get; set; } = true;
        public string Prefix { get; set; } = "[VAuto] ";
        public string Suffix { get; set; } = "";
        public bool UseColors { get; set; } = true;
        public string DefaultColor { get; set; } = "gold";
    }

    public class EventsConfig
    {
        public bool BossSpawn { get; set; } = true;
        public bool VBloodKill { get; set; } = true;
        public bool CastleHeart { get; set; } = true;
    }

    public class KillFeedConfig
    {
        public bool Enabled { get; set; } = true;
        public bool ShowLocations { get; set; } = true;
        public int Duration { get; set; } = 30;
    }

    public class FormattingConfig
    {
        public bool UseRichFormatting { get; set; } = true;
        public bool ShortenNumbers { get; set; } = true;
    }
    #endregion
}
