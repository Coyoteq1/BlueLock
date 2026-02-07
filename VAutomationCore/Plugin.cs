using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAutomationCore;
using VAuto.Core;
namespace VAutomationCore
{
    [BepInPlugin(MyPluginInfo.GUID, MyPluginInfo.NAME, MyPluginInfo.VERSION)]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static ManualLogSource _log;
        public new static ManualLogSource Log => _log ??= BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.NAME);
        private Harmony _harmony;
        private static ConfigFile _configFile;
        private static ConfigEntry<bool> _configEnabled;

        public override void Load()
        {
            try
            {
                _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAuto.Core.cfg"), true);
                _configEnabled = _configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Core plugin.");
                if (_configEnabled != null && !_configEnabled.Value)
                {
                    Log.LogInfo("[VAutomationCore] Disabled via config.");
                    return;
                }

                Log.LogInfo($"[{MyPluginInfo.NAME}] Loading {MyPluginInfo.VERSION}...");

                Log.LogInfo($"[{MyPluginInfo.NAME}] Loaded core shared library.");
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
