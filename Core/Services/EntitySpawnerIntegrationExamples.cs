using System;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core.Services;

namespace VAuto.Core.Examples
{
    /// <summary>
    /// Integration examples for EntitySpawner with V Rising mods.
    /// </summary>
    public static class EntitySpawnerIntegrationExamples
    {
        /// <summary>
        /// Example: Spawn multiple glowing entities for arena borders.
        /// </summary>
        public static void SpawnArenaBorder()
        {
            // Initialize the spawner
            EntitySpawner.Initialize();

            // Configure glow for arena (purple glow)
            var arenaGlow = new EntitySpawner.GlowConfig
            {
                Color = new float3(0.6f, 0f, 0.8f), // Purple
                Intensity = 1.5f,
                Radius = 8f,
                Duration = 300f // 5 minutes
            };

            // Spawn border at 20m radius with 16 entities
            var result = EntitySpawner.SpawnGlowBorder(
                center: new float3(0, 0, 0),
                radius: 20f,
                entityCount: 16,
                glowConfig: arenaGlow,
                buffId: 561176
            );

            Console.WriteLine($"Spawned {result.SuccessCount} arena border entities");
        }

        /// <summary>
        /// Example: Spawn a grid of buff entities for events.
        /// </summary>
        public static void SpawnEventGrid()
        {
            EntitySpawner.Initialize();

            // Configure glow for events (gold glow)
            var eventGlow = new EntitySpawner.GlowConfig
            {
                Color = new float3(1f, 0.8f, 0f), // Gold
                Intensity = 2f,
                Radius = 5f,
                Duration = 600f // 10 minutes
            };

            // Spawn 10x10 grid with 5m spacing
            var result = EntitySpawner.SpawnGlowGrid(
                origin: new float3(100f, 0f, 100f),
                rows: 10,
                columns: 10,
                spacing: 5f,
                glowConfig: eventGlow,
                buffId: 561176
            );

            Console.WriteLine($"Spawned {result.SuccessCount} event grid entities");

            // Store for later cleanup
            var spawnedEntities = result.SpawnedEntities;
        }

        /// <summary>
        /// Example: Batch spawn with random positions around a center.
        /// </summary>
        public static void SpawnRandomCluster()
        {
            EntitySpawner.Initialize();

            // Configure spawn
            var spawnConfig = new EntitySpawner.SpawnConfig
            {
                Count = 20,
                CenterPosition = new float3(500f, 0f, 500f),
                SpawnRadius = 15f,
                RandomRotation = true,
                Glow = new EntitySpawner.GlowConfig
                {
                    Color = new float3(0f, 1f, 0.5f), // Teal
                    Intensity = 1.2f,
                    Radius = 6f,
                    Duration = 120f
                },
                BuffId = 561176
            };

            var result = EntitySpawner.SpawnGlowingBuffEntities(spawnConfig);
            Console.WriteLine($"Cluster spawn: {result.SuccessCount}/{spawnConfig.Count} succeeded");
        }

        /// <summary>
        /// Example: Single entity spawn at specific position.
        /// </summary>
        public static void SpawnSingleAtPosition()
        {
            EntitySpawner.Initialize();

            var entity = EntitySpawner.SpawnSingleGlowingBuffEntity(
                position: new float3(250f, 0f, 350f),
                glowConfig: new EntitySpawner.GlowConfig
                {
                    Color = new float3(1f, 0.2f, 0.2f), // Red
                    Intensity = 2f,
                    Radius = 10f,
                    Duration = 60f
                },
                buffId: 561176,
                randomRotation: false
            );

            Console.WriteLine($"Spawned single entity: {entity != Unity.Entities.Entity.Null}");
        }

        /// <summary>
        /// Example: Update glow on all spawned entities.
        /// </summary>
        public static void UpdateAllGlows(NativeList<Unity.Entities.Entity> entities)
        {
            var newGlow = new EntitySpawner.GlowConfig
            {
                Color = new float3(0f, 0.5f, 1f), // Blue
                Intensity = 1.5f,
                Radius = 7f,
                Duration = 180f
            };

            EntitySpawner.UpdateGlowConfig(entities, newGlow);
            Console.WriteLine("Updated glow on all entities");
        }

        /// <summary>
        /// Example: Clean up all spawned entities.
        /// </summary>
        public static void Cleanup(NativeList<Unity.Entities.Entity> entities)
        {
            EntitySpawner.DespawnAll(entities);
            entities.Dispose();
        }

        /// <summary>
        /// Example: Custom glow configuration presets.
        /// </summary>
        public static EntitySpawner.GlowConfig GetPreset(string presetName)
        {
            return presetName.ToLowerInvariant() switch
            {
                "arena" => new EntitySpawner.GlowConfig
                {
                    Color = new float3(0.6f, 0f, 0.8f),
                    Intensity = 1.5f,
                    Radius = 8f,
                    Duration = 300f
                },
                "event" => new EntitySpawner.GlowConfig
                {
                    Color = new float3(1f, 0.8f, 0f),
                    Intensity = 2f,
                    Radius = 5f,
                    Duration = 600f
                },
                "warning" => new EntitySpawner.GlowConfig
                {
                    Color = new float3(1f, 0f, 0f),
                    Intensity = 2.5f,
                    Radius = 12f,
                    Duration = 30f
                },
                _ => EntitySpawner.GlowConfig.Default
            };
        }
    }
}
