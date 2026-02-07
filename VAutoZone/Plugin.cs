using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using ProjectM;
using ProjectM.Network;
using System;
using VAuto.Zone.Services;

namespace VAuto.Zone
{
    [BepInPlugin("gg.coyote.VAutomationZone", "VAutoZone", "1.0.0")]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.0")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInDependency("gg.coyote.lifecycle", "1.0.0")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        #region Logging
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource("VAutoZone");
        public static ManualLogSource Logger => _staticLog;
        #endregion

        public static Plugin Instance { get; private set; }
        
        private Harmony _harmony;
        
        public Plugin()
        {
            Instance = this;
        }

        public override void Load()
        {
            _harmony = new Harmony("gg.coyote.VAutomationZone");
            _harmony.PatchAll(typeof(Patches));
            
            // Register commands
            CommandRegistry.RegisterAll();
            
            Logger.LogInfo("VAutoZone loaded!");
        }

        public void Start()
        {
            try
            {
                ArenaTerritory.InitializeArenaGrid();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Territory init: {ex.Message}");
            }
        }

        public void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    public static class Patches
    {
    }
}
