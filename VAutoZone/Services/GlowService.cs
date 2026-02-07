using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Core;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service for managing glow buff choices with named presets.
    /// </summary>
    public class GlowService
    {
        private static readonly string ConfigPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto.Zone");
        private static readonly string GlowChoicesPath = Path.Combine(ConfigPath, "glowChoices.txt");

        private readonly Dictionary<string, PrefabGUID> _glowChoices = new Dictionary<string, PrefabGUID>(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<PrefabGUID, string> _prefabToGlowName = new Dictionary<PrefabGUID, string>();

        public GlowService()
        {
            InitializeDefaults();
            LoadGlowChoices();
        }

        private void InitializeDefaults()
        {
            // Glow buff prefabs
            _glowChoices["InkShadow"] = new PrefabGUID(-1124645803);
            _glowChoices["Cursed"] = new PrefabGUID(1425734039);
            _glowChoices["Howl"] = new PrefabGUID(-91451769);
            _glowChoices["Chaos"] = new PrefabGUID(1163490655);
            _glowChoices["Emerald"] = new PrefabGUID(-1559874083);
            _glowChoices["Poison"] = new PrefabGUID(-1965215729);
            _glowChoices["Agony"] = new PrefabGUID(1025643444);
            _glowChoices["Light"] = new PrefabGUID(178225731);
            
            // Decoration prefabs for border spawning
            _glowChoices["Table3x3Cabal"] = new PrefabGUID("TM_Castle_ObjectDecor_Table_3x3_Cabal01".GetHashCode());
            _glowChoices["ChairCabal"] = new PrefabGUID("TM_Castle_ObjectDecor_Chair_01_Cabal01".GetHashCode());
            _glowChoices["Barrel01"] = new PrefabGUID("NM_Castle_Prop_Barrel_01".GetHashCode());
            _glowChoices["Crate01"] = new PrefabGUID("NM_Castle_Prop_Crate_01".GetHashCode());
            _glowChoices["Fireplace"] = new PrefabGUID("NM_Castle_Prop_Fireplace_01".GetHashCode());
            _glowChoices["Banner01"] = new PrefabGUID("NM_Castle_Deco_Banner_01".GetHashCode());
        }

        public void SaveGlowChoices()
        {
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);

            var sb = new StringBuilder();
            foreach (var entry in _glowChoices)
            {
                sb.AppendLine($"{entry.Key}={entry.Value.GuidHash}");
            }
            File.WriteAllText(GlowChoicesPath, sb.ToString());
        }

        public void LoadGlowChoices()
        {
            if (!File.Exists(GlowChoicesPath))
            {
                BuildPrefabToGlowName();
                return;
            }

            _glowChoices.Clear();
            InitializeDefaults();

            var lines = File.ReadAllLines(GlowChoicesPath);
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[1], out var guid))
                {
                    var prefabGuid = new PrefabGUID(guid);
                    if (VRCore.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(prefabGuid, out var entity))
                    {
                        if (VRCore.EntityManager.HasComponent<Buff>(entity))
                        {
                            _glowChoices[parts[0]] = prefabGuid;
                        }
                    }
                }
            }

            BuildPrefabToGlowName();
        }

        private void BuildPrefabToGlowName()
        {
            _prefabToGlowName.Clear();
            foreach (var entry in _glowChoices)
            {
                _prefabToGlowName[entry.Value] = entry.Key;
            }
        }

        public void AddNewGlowChoice(PrefabGUID prefab, string name)
        {
            _glowChoices[name] = prefab;
            _prefabToGlowName[prefab] = name;
            SaveGlowChoices();
        }

        public bool RemoveGlowChoice(string name)
        {
            if (_glowChoices.Remove(name))
            {
                SaveGlowChoices();
                return true;
            }
            return false;
        }

        public PrefabGUID GetGlowPrefab(string name)
        {
            if (_glowChoices.TryGetValue(name, out var guid))
            {
                return guid;
            }
            return default;
        }

        public IEnumerable<(string name, PrefabGUID prefab)> ListGlowChoices()
        {
            foreach (var entry in _glowChoices)
            {
                yield return (entry.Key, entry.Value);
            }
        }

        public string GetGlowName(PrefabGUID guid)
        {
            return _prefabToGlowName.TryGetValue(guid, out var name) ? name : null;
        }
    }
}
