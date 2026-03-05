using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAutomationCore.Core;
using VAutomationCore.Core.Api;
using VAutomationCore.Core.Logging;
using VAutomationCore.Core.Automation;

namespace VAutomationCore
{
    [BepInPlugin(MyPluginInfo.GUID, MyPluginInfo.NAME, MyPluginInfo.VERSION)]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private const string ConfigFileName = "gg.coyote.VAutomationCore.cfg";
        private const string CommandRoots = "coreauth, jobs";

        private static ManualLogSource _coreLog;
        private Harmony _harmony;
        private static bool _earlyDiagnosticsInstalled;

        public static ManualLogSource CoreLog => _coreLog ??= BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.NAME);

        private static ConfigFile _configFile;
        private static ConfigEntry<bool> _configEnabled;

        public override void Load()
        {
            try
            {
                InstallEarlyDiagnostics();
                CoreLogger.Initialize(CoreLog);

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
                VerifyVcfCommandBindings();
                CoreLog.LogInfo($"[{MyPluginInfo.NAME}] Commands registered: {CommandRoots}");

                // Keep PlayerAPI integration hook in place for optional external plugin wiring.
                RegisterPlayerApiEndpoints();

                // Keep Harmony instance ready for future patch registration.
                _harmony = new Harmony(MyPluginInfo.GUID);
                
                // Register VAuto patches
                _harmony.PatchAll();
                
                // Initialize ModTalk automation system
                try
                {
                    CoreLog.LogInfo($"[{MyPluginInfo.NAME}] Initializing ModTalk automation system...");
                    DynamicCommandRegistration.Initialize();
                    CoreLog.LogInfo($"[{MyPluginInfo.NAME}] ModTalk automation system initialized successfully.");
                }
                catch (Exception ex)
                {
                    CoreLog.LogError($"[{MyPluginInfo.NAME}] Failed to initialize ModTalk automation: {ex.Message}");
                }

                LogStartupSummary();
            }
            catch (Exception ex)
            {
                CoreLog.LogError(ex);
            }
        }

        private static void InstallEarlyDiagnostics()
        {
            if (_earlyDiagnosticsInstalled)
            {
                return;
            }

            _earlyDiagnosticsInstalled = true;

            try
            {
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
            }
            catch (Exception ex)
            {
                CoreLog.LogWarning($"[EarlyDiag] Failed to install first-chance exception hook: {ex.Message}");
            }

            CoreLog.LogInfo("[EarlyDiag] Startup diagnostics hooks installed.");
            CoreLog.LogInfo("[EarlyDiag] If server runs with NullGfx/headless, 'There is no texture data available to upload.' is expected and non-fatal.");
        }

        private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            try
            {
                var ex = e.Exception;
                if (ex == null)
                {
                    return;
                }

                // Keep noise low while still catching startup faults.
                if (ex is OperationCanceledException || ex is TaskCanceledException)
                {
                    return;
                }

                CoreLog.LogDebug($"[EarlyDiag][FirstChance] {ex.GetType().Name}: {ex.Message}");
            }
            catch
            {
                // no-op
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

        private static void VerifyVcfCommandBindings()
        {
            try
            {
                var required = new (Type Type, string Command)[]
                {
                    (typeof(CoreAuthCommands), "help"),
                    (typeof(CoreAuthCommands), "login admin"),
                    (typeof(CoreAuthCommands), "login dev"),
                    (typeof(CoreAuthCommands), "status"),
                    (typeof(CoreAuthCommands), "logout"),
                    (typeof(CoreJobFlowCommands), "help"),
                    (typeof(CoreJobFlowCommands), "flow add"),
                    (typeof(CoreJobFlowCommands), "flow list"),
                    (typeof(CoreJobFlowCommands), "run")
                };

                var missing = new System.Collections.Generic.List<string>();
                foreach (var item in required)
                {
                    var found = false;
                    var methods = item.Type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var attribute = method.GetCustomAttribute<CommandAttribute>();
                        if (attribute == null)
                        {
                            continue;
                        }

                        if (string.Equals(attribute.Name, item.Command, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        missing.Add($"{item.Type.Name}:{item.Command}");
                    }
                }

                if (missing.Count == 0)
                {
                    CoreLog.LogInfo($"[{MyPluginInfo.NAME}] VCF command verification passed ({required.Length} commands).");
                }
                else
                {
                    CoreLog.LogWarning($"[{MyPluginInfo.NAME}] VCF command verification missing: {string.Join(", ", missing)}");
                }
            }
            catch (Exception ex)
            {
                CoreLog.LogWarning($"[{MyPluginInfo.NAME}] VCF command verification failed: {ex.Message}");
            }
        }
    }
}
