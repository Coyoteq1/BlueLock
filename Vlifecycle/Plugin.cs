using VAuto.EndGameKit.Systems;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;
using VAuto.Core.Configuration;
using VAuto.Core.Lifecycle;
using VAuto;
using Unity.Entities;

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
        private static ConfigFile? _configFile;
        private static ConfigEntry<bool>? _configEnabled;

        public override void Load()
        {
            try
            {
                _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "VAuto.Lifecycle.cfg"), true);
                _configEnabled = _configFile.Bind("General", "Enabled", true, "Enable or disable VAuto Lifecycle plugin.");
                if (_configEnabled != null && !_configEnabled.Value)
                {
                    Log.LogInfo("[Vlifecycle] Disabled via config.");
                    return;
                }

                var manifest = MyPluginInfo.Lifecycle.Manifest;
                Log.LogInfo($"[{manifest.Name}] Loading v{manifest.Version}...");

                // Initialize VRCore early to prevent race conditions
                VRCore.Initialize();
                Log.LogInfo($"[{manifest.Name}] VRCore initialized successfully.");

                if (manifest.EnableHarmony)
                {
                    _harmony ??= new Harmony(manifest.HarmonyId);
                    _harmony.PatchAll(typeof(Plugin).Assembly);
                    Log.LogInfo($"[{manifest.Name}] Harmony patches applied.");
                }

                PVPItemLifecycle.Instance.Initialize();
                var loader = new PVPLifecycleConfigLoader();
                loader.Initialize();

                var serviceManager = ServiceManager.Instance;
                var snapshotService = new SnapshotLifecycleService();
                var vbloodService = new VBloodUnlockLifecycleService();
                var autoEnterService = new AutoEnterService();
                var buildingService = new BuildingService();
                var locationTracker = new LocationTracker();
                var kitService = new KitLifecycleService();

                serviceManager.Register(snapshotService);
                serviceManager.Register(vbloodService);
                serviceManager.Register(autoEnterService);
                serviceManager.Register(buildingService);
                serviceManager.Register(locationTracker);
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

                TryRegisterVbloodRepairRefreshSystem();

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

        private void TryRegisterVbloodRepairRefreshSystem()
        {
            try
            {
                var world = VRCore.ServerWorld;
                if (world == null)
                {
                    Log.LogWarning("[Lifecycle] ServerWorld is null, cannot register systems");
                    return;
                }

                // Register VBlood repair refresh system
                var vbloodSystem = world.GetOrCreateSystemManaged<VBloodRepairRefreshSystem>();
                var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
                if (simGroup != null)
                {
                    simGroup.AddSystemToUpdateList(vbloodSystem);
                    simGroup.SortSystems();
                    Log.LogInfo("[Lifecycle] VBlood repair refresh system registered");
                }

                // Register Kit request system
                var kitSystem = world.GetOrCreateSystemManaged<KitRequestSystem>();
                if (simGroup != null)
                {
                    simGroup.AddSystemToUpdateList(kitSystem);
                    simGroup.SortSystems();
                    Log.LogInfo("[Lifecycle] Kit request system registered");
                }
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Lifecycle] System registration failed: {ex.Message}");
            }
        }
    }
}
