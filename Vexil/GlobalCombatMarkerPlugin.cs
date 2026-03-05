using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Unity.Entities;
using VampireCommandFramework;
using VAutomationCore.Core;
using Vexil;

namespace GlobalCombatMarker
{
    [BepInPlugin(MyPluginInfo.GUID, MyPluginInfo.NAME, MyPluginInfo.VERSION)]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.1")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class GlobalCombatMarkerPlugin : BasePlugin
    {
        private Harmony _harmony;
        private static DateTime _nextDeferredWorldNotReadyLogUtc = DateTime.MinValue;
        private static bool _ecsInitializationDeferred;

        public override void Load()
        {
            _harmony = new Harmony("gg.coyote.vexil");
            _harmony.PatchAll();

            CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());

            InitializeEcsSystems();

            Log.LogInfo("Global Combat Marker plugin loaded.");
        }

        public override bool Unload()
        {
            _harmony?.UnpatchSelf();
            return true;
        }

        private void InitializeEcsSystems()
        {
            try
            {
                var serverWorld = UnifiedCore.Server;
                if (serverWorld == null || !serverWorld.IsCreated)
                {
                    var now = DateTime.UtcNow;
                    if (now >= _nextDeferredWorldNotReadyLogUtc)
                    {
                        Log.LogInfo("[Vexil] Server world not ready during Load(); deferring ECS initialization.");
                        _nextDeferredWorldNotReadyLogUtc = now.AddSeconds(10);
                    }

                    _ecsInitializationDeferred = true;
                    return;
                }

                _ecsInitializationDeferred = false;
                _ = serverWorld.GetOrCreateSystemManaged<GlobalCombatMarkerSystem>();

                Log.LogInfo("[Vexil] ECS GlobalCombatMarkerSystem initialized.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[Vexil] Failed to initialize ECS system: {ex.Message}");
            }
        }
    }
}
