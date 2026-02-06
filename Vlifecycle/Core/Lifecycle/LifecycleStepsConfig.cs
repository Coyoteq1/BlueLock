using System;
using System.IO;
using System.Text.Json;

namespace VAuto.Core.Lifecycle
{
    public sealed class LifecycleStepsConfig
    {
        public bool TeleportOnEnter { get; set; } = true;
        public bool ApplyKit { get; set; } = true;
        public bool ApplySpellbooks { get; set; } = true;
        public bool OpenSpellbookUi { get; set; } = true;
        public bool UnlockVBloods { get; set; } = true;
        public bool SpawnZoneGlows { get; set; } = true;
        public bool SaveSnapshotOnEnter { get; set; } = true;
        public bool RestoreSnapshotOnExit { get; set; } = true;
        public bool ClearInventoryOnRestore { get; set; } = true;
    }

    public static class LifecycleStepsPolicy
    {
        private static readonly string ConfigPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "LifecycleSteps.json");

        public static LifecycleStepsConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<LifecycleStepsConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    }) ?? new LifecycleStepsConfig();
                    return Normalize(cfg);
                }
            }
            catch
            {
                // ignore and recreate
            }

            var def = Normalize(new LifecycleStepsConfig());
            Save(def);
            return def;
        }

        public static void Save(LifecycleStepsConfig cfg)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static LifecycleStepsConfig Normalize(LifecycleStepsConfig cfg)
        {
            if (cfg.UnlockVBloods)
                cfg.OpenSpellbookUi = false;
            if (!cfg.ApplySpellbooks)
                cfg.OpenSpellbookUi = false;
            return cfg;
        }
    }
}
