using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using VAuto.Core;

namespace VAuto.Core.Lifecycle
{
    internal static class KitRecordsService
    {
        private static readonly string RecordsPath = Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", "kit_records.json");
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public static void Record(EntityManager em, Entity character, string zoneName, string kitName)
        {
            Record(em, Entity.Null, character, zoneName, kitName);
        }

        public static void Record(EntityManager em, Entity user, Entity character, string zoneName, string kitName)
        {
            var platformId = GetPlatformId(em, user, character);
            var records = LoadRecords();
            records.Add(new KitRecord
            {
                PlatformId = platformId,
                ZoneName = zoneName,
                KitName = kitName,
                AppliedUtc = DateTime.UtcNow
            });
            SaveRecords(records);
        }

        private static List<KitRecord> LoadRecords()
        {
            try
            {
                if (!File.Exists(RecordsPath)) return new List<KitRecord>();
                var json = File.ReadAllText(RecordsPath);
                return JsonSerializer.Deserialize<List<KitRecord>>(json, Options) ?? new List<KitRecord>();
            }
            catch
            {
                return new List<KitRecord>();
            }
        }

        private static void SaveRecords(List<KitRecord> records)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RecordsPath)!);
                File.WriteAllText(RecordsPath, JsonSerializer.Serialize(records, Options));
            }
            catch
            {
                // If writing fails, we silently ignore to avoid breaking kit flow.
            }
        }

        private static ulong GetPlatformId(EntityManager em, Entity user, Entity character)
        {
            try
            {
                if (user != Entity.Null && em.Exists(user) && em.HasComponent<User>(user))
                {
                    return em.GetComponentData<User>(user).PlatformId;
                }

                if (character != Entity.Null && em.Exists(character) && em.HasComponent<User>(character))
                {
                    return em.GetComponentData<User>(character).PlatformId;
                }
            }
            catch
            {
            }

            return 0;
        }

        private sealed class KitRecord
        {
            public ulong PlatformId { get; set; }
            public string ZoneName { get; set; } = string.Empty;
            public string KitName { get; set; } = string.Empty;
            public DateTime AppliedUtc { get; set; }
        }
    }
}
