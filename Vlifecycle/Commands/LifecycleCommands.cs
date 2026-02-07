using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Core.Lifecycle;
using VAuto.Core;

namespace VLifecycle.Commands
{
    /// <summary>
    /// Lifecycle management commands for VLifecycle.
    /// Provides commands for managing arena state transitions.
    /// </summary>
    [CommandGroup("lifecycle", "lc")]
    public static class LifecycleCommands
    {
        /// <summary>
        /// Display help for lifecycle commands.
        /// </summary>
        [Command("help", shortHand: "h", description: "Show lifecycle command help", adminOnly: false)]
        public static void Help(ChatCommandContext ctx)
        {
            var message = @"<color=#FFD700>[Lifecycle Commands]</color>
<color=#00FFFF>.lifecycle status (.lc s)</color> - Show current lifecycle status
<color=#00FFFF>.lifecycle enter (.lc e) [zone]</color> - Force enter arena zone [Admin]
<color=#00FFFF>.lifecycle exit (.lc x)</color> - Force exit arena zone [Admin]
<color=#00FFFF>.lifecycle config (.lc c)</color> - Show lifecycle configuration [Admin]
<color=#00FFFF>.lifecycle stages (.lc st)</color> - List lifecycle stages [Admin]
<color=#00FFFF>.lifecycle trigger (.lc t) [stage]</color> - Trigger a stage [Admin]";
            ctx.Reply(message);
        }

        /// <summary>
        /// Display current lifecycle status for the player.
        /// </summary>
        [Command("status", shortHand: "s", description: "Show current lifecycle status", adminOnly: false)]
        public static void Status(ChatCommandContext ctx)
        {
            try
            {
                var characterEntity = GetCharacterEntity(ctx);
                if (characterEntity == Entity.Null)
                {
                    ctx.Reply("<color=#FF0000>Error: Could not find your character.</color>");
                    return;
                }

                var entityManager = VRCore.ServerWorld.EntityManager;
                var position = GetPosition(entityManager, characterEntity);
                var isInArena = IsInArenaZone(position);
                
                var message = "<color=#FFD700>[Lifecycle Status]</color>\n" +
                              "In Arena: " + (isInArena ? "<color=#00FF00>Yes</color>" : "<color=#FF0000>No</color>") + "\n" +
                              "Enabled: " + (Plugin.IsEnabled ? "<color=#00FF00>Yes</color>" : "<color=#FF0000>No</color>") + "\n" +
                              "Initialized: " + (ArenaLifecycleManager.Instance.IsInitialized ? "<color=#00FF00>Yes</color>" : "<color=#FF0000>No</color>");

                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                ctx.Reply("<color=#FF0000>Error: " + ex.Message + "</color>");
            }
        }

        /// <summary>
        /// Force enter an arena zone (admin).
        /// </summary>
        [Command("enter", shortHand: "e", description: "Force enter arena zone", adminOnly: true)]
        public static void ForceEnter(ChatCommandContext ctx, string zoneId = "arena_main")
        {
            try
            {
                var characterEntity = GetCharacterEntity(ctx);
                if (characterEntity == Entity.Null)
                {
                    ctx.Reply("<color=#FF0000>Error: Could not find character.</color>");
                    return;
                }

                var userEntity = GetUserEntity(ctx, characterEntity);
                var position = GetPosition(VRCore.ServerWorld.EntityManager, characterEntity);
                
                ArenaLifecycleManager.Instance.OnPlayerEnter(userEntity, characterEntity, zoneId);
                ctx.Reply("<color=#00FF00>Entered arena zone: " + zoneId + "</color>");
            }
            catch (Exception ex)
            {
                ctx.Reply("<color=#FF0000>Error: " + ex.Message + "</color>");
            }
        }

        /// <summary>
        /// Force exit the current arena zone (admin).
        /// </summary>
        [Command("exit", shortHand: "x", description: "Force exit arena zone", adminOnly: true)]
        public static void ForceExit(ChatCommandContext ctx)
        {
            try
            {
                var characterEntity = GetCharacterEntity(ctx);
                if (characterEntity == Entity.Null)
                {
                    ctx.Reply("<color=#FF0000>Error: Could not find character.</color>");
                    return;
                }

                var userEntity = GetUserEntity(ctx, characterEntity);
                
                ArenaLifecycleManager.Instance.OnPlayerExit(userEntity, characterEntity, "none");
                ctx.Reply("<color=#00FF00>Exited arena zone.</color>");
            }
            catch (Exception ex)
            {
                ctx.Reply("<color=#FF0000>Error: " + ex.Message + "</color>");
            }
        }

        /// <summary>
        /// Show lifecycle configuration (admin).
        /// </summary>
        [Command("config", shortHand: "c", description: "Show lifecycle configuration", adminOnly: true)]
        public static void Config(ChatCommandContext ctx)
        {
            var message = "<color=#FFD700>[Lifecycle Configuration]</color>\n" +
                          "Save Inventory: " + (Plugin.SaveInventory ? "Yes" : "No") + "\n" +
                          "Save Buffs: " + (Plugin.SaveBuffs ? "Yes" : "No") + "\n" +
                          "Restore Inventory: " + (Plugin.RestoreInventory ? "Yes" : "No") + "\n" +
                          "Restore Buffs: " + (Plugin.RestoreBuffs ? "Yes" : "No") + "\n" +
                          "Save Equipment: " + (Plugin.SaveEquipment ? "Yes" : "No") + "\n" +
                          "Save Blood: " + (Plugin.SaveBlood ? "Yes" : "No") + "\n" +
                          "Save Spells: " + (Plugin.SaveSpells ? "Yes" : "No") + "\n" +
                          "Zone Triggers: " + (Plugin.ZoneTriggersLifecycle ? "Yes" : "No") + "\n" +
                          "Service Count: " + ArenaLifecycleManager.Instance.ServiceCount;
            ctx.Reply(message);
        }

