using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Bluelock.Tests
{
    public class ProjectCompileContractTests
    {
        [Fact]
        public void Bluelock_Project_DoesNotExcludeSystems_WhenPluginReferencesSystemsNamespace()
        {
            var repoRoot = ResolveRepoRoot();
            var pluginPath = Path.Combine(repoRoot, "Bluelock", "Plugin.cs");
            var csprojPath = Path.Combine(repoRoot, "Bluelock", "VAutoZone.csproj");

            Assert.True(File.Exists(pluginPath), "Bluelock Plugin.cs missing.");
            Assert.True(File.Exists(csprojPath), "Bluelock VAutoZone.csproj missing.");

            var pluginText = File.ReadAllText(pluginPath);
            var csprojText = File.ReadAllText(csprojPath);

            var referencesSystemsNamespace = pluginText.Contains("VAuto.Zone.Systems", StringComparison.Ordinal);
            var excludesSystemsCompile = csprojText.Contains("<Compile Remove=\"Systems\\**\\*.cs\"", StringComparison.OrdinalIgnoreCase);

            if (referencesSystemsNamespace)
            {
                Assert.False(excludesSystemsCompile,
                    "VAutoZone.csproj excludes Systems/** while Plugin.cs references VAuto.Zone.Systems.*.");
            }
        }

        [Fact]
        public void ZoneTransitionEvent_HasSingleOwnerConsumerContract()
        {
            var repoRoot = ResolveRepoRoot();
            var routerPath = Path.Combine(repoRoot, "Bluelock", "Systems", "ZoneTransitionRouterSystem.cs");
            var flowPath = Path.Combine(repoRoot, "Bluelock", "Systems", "FlowExecutionSystem.cs");
            var templatePath = Path.Combine(repoRoot, "Bluelock", "Systems", "ZoneTemplateLifecycleSystem.cs");

            Assert.True(File.Exists(routerPath), "ZoneTransitionRouterSystem.cs missing.");
            Assert.True(File.Exists(flowPath), "FlowExecutionSystem.cs missing.");
            Assert.True(File.Exists(templatePath), "ZoneTemplateLifecycleSystem.cs missing.");

            var routerText = File.ReadAllText(routerPath);
            var flowText = File.ReadAllText(flowPath);
            var templateText = File.ReadAllText(templatePath);

            Assert.Contains("DestroyEntity(evtEntity)", routerText, StringComparison.Ordinal);
            Assert.DoesNotContain("DestroyEntity(evtEntity)", flowText, StringComparison.Ordinal);
            Assert.DoesNotContain("DestroyEntity(evtEntity)", templateText, StringComparison.Ordinal);
        }

        [Fact]
        public void ZoneTransitionEvent_IsConsumedOnlyByRouterSystem()
        {
            var repoRoot = ResolveRepoRoot();
            var offenders = new List<string>();
            var sourceFiles = Directory.GetFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase));

            foreach (var file in sourceFiles)
            {
                var rel = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                var text = File.ReadAllText(file);
                if (!text.Contains("ZoneTransitionEvent", StringComparison.Ordinal))
                {
                    continue;
                }

                if (rel.StartsWith("tests/Bluelock.Tests/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rel.Equals("Bluelock/Systems/ZoneDetectionSystem.cs", StringComparison.OrdinalIgnoreCase) ||
                    rel.Equals("Bluelock/Systems/ZoneTransitionRouterSystem.cs", StringComparison.OrdinalIgnoreCase) ||
                    rel.Equals("Core/ECS/Components/ZoneTransitionEvent.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (text.Contains("GetComponentData<ZoneTransitionEvent>", StringComparison.Ordinal) ||
                    text.Contains("ToComponentDataArray<ZoneTransitionEvent>", StringComparison.Ordinal) ||
                    text.Contains("RequireForUpdate<ZoneTransitionEvent>", StringComparison.Ordinal))
                {
                    offenders.Add(rel);
                }
            }

            Assert.True(offenders.Count == 0, "Only router may consume ZoneTransitionEvent. Offenders: " + string.Join(", ", offenders));
        }

        [Fact]
        public void FlowExecutionSystem_UsesDeterministicResolutionChain_AndOptionalGlow()
        {
            var repoRoot = ResolveRepoRoot();
            var flowPath = Path.Combine(repoRoot, "Bluelock", "Systems", "FlowExecutionSystem.cs");
            Assert.True(File.Exists(flowPath), "FlowExecutionSystem.cs missing.");

            var flowText = File.ReadAllText(flowPath);
            Assert.Contains("BuildFlowResolutionCandidates", flowText, StringComparison.Ordinal);

            var baseCandidate = flowText.IndexOf("flowId.Trim() + \".\" + direction", StringComparison.Ordinal);
            var zoneCandidate = flowText.IndexOf("\"zone.\" + direction + \".\" + zoneId.Trim()", StringComparison.Ordinal);
            var defaultCandidate = flowText.IndexOf("\"zone.\" + direction + \".default\"", StringComparison.Ordinal);
            Assert.True(baseCandidate >= 0, "Missing base flow candidate pattern.");
            Assert.True(zoneCandidate >= 0, "Missing zone-specific flow candidate pattern.");
            Assert.True(defaultCandidate >= 0, "Missing default flow candidate pattern.");
            Assert.True(baseCandidate < zoneCandidate && zoneCandidate < defaultCandidate,
                "Flow resolution candidates must be ordered as base, zone-specific, default.");

            Assert.Contains("if (!FlowService.TryGetFlow(glowFlowId, out _))", flowText, StringComparison.Ordinal);
        }

        [Fact]
        public void ExcludedArenaCommands_AreNotDocumentedAsActiveRuntimeSurface()
        {
            var repoRoot = ResolveRepoRoot();
            var csprojPath = Path.Combine(repoRoot, "Bluelock", "VAutoZone.csproj");
            var readmePath = Path.Combine(repoRoot, "README.md");
            var docsReadmePath = Path.Combine(repoRoot, "docs", "README.md");
            var pluginPath = Path.Combine(repoRoot, "Bluelock", "Plugin.cs");

            Assert.True(File.Exists(csprojPath), "Bluelock csproj missing.");
            Assert.True(File.Exists(readmePath), "README.md missing.");
            Assert.True(File.Exists(docsReadmePath), "docs/README.md missing.");
            Assert.True(File.Exists(pluginPath), "Bluelock/Plugin.cs missing.");

            var csproj = File.ReadAllText(csprojPath);
            var readme = File.ReadAllText(readmePath);
            var docsReadme = File.ReadAllText(docsReadmePath);
            var plugin = File.ReadAllText(pluginPath);

            var arenaCommandsExcluded = csproj.Contains("<Compile Remove=\"Commands\\Core\\ArenaEcsCommands.cs\"",
                StringComparison.OrdinalIgnoreCase);
            Assert.True(arenaCommandsExcluded, "Expected ArenaEcsCommands.cs to be excluded by csproj.");

            Assert.DoesNotContain("Command Roots: zone, arena", plugin, StringComparison.OrdinalIgnoreCase);

            if (readme.Contains(".arena", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains("excluded by build", readme, StringComparison.OrdinalIgnoreCase);
            }

            if (docsReadme.Contains(".arena", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains("excluded by build", docsReadme, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Bluelock_LifecycleModel_HasSingleRuntimeSource()
        {
            var repoRoot = ResolveRepoRoot();
            var pluginPath = Path.Combine(repoRoot, "Bluelock", "Plugin.cs");
            var legacyModelPath = Path.Combine(repoRoot, "Bluelock", "Models", "ZoneLifecycleConfig.cs");

            Assert.True(File.Exists(pluginPath), "Bluelock/Plugin.cs missing.");
            var pluginText = File.ReadAllText(pluginPath);
            Assert.Contains("class ZoneJsonConfig", pluginText, StringComparison.Ordinal);

            // Minimal-touch mode keeps Plugin.cs runtime model authoritative.
            Assert.False(File.Exists(legacyModelPath),
                "Legacy lifecycle model file should be removed to avoid duplicate runtime model sources.");
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
