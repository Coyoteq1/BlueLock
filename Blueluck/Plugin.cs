using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
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
using Blueluck.Commands;

namespace Blueluck
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("gg.coyote.VAutomationCore", "1.1.0")]
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
        private bool _ecsServicesInitialized;
        private bool _deferredEcsHookRegistered;
        private static bool _fallbackZoneDetectionEnabled;

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

        // Flow Validation
        public static FlowValidationService FlowValidation { get; private set; }

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
        public static BossCoopService BossCoop { get; private set; }
        public static PrefabRemapService PrefabRemap { get; private set; }
        public static PrefabToGuidService PrefabToGuid { get; private set; }
        public static UnlockService Unlock { get; private set; }
        public static KitService Kits { get; private set; }
        public static bool FallbackZoneDetectionEnabled => _fallbackZoneDetectionEnabled;
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
                
                // Initialize ECS-dependent services (method checks if world is ready).
                if (!InitializeEcsDependentServices())
                {
                    RegisterDeferredEcsInitialization();
                }
                
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
                VerifyAutomationCommandBindings();
                Logger.LogInfo("[Blueluck] Commands registered successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Blueluck] Failed to register commands: {ex.Message}");
            }
        }

        private static void VerifyAutomationCommandBindings()
        {
            var required = new (Type Type, string Command)[]
            {
                (typeof(ZoneCommands), "zone status"),
                (typeof(ZoneCommands), "zone list"),
                (typeof(ZoneCommands), "zone reload"),
                (typeof(ZoneCommands), "flow reload"),
                (typeof(ZoneCommands), "zone debug"),
                (typeof(KitCommands), "kit list"),
                (typeof(KitCommands), "kit"),
                (typeof(SnapshotCommands), "snap status"),
                (typeof(SnapshotCommands), "snap save"),
                (typeof(SnapshotCommands), "snap apply"),
                (typeof(SnapshotCommands), "snap restore"),
                (typeof(SnapshotCommands), "snap clear")
            };

            var missing = new List<string>();
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
                Logger.LogInfo($"[Blueluck] Automation command verification passed ({required.Length} commands).");
            }
            else
            {
                Logger.LogWarning($"[Blueluck] Automation command verification missing: {string.Join(", ", missing)}");
            }
        }

        private void InitializeServices()
        {
            if (!GeneralEnabled.Value)
            {
                Logger.LogWarning("[Blueluck] Plugin is disabled via config.");
                return;
            }

            EnsureRequiredJsonConfigs();

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

        private void EnsureRequiredJsonConfigs()
        {
            try
            {
                var configDir = Path.Combine(Paths.ConfigPath, "Blueluck");
                Directory.CreateDirectory(configDir);

                EnsureConfigFile(
                    "zones.json",
                    IsZonesConfigValid,
                    new
                    {
                        detection = new { checkIntervalMs = 500, positionThreshold = 1.0f },
                        fxPresetList = Array.Empty<int>(),
                        zones = Array.Empty<object>()
                    });

                EnsureConfigFile(
                    "flows.json",
                    IsFlowsConfigValid,
                    new
                    {
                        flows = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    });

                EnsureConfigFile(
                    "kits.json",
                    IsKitsConfigValid,
                    new
                    {
                        kits = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    });

                EnsureConfigFile(
                    "abilities.json",
                    IsAbilitiesConfigValid,
                    new
                    {
                        sets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["arena"] = Array.Empty<string>(),
                            ["boss"] = Array.Empty<string>()
                        },
                        aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["a"] = "arena",
                            ["b"] = "boss"
                        }
                    });

                EnsureConfigFile(
                    "prefab_remap.json",
                    IsPrefabRemapConfigValid,
                    new
                    {
                        mappings = Array.Empty<object>(),
                        aliases = Array.Empty<object>()
                    });

                EnsureTextConfigFile("buffs_numbered.txt", IsBuffsNumberedValid);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Blueluck] Failed ensuring required JSON configs: {ex.Message}");
            }
        }

        internal static bool EnsureConfigFile(string fileName, Func<string, bool> validator, object fallbackPayload)
        {
            var path = Path.Combine(Paths.ConfigPath, "Blueluck", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Paths.ConfigPath);

            try
            {
                if (File.Exists(path))
                {
                    var existing = File.ReadAllText(path);
                    if (validator(existing))
                    {
                        return false;
                    }

                    BackupInvalidConfig(path);
                    Logger.LogWarning($"[Blueluck] Replacing invalid or empty config: {path}");
                }

                var content = GetEmbeddedTemplate(fileName);
                if (string.IsNullOrWhiteSpace(content))
                {
                    content = JsonSerializer.Serialize(fallbackPayload, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }

                File.WriteAllText(path, content);
                Logger.LogInfo($"[Blueluck] Wrote config template: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Blueluck] Failed ensuring config '{fileName}': {ex.Message}");
                return false;
            }
        }

        internal static bool EnsureTextConfigFile(string fileName, Func<string, bool> validator)
        {
            var path = Path.Combine(Paths.ConfigPath, "Blueluck", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Paths.ConfigPath);

            try
            {
                if (File.Exists(path))
                {
                    var existing = File.ReadAllText(path);
                    if (validator(existing))
                    {
                        return false;
                    }

                    BackupInvalidConfig(path);
                    Logger.LogWarning($"[Blueluck] Replacing invalid or empty text config: {path}");
                }

                var content = GetEmbeddedTemplate(fileName);
                if (string.IsNullOrWhiteSpace(content))
                {
                    Logger.LogWarning($"[Blueluck] No embedded template found for text config: {fileName}");
                    return false;
                }

                File.WriteAllText(path, content);
                Logger.LogInfo($"[Blueluck] Wrote text config template: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Blueluck] Failed ensuring text config '{fileName}': {ex.Message}");
                return false;
            }
        }

        private static string? GetEmbeddedTemplate(string fileName)
        {
            var resourceName = $"Blueluck.Templates.{fileName}";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static void BackupInvalidConfig(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var backupPath = $"{path}.invalid-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Copy(path, backupPath, overwrite: true);
        }

        private static bool IsZonesConfigValid(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("zones", out var zones) || zones.ValueKind != JsonValueKind.Array || zones.GetArrayLength() == 0)
            {
                return false;
            }

            if (!root.TryGetProperty("fxPresetList", out var presets) || presets.ValueKind != JsonValueKind.Array || presets.GetArrayLength() == 0)
            {
                return false;
            }

            foreach (var item in presets.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value) && value != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFlowsConfigValid(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("flows", out var flows)
                && flows.ValueKind == JsonValueKind.Object
                && flows.EnumerateObject().MoveNext();
        }

        private static bool IsKitsConfigValid(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("kits", out var kits)
                && kits.ValueKind == JsonValueKind.Object
                && kits.EnumerateObject().MoveNext();
        }

        private static bool IsAbilitiesConfigValid(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sets", out var sets)
                && sets.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("aliases", out var aliases)
                && aliases.ValueKind == JsonValueKind.Object;
        }

        private static bool IsPrefabRemapConfigValid(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("mappings", out var mappings)
                && mappings.ValueKind == JsonValueKind.Array
                && doc.RootElement.TryGetProperty("aliases", out var aliases)
                && aliases.ValueKind == JsonValueKind.Array;
        }

        private static bool IsBuffsNumberedValid(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var matches = Regex.Matches(text, @"^\s*\d+\.\s*-?\d+\s*$", RegexOptions.Multiline);
            return matches.Count >= 50;
        }

        private bool InitializeEcsDependentServices()
        {
            if (_ecsServicesInitialized)
            {
                return true;
            }

            // World-safe guard - prevent accidental early execution
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Logger.LogWarning("[Blueluck] World not ready yet - delaying ECS init.");
                return false;
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
                var ecsZoneDetectionReady =
                    EnsureSystemInSimulationGroup<ZoneDetectionSystem>(world) &&
                    EnsureSystemInSimulationGroup<ZoneTransitionRouterSystem>(world) &&
                    EnsureSystemInSimulationGroup<ZoneBorderVisualSystem>(world);
                _fallbackZoneDetectionEnabled = !ecsZoneDetectionReady;

                // Flow system
                if (FlowSystemEnabled.Value)
                {
                    FlowRegistry = new FlowRegistryService();
                    FlowRegistry.Initialize();
                    Logger.LogInfo("[Blueluck] FlowRegistry initialized");

                    // Initialize validation service after FlowRegistry
                    FlowValidation = new FlowValidationService();
                    FlowValidation.Initialize();
                    FlowValidation.SetDependencies(PrefabToGuid, Kits, ZoneConfig, FlowRegistry);
                    Logger.LogInfo("[Blueluck] FlowValidation initialized");
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

                // Boss co-op override service (PvP suppression within co-op boss zones)
                BossCoop = new BossCoopService();
                BossCoop.Initialize();
                Logger.LogInfo("[Blueluck] BossCoop initialized");

                // ZoneTransition depends on the services above for enter/exit side effects.
                ZoneTransition = new ZoneTransitionService();
                ZoneTransition.Initialize();
                Logger.LogInfo("[Blueluck] ZoneTransition initialized");

                if (ecsZoneDetectionReady)
                {
                    // Spawn zone entities only after all enter/exit dependencies are ready.
                    ZoneConfig.SpawnZoneEntitiesIfReady();
                    Logger.LogInfo("[Blueluck] Zone entities spawned for detection");
                }
                else
                {
                    Logger.LogInfo("[Blueluck] Custom ECS zone systems unavailable; using fallback zone detection.");
                }
                
                Logger.LogInfo("[Blueluck] All ECS-dependent services initialized.");
                _ecsServicesInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Blueluck] Failed to initialize ECS-dependent services: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void RegisterDeferredEcsInitialization()
        {
            if (_deferredEcsHookRegistered || _ecsServicesInitialized)
            {
                return;
            }

            _deferredEcsHookRegistered = true;

            try
            {
                HookPatchEvent("VAutomationCore.Core.Patches.ServerBootstrapSystemPatch, VAutomationCore", "OnWorldReady");
                HookPatchEvent("VAutomationCore.Core.Patches.WorldBootstrapPatch, VAutomationCore", "OnWorldInitialized");
                Logger.LogInfo("[Blueluck] Deferred ECS init hooks registered.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Blueluck] Failed to register deferred ECS init hooks: {ex.Message}");
            }
        }

        private void HookPatchEvent(string typeName, string eventName)
        {
            var patchType = Type.GetType(typeName, throwOnError: false);
            if (patchType == null)
            {
                Logger.LogWarning($"[Blueluck] Deferred init hook type not found: {typeName}");
                return;
            }

            var eventInfo = patchType.GetEvent(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (eventInfo == null)
            {
                Logger.LogWarning($"[Blueluck] Deferred init event not found: {typeName}.{eventName}");
                return;
            }

            EventHandler handler = (_, _) =>
            {
                if (_ecsServicesInitialized)
                {
                    return;
                }

                try
                {
                    if (InitializeEcsDependentServices())
                    {
                        Logger.LogInfo("[Blueluck] Deferred ECS init completed.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[Blueluck] Deferred ECS init attempt failed: {ex.Message}");
                }
            };

            eventInfo.AddEventHandler(null, handler);
        }

        private static bool EnsureSystemInSimulationGroup<T>(World world) where T : ComponentSystemBase
        {
            try
            {
                var systemType = typeof(T);
                var system = ResolveManagedSystem(world, systemType);
                var systemHandle = ResolveSystemHandle(world, systemType);
                var group = ResolveManagedSystem(world, typeof(SimulationSystemGroup)) as ComponentSystemBase;

                if (system == null && systemHandle == SystemHandle.Null)
                {
                    Logger.LogInfo($"[Blueluck] ECS system unavailable for {systemType.Name}; fallback mode will be used.");
                    return false;
                }

                if (group == null)
                {
                    Logger.LogWarning("[Blueluck] Failed to resolve SimulationSystemGroup.");
                    return false;
                }

                var addMethod = system != null
                    ? FindSingleParameterMethod(group.GetType(), "AddSystemToUpdateList", typeof(ComponentSystemBase))
                    : FindSingleParameterMethod(group.GetType(), "AddSystemToUpdateList", typeof(SystemHandle));
                if (addMethod == null)
                {
                    Logger.LogWarning($"[Blueluck] SimulationSystemGroup.AddSystemToUpdateList overload not found for {systemType.Name}.");
                    return false;
                }

                addMethod.Invoke(group, new object[] { system ?? (object)systemHandle });

                var sortMethod = FindParameterlessMethod(group.GetType(), "SortSystems");
                sortMethod?.Invoke(group, Array.Empty<object>());
                return true;
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException();
                Logger.LogWarning($"[Blueluck] Failed to register ECS system {typeof(T).Name}: {root.Message}");
                return false;
            }
        }

        private static ComponentSystemBase? ResolveManagedSystem(World world, Type systemType)
        {
            var existing = InvokeWorldSystemMethod(world, "GetExistingSystemManaged", systemType) as ComponentSystemBase;
            if (existing != null)
            {
                return existing;
            }

            var created = InvokeWorldSystemMethod(world, "GetOrCreateSystemManaged", systemType) as ComponentSystemBase;
            if (created != null)
            {
                return created;
            }

            return InvokeWorldSystemMethod(world, "CreateSystemManaged", systemType) as ComponentSystemBase;
        }

        private static SystemHandle ResolveSystemHandle(World world, Type systemType)
        {
            var existing = InvokeWorldSystemMethod(world, "GetExistingSystem", systemType);
            if (existing is SystemHandle existingHandle && existingHandle != SystemHandle.Null)
            {
                return existingHandle;
            }

            var created = InvokeWorldSystemMethod(world, "GetOrCreateSystem", systemType);
            if (created is SystemHandle createdHandle && createdHandle != SystemHandle.Null)
            {
                return createdHandle;
            }

            var fallback = InvokeWorldSystemMethod(world, "CreateSystem", systemType);
            return fallback is SystemHandle fallbackHandle ? fallbackHandle : SystemHandle.Null;
        }

        private static object? InvokeWorldSystemMethod(World world, string methodName, Type systemType)
        {
            var il2CppType = Il2CppType.From(systemType, throwOnFailure: false);
            if (il2CppType == null)
            {
                return null;
            }

            var methods = world.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.FullName == "Il2CppSystem.Type")
                {
                    return method.Invoke(world, new object[] { il2CppType });
                }
            }

            return null;
        }

        private static MethodInfo? FindSingleParameterMethod(Type type, string name, Type parameterType)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(parameterType))
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo? FindParameterlessMethod(Type type, string name)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (string.Equals(method.Name, name, StringComparison.Ordinal) && method.GetParameters().Length == 0)
                {
                    return method;
                }
            }

            return null;
        }

        private void CleanupServices()
        {
            // Cleanup in reverse order
            BossCoop?.Cleanup();
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
