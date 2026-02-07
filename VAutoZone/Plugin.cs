using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAutoZone;
using VAuto.Zone.Services;

namespace VAutoZone
{
    [BepInPlugin(MyPluginInfo.GUID, MyPluginInfo.NAME, MyPluginInfo.VERSION)]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.0")]
    [BepInDependency("gg.coyote.VAutomationLifecycle", "1.0.0")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.NAME);
        public new static ManualLogSource Log => _staticLog;
        public static ManualLogSource Logger => _staticLog;
        private Harmony? _harmony;
        private static ConfigFile? _configFile;
        private static ConfigEntry<bool>? _configEnabled;

        public override void Load()
        {
            try
            {
                _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAuto.Zone.cfg"), true);
                _configEnabled = _configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Zone plugin.");
                if (_configEnabled != null && !_configEnabled.Value)
                {
                    Log.LogInfo("[VAutoZone] Disabled via config.");
                    return;
                }

                Log.LogInfo($"[{MyPluginInfo.NAME}] Loading v{MyPluginInfo.VERSION}...");

                CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());

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
                Log.LogInfo("[VAutoZone] Unloaded.");
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
