using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAutomationCore.Core;
using VAutomationCore.Core.Api;
using VAutomationCore.Core.Logging;

namespace VAutomationCore
{
    [BepInPlugin("gg.coyote.VAutomationCore", "VAutomationCore", "1.0.1")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private const string ConfigFileName = "gg.coyote.VAutomationCore.cfg";
        private const string CommandRoots = "coreauth, jobs";

        private static ManualLogSource _coreLog;
        private Harmony _harmony;

        public static ManualLogSource CoreLog => _coreLog ??= BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.NAME);

        private static ConfigFile _configFile;
        private static ConfigEntry<bool> _configEnabled;

        public override void Load()
        {
            try
            {
                _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, ConfigFileName), true);
                _configEnabled = _configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Core plugin.");

                if (!_configEnabled.Value)
                {
                    CoreLog.LogInfo("[VAutomationCore] Disabled via config.");
                    return;
                }

                CoreLog.LogInfo($"[{MyPluginInfo.NAME}] Loading {MyPluginInfo.VERSION}...");
                CoreLog.LogInfo($"[{MyPluginInfo.NAME}] Loaded core shared library.");

                // Initialize module ID registry for cross-mod compatibility
                var coreLogger = new CoreLogger("ModuleRegistry");
                ModuleIdRegistry.Initialize(coreLogger);

                ConsoleRoleAuthService.Initialize();
                CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());
                CoreLog.LogInfo($"[{MyPluginInfo.NAME}] Commands registered: {CommandRoots}");

                // Keep PlayerAPI integration hook in place for optional external plugin wiring.
                RegisterPlayerApiEndpoints();

                // Keep Harmony instance ready for future patch registration.
                _harmony = new Harmony(MyPluginInfo.GUID);
                
                // Register VAuto patches
                _harmony.PatchAll();

                LogStartupSummary();
            }
            catch (Exception ex)
            {
                CoreLog.LogError(ex);
            }
        }

        public override bool Unload()
        {
            try
            {
                if (_harmony != null)
                {
                    // Harmony 2.2+: prefer instance-scoped unpatching.
                    _harmony.UnpatchSelf();
                    _harmony = null;
                }

                if (_configFile != null)
                {
                    _configFile.Save();
                    _configFile = null;
                    _configEnabled = null;
                }
            }
            catch (Exception ex)
            {
                CoreLog.LogError(ex);
            }

            return true;
        }

        private static void LogStartupSummary()
        {
            var cfgPath = Path.Combine(Paths.ConfigPath, ConfigFileName);
            CoreLog.LogInfo($"[{MyPluginInfo.NAME}] Startup Summary:");
            CoreLog.LogInfo($"[{MyPluginInfo.NAME}]   Config: {cfgPath}");
            CoreLog.LogInfo($"[{MyPluginInfo.NAME}]   ConsoleRoleAuth: {(ConsoleRoleAuthService.IsEnabled ? "Enabled" : "Disabled")}");
            CoreLog.LogInfo($"[{MyPluginInfo.NAME}]   Command Roots: {CommandRoots}");
            CoreLog.LogInfo($"[{MyPluginInfo.NAME}]   Processes: VRisingServer.exe, VRising.exe");
            
            // Log registered modules
            ModuleIdRegistry.LogRegistryState();
        }

        private void RegisterPlayerApiEndpoints()
        {
            try
            {
                // Placeholder hook for optional external API plugin integration.
                // Intentionally non-fatal when dependency is not present.
                CoreLog.LogInfo("[PlayerAPI] Integration hook loaded (external API plugin not bound).");
            }
            catch (Exception ex)
            {
                CoreLog.LogWarning($"[PlayerAPI] Failed to initialize integration hook: {ex.Message}");
            }
        }
    }
}