        /// <summary>
        /// List all lifecycle stages (admin).
        /// </summary>
        [Command("stages", shortHand: "st", description: "List lifecycle stages", adminOnly: true)]
        public static void ListStages(ChatCommandContext ctx)
        {
            try
            {
                var stages = ArenaLifecycleManager.Instance.GetAllStageActionCounts();
                var message = "<color=#FFD700>[Lifecycle Stages]</color>\n";
                
                foreach (var stage in stages)
                {
                    message += stage.Key + ": " + stage.Value + " actions\n";
                }
                
                ctx.Reply(message);
            }
            catch (Exception ex)
            {
                ctx.Reply("<color=#FF0000>Error: " + ex.Message + "</color>");
            }
        }

        /// <summary>
        /// Trigger a lifecycle stage manually (admin).
        /// </summary>
        [Command("trigger", shortHand: "t", description: "Trigger a lifecycle stage", adminOnly: true)]
        public static void TriggerStage(ChatCommandContext ctx, string stageName)
        {
            try
            {
                var characterEntity = GetCharacterEntity(ctx);
                if (characterEntity == Entity.Null)
                {
                    ctx.Reply("<color=#FF0000>Error: Could not find character.</color>");
                    return;
                }

                var position = GetPosition(VRCore.ServerWorld.EntityManager, characterEntity);
                
                var context = new LifecycleContext
                {
                    CharacterEntity = characterEntity,
                    Position = position
                };
                
                var result = ArenaLifecycleManager.Instance.TriggerLifecycleStage(stageName, context);
                ctx.Reply("<color=#" + (result ? "00FF00" : "FF0000") + ">" + 
                          (result ? "Stage triggered successfully" : "Stage trigger failed") + "</color>");
            }
            catch (Exception ex)
            {
                ctx.Reply("<color=#FF0000>Error: " + ex.Message + "</color>");
            }
        }

        #region Helper Methods

        private static Entity GetCharacterEntity(ChatCommandContext ctx)
        {
            try
            {
                var serverWorld = VRCore.ServerWorld;
                if (serverWorld == null) return Entity.Null;

                var entityManager = serverWorld.EntityManager;
                var characterEntity = ctx.Event?.SenderCharacterEntity ?? Entity.Null;

                if (characterEntity != Entity.Null && entityManager.Exists(characterEntity))
                    return characterEntity;

                // Try to find by user entity
                var userEntity = ctx.Event?.SenderUserEntity ?? Entity.Null;
                if (userEntity != Entity.Null && entityManager.Exists(userEntity))
                {
                    var query = entityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PlayerCharacter>(),
                        ComponentType.ReadOnly<LocalTransform>()
                    );

                    var entities = query.ToEntityArray(Allocator.Temp);
                    foreach (var entity in entities)
                    {
                        var pc = entityManager.GetComponentData<PlayerCharacter>(entity);
                        if (pc.UserEntity == userEntity)
                        {
                            entities.Dispose();
                            return entity;
                        }
                    }
                    entities.Dispose();
                }

                return Entity.Null;
            }
            catch
            {
                return Entity.Null;
            }
        }

        private static Entity GetUserEntity(ChatCommandContext ctx, Entity characterEntity)
        {
            try
            {
                var entityManager = VRCore.ServerWorld.EntityManager;
                
                if (entityManager.HasComponent<PlayerCharacter>(characterEntity))
                {
                    var pc = entityManager.GetComponentData<PlayerCharacter>(characterEntity);
                    return pc.UserEntity;
                }

                return ctx.Event?.SenderUserEntity ?? Entity.Null;
            }
            catch
            {
                return Entity.Null;
            }
        }

        private static float3 GetPosition(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                return entityManager.GetComponentData<LocalTransform>(entity).Position;
            }
            return float3.zero;
        }

        private static bool IsInArenaZone(float3 position)
        {
            // Check if position is within any registered arena territory
            try
            {
                var territoryFile = "arena_territory.json";
                var territoryPath = FindConfigFile(territoryFile);
                if (!string.IsNullOrEmpty(territoryPath) && System.IO.File.Exists(territoryPath))
                {
                    var json = System.IO.File.ReadAllText(territoryPath);
                    var territory = System.Text.Json.JsonSerializer.Deserialize<ArenaTerritoryConfig>(json);
                    if (territory != null)
                    {
                        var center = new float3((float)territory.Center[0], (float)territory.Center[1], (float)territory.Center[2]);
                        var distance = math.distance(position, center);
                        return distance <= (float)territory.Radius;
                    }
                }
            }
            catch { }
            return false;
        }

        private static string FindConfigFile(string fileName)
        {
            var paths = new[]
            {
                System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "VAuto", fileName),
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(VLifecycle.Plugin).Assembly.Location) ?? "", "config", "VAuto", fileName),
                "config/VAuto/" + fileName
            };
            foreach (var path in paths)
            {
                if (System.IO.File.Exists(path))
                    return path;
            }
            return "";
        }

        #endregion
    }

    #region Config Models
    public class ArenaTerritoryConfig
    {
        public string Id { get; set; }
        public double[] Center { get; set; }
        public double Radius { get; set; }
        public string RegionType { get; set; }
    }
    #endregion
}
