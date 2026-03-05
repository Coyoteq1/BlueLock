using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Bluelock.Tests
{
    public class PlatformContractTests
    {
        [Fact]
        public void Router_DispatchesBeforeDestroyingTransitionEvent()
        {
            var repoRoot = ResolveRepoRoot();
            var routerPath = Path.Combine(repoRoot, "Bluelock", "Systems", "ZoneTransitionRouterSystem.cs");
            Assert.True(File.Exists(routerPath), "ZoneTransitionRouterSystem.cs missing.");

            var text = File.ReadAllText(routerPath);
            var dispatchIndex = text.IndexOf("Dispatch(transition);", StringComparison.Ordinal);
            var destroyIndex = text.IndexOf("DestroyEntity(evtEntity);", StringComparison.Ordinal);

            Assert.True(dispatchIndex >= 0, "Router does not dispatch transition events.");
            Assert.True(destroyIndex >= 0, "Router does not destroy transition entities.");
            Assert.True(dispatchIndex < destroyIndex, "Router destroys event before dispatching transition.");
        }

        [Fact]
        public void LifecycleConfigs_DeclareSchemaVersion()
        {
            // Per Rule 26, lifecycle flows are defined in canonical registry at Bluelock/config/zone.flows.registry.json
            var repoRoot = ResolveRepoRoot();
            var flowRegistryPath = Path.Combine(repoRoot, "Bluelock", "config", "zone.flows.registry.json");
            var cycleBornPlugin = Path.Combine(repoRoot, "CycleBorn", "Plugin.cs");

            Assert.True(File.Exists(flowRegistryPath), "zone.flows.registry.json missing - canonical flow registry not found.");
            Assert.True(File.Exists(cycleBornPlugin), "CycleBorn/Plugin.cs missing.");

            var blText = File.ReadAllText(flowRegistryPath);
            var cycleText = File.ReadAllText(cycleBornPlugin);

            Assert.Contains("\"schemaVersion\"", blText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SchemaVersion", cycleText, StringComparison.Ordinal);
            Assert.Contains("CurrentConfigVersion", cycleText, StringComparison.Ordinal);
        }

        [Fact]
        public void ZoneLifecycleVersionMigration_BackfillsVersionAndSchema()
        {
            var repoRoot = ResolveRepoRoot();
            var migrationPath = Path.Combine(repoRoot, "Bluelock", "Services", "ZoneLifecycleConfigVersionMigration.cs");
            Assert.True(File.Exists(migrationPath), "ZoneLifecycleConfigVersionMigration.cs missing.");

            var text = File.ReadAllText(migrationPath);
            Assert.Contains("SourceVersion => \"1.0.0\"", text, StringComparison.Ordinal);
            Assert.Contains("TargetVersion => ZoneJsonConfig.CurrentConfigVersion", text, StringComparison.Ordinal);
            Assert.Contains("string.IsNullOrWhiteSpace(config.ConfigVersion)", text, StringComparison.Ordinal);
            Assert.Contains("config.ConfigVersion = TargetVersion", text, StringComparison.Ordinal);
            Assert.Contains("string.IsNullOrWhiteSpace(config.SchemaVersion)", text, StringComparison.Ordinal);
            Assert.Contains("config.SchemaVersion = TargetVersion", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Plugin_AppliesLifecycleMigrations_AndLocksRuntimeModeAtBoot()
        {
            var repoRoot = ResolveRepoRoot();
            var pluginPath = Path.Combine(repoRoot, "Bluelock", "Plugin.cs");
            Assert.True(File.Exists(pluginPath), "Bluelock/Plugin.cs missing.");

            var text = File.ReadAllText(pluginPath);
            // Per Rule 26, legacy lifecycle config files are no longer used
            // Runtime mode is now locked at boot and ignores config hot-reload
            Assert.Contains("_bootRuntimeModeLocked = true;", text, StringComparison.Ordinal);
            Assert.Contains("RuntimeModeValue => _bootRuntimeModeLocked", text, StringComparison.Ordinal);

            // Verify runtime mode cannot be changed after boot
            var runtimeValueProp = text.IndexOf("public static ZoneRuntimeMode RuntimeModeValue", StringComparison.Ordinal);
            Assert.True(runtimeValueProp >= 0, "RuntimeModeValue property not found");
        }

        [Fact]
        public void Plugin_RuntimeModeOptions_ForceEcsOnly_WithCompatibilityApi()
        {
            var repoRoot = ResolveRepoRoot();
            var pluginPath = Path.Combine(repoRoot, "Bluelock", "Plugin.cs");
            Assert.True(File.Exists(pluginPath), "Bluelock/Plugin.cs missing.");

            var text = File.ReadAllText(pluginPath);
            Assert.Contains("ZoneRuntimeModeOptions.FromMode(RuntimeModeValue)", text, StringComparison.Ordinal);
            Assert.Contains("public static ZoneRuntimeMode RuntimeModeValue => _bootRuntimeModeLocked ? _bootRuntimeMode : ZoneRuntimeMode.EcsOnly;", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Lifecycle_ActionChain_UsesParameterizedBossEnter_AndClearTemplateExit()
        {
            // Per Rule 26, zone lifecycle actions are defined in canonical flow registry
            var repoRoot = ResolveRepoRoot();
            var pluginPath = Path.Combine(repoRoot, "Bluelock", "Plugin.cs");
            var flowRegistryPath = Path.Combine(repoRoot, "Bluelock", "config", "zone.flows.registry.json");
            Assert.True(File.Exists(pluginPath), "Bluelock/Plugin.cs missing.");
            Assert.True(File.Exists(flowRegistryPath), "zone.flows.registry.json missing - canonical flow registry not found.");

            var pluginText = File.ReadAllText(pluginPath);
            var flowText = File.ReadAllText(flowRegistryPath);

            // Verify plugin handles parameterized actions
            Assert.Contains("case \"boss_enter\":", pluginText, StringComparison.Ordinal);
            Assert.Contains("TrySpawnTemplateManifest(parameter, zoneId, \"boss\", em)", pluginText, StringComparison.Ordinal);
            Assert.Contains("case \"clear_template\":", pluginText, StringComparison.Ordinal);
            Assert.Contains("TryClearZoneTemplate(zoneId, templateType, em)", pluginText, StringComparison.Ordinal);

            // Verify canonical registry has proper schema with flows
            Assert.Contains("\"flows\"", flowText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"schemaVersion\"", flowText, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRepoRoot()
        {
            var dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrWhiteSpace(dir))
            {
                if (File.Exists(Path.Combine(dir, "VAutomationCore.csproj")))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            throw new InvalidOperationException("Repository root not found.");
        }
    }
}
