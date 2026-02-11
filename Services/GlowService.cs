using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Zone.Core;
using VAutomationCore;
using VAutomationCore.Core.ECS;

namespace VAuto.Zone.Services
{
    /// <summary>
    /// Service for managing glow buff choices with named presets.
    /// 
    /// IMPORTANT: This service manages TWO types of glows:
    /// 1. Buff Glows - Applied via AddBuff (use Buffs.AddBuff pattern)
    /// 2. World Glows - Instantiated as entities (carpets, decorations)
    /// 
    /// Using the wrong spawn method will cause the glow to fail or not replicate properly.
    /// </summary>
    public class GlowService
    {
        private static readonly string GlowChoicesFileName = "glowChoices.txt";

        // Buff glow prefabs - these should be applied via AddBuff, NOT instantiated
        private static readonly Dictionary<string, PrefabGUID> BuffGlowPrefabs = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase)
        {
            { "InkShadow", new PrefabGUID(-1124645803) },
            { "Cursed", new PrefabGUID(1425734039) },
            { "Howl", new PrefabGUID(-91451769) },
            { "Chaos", new PrefabGUID(1163490655) },
            { "Emerald", new PrefabGUID(-1559874083) },
            { "Poison", new PrefabGUID(-1965215729) },
            { "Agony", new PrefabGUID(1025643444) },
            { "Light", new PrefabGUID(178225731) },
        };

