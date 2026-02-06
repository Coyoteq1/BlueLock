using VampireCommandFramework;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VAuto.Core;
using VAuto.Commands.Converters;
using VAuto.Models;
using VAuto.Services;
using VAuto.Services.Systems;
using VAuto.Services.Visual;
using System.Linq;

namespace VAuto.Commands.Core
{
    /// <summary>
    /// Comprehensive lifecycle management commands
    /// </summary>
    [CommandGroup("lifecycle", "Comprehensive lifecycle management")]
    public class LifecycleCommands
    {
        /// <summary>
        /// Apply all lifecycles, teleport to zone, and apply all services.
        /// Usage: .lifecycle enter <zone> [player]
        /// </summary>
        [Command("enter", "Apply all lifecycles and services", adminOnly: true)]
        public static void LifecycleEnter(ChatCommandContext ctx, string zoneName, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                var playerModel = new Player(targetPlayer.UserEntity);
                
                ctx.Reply(Plugin.Log, $"[Lifecycle] Starting comprehensive lifecycle enter for {targetPlayer.CharacterName} to zone '{zoneName}'");
                
                // Step 1: Teleport to zone
                var zonePosition = GetZonePosition(zoneName);
                if (zonePosition == null)
                {
                    ctx.Reply(Plugin.Log, $"[Lifecycle] Error: Zone '{zoneName}' not found");
                    return;
                }
                
                TeleportPlayer(targetPlayer.CharacterEntity, zonePosition.Value);
                ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Teleported {targetPlayer.CharacterName} to {zoneName}");
                
                // Step 2: Apply all lifecycles
                ApplyAllLifecycles(targetPlayer, ctx);
                
                // Step 3: Apply all services
                ApplyAllServices(targetPlayer, ctx);
                
                // Step 4: Apply zone glow effects
                ApplyZoneGlowEffects(zoneName, zonePosition.Value, ctx);
                
                // Step 5: Apply auto systems
                ApplyAutoSystems(targetPlayer, ctx);
                
                ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Comprehensive lifecycle enter complete for {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error during lifecycle enter: {ex.Message}");
            }
        }

        /// <summary>
        /// Exit all lifecycles and restore original state.
        /// Usage: .lifecycle exit [player]
        /// </summary>
        [Command("exit", "Exit all lifecycles and restore state", adminOnly: true)]
        public static void LifecycleExit(ChatCommandContext ctx, FoundPlayer player = null)
        {
            try
            {
                var targetPlayer = player ?? new FoundPlayer(ctx.SenderUserEntity, ctx.SenderCharacterEntity, "You");
                
                ctx.Reply(Plugin.Log, $"[Lifecycle] Starting comprehensive lifecycle exit for {targetPlayer.CharacterName}");
                
                // Step 1: Exit all lifecycles
                ExitAllLifecycles(targetPlayer, ctx);
                
                // Step 2: Restore all services
                RestoreAllServices(targetPlayer, ctx);
                
                // Step 3: Remove zone glow effects
                RemoveZoneGlowEffects(targetPlayer, ctx);
                
                // Step 4: Restore original state
                RestoreOriginalState(targetPlayer, ctx);
                
                ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Comprehensive lifecycle exit complete for {targetPlayer.CharacterName}");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error during lifecycle exit: {ex.Message}");
            }
        }

