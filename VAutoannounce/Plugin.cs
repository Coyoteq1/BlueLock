using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto.Core.Services;
using VAuto;

namespace VAuto
{
    [BepInPlugin(MyPluginInfo.Announcement.Guid, MyPluginInfo.Announcement.Name, MyPluginInfo.Announcement.Version)]
    [BepInDependency(MyPluginInfo.Core.Guid)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.Announcement.Name);
        public new static ManualLogSource Log => _staticLog;
        public static ManualLogSource Logger => _staticLog;
        private Harmony? _harmony;
        private static ConfigFile? _configFile;
        private static ConfigEntry<bool>? _configEnabled;

        public override void Load()
        {
            try
            {
                _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAuto.Announcement.cfg"), true);
                _configEnabled = _configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Announcement plugin.");
                if (_configEnabled != null && !_configEnabled.Value)
                {
                    Log.LogInfo("[VAutoannounce] Disabled via config.");
                    return;
                }

                var manifest = MyPluginInfo.Announcement.Manifest;
                Log.LogInfo($"[{manifest.Name}] Loading v{manifest.Version}...");

                if (manifest.EnableHarmony && _harmony == null)
                {
                    _harmony = new Harmony(manifest.HarmonyId);
                    _harmony.PatchAll(typeof(Plugin).Assembly);
                    Log.LogInfo($"[{manifest.Name}] Harmony patches applied.");
                }

                AnnouncementService.Initialize();

                CommandRegistry.RegisterAll();

                Log.LogInfo($"[{manifest.Name}] Loaded.");
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
            }
        }

        public override bool Unload()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
            }

            return true;
        }
    }
}
