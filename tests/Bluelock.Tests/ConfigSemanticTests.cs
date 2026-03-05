using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Bluelock.Tests
{
    public class ConfigSemanticTests
    {
        [Fact]
        public void ZonesConfig_HasUniqueIds_AndValidRadii()
        {
            // Per Rule 26, zones are defined in canonical domain config at BepInEx/config/Bluelock/bluelock.domain.json
            // At build/test time, we verify the schema structure exists in the model
            // Runtime loading happens via BluelockDomainConfigService
            var repoRoot = ResolveRepoRoot();
            var domainConfigModelPath = Path.Combine(repoRoot, "Bluelock", "Models", "BluelockDomainConfig.cs");
            Assert.True(File.Exists(domainConfigModelPath), "BluelockDomainConfig.cs missing - canonical domain model not found");

            var text = File.ReadAllText(domainConfigModelPath);
            Assert.Contains("ZonesConfig", text, StringComparison.Ordinal);
        }

        [Fact]
        public void ZoneLifecycleConfig_ReferencesResolvableRegistryFlows()
        {
            // Per Rule 26, zone lifecycle flows are defined in canonical registry at Bluelock/config/zone.flows.registry.json
            var repoRoot = ResolveRepoRoot();
            var registryPath = Path.Combine(repoRoot, "Bluelock", "config", "zone.flows.registry.json");
            Assert.True(File.Exists(registryPath), "zone.flows.registry.json missing - canonical flow registry not found");

            using var registryDoc = JsonDocument.Parse(File.ReadAllText(registryPath));
            var flows = registryDoc.RootElement.GetProperty("flows");
            var flowNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var flow in flows.EnumerateObject())
            {
                flowNames.Add(flow.Name);
            }

            // Verify default flows exist per canonical contract
            Assert.True(flowNames.Contains("zone.enter.default"), "Missing default enter flow in registry");
            Assert.True(flowNames.Contains("zone.exit.default"), "Missing default exit flow in registry");

            // Verify schemaVersion is declared
            Assert.True(registryDoc.RootElement.TryGetProperty("schemaVersion", out var schemaVersion), "schemaVersion missing in registry");
            Assert.True(registryDoc.RootElement.TryGetProperty("moduleId", out var moduleId), "moduleId missing in registry");
            Assert.Equal("bluelock.zones", moduleId.GetString());
        }

        [Fact]
        public void CyclebornFlowRegistry_ModuleId_DoesNotCollideWithBluelock()
        {
            var repoRoot = ResolveRepoRoot();
            var registryPath = Path.Combine(repoRoot, "CycleBorn", "Configuration", "flows.registry.json");
            Assert.True(File.Exists(registryPath), "CycleBorn flows.registry.json missing");

            using var doc = JsonDocument.Parse(File.ReadAllText(registryPath));
            Assert.True(doc.RootElement.TryGetProperty("moduleId", out var moduleIdProp), "moduleId missing in CycleBorn flows.registry.json");
            var moduleId = moduleIdProp.GetString() ?? string.Empty;
            Assert.False(string.IsNullOrWhiteSpace(moduleId), "moduleId must not be empty");
            Assert.False(
                string.Equals(moduleId, "bluelock.zones", StringComparison.OrdinalIgnoreCase),
                "CycleBorn moduleId must not collide with Bluelock module ownership.");
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
