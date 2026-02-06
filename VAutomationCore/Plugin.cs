using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto;

namespace VAutomationCore
{
    [BepInPlugin(MyPluginInfo.Core.Guid, MyPluginInfo.Core.Name, MyPluginInfo.Core.Version)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static ManualLogSource? _log;
        public new static ManualLogSource Log => _log ??= BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.Core.Name);
        private Harmony? _harmony;

        public override void Load()
        {
            try
            {
                var manifest = MyPluginInfo.Core.Manifest;
                Log.LogInfo($"[{manifest.Name}] Loading v{manifest.Version}...");

                if (manifest.EnableHarmony)
                {
                    _harmony ??= new Harmony(manifest.HarmonyId);
                    _harmony.PatchAll(typeof(Plugin).Assembly);
                    Log.LogInfo($"[{manifest.Name}] Harmony patches applied.");
                }

                Log.LogInfo($"[{manifest.Name}] Loaded core shared library.");
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
