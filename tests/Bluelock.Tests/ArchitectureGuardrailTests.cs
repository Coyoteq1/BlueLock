using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VAutomationCore.Core.Api;
using VAutomationCore.Core.Data;
using VAutomationCore.Core.Events;
using VAutomationCore.Core.Lifecycle;
using Xunit;

namespace Bluelock.Tests
{
    public class ArchitectureGuardrailTests
    {
        [Fact]
        public void CoreContracts_ArePresent()
        {
            Assert.NotNull(typeof(IZoneLifecycleContext));
            Assert.NotNull(typeof(IZoneEnterStep));
            Assert.NotNull(typeof(IZoneExitStep));
            Assert.NotNull(typeof(IZoneLifecycleStepRegistry));
            Assert.NotNull(typeof(TypedEventBus));
        }

        [Fact]
        public void CoreReleaseContracts_ArePresent()
        {
            Assert.NotNull(typeof(ConsoleRoleAuthService));
            Assert.NotNull(typeof(EntityAliasMapper));
            Assert.NotNull(typeof(EntityMap));
            Assert.NotNull(typeof(FlowService));
            Assert.NotNull(typeof(ConsoleRoleComponent));
        }

        [Fact]
        public void PluginProjectReferences_FollowGuardrails()
        {
            var repoRoot = ResolveRepoRoot();
            var pluginProjects = new[]
            {
                Path.Combine(repoRoot, "Bluelock", "VAutoZone.csproj"),
                Path.Combine(repoRoot, "CycleBorn", "Vlifecycle.csproj"),
                Path.Combine(repoRoot, "VAutoannounce", "VAutoannounce.csproj"),
                Path.Combine(repoRoot, "VAutoTraps", "VAutoTraps.csproj")
            };

            var allowedByProject = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["VAutoZone.csproj"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "../VAutomationCore.csproj"
                },
                ["Vlifecycle.csproj"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "../VAutomationCore.csproj"
                },
                ["VAutoannounce.csproj"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "..\\VAutomationCore.csproj",
                    "../VAutomationCore.csproj"
                },
                ["VAutoTraps.csproj"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "../VAutomationCore.csproj"
                }
            };

            foreach (var projectPath in pluginProjects)
            {
                Assert.True(File.Exists(projectPath), $"Project file missing: {projectPath}");
                var fileName = Path.GetFileName(projectPath);
                Assert.True(allowedByProject.TryGetValue(fileName, out var allowed), $"No guardrail rule for {fileName}");

                var lines = File.ReadAllLines(projectPath);
                var projectRefs = lines
                    .Select(line => line.Trim())
                    .Where(line => line.StartsWith("<ProjectReference Include=", StringComparison.OrdinalIgnoreCase))
                    .Select(line => ExtractIncludeValue(line))
                    .Select(NormalizeProjectReferencePath)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();

                foreach (var projectRef in projectRefs)
                {
                    Assert.Contains(projectRef, allowed.Select(NormalizeProjectReferencePath));
                }
            }
        }

        [Fact]
        public void RepositoryRoot_RejectsClutterAndUnexpectedSourceFiles()
        {
            var repoRoot = ResolveRepoRoot();

            var rootLogs = Directory.GetFiles(repoRoot, "*.log", SearchOption.TopDirectoryOnly);
            Assert.Empty(rootLogs);

            var rootZips = Directory.GetFiles(repoRoot, "*.zip", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            var allowedRootZips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "LICENSE.zip",
                "VAutomationCore.zip",
                "VAutomationCore-1.0.1.zip",
                "VAutomationZone-1.0.1.zip"
            };

            foreach (var zip in rootZips)
            {
                Assert.Contains(zip, allowedRootZips);
            }

            var allowedRootCs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GlobalUsings.cs",
                "IService.cs",
                "MyPluginInfo.cs",
                "PlayerAPI.cs",
                "Plugin.cs",
                "PrefabGuidConverter.cs",
                "VAutoLogger.cs"
            };

            var rootCs = Directory.GetFiles(repoRoot, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            foreach (var fileName in rootCs)
            {
                Assert.Contains(fileName, allowedRootCs);
            }
        }