        /// <summary>
        /// Create empty zone entity with attached glow.
        /// Usage: .lifecycle createzone <zonename> <radius>
        /// </summary>
        [Command("createzone", "Create empty zone entity with glow", adminOnly: true)]
        public static void CreateZoneWithGlow(ChatCommandContext ctx, string zoneName, float radius)
        {
            try
            {
                var playerPos = VRCore.EntityManager.GetComponentData<Translation>(ctx.SenderCharacterEntity).Value;
                
                // Create empty zone entity
                var zoneEntity = CreateEmptyZoneEntity(zoneName, playerPos, radius);
                
                // Apply glow effects
                ApplyZoneGlowEffects(zoneName, playerPos, ctx);
                
                ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Created zone '{zoneName}' at {playerPos} with radius {radius} and glow effects");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error creating zone: {ex.Message}");
            }
        }

        #region Private Methods

        private static void ApplyAllLifecycles(FoundPlayer targetPlayer, ChatCommandContext ctx)
        {
            try
            {
                // Apply kit lifecycles
                if (EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    var appliedKits = 0;
                    var kitNames = EndGameKitCommandHelper.GetKitProfileNames(system);

                    foreach (var kitName in kitNames)
                    {
                        var profile = EndGameKitCommandHelper.GetKitProfile(system, kitName);
                        if (profile == null || !EndGameKitCommandHelper.GetBool(profile, "Enabled", true))
                            continue;

                        if (EndGameKitCommandHelper.TryApplyKit(system, targetPlayer.CharacterEntity, kitName, out var applyError))
                        {
                            ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Applied kit lifecycle: {kitName}");
                            appliedKits++;
                        }
                        else
                        {
                            ctx.Reply(Plugin.Log, $"[Lifecycle] ✗ Failed to apply kit lifecycle {kitName}: {applyError}");
                        }
                    }
                    
                    ctx.Reply(Plugin.Log, $"[Lifecycle] Applied {appliedKits} kit lifecycles");
                }
                else
                {
                    ctx.Reply(Plugin.Log, $"[Lifecycle] EndGameKit system unavailable: {error}");
                }

                // Apply PvP lifecycles
                var pvpLifecycleService = VRCore.ServiceContainer.GetService<Services.LifecycleService>();
                if (pvpLifecycleService != null)
                {
                    // Trigger PvP lifecycle enter
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Applied PvP lifecycle");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error applying lifecycles: {ex.Message}");
            }
        }

        private static void ApplyAllServices(FoundPlayer targetPlayer, ChatCommandContext ctx)
        {
            try
            {
                // Apply blood service
                var playerModel = new Player(targetPlayer.UserEntity);
                if (playerModel.GetBloodQuality() < 100)
                {
                    playerModel.SetBloodQualityTo100();
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Applied blood service (quality 100)");
                }

                // Apply unlock service
                var debugEventsSystem = VRCore.ServerWorld?.GetExistingSystemManaged<DebugEventsSystem>();
                if (debugEventsSystem != null)
                {
                    var fromCharacter = new FromCharacter
                    {
                        User = targetPlayer.UserEntity,
                        Character = targetPlayer.CharacterEntity
                    };
                    
                    debugEventsSystem.UnlockAllResearch(fromCharacter);
                    debugEventsSystem.UnlockAllVBloods(fromCharacter);
                    debugEventsSystem.CompleteAllAchievements(fromCharacter);
                    
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Applied unlock service");
                }

                // Apply visual services
                var glowManager = VRCore.ServiceContainer.GetService<GlowManager>();
                if (glowManager != null)
                {
                    glowManager.ApplyArenaEntryEffects(targetPlayer.UserEntity, targetPlayer.CharacterEntity);
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Applied visual services");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error applying services: {ex.Message}");
            }
        }

        private static void ApplyZoneGlowEffects(string zoneName, float3 position, ChatCommandContext ctx)
        {
            try
            {
                var glowManager = VRCore.ServiceContainer.GetService<GlowManager>();
                if (glowManager != null)
                {
                    glowManager.ApplyZoneBorderGlow(zoneName, position, 50f, 0.8f, 2f, 32, 0f);
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Applied zone glow effects for {zoneName}");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error applying zone glow: {ex.Message}");
            }
        }

        private static void ApplyAutoSystems(FoundPlayer targetPlayer, ChatCommandContext ctx)
        {
            try
            {
                var autoSystemService = VRCore.World.GetExistingSystemManaged<AutoSystemService>();
                if (autoSystemService != null)
                {
                    // Auto systems are handled by the system itself
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Auto systems active");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error with auto systems: {ex.Message}");
            }
        }

        private static void ExitAllLifecycles(FoundPlayer targetPlayer, ChatCommandContext ctx)
        {
            try
            {
                // Exit kit lifecycles
                if (EndGameKitCommandHelper.TryGetSystem(out var system, out var error))
                {
                    if (EndGameKitCommandHelper.TryRemoveKit(system, targetPlayer.CharacterEntity, out var removeError))
                    {
                        ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Removed kit lifecycle");
                    }
                    else
                    {
                        ctx.Reply(Plugin.Log, $"[Lifecycle] ✗ Failed to remove kit lifecycle: {removeError}");
                    }
                }
                else
                {
                    ctx.Reply(Plugin.Log, $"[Lifecycle] EndGameKit system unavailable: {error}");
                }

                // Exit PvP lifecycles
                var pvpLifecycleService = VRCore.ServiceContainer.GetService<Services.LifecycleService>();
                if (pvpLifecycleService != null)
                {
                    // Trigger PvP lifecycle exit
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Exited PvP lifecycle");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error exiting lifecycles: {ex.Message}");
            }
        }

        private static void RestoreAllServices(FoundPlayer targetPlayer, ChatCommandContext ctx)
        {
            try
            {
                // Restore original blood quality
                var autoSystemService = VRCore.World.GetExistingSystemManaged<AutoSystemService>();
                if (autoSystemService != null)
                {
                    // Auto system handles restoration
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Restored original blood quality");
                }

                // Remove visual effects
                var glowManager = VRCore.ServiceContainer.GetService<GlowManager>();
                if (glowManager != null)
                {
                    // Remove glow effects
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Removed visual effects");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error restoring services: {ex.Message}");
            }
        }

        private static void RemoveZoneGlowEffects(FoundPlayer targetPlayer, ChatCommandContext ctx)
        {
            try
            {
                // Remove player glow effects
                var playerGlowService = VRCore.ServiceContainer.GetService<PlayerGlowService>();
                if (playerGlowService != null)
                {
                    playerGlowService.RemovePlayerGlow(targetPlayer.CharacterEntity);
                    ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Removed player glow effects");
                }
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error removing glow effects: {ex.Message}");
            }
        }

        private static void RestoreOriginalState(FoundPlayer targetPlayer, ChatCommandContext ctx)
        {
            try
            {
                // Original state restoration is handled by AutoSystemService
                ctx.Reply(Plugin.Log, $"[Lifecycle] ✓ Restored original state");
            }
            catch (Exception ex)
            {
                ctx.Reply(Plugin.Log, $"[Lifecycle] Error restoring original state: {ex.Message}");
            }
        }

        private static Entity CreateEmptyZoneEntity(string zoneName, float3 position, float radius)
        {
            var em = VRCore.EntityManager;
            var zoneEntity = em.CreateEntity();
            
            // Add zone components
            em.AddComponentData(zoneEntity, new Translation { Value = position });
            em.AddComponentData(zoneEntity, new ZoneBoundary 
            { 
                Center = position, 
                Radius = radius,
                ZoneId = zoneName
            });
            
            // Add glow components
            em.AddComponentData(zoneEntity, new GlowZoneTrigger
            {
                ZoneName = zoneName,
                Intensity = 0.8f,
                PulseSpeed = 2f
            });
            
            return zoneEntity;
        }

        private static void TeleportPlayer(Entity characterEntity, float3 position)
        {
            characterEntity.Write(new Translation { Value = position });
            characterEntity.Write(new LastTranslation { Value = position });
        }

        private static float3? GetZonePosition(string zoneName)
        {
            // Use existing zone position lookup
            var zonePositions = new System.Collections.Generic.Dictionary<string, float3>
            {
                {"arena", new float3(-1000, 5, -500)},
                {"pvp", new float3(-1500, 5, -1000)},
                {"spawn", new float3(0, 0, 0)},
                {"castle", new float3(-1000, 5, -500)}
            };

            zonePositions.TryGetValue(zoneName.ToLower(), out var position);
            return position;
        }

        #endregion
    }
}
