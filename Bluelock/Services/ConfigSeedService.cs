using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace VAuto.Zone.Services
{
    internal static class ConfigSeedService
    {
        internal enum SeedResultStatus
        {
            Created,
            Upgraded,
            SkippedCustomized,
            SkippedUpToDate,
            Failed
        }

        internal sealed class SeedSummary
        {
            public SeedSummary(string category, bool autoSeedRequested, bool forceMode)
            {
                Category = category;
                AutoSeedRequested = autoSeedRequested;
                ForceMode = forceMode;
            }

            public string Category { get; }
            public bool AutoSeedRequested { get; }
            public bool ForceMode { get; }
            public bool Disabled => !AutoSeedRequested;

            public int Created { get; private set; }
            public int Upgraded { get; private set; }
            public int SkippedCustomized { get; private set; }
            public int SkippedUpToDate { get; private set; }
            public int Failed { get; private set; }

            public int Skipped => SkippedCustomized + SkippedUpToDate;

            public void Record(SeedResultStatus status)
            {
                switch (status)
                {
                    case SeedResultStatus.Created:
                        Created++;
                        break;
                    case SeedResultStatus.Upgraded:
                        Upgraded++;
                        break;
                    case SeedResultStatus.SkippedCustomized:
                        SkippedCustomized++;
                        break;
                    case SeedResultStatus.SkippedUpToDate:
                        SkippedUpToDate++;
                        break;
                    case SeedResultStatus.Failed:
                        Failed++;
                        break;
                }
            }
        }

        internal static SeedSummary EnsureDatabaseFiles(string baseConfigPath, bool autoSeedEnabled, bool forceSeed)
        {
            return EnsureFiles(baseConfigPath, "database", DefaultConfigManifest.DatabaseEntries, autoSeedEnabled, forceSeed);
        }

        internal static SeedSummary EnsureFlowFiles(string baseConfigPath, bool autoSeedEnabled, bool forceSeed)
        {
            return EnsureFiles(baseConfigPath, "flows", DefaultConfigManifest.FlowEntries, autoSeedEnabled, forceSeed);
        }

        private static SeedSummary EnsureFiles(
            string baseConfigPath,
            string category,
            IReadOnlyList<DefaultConfigManifest.DefaultConfigEntry> entries,
            bool autoSeedEnabled,
            bool forceSeed)
        {
            var summary = new SeedSummary(category, autoSeedEnabled, forceSeed);
            if (!autoSeedEnabled)
            {
                return summary;
            }

            var targetRoot = Path.Combine(baseConfigPath, category);
            Directory.CreateDirectory(targetRoot);

            foreach (var entry in entries)
            {
                var targetPath = Path.Combine(baseConfigPath, entry.LogicalPath.Replace('/', Path.DirectorySeparatorChar));
                var status = EnsureFile(entry, targetPath, forceSeed);
                summary.Record(status);
            }

            return summary;
        }

        private static SeedResultStatus EnsureFile(DefaultConfigManifest.DefaultConfigEntry entry, string targetPath, bool forceSeed)
        {
            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(targetPath))
                {
                    WriteEmbeddedResource(entry.ResourceName, targetPath);
                    Plugin.Logger.LogInfo($"[BlueLock][AutoSeed][{entry.Category}] {entry.LogicalPath} created (manifest {entry.VersionTag})");
                    return SeedResultStatus.Created;
                }

                if (!IsJsonValid(targetPath))
                {
                    BackupFile(targetPath, "broken");
                    WriteEmbeddedResource(entry.ResourceName, targetPath);
                    Plugin.Logger.LogWarning($"[BlueLock][AutoSeed][{entry.Category}] {entry.LogicalPath} invalid JSON detected; restored template.");
                    return SeedResultStatus.Upgraded;
                }

                if (!forceSeed)
                {
                    if (CheckHashMatches(entry, targetPath))
                    {
                        return SeedResultStatus.SkippedUpToDate;
                    }

                    Plugin.Logger.LogInfo($"[BlueLock][AutoSeed][{entry.Category}] {entry.LogicalPath} skipped (customized).");
                    return SeedResultStatus.SkippedCustomized;
                }

                BackupFile(targetPath, "bak");
                WriteEmbeddedResource(entry.ResourceName, targetPath);
                Plugin.Logger.LogInfo($"[BlueLock][AutoSeed][{entry.Category}] {entry.LogicalPath} upgraded (manifest {entry.VersionTag}).");
                return SeedResultStatus.Upgraded;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[BlueLock][AutoSeed][{entry.Category}] {entry.LogicalPath} failed: {ex.Message}");
                return SeedResultStatus.Failed;
            }
        }

        private static void WriteEmbeddedResource(string resourceName, string targetPath)
        {
            using var resourceStream = typeof(ConfigSeedService).Assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
            }

            using var fileStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            resourceStream.CopyTo(fileStream);
        }

        private static bool IsJsonValid(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var json = JsonDocument.Parse(stream);
                return json.RootElement.ValueKind != JsonValueKind.Undefined;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckHashMatches(DefaultConfigManifest.DefaultConfigEntry entry, string targetPath)
        {
            using var fileStream = File.OpenRead(targetPath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fileStream);
            var hashString = Convert.ToHexString(hash);
            return string.Equals(hashString, entry.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        private static void BackupFile(string path, string suffix)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
            var backupPath = path + $".{suffix}.{timestamp}";
            File.Copy(path, backupPath, overwrite: true);
        }
    }
}
