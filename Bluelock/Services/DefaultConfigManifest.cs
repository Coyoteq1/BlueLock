using System;
using System.Collections.Generic;

namespace VAuto.Zone.Services
{
    internal static class DefaultConfigManifest
    {
        public const string ManifestVersion = "2026.02.28";

        public static readonly IReadOnlyList<DefaultConfigEntry> DatabaseEntries = new[]
        {
            new DefaultConfigEntry(
                "database/abilities.json",
                "VAuto.Zone.Assets.Database.abilities.json",
                "72E19C75F9F8ACE593D9805D09F2D44F97F2D9E4AC807A591E733F310B896DFC",
                "db-abilities-1"),
            new DefaultConfigEntry(
                "database/armors.json",
                "VAuto.Zone.Assets.Database.armors.json",
                "3BD0AE91B43F7CB92C596AAD70415F057AAAC579C70484BD5F3FB59BBE1F6971",
                "db-armors-1"),
            new DefaultConfigEntry(
                "database/buffs.json",
                "VAuto.Zone.Assets.Database.buffs.json",
                "4E4FB8A52CE315F0E47F2FC8640484052E01CE1891D7DA484DED7318FAA77C9C",
                "db-buffs-1"),
            new DefaultConfigEntry(
                "database/vbloods.json",
                "VAuto.Zone.Assets.Database.vbloods.json",
                "4E98616F5D88167777AB510EC0F4051C335DEAE04C5B8AA2F2EA0D3955B0BF31",
                "db-vbloods-1"),
                new DefaultConfigEntry(
                "database/weapons.json",
                "VAuto.Zone.Assets.Database.weapons.json",
                "8FC5AE0196413FDDFD2F22CFCB0F4EE1D1EE201B2568A154F99C8F741C61A9AE",
                "db-weapons-1")
        };

        public static readonly IReadOnlyList<DefaultConfigEntry> FlowEntries = new[]
        {
            new DefaultConfigEntry(
                "flows/ZoneDefault.json",
                "VAuto.Zone.Assets.Flows.ZoneDefault.json",
                "25070C4332D2B4D178CC616470D8EFF85A0067C99A9DCB11A14BA39D42CA3401",
                "flow-zonedefault-1"),
            new DefaultConfigEntry(
                "flows/A1.json",
                "VAuto.Zone.Assets.Flows.A1.json",
                "E88CB0B1B874A17932E06D16E9E19B86872944C434EF176D955E9F900E3AC018",
                "flow-a1-1"),
            new DefaultConfigEntry(
                "flows/B1.json",
                "VAuto.Zone.Assets.Flows.B1.json",
                "4A79FF114BAC89D4DAA7AE1EE8E16DFA3FA6ED6530C58273D6122364C96BB85B",
                "flow-b1-1"),
            new DefaultConfigEntry(
                "flows/T3.json",
                "VAuto.Zone.Assets.Flows.T3.json",
                "F66D1CDBF21F2542ABB6CE2616DDD5CA9F37F787408BB4FED2E4666E09897778",
                "flow-t3-1")
        };

        public static int TotalEntries => DatabaseEntries.Count + FlowEntries.Count;

        internal sealed class DefaultConfigEntry
        {
            public DefaultConfigEntry(string logicalPath, string resourceName, string sha256, string versionTag)
            {
                LogicalPath = logicalPath?.Replace('\\', '/') ?? throw new ArgumentNullException(nameof(logicalPath));
                ResourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
                Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
                VersionTag = versionTag ?? throw new ArgumentNullException(nameof(versionTag));
                Category = LogicalPath.Split('/')[0];
            }

            public string LogicalPath { get; }
            public string ResourceName { get; }
            public string Sha256 { get; }
            public string VersionTag { get; }
            public string Category { get; }
        }
    }
}
