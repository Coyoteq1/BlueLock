using System;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using Stunlock.Core;
using Unity.Collections;
using Unity.Mathematics;
using VAuto.Zone.Core;
using VAuto.Zone.Models;

namespace VAuto.Zone.Services
{
    public static class TemplateRepository
    {
        private static readonly string TemplatesRoot = Path.Combine(Paths.ConfigPath, "Bluelock", "templates");
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static bool TryLoadTemplate(string templateName, out TemplateSnapshot template)
        {
            template = null;
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return false;
            }

            Directory.CreateDirectory(TemplatesRoot);
            var sanitized = SanitizeTemplateName(templateName);
            var path = Path.Combine(TemplatesRoot, $"{sanitized}.json");
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<TemplateSnapshot>(json, JsonOptions);
                if (loaded == null)
                {
                    return false;
                }

                loaded.Name = loaded.Name.Length == 0 ? templateName : loaded.Name;
                template = loaded;
                return true;
            }
            catch (Exception ex)
            {
                ZoneCore.LogWarning($"[TemplateRepository] Failed to load template '{templateName}': {ex.Message}");
                return false;
            }
        }

        private static string SanitizeTemplateName(string templateName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                templateName = templateName.Replace(c, '_');
            }

            return templateName;
        }
    }
}
