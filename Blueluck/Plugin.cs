using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Unity.Entities;
using VampireCommandFramework;
using VAuto.Services.Interfaces;
using VAutomationCore;
using VAutomationCore.Core;
using VAutomationCore.Core.Lifecycle;
using VAutomationCore.Core.Logging;
using VAutomationCore.Services;
using Blueluck.Services;
using Blueluck.Systems;

namespace Blueluck
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("gg.coyote.VAutomationCore", "1.0.1")]
    [BepInDependency("gg.deca.VampireCommandFramework", "0.10.4")]
    [BepInProcess("VRisingServer.exe")]
    public class Plugin : BasePlugin
    {
        #region Logging
        private static readonly ManualLogSource _staticLog = BepInEx.Logging.Logger.CreateLogSource("Blueluck");
        public static ManualLogSource Logger => _staticLog;
        public static CoreLogger CoreLog { get; private set; }
        #endregion

        public static Plugin Instance { get; private set; }
        
        private Harmony _harmony;

        #region Config Entries
        // General
        public static ConfigEntry<bool> GeneralEnabled;
        public static ConfigEntry<string> LogLevel;
        
        // Detection
        public static ConfigEntry<int> ZoneDetectionCheckIntervalMs;
        public static ConfigEntry<float> ZoneDetectionPositionThreshold;
        public static ConfigEntry<bool> ZoneDetectionDebugMode;
        
        // Flow System
        public static ConfigEntry<bool> FlowSystemEnabled;

        // Kits
        public static ConfigEntry<bool> KitsEnabled;

        // Progress save/restore (per-zone flags in the zones config opt-in to Save/Restore)
        public static ConfigEntry<bool> ProgressEnabled;

        // Abilities (server-side ability loadouts as buffs)
        public static ConfigEntry<bool> AbilitiesEnabled;
        #endregion

        #region Services
        // Zone transitions are driven by ECS systems (ZoneDetectionSystem + ZoneTransitionRouterSystem).
        public static ZoneConfigService ZoneConfig { get; private set; }
        public static ZoneTransitionService ZoneTransition { get; private set; }
        public static FlowRegistryService FlowRegistry { get; private set; }
        public static ProgressService Progress { get; private set; }
        public static AbilityService Abilities { get; private set; }
        public static PrefabRemapService PrefabRemap { get; private set; }
        public static PrefabToGuidService PrefabToGuid { get; private set; }
        public static UnlockService Unlock { get; private set; }
        public static KitService Kits { get; private set; }
        #endregion

        public override void Load()
        {
            Instance = this;
            CoreLog = new CoreLogger("Blueluck");

            try
            {
                InitializeConfig();
                RegisterCommands();
                InitializeServices();
                
                _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                _harmony.PatchAll(typeof(Plugin).Assembly);
                
                // Initialize ECS-dependent services (method checks if world is ready)
                InitializeEcsDependentServices();
                
                CoreLog.LogInfo("[Blueluck] Plugin loaded successfully.");
            }
            catch (Exception ex)
            {
                CoreLog.LogError($"[Blueluck] Failed to load: {ex.Message}");
                throw;
            }
        }

        public override bool Unload()
        {
            try
            {
                CleanupServices();
                // Harmony 2.2+: prefer instance-scoped unpatching.
                _harmony?.UnpatchSelf();
                Logger.LogInfo("[Blueluck] Plugin unloaded.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Blueluck] Error during unload: {ex.Message}\n{ex.StackTrace}");
            }

            return true;
        }

        private void InitializeConfig()
        {
            var config = Config;
            
            // General
            GeneralEnabled = config.Bind("General", "Enabled", true, "Enable Blueluck functionality");
            LogLevel = config.Bind("General", "LogLevel", "Info", "Logging level (Debug, Info, Warning, Error)");
            
            // Detection
            ZoneDetectionCheckIntervalMs = config.Bind("Detection", "CheckIntervalMs", 500, "Zone detection check interval in milliseconds");
            ZoneDetectionPositionThreshold = config.Bind("Detection", "PositionThreshold", 1.0f, "Position change threshold for detection");
            ZoneDetectionDebugMode = config.Bind("Detection", "DebugMode", false, "Enable debug logging for zone detection");
            
            // Flow System
            FlowSystemEnabled = config.Bind("Flow", "Enabled", true, "Enable flow system");

            // Kits
            KitsEnabled = config.Bind("Kits", "Enabled", true, "Enable kit system (kits.json) for zone transitions and commands");

            // Progress
            ProgressEnabled = config.Bind("Progress", "Enabled", true, "Enable progress save/restore when zones request it");

            // Abilities
            AbilitiesEnabled = config.Bind("Abilities", "Enabled", true, "Enable ability loadouts (abilities.json) for zones");

            Logger.LogInfo("[Blueluck] Configuration initialized.");
        }

        private void RegisterCommands()
        {
            try
            {
                CommandRegistry.RegisterAll(Assembly.GetExecutingAssembly());
                Logger.LogInfo("[Blueluck] Commands registered successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Blueluck] Failed to register commands: {ex.Message}");
            }
        }

        private void InitializeServices()
        {
            if (!GeneralEnabled.Value)
            {
                Logger.LogWarning("[Blueluck] Plugin is disabled via config.");
                return;
            }

            // Initialize non-ECS services first (these can initialize immediately)
            try
            {
                // Core config service (no ECS dependency)
                ZoneConfig = new ZoneConfigService();
                ZoneConfig.Initialize();
                
                // Prefab remap service (no ECS dependency - uses static data)
                PrefabRemap = new PrefabRemapService();
                PrefabRemap.Initialize();
                
                Logger.LogInfo("[Blueluck] Non-ECS services initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Blueluck] Failed to initialize non-ECS services: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void InitializeEcsDependentServices()
        {
            // World-safe guard - prevent accidental early execution
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Logger.LogWarning("[Blueluck] World not ready yet - delaying ECS init.");
                return;
            }
            
            // Initialize ECS-dependent services after world is ready
            try
            {
                // PrefabToGuid service (requires ECS world)
                PrefabToGuid = new PrefabToGuidService();
                PrefabToGuid.Initialize();
                Logger.LogInfo("[Blueluck] PrefabToGuid initialized");
                
                // Unlock service (requires ECS world)
                Unlock = new UnlockService();
                Unlock.Initialize();
                Logger.LogInfo("[Blueluck] Unlock initialized");

                // Kits service (requires ECS world for DebugEventsSystem)
                if (KitsEnabled.Value)
                {
                    Kits = new KitService();
                    Kits.Initialize();
                    Logger.LogInfo("[Blueluck] Kits initialized");
                }

                // Ensure ECS systems exist and are in an update group (mod assemblies are not always auto-bootstrapped).
                EnsureSystemInSimulationGroup<ZoneDetectionSystem>(world);
                EnsureSystemInSimulationGroup<ZoneTransitionRouterSystem>(world);
                EnsureSystemInSimulationGroup<ZoneBorderVisualSystem>(world);
                
                // ZoneTransition service (driven by ECS ZoneTransitionEvent router)
                ZoneTransition = new ZoneTransitionService();
                ZoneTransition.Initialize();
                Logger.LogInfo("[Blueluck] ZoneTransition initialized");
                
                // Flow system
                if (FlowSystemEnabled.Value)
                {
                    FlowRegistry = new FlowRegistryService();
                    FlowRegistry.Initialize();
                    Logger.LogInfo("[Blueluck] FlowRegistry initialized");
                }

                // Progress service
                if (ProgressEnabled.Value)
                {
                    Progress = new ProgressService();
                    Progress.Initialize();
                    Logger.LogInfo("[Blueluck] Progress initialized");
                }

                // Abilities service
                if (AbilitiesEnabled.Value)
                {
                    Abilities = new AbilityService();
                    Abilities.Initialize();
                    Logger.LogInfo("[Blueluck] Abilities initialized");
                }
                
                Logger.LogInfo("[Blueluck] All ECS-dependent services initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Blueluck] Failed to initialize ECS-dependent services: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private static void EnsureSystemInSimulationGroup<T>(World world) where T : ComponentSystemBase
        {
            try
            {
                var system = world.GetOrCreateSystemManaged<T>();
                var group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

                // Use reflection to avoid hard-depending on a specific Entities API surface.
                var addMethod = group.GetType().GetMethod("AddSystemToUpdateList", new[] { typeof(ComponentSystemBase) });
                addMethod?.Invoke(group, new object[] { system });

                var sortMethod = group.GetType().GetMethod("SortSystems");
                sortMethod?.Invoke(group, Array.Empty<object>());
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Blueluck] Failed to register ECS system {typeof(T).Name}: {ex.Message}");
            }
        }

        private void CleanupServices()
        {
            // Cleanup in reverse order
            Abilities?.Cleanup();
            Progress?.Cleanup();
            FlowRegistry?.Cleanup();
            ZoneTransition?.Cleanup();
            Unlock?.Cleanup();
            Kits?.Cleanup();
            PrefabToGuid?.Cleanup();
            PrefabRemap?.Cleanup();
            ZoneConfig?.Cleanup();
            
            Logger.LogInfo("[Blueluck] Services cleaned up.");
        }

        #region Helper Methods
        public static void LogInfo(string message) => Logger.LogInfo($"[Blueluck] {message}");
        public static void LogWarning(string message) => Logger.LogWarning($"[Blueluck] {message}");
        public static void LogError(string message) => Logger.LogError($"[Blueluck] {message}");
        public static void LogDebug(string message) => Logger.LogDebug($"[Blueluck] {message}");
        #endregion
    }
}
