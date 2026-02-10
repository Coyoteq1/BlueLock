using System;
using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Patches
{
    /// <summary>
    /// Patch for UnitSpawnerSystem to track when units are spawned.
    /// Useful for tracking spawns, spawners, and spawn locations.
    /// </summary>
    [HarmonyPatch(typeof(UnitSpawnerSystem), nameof(UnitSpawnerSystem.OnUpdate))]
    internal static class UnitSpawnerSystemPatch
    {
        public static event EventHandler<UnitSpawnEventArgs> OnUnitSpawned;
        
        public class UnitSpawnEventArgs : EventArgs
        {
            public Entity Spawner { get; set; }
            public Entity SpawnedUnit { get; set; }
            public PrefabGUID PrefabGuid { get; set; }
            public float3 Position { get; set; }
            public int Level { get; set; }
            public bool IsNightSpawn { get; set; }
        }

        [HarmonyPostfix]
        static unsafe void OnUpdatePostfix(UnitSpawnerSystem __instance)
        {
            if (!CoreLogger.IsInitialized) return;

            try
            {
                // Access the internal spawn buffer if available
                var spawnBufferField = typeof(UnitSpawnerSystem)
                    .GetField("_SpawnBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (spawnBufferField != null)
                {
                    var spawnBuffer = spawnBufferField.GetValue(__instance) as NativeList<UnitSpawnRequest>;
                    if (spawnBuffer != null)
                    {
                        for (int i = 0; i < spawnBuffer.Length; i++)
                        {
                            var spawnRequest = spawnBuffer[i];
                            
                            var args = new UnitSpawnEventArgs
                            {
                                Spawner = spawnRequest.SpawnerEntity,
                                SpawnedUnit = spawnRequest.UnitEntity,
                                PrefabGuid = spawnRequest.PrefabGuid,
                                Position = spawnRequest.Position,
                                Level = spawnRequest.Level,
                                IsNightSpawn = spawnRequest.NightPop
                            };

                            OnUnitSpawned?.Invoke(__instance, args);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error processing unit spawn events", ex);
            }
        }
    }

    /// <summary>
    /// Patch for tracking spawn travel buff events.
    /// Useful for tracking when units get spawn travel buffs.
    /// </summary>
    [HarmonyPatch(typeof(SpawnTravelBuffSystem), nameof(SpawnTravelBuffSystem.OnUpdate))]
    internal static class SpawnTravelBuffSystemPatch
    {
        public static event EventHandler<SpawnTravelBuffEventArgs> OnSpawnTravelBuffApplied;
        
        public class SpawnTravelBuffEventArgs : EventArgs
        {
            public Entity Unit { get; set; }
            public PrefabGUID PrefabGuid { get; set; }
            public float3 Position { get; set; }
            public bool IsMoving { get; set; }
        }

        [HarmonyPostfix]
        static void OnUpdatePostfix(SpawnTravelBuffSystem __instance)
        {
            if (!CoreLogger.IsInitialized) return;

            try
            {
                using var query = __instance.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<SpawnTravelBuff>(),
                    ComponentType.ReadOnly<LocalTransform>()
                );

                var entities = query.ToEntityArray(Allocator.Temp);
                var buffs = query.ToComponentDataArray<SpawnTravelBuff>(Allocator.Temp);
                var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    var args = new SpawnTravelBuffEventArgs
                    {
                        Unit = entities[i],
                        PrefabGuid = buffs[i].PrefabGuid,
                        Position = transforms[i].Position,
                        IsMoving = !buffs[i].Arrived
                    };

                    OnSpawnTravelBuffApplied?.Invoke(__instance, args);
                }

                entities.Dispose();
                buffs.Dispose();
                transforms.Dispose();
            }
            catch (Exception ex)
            {
                CoreLogger.LogException("Error processing spawn travel buff events", ex);
            }
        }
    }
}
