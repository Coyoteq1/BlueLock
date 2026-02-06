using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto.Arena.Services;
using VAuto;
using VAuto.Core;

namespace VAuto
{
    [BepInPlugin(MyPluginInfo.Arena.Guid, MyPluginInfo.Arena.Name, MyPluginInfo.Arena.Version)]
    [BepInDependency(MyPluginInfo.Core.Guid)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.Arena.Name);
        public new static ManualLogSource Log => _staticLog;
        public static ManualLogSource Logger => _staticLog;
        private Harmony? _harmony;

        public override void Load()
        {
            try
            {
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
