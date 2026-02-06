using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto.Arena.Services;
using VAuto;
using VAuto.Core;
using Unity.Entities;

namespace VAuto
{
    [BepInPlugin(MyPluginInfo.Arena.Guid, MyPluginInfo.Arena.Name, MyPluginInfo.Arena.Version)]
    [BepInDependency(MyPluginInfo.Core.Guid)]
    [BepInDependency(MyPluginInfo.Lifecycle.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.Arena.Name);
        public new static ManualLogSource Log => _staticLog;
        public static ManualLogSource Logger => _staticLog;
        private Harmony? _harmony;
        private static ConfigFile? _configFile;
        private static ConfigEntry<bool>? _configEnabled;
        private static ConfigEntry<bool>? _configAutoEnter;
        private static ConfigEntry<bool>? _configAutoExit;

        public override void Load()
        {
            try
            {
                _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAuto.Arena.cfg"), true);
                _configEnabled = _configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Arena plugin.");
                _configAutoEnter = _configFile.Bind("Automation", "AutoEnter", true, "Automatically enter arena when inside zone radius.");
                _configAutoExit = _configFile.Bind("Automation", "AutoExit", true, "Automatically exit arena when leaving zone radius.");
                if (_configEnabled != null && !_configEnabled.Value)
                {
                    Log.LogInfo("[VAutoArena] Disabled via config.");
                    return;
                }
                ArenaAutoEnterSettings.Configure(_configAutoEnter?.Value ?? true, _configAutoExit?.Value ?? true);

                var manifest = MyPluginInfo.Arena.Manifest;
                Log.LogInfo($"[{manifest.Name}] Loading v{manifest.Version}...");

                if (manifest.EnableHarmony)
                {
                    _harmony ??= new Harmony(manifest.HarmonyId);
                    _harmony.PatchAll(typeof(Plugin).Assembly);
                    Log.LogInfo($"[{manifest.Name}] Harmony patches applied.");
                }

                // Ensure territory config is loaded early so commands/status reflect correct settings.
                ArenaTerritory.InitializeArenaGrid();
                ArenaPlayerService.InitializeFromTerritory();

                // If enabled, attempt to spawn the glow border once on startup.
                // If the Server world isn't ready yet, the admin can run `.arena glow spawn` later.
                if (ArenaTerritory.EnableGlowBorder)
                {
                    try
                    {
                        VRCore.Initialize();
                        if (VRCore.ServerWorld != null)
                        {
                            var configPath = ArenaTerritory.GetPreferredConfigPath();
                            var prefab = !string.IsNullOrWhiteSpace(ArenaTerritory.GlowPrefab)
                                ? ArenaTerritory.GlowPrefab
                                : ArenaGlowBorderService.GetDefaultPrefabName();
                            var spacing = ArenaTerritory.GlowSpacingMeters > 0 ? ArenaTerritory.GlowSpacingMeters : 3f;

                            if (ArenaGlowBorderService.SpawnBorderGlows(configPath, prefab, spacing, out var error))
                            {
                                Log.LogInfo($"[{manifest.Name}] Glow border spawned ({spacing:F1}m, '{prefab}').");
                            }
                            else
                            {
                                Log.LogWarning($"[{manifest.Name}] Glow border spawn skipped: {error}");
                            }
                        }
                        else
                        {
                            Log.LogWarning($"[{manifest.Name}] Server world not ready; glow border not spawned. Use `.arena glow spawn` after startup.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[{manifest.Name}] Glow border spawn failed: {ex.Message}");
                    }
                }

                CommandRegistry.RegisterAll();
                TryRegisterArenaAutoEnterSystem();

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

        private void TryRegisterArenaAutoEnterSystem()
        {
            try
            {
                VRCore.Initialize();
                var world = VRCore.ServerWorld;
                if (world == null)
                    return;

                var system = world.GetOrCreateSystemManaged<ArenaAutoEnterSystem>();
                var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
                if (simGroup != null)
                {
                    simGroup.AddSystemToUpdateList(system);
                    simGroup.SortSystems();
                }
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[ArenaAutoEnter] Registration failed: {ex.Message}");
            }
        }
    }
}