        [Fact]
        public void Repository_RejectsExtractorDependencyPatterns()
        {
            var repoRoot = ResolveRepoRoot();
            var disallowedPatterns = new[]
            {
                "KindredExtract",
                "ComponentExtractors",
                "EntityDebug.RegisterExtractor"
            };

            var disallowed = new List<string>();
            foreach (var file in EnumerateTextFiles(repoRoot))
            {
                var relPath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (string.Equals(relPath, "tests/Bluelock.Tests/ArchitectureGuardrailTests.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++;
                    foreach (var pattern in disallowedPatterns)
                    {
                        if (!line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        disallowed.Add($"{relPath}:{lineNumber}:{pattern}");
                    }
                }
            }

            Assert.True(disallowed.Count == 0,
                "Disallowed extractor/dependency patterns found:\n" + string.Join('\n', disallowed));
        }

        [Fact]
        public void CorePluginIdentity_AndIntegrationTouchpoints_AreConsistent()
        {
            var repoRoot = ResolveRepoRoot();
            var pluginPath = Path.Combine(repoRoot, "Plugin.cs");
            var infoPath = Path.Combine(repoRoot, "MyPluginInfo.cs");

            Assert.True(File.Exists(pluginPath), "Root Plugin.cs is missing.");
            Assert.True(File.Exists(infoPath), "Root MyPluginInfo.cs is missing.");

            var pluginText = File.ReadAllText(pluginPath);
            var infoText = File.ReadAllText(infoPath);

            // Identity: root plugin attribute must use explicit identity constants.
            Assert.Contains("[BepInPlugin(\"gg.coyote.VAutomationCore\", \"VAutomationCore\", \"1.0.1\")]", pluginText);

            // Bind/settings: config file naming should stay aligned with plugin GUID identity contract.
            Assert.Contains("public const string GUID = \"gg.coyote.VAutomationCore\";", infoText);
            Assert.Contains("private const string ConfigFileName = \"gg.coyote.VAutomationCore.cfg\";", pluginText);
            Assert.Contains("_configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, ConfigFileName), true);", pluginText);

            // Integration touchpoints requested for compatibility checks.
            Assert.Contains("Core/Services/SandboxSnapshotStore.cs", Directory.GetFiles(repoRoot, "SandboxSnapshotStore.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/')));
            Assert.Contains("Core/Services/Sandbox/SnapshotCaptureService.cs", Directory.GetFiles(repoRoot, "SnapshotCaptureService.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/')));
            Assert.Contains("Core/Services/Sandbox/SnapshotDiffService.cs", Directory.GetFiles(repoRoot, "SnapshotDiffService.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/')));
            Assert.Contains("Core/Api/FlowService.cs", Directory.GetFiles(repoRoot, "FlowService.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/')));
            Assert.Contains("config/VAuto.unified_config.schema.json", Directory.GetFiles(Path.Combine(repoRoot, "config"), "*.json", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/')));

            var commandFiles = Directory.GetFiles(Path.Combine(repoRoot, "Core", "Commands"), "*.cs", SearchOption.AllDirectories);
            Assert.True(commandFiles.Length > 0, "Core command surface is unexpectedly empty.");
        }

        [Fact]
        public void AllPlugins_FollowIdentityConsistency()
        {
            var repoRoot = ResolveRepoRoot();
            
            // Define expected patterns for each plugin
            var plugins = new[]
            {
                new { Name = "Bluelock", Folder = "Bluelock", ExpectedGuid = "gg.coyote.BlueLock" },
                new { Name = "VAutoannounce", Folder = "VAutoannounce", ExpectedGuid = "gg.coyote.VAutoannounce" },
                new { Name = "VAutoTraps", Folder = "VAutoTraps", ExpectedGuid = "gg.coyote.VAutomationtrap" },
                new { Name = "CycleBorn", Folder = "CycleBorn", ExpectedGuid = "gg.coyote.lifecycle" }
            };

            foreach (var plugin in plugins)
            {
                var pluginFolder = Path.Combine(repoRoot, plugin.Folder);
                if (!Directory.Exists(pluginFolder))
                    continue; // Skip if plugin folder doesn't exist in this workspace

                var pluginPath = Path.Combine(pluginFolder, "Plugin.cs");
                var infoPath = Path.Combine(pluginFolder, "MyPluginInfo.cs");

                Assert.True(File.Exists(pluginPath), $"{plugin.Name}: Plugin.cs missing");
                Assert.True(File.Exists(infoPath), $"{plugin.Name}: MyPluginInfo.cs missing");

                var pluginText = File.ReadAllText(pluginPath);
                var infoText = File.ReadAllText(infoPath);

                // Check MyPluginInfo has GUID constant
                Assert.True(infoText.Contains("public const string GUID = ", StringComparison.OrdinalIgnoreCase),
                    $"{plugin.Name}: GUID constant missing in MyPluginInfo.cs");
                
                // Check GUID value matches expected
                var expectedGuidLine = $"public const string GUID = \"{plugin.ExpectedGuid}\";";
                Assert.True(infoText.Contains(expectedGuidLine, StringComparison.OrdinalIgnoreCase),
                    $"{plugin.Name}: Expected GUID \"{plugin.ExpectedGuid}\" not found");

                // Check BepInPlugin attribute uses MyPluginInfo constants (or direct GUID that matches)
                var hasBepInPlugin = pluginText.Contains("[BepInPlugin(") || 
                    pluginText.Contains($"\"{plugin.ExpectedGuid}\"");
                Assert.True(hasBepInPlugin, $"{plugin.Name}: [BepInPlugin] attribute missing or incorrect");
            }
        }

        private static string ResolveRepoRoot()
        {
            var current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                var candidate = Path.Combine(current, "VAutomationCore.csproj");
                if (File.Exists(candidate))
                {
                    return current;
                }

                var parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }

                current = parent.FullName;
            }

            throw new DirectoryNotFoundException("Failed to resolve repository root from test base directory.");
        }

        private static string ExtractIncludeValue(string trimmedProjectReferenceLine)
        {
            const string marker = "Include=\"";
            var start = trimmedProjectReferenceLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return string.Empty;
            }

            start += marker.Length;
            var end = trimmedProjectReferenceLine.IndexOf('"', start);
            if (end < 0 || end <= start)
            {
                return string.Empty;
            }

            return trimmedProjectReferenceLine.Substring(start, end - start);
        }

        private static string NormalizeProjectReferencePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = raw.Trim();
            while (normalized.Contains("\\\\", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("\\\\", "\\", StringComparison.Ordinal);
            }

            return normalized.Replace('\\', '/');
        }

        private static IEnumerable<string> EnumerateTextFiles(string repoRoot)
        {
            static bool ShouldSkipDirectory(string path)
            {
                var normalized = path.Replace('\\', '/');
                return normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/artifacts/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/out/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/_bepinex_out/", StringComparison.OrdinalIgnoreCase);
            }

            var stack = new Stack<string>();
            stack.Push(repoRoot);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    if (ShouldSkipDirectory(dir))
                    {
                        continue;
                    }

                    stack.Push(dir);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    var name = Path.GetFileName(file);
                    if (name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return file;
                    }
                }
            }
        }
    }
}
