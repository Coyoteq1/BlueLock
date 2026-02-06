using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto.Core.Configuration;
using VAuto.Core.Lifecycle;
using VAuto;

namespace VAuto
{
    [BepInPlugin(MyPluginInfo.Lifecycle.Guid, MyPluginInfo.Lifecycle.Name, MyPluginInfo.Lifecycle.Version)]
    [BepInDependency(MyPluginInfo.Core.Guid)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.Lifecycle.Name);
        public new static ManualLogSource Log => _staticLog;
        public static ManualLogSource Logger => _staticLog;
        private Harmony? _harmony;

        public override void Load()
        {
            try
            {
                var manifest = MyPluginInfo.Lifecycle.Manifest;
                Log.LogInfo($"[{manifest.Name}] Loading v{manifest.Version}...");

                if (manifest.EnableHarmony)
                {
                    _harmony ??= new Harmony(manifest.HarmonyId);
                    _harmony.PatchAll(typeof(Plugin).Assembly);
                    Log.LogInfo($"[{manifest.Name}] Harmony patches applied.");
                }

                PVPItemLifecycle.Instance.Initialize();
                var loader = new PVPLifecycleConfigLoader();
                loader.Initialize();

                var kitServiceImpl = new KitConfigService();

                var serviceManager = ServiceManager.Instance;
                var snapshotService = new SnapshotLifecycleService();
                var vbloodService = new VBloodUnlockLifecycleService();
                var autoEnterService = new AutoEnterService();
                var buildingService = new BuildingService();
                var locationTracker = new LocationTracker();
                var kitService = new KitLifecycleService();
                kitService.SetKitService(kitServiceImpl);

                serviceManager.Register(snapshotService);
                serviceManager.Register(vbloodService);
                serviceManager.Register(autoEnterService);
                serviceManager.Register(buildingService);
                serviceManager.Register(locationTracker);
                serviceManager.Register(kitServiceImpl);
                serviceManager.Register(kitService);
                serviceManager.InitializeAll();

                var arenaManager = ArenaLifecycleManager.Instance;
                arenaManager.RegisterService(snapshotService);
                arenaManager.RegisterService(vbloodService);
                arenaManager.RegisterService(autoEnterService);
                arenaManager.RegisterService(buildingService);
                arenaManager.RegisterService(locationTracker);
                arenaManager.RegisterService(kitService);
                arenaManager.PostInitialize();

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