        // World decoration prefabs for manual border spawning (fallback when glow prefabs fail)
        // These can be instantiated as entities
        private static readonly Dictionary<string, PrefabGUID> WorldGlowPrefabs = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase)
        {
            // Note: These require actual game prefab GUIDs, not hash codes
            // Add known GUIDs here, or remove entries if not available
            // Example: { "BlackCarpet", new PrefabGUID(123456789) },
        };

        private readonly Dictionary<string, PrefabGUID> _glowChoices = new Dictionary<string, PrefabGUID>(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<PrefabGUID, string> _prefabToGlowName = new Dictionary<PrefabGUID, string>();
        private readonly HashSet<PrefabGUID> _buffGlowPrefabs = new HashSet<PrefabGUID>();
        private readonly HashSet<PrefabGUID> _worldGlowPrefabs = new HashSet<PrefabGUID>();

        // Cached delegate for prefab lookup - avoid reflection on every call
        private static Func<PrefabGUID, Entity> _cachedPrefabLookup;

        private string ConfigPath => ArenaTerritory.GetPreferredConfigPath();
        private string GlowChoicesPath => Path.Combine(ConfigPath, GlowChoicesFileName);

        public GlowService()
        {
            // Initialize category tracking
            foreach (var prefab in BuffGlowPrefabs.Values)
            {
                _buffGlowPrefabs.Add(prefab);
            }
            foreach (var prefab in WorldGlowPrefabs.Values)
            {
                _worldGlowPrefabs.Add(prefab);
            }

            InitializeDefaults();
            LoadGlowChoices();
        }

        private void InitializeDefaults()
        {
            // Add all buff glows to choices
            foreach (var kvp in BuffGlowPrefabs)
            {
                _glowChoices[kvp.Key] = kvp.Value;
            }
            
            // Add world prefabs (commented out - need actual GUIDs)
            // foreach (var kvp in WorldGlowPrefabs)
            // {
            //     _glowChoices[kvp.Key] = kvp.Value;
            // }

            // NOTE: The following entries using GetHashCode() were REMOVED because:
            // string.GetHashCode() is NOT stable across runtimes/sessions
            // PrefabGUIDs derived this way will silently fail or spawn wrong prefabs
            // 
            // If you have actual PrefabGUID values for these decorations, add them to WorldGlowPrefabs
            // with proper int values, e.g.:
            // { "Table3x3Cabal", new PrefabGUID(123456789) },
        }

        /// <summary>
        /// Gets the cached prefab lookup delegate, initializing if needed.
        /// Uses reflection once and caches the result.
        /// </summary>
        private static Func<PrefabGUID, Entity> GetPrefabLookup()
        {
            if (_cachedPrefabLookup != null)
                return _cachedPrefabLookup;

            try
            {
                var prefabSystem = ZoneCore.Server?.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (prefabSystem == null)
                {
                    ZoneCore.LogWarning("[GlowService] PrefabCollectionSystem not available");
                    _cachedPrefabLookup = _ => Entity.Null;
                    return _cachedPrefabLookup;
                }

                // Try to get the dictionary via reflection
                var field = typeof(PrefabCollectionSystem).GetField(
                    "_PrefabGuidToEntityDictionary",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    var dictionary = field.GetValue(prefabSystem) as System.Collections.IDictionary;
                    if (dictionary != null)
                    {
                        _cachedPrefabLookup = guid =>
                        {
                            if (dictionary.Contains(guid))
                                return (Entity)dictionary[guid];
                            return Entity.Null;
                        };
                        ZoneCore.LogInfo("[GlowService] Prefab lookup cached successfully");
                        return _cachedPrefabLookup;
                    }
                }

                // Fallback: try alternate field name
                var altField = typeof(PrefabCollectionSystem).GetField(
                    "_PrefabGuidToEntityMap",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (altField != null)
                {
                    var dictionary = altField.GetValue(prefabSystem) as System.Collections.IDictionary;
                    if (dictionary != null)
                    {
                        _cachedPrefabLookup = guid =>
                        {
                            if (dictionary.Contains(guid))
                                return (Entity)dictionary[guid];
                            return Entity.Null;
                        };
                        ZoneCore.LogInfo("[GlowService] Prefab lookup cached (alternate field)");
                        return _cachedPrefabLookup;
                    }
                }

                ZoneCore.LogWarning("[GlowService] Could not find prefab dictionary field");
                _cachedPrefabLookup = _ => Entity.Null;
                return _cachedPrefabLookup;
            }
            catch (Exception ex)
            {
                ZoneCore.LogException("[GlowService] Failed to cache prefab lookup", ex);
                _cachedPrefabLookup = _ => Entity.Null;
                return _cachedPrefabLookup;
            }
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
            ZoneCore.LogInfo("Glow choices saved");
        }

        public void LoadGlowChoices()
        {
            if (!File.Exists(GlowChoicesPath))
            {
                BuildPrefabToGlowName();
                return;
            }

            // Guard: Check ZoneCore initialization before accessing PrefabCollection
            if (!IsVRCoreInitialized())
            {
                // ZoneCore not ready, use defaults only
                BuildPrefabToGlowName();
                ZoneCore.LogWarning("VRCore not initialized, using default glow choices");
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
                    if (ValidateGlowPrefab(prefabGuid, out var isBuff))
                    {
                        _glowChoices[parts[0]] = prefabGuid;
                        if (isBuff.HasValue)
                        {
                            if (isBuff.Value)
                                _buffGlowPrefabs.Add(prefabGuid);
                            else
                                _worldGlowPrefabs.Add(prefabGuid);
                        }
                    }
                }
            }

            BuildPrefabToGlowName();
            ZoneCore.LogInfo($"Loaded {_glowChoices.Count} glow choices");
        }

        /// <summary>
        /// Validates if a prefab is a valid glow (Buff or World) and returns its type.
        /// </summary>
        private bool ValidateGlowPrefab(PrefabGUID prefabGuid, out bool? isBuff)
        {
            isBuff = null;
            
            var entity = GetPrefabLookup()(prefabGuid);
            if (entity == Entity.Null)
            {
                ZoneCore.LogWarning($"[GlowService] Prefab {prefabGuid.Name()} not found in collection");
                return false;
            }

            var em = ZoneCore.EntityManager;
            
            // Check if it's a buff prefab
            if (em.HasComponent<Buff>(entity))
            {
                isBuff = true;
                return true;
            }
            
            // Check if it can be instantiated as a world object
            if (em.HasComponent<PrefabGUID>(entity))
            {
                isBuff = false;
                return true;
            }

            ZoneCore.LogWarning($"[GlowService] Prefab {prefabGuid.Name()} is neither a Buff nor instantiable");
            return false;
        }

        private static bool IsVRCoreInitialized()
        {
            try
            {
                return ZoneCore.Server != null && 
                       ZoneCore.Server.IsCreated;
            }
            catch
            {
                return false;
            }
        }

        private void BuildPrefabToGlowName()
        {
            _prefabToGlowName.Clear();
            foreach (var entry in _glowChoices)
            {
                _prefabToGlowName[entry.Value] = entry.Key;
            }
        }

        /// <summary>
        /// Determines if a glow prefab is a Buff type (applied via AddBuff) or World type (instantiated).
        /// </summary>
        public bool IsBuffGlow(PrefabGUID prefabGuid)
        {
            // Check cached categories first
            if (_buffGlowPrefabs.Contains(prefabGuid))
                return true;
            if (_worldGlowPrefabs.Contains(prefabGuid))
                return false;

            // Fallback: check the entity
            var em = ZoneCore.EntityManager;
            var lookup = GetPrefabLookup();
            var entity = lookup(prefabGuid);
            
            if (em.Exists(entity) && em.HasComponent<Buff>(entity))
            {
                _buffGlowPrefabs.Add(prefabGuid);
                return true;
            }

            _worldGlowPrefabs.Add(prefabGuid);
            return false;
        }

        public void AddNewGlowChoice(PrefabGUID prefab, string name)
        {
            _glowChoices[name] = prefab;
            _prefabToGlowName[prefab] = name;
            SaveGlowChoices();
            ZoneCore.LogInfo($"Added new glow choice: {name}");
        }

        public bool RemoveGlowChoice(string name)
        {
            if (_glowChoices.Remove(name))
            {
                SaveGlowChoices();
                ZoneCore.LogInfo($"Removed glow choice: {name}");
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

        /// <summary>
        /// Gets a glow prefab entity, properly handling Buff vs World types.
        /// Returns Entity.Null if not found.
        /// </summary>
        public bool TryGetGlowEntity(PrefabGUID prefabGuid, out Entity entity)
        {
            entity = Entity.Null;
            
            var lookup = GetPrefabLookup();
            entity = lookup(prefabGuid);
            
            if (entity == Entity.Null)
            {
                ZoneCore.LogWarning($"[GlowService] Glow prefab {prefabGuid.Name()} entity not found");
                return false;
            }

            // Guard: verify entity still exists
            var em = ZoneCore.EntityManager;
            if (!em.Exists(entity))
            {
                ZoneCore.LogWarning($"[GlowService] Glow prefab entity is invalid");
                entity = Entity.Null;
                return false;
            }

            return true;
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

        /// <summary>
        /// Gets a world glow prefab for manual border spawning.
        /// Returns default if not found or is a buff type.
        /// </summary>
        public PrefabGUID GetWorldGlowPrefab(string name)
        {
            if (WorldGlowPrefabs.TryGetValue(name, out var prefab) && !prefab.IsEmpty())
            {
                return prefab;
            }
            return default;
        }

        /// <summary>
        /// Lists available world glow prefabs for border spawning.
        /// </summary>
        public IEnumerable<(string name, PrefabGUID prefab)> ListWorldGlowChoices()
        {
            foreach (var entry in WorldGlowPrefabs)
            {
                if (!entry.Value.IsEmpty())
                {
                    yield return (entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Spawns a world glow prefab at the specified position.
        /// Use this for carpets and other world decoration prefabs.
        /// 
        /// IMPORTANT: This is for WORLD prefabs only. Buff prefabs should use AddBuff pattern.
        /// </summary>
        public bool SpawnWorldGlow(PrefabGUID worldPrefab, float3 position, quaternion rotation, out Entity spawnedEntity, out string error)
        {
            spawnedEntity = Entity.Null;
            error = string.Empty;
            
            var em = ZoneCore.EntityManager;

            if (worldPrefab.IsEmpty())
            {
                error = "World glow prefab is empty";
                return false;
            }

            if (!TryGetGlowEntity(worldPrefab, out var prefabEntity))
            {
                error = $"World glow prefab {worldPrefab.Name()} not found";
                return false;
            }

            // Guard: verify entity exists
            if (!em.Exists(prefabEntity))
            {
                error = "Prefab entity no longer exists";
                return false;
            }

            try
            {
                spawnedEntity = em.Instantiate(prefabEntity);

                // Set position - support both LocalTransform and Translation
                if (em.HasComponent<LocalTransform>(spawnedEntity))
                {
                    var transform = LocalTransform.FromPositionRotation(position, rotation);
                    em.SetComponentData(spawnedEntity, transform);
                }
                else if (em.HasComponent<Translation>(spawnedEntity))
                {
                    em.SetComponentData(spawnedEntity, new Translation { Value = position });
                    if (em.HasComponent<Rotation>(spawnedEntity))
                    {
                        em.SetComponentData(spawnedEntity, new Rotation { Value = rotation });
                    }
                }
                else
                {
                    ZoneCore.LogWarning($"[GlowService] Spawned entity has no position component");
                }

                ZoneCore.LogInfo($"[GlowService] Spawned world glow {worldPrefab.Name()} at {position}");
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to spawn world glow: {ex.Message}";
                ZoneCore.LogException(error, ex);
                return false;
            }
        }

        /// <summary>
        /// Tries to spawn a glow prefab, with fallback to world prefab if it's a buff type.
        /// Returns the prefab that was spawned.
        /// </summary>
        public bool TrySpawnWithFallback(PrefabGUID glowPrefab, PrefabGUID worldFallbackPrefab, float3 position, quaternion rotation, out PrefabGUID spawnedPrefab, out string error)
        {
            error = string.Empty;
            spawnedPrefab = default;

            // Validate the prefab
            if (glowPrefab.IsEmpty())
            {
                error = "Glow prefab is empty";
                return false;
            }

            // Check if it's a buff or world prefab
            var isBuff = IsBuffGlow(glowPrefab);

            if (isBuff)
            {
                // Buff prefabs should be applied, not instantiated
                error = $"Cannot instantiate buff prefab {glowPrefab.Name()} - use AddBuff pattern instead";
                ZoneCore.LogWarning($"[GlowService] {error}");
                
                // Try fallback to world prefab
                if (!worldFallbackPrefab.IsEmpty())
                {
                    return SpawnWorldGlow(worldFallbackPrefab, position, rotation, out var entity, out error);
                }
                
                return false;
            }

            // It's a world prefab, try to spawn it
            if (SpawnWorldGlow(glowPrefab, position, rotation, out var spawnedEntity, out error))
            {
                spawnedPrefab = glowPrefab;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the count of available glow choices.
        /// </summary>
        public int Count => _glowChoices.Count;

        /// <summary>
        /// Gets whether a glow choice with the specified name exists.
        /// </summary>
        public bool HasGlowChoice(string name) => _glowChoices.ContainsKey(name);
    }
}
