using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto;

namespace VAuto
{
    [BepInPlugin(MyPluginInfo.GUID, MyPluginInfo.NAME, MyPluginInfo.VERSION)]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.0")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.NAME);
        public new static ManualLogSource Log => _staticLog;
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

                Log.LogInfo($"[{MyPluginInfo.NAME}] Loading v{MyPluginInfo.VERSION}...");

                CommandRegistry.RegisterAll();

                Log.LogInfo($"[{MyPluginInfo.NAME}] Loaded.");
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
}
