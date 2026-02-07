using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VAuto.Core;
using VAuto.Zone.Services;

namespace VAuto.Zone.Commands
{
    [CommandGroup("Glow", description: "Commands for managing zone glows")]
    internal class ZoneGlowCommands
    {
        [Command("build", description: "Build glows for all zones")]
        public void Build(ChatCommandContext ctx)
        {
            ZoneGlowBorderService.BuildAll(rebuild: true);
            ctx.Reply("[ZoneGlow] Build requested.");
        }

        [Command("clear", description: "Clear all zone glows")]
        public void Clear(ChatCommandContext ctx)
        {
            ZoneGlowBorderService.ClearAll();
            ctx.Reply("[ZoneGlow] Cleared.");
        }

        [Command("rotate", description: "Force rotate glows now")]
        public void Rotate(ChatCommandContext ctx)
        {
            ZoneGlowBorderService.RotateAll();
            ctx.Reply("[ZoneGlow] Rotation triggered.");
        }

        [Command("status", description: "Show zone glow status")]
        public void Status(ChatCommandContext ctx)
        {
            foreach (var line in ZoneGlowBorderService.Status())
            {
                ctx.Reply(line);
            }
        }

        [Command("list", shortHand: "l", description: "List available glow prefabs")]
        public void ListGlows(ChatCommandContext ctx)
        {
            var glowService = new GlowService();
            var choices = glowService.ListGlowChoices().ToList();
            
            ctx.Reply($"[Glow] Available glow prefabs ({choices.Count}):");
            foreach (var (name, prefab) in choices.Take(20))
            {
                ctx.Reply($"  {name} ({prefab.GuidHash})");
            }
            if (choices.Count > 20)
            {
                ctx.Reply($"  ... and {choices.Count - 20} more");
            }
        }

        [Command("add", shortHand: "a", description: "Add glow prefab by GUID")]
        public void AddGlow(ChatCommandContext ctx, int prefabGuid)
        {
            var glowService = new GlowService();
            var prefab = new PrefabGUID(prefabGuid);
            
            // Check if prefab exists
            if (!VRCore.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(prefab, out var entity))
            {
                ctx.Reply($"[Glow] Error: Prefab GUID {prefabGuid} not found");
                return;
            }
            
            // Check if it has Buff component
            if (VRCore.EntityManager.HasComponent<Buff>(entity))
            {
                // Use existing name or generate one
                var existingName = glowService.GetGlowName(prefab);
                var name = existingName ?? $"Glow_{prefabGuid}";
                glowService.AddNewGlowChoice(prefab, name);
                ctx.Reply($"[Glow] Added: {name} ({prefabGuid})");
            }
            else
            {
                ctx.Reply($"[Glow] Warning: Prefab {prefabGuid} does not have Buff component");
                var name = $"Custom_{prefabGuid}";
                glowService.AddNewGlowChoice(prefab, name);
                ctx.Reply($"[Glow] Added: {name} ({prefabGuid}) - custom prefab");
            }
        }

        [Command("spawn", shortHand: "s", description: "Spawn single glow at mouse position for testing")]
        public void SpawnSingleGlow(ChatCommandContext ctx, string prefabNameOrGuid)
        {
            try
            {
                var glowService = new GlowService();
                PrefabGUID prefab;
                
                // Try to parse as GUID number first
                if (int.TryParse(prefabNameOrGuid, out var guid))
                {
                    prefab = new PrefabGUID(guid);
                }
                else
                {
                    prefab = glowService.GetGlowPrefab(prefabNameOrGuid);
                    if (prefab.IsEmpty())
                    {
                        // Try by name hash
                        var hashCode = prefabNameOrGuid.GetHashCode();
                        if (VRCore.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(new PrefabGUID(hashCode), out _))
                        {
                            prefab = new PrefabGUID(hashCode);
                        }
                        else
                        {
                            ctx.Reply($"[Glow] Error: Prefab '{prefabNameOrGuid}' not found");
                            return;
                        }
                    }
                }
                
                // Get player position
                var character = ctx.Event?.SenderCharacterEntity ?? Entity.Null;
                if (character == Entity.Null)
                {
                    ctx.Reply("[Glow] Error: Could not find your character");
                    return;
                }
                
                if (!VRCore.EntityManager.HasComponent<LocalTransform>(character))
                {
                    ctx.Reply("[Glow] Error: Character has no transform");
                    return;
                }
                
                var playerPos = VRCore.EntityManager.GetComponentData<LocalTransform>(character).Position;
                
                // Spawn the glow entity
                if (!VRCore.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(prefab, out var sourceEntity))
                {
                    ctx.Reply($"[Glow] Error: Could not find entity for prefab {prefab.GuidHash}");
                    return;
                }
                
                var entity = VRCore.EntityManager.Instantiate(sourceEntity);
                VRCore.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(playerPos));
                
                var glowName = glowService.GetGlowName(prefab) ?? prefabNameOrGuid;
                ctx.Reply($"[Glow] Spawned '{glowName}' at {playerPos}");
            }
            catch (Exception ex)
            {
                ctx.Reply($"[Glow] Error: {ex.Message}");
            }
        }
    }
}
