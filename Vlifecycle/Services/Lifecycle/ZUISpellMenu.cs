using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VLifecycle.Services.Lifecycle
{
    /// <summary>
    /// ZUI Spell Menu for arena spellbook management.
    /// Provides spell categorization, favorites, and quick access menus.
    /// </summary>
    public static class ZUISpellMenu
    {
        private static bool _isInitialized = false;
        private static Type _zuiApi;
        private static readonly Dictionary<string, List<SpellEntry>> _spellCategories = new();
        private static readonly List<string> _favoriteSpells = new();

        public struct SpellEntry
        {
            public string Name;
            public string PrefabGuid;
            public string Category;
            public bool IsFavorite;
        }

        /// <summary>
        /// Initialize ZUI spell menu - sets up reflection to ZUI API.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // Get ZUI Assembly
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "ZUI");

                if (assembly != null)
                {
                    _zuiApi = assembly.GetType("ZUI.API.ZUI");
                    Plugin.Log.LogInfo("[ZUISpellMenu] ZUI API found - spell menu ready");
                }
                else
                {
                    Plugin.Log.LogWarning("[ZUISpellMenu] ZUI not found - using fallback commands only");
                }

                // Initialize spell categories
                InitializeSpellCategories();
                
                _isInitialized = true;
                Plugin.Log.LogInfo("[ZUISpellMenu] Initialized");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[ZUISpellMenu] Init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize default spell categories.
        /// </summary>
        private static void InitializeSpellCategories()
        {
            _spellCategories.Clear();

            // Default categories
            _spellCategories["Combat"] = new List<SpellEntry>();
            _spellCategories["Support"] = new List<SpellEntry>();
            _spellCategories["Utility"] = new List<SpellEntry>();
            _spellCategories["Favorites"] = new List<SpellEntry>();
        }

        /// <summary>
        /// Register a spell in the menu system.
        /// </summary>
        public static void RegisterSpell(string name, string prefabGuid, string category, bool isFavorite = false)
        {
            var entry = new SpellEntry
            {
                Name = name,
                PrefabGuid = prefabGuid,
                Category = category,
                IsFavorite = isFavorite
            };

            if (!_spellCategories.ContainsKey(category))
            {
                _spellCategories[category] = new List<SpellEntry>();
            }

            _spellCategories[category].Add(entry);

            if (isFavorite)
            {
                _favoriteSpells.Add(name);
            }
        }

        /// <summary>
        /// Open the spell menu for a player.
        /// </summary>
        public static void OpenSpellMenu(string playerName)
        {
            if (_zuiApi != null)
            {
                Call("SetPlugin", new object[] { "VLifecycle" });
                Call("SetTargetWindow", new object[] { $"SpellMenu_{playerName}" });
                Call("SetUI", new object[] { 600, 500 });
                Call("HideTitleBar", Array.Empty<object>());
                Call("SetTitle", new object[] { "<color=#B30000>Arena Spell Menu</color>" });

                // Add categories as buttons
                Call("AddButton", new object[] { "Combat", ".spell category combat", 15f, 450f, 80f, 30f });
                Call("AddButton", new object[] { "Support", ".spell category support", 105f, 450f, 80f, 30f });
                Call("AddButton", new object[] { "Utility", ".spell category utility", 195f, 450f, 80f, 30f });
                Call("AddButton", new object[] { "Favorites", ".spell category favorites", 285f, 450f, 80f, 30f });

                // Render spells
                RenderSpellCategory("Combat", 410);
                RenderSpellCategory("Support", 370);
                RenderSpellCategory("Utility", 330);
                RenderSpellCategory("Favorites", 290);

                Call("Open", Array.Empty<object>());
            }
            else
            {
                Plugin.Log.LogInfo($"[ZUISpellMenu] Would open spell menu for {playerName} (ZUI not available)");
            }
        }

        /// <summary>
        /// Render a spell category section.
        /// </summary>
        private static void RenderSpellCategory(string category, int yOffset)
        {
            if (!_spellCategories.TryGetValue(category, out var spells) || spells.Count == 0)
            {
                return;
            }

            int currentY = yOffset;
            int xOffset = 15;

            foreach (var spell in spells.Take(7))
            {
                var cmd = $".spell cast \"{spell.Name}\"";
                Call("AddButton", new object[] { spell.Name, cmd, xOffset, currentY, 80f, 25f });
                xOffset += 85;

                if (xOffset > 520)
                {
                    xOffset = 15;
                    currentY -= 30;
                }
            }
        }

        /// <summary>
        /// Add a spell to favorites.
        /// </summary>
        public static void ToggleFavorite(string spellName)
        {
            if (_favoriteSpells.Contains(spellName))
            {
                _favoriteSpells.Remove(spellName);
            }
            else
            {
                _favoriteSpells.Add(spellName);
            }
        }

        /// <summary>
        /// Get all registered spells.
        /// </summary>
        public static List<SpellEntry> GetAllSpells()
        {
            return _spellCategories.Values
                .SelectMany(x => x)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Get spells by category.
        /// </summary>
        public static List<SpellEntry> GetSpellsByCategory(string category)
        {
            return _spellCategories.TryGetValue(category, out var spells) 
                ? spells 
                : new List<SpellEntry>();
        }

        /// <summary>
        /// Call ZUI API method via reflection.
        /// </summary>
        private static void Call(string methodName, object[] args)
        {
            if (_zuiApi == null) return;

            try
            {
                var method = _zuiApi.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length);

                if (method != null)
                {
                    method.Invoke(null, args);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[ZUISpellMenu] ZUI call failed: {ex.Message}");
            }
        }
    }
}
