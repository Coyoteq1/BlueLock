using System;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VAuto.EndGameKit.Configuration;
using VAuto.EndGameKit.Helpers;

namespace VAuto.EndGameKit.Services

{

    /// <summary>

    /// Service responsible for stat extension via buffs.

    /// 

    /// Why buffs instead of raw stat writes?

    /// - Properly replicated to clients

    /// - UI-visible (buff icons, tooltips)

    /// - Stack-safe with other buffs

    /// - Patch-resilient (doesn't break on stat structure changes)

    /// - Clean separation of concerns

    /// </summary>

    public class StatExtensionService

    {

        private readonly ServerGameManager? _serverGameManager;

        private readonly EntityManager _entityManager;



        // Buff GUIDs for stat extensions (replace with actual GUIDs)

        private static readonly PrefabGUID PowerBonusBuff = new(-2000000001);

        private static readonly PrefabGUID MaxHealthBonusBuff = new(-2000000002);

        private static readonly PrefabGUID SpellPowerBonusBuff = new(-2000000003);

        private static readonly PrefabGUID MoveSpeedBonusBuff = new(-2000000004);

        private static readonly PrefabGUID PhysicalResistanceBonusBuff = new(-2000000005);

        private static readonly PrefabGUID SpellResistanceBonusBuff = new(-2000000006);

        private static readonly PrefabGUID ArmorBonusBuff = new(-2000000007);

        private static readonly PrefabGUID MaxStaminaBonusBuff = new(-2000000008);



        /// <summary>

        /// Creates a new StatExtensionService instance.

        /// </summary>

        /// <param name="serverGameManager">Server game manager for buff operations (nullable for safety).</param>

        /// <param name="entityManager">Entity manager for validation.</param>

        public StatExtensionService(ServerGameManager? serverGameManager, EntityManager entityManager)

        {

            _serverGameManager = serverGameManager;

            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));

        }



        /// <summary>

        /// Updates the ServerGameManager reference.

        /// </summary>

        public void UpdateServerGameManager(ServerGameManager? serverGameManager)

        {

            _serverGameManager = serverGameManager;

        }



        /// <summary>

        /// Apply all stat overrides from a configuration using appropriate buffs.

        /// </summary>

        /// <param name="player">Player entity.</param>

        /// <param name="statOverrides">Stat override configuration.</param>

        /// <returns>Number of buffs successfully applied.</returns>

        public int ApplyStatOverrides(Entity player, StatOverrideConfig statOverrides)

        {

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))

            {

                Plugin.Log.LogWarning($"[StatExtensionService] Invalid player entity for stat overrides");

                return 0;

            }



            if (statOverrides == null)

            {

                return 0;

            }



            if (_serverGameManager == null)

            {

                Plugin.Log.LogWarning("[StatExtensionService] ServerGameManager not available");

                return 0;

            }



            int appliedCount = 0;



            // Apply each stat buff if the override is greater than 0

            if (statOverrides.BonusPower > 0 && ApplyBuff(player, PowerBonusBuff))

                appliedCount++;



            if (statOverrides.BonusMaxHealth > 0 && ApplyBuff(player, MaxHealthBonusBuff))

                appliedCount++;



            if (statOverrides.BonusSpellPower > 0 && ApplyBuff(player, SpellPowerBonusBuff))

                appliedCount++;



            if (statOverrides.BonusMoveSpeed > 0 && ApplyBuff(player, MoveSpeedBonusBuff))

                appliedCount++;



            if (statOverrides.BonusPhysicalResistance > 0 && ApplyBuff(player, PhysicalResistanceBonusBuff))

                appliedCount++;



            if (statOverrides.BonusSpellResistance > 0 && ApplyBuff(player, SpellResistanceBonusBuff))

                appliedCount++;



            if (statOverrides.BonusArmor > 0 && ApplyBuff(player, ArmorBonusBuff))

                appliedCount++;



            if (statOverrides.BonusMaxStamina > 0 && ApplyBuff(player, MaxStaminaBonusBuff))

                appliedCount++;



            if (appliedCount > 0)

            {

                Plugin.Log.LogInfo($"[StatExtensionService] Applied {appliedCount} stat buffs to player {player.Index}");

            }

            

            return appliedCount;

        }



        /// <summary>

        /// Apply standard GS91 stat overrides.

        /// </summary>

        public int ApplyGS91Stats(Entity player)

        {

            var gs91Stats = new StatOverrideConfig

            {

                BonusPower = 25f,

                BonusMaxHealth = 300f,

                BonusSpellPower = 15f,

                BonusMoveSpeed = 0.05f

            };



            return ApplyStatOverrides(player, gs91Stats);

        }



        /// <summary>

        /// Apply a single stat buff.

        /// </summary>

        public bool ApplyBuff(Entity player, PrefabGUID buffGuid)

        {

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))

                return false;



            if (!GuidHelper.IsValid(buffGuid))

                return false;



            if (_serverGameManager == null)

                return false;



            try

            {

                return _serverGameManager.TryApplyBuff(player, buffGuid);

            }

            catch (Exception ex)

            {

                Plugin.Log.LogWarning($"[StatExtensionService] Failed to apply buff {buffGuid.Value}: {ex.Message}");

                return false;

            }

        }



        /// <summary>

        /// Apply a single stat buff from long value.

        /// </summary>

        public bool ApplyBuff(Entity player, long guidValue)

        {

            return ApplyBuff(player, new PrefabGUID((int)guidValue));

        }



        /// <summary>

        /// Apply power bonus buff.

        /// </summary>

        public bool ApplyPowerBonus(Entity player, float amount)

        {

            if (amount <= 0)

                return false;



            return ApplyBuff(player, PowerBonusBuff);

        }



        /// <summary>

        /// Apply max health bonus buff.

        /// </summary>

        public bool ApplyMaxHealthBonus(Entity player, float amount)

        {

            if (amount <= 0)

                return false;



            return ApplyBuff(player, MaxHealthBonusBuff);

        }



        /// <summary>

        /// Apply movement speed bonus buff.

        /// </summary>

        public bool ApplyMoveSpeedBonus(Entity player, float percentage)

        {

            if (percentage <= 0)

                return false;



            return ApplyBuff(player, MoveSpeedBonusBuff);

        }



        /// <summary>

        /// Remove all stat buffs (for restoration scenarios).

        /// Note: V Rising doesn't have a direct "remove buff" API.

        /// Buffs expire based on their duration.

        /// </summary>

        public void RemoveStatBuffs(Entity player)

        {

            if (!PlayerHelper.IsValidPlayer(_entityManager, player))

                return;



            Plugin.Log.LogDebug($"[StatExtensionService] Stat buff removal for player {player.Index} - buffs expire naturally");

        }



        /// <summary>

        /// Check if stat buffs can be applied to a player.

        /// </summary>

        public bool CanApplyStats(Entity player)

        {

            return PlayerHelper.IsValidPlayer(_entityManager, player) && _serverGameManager != null;

        }

    }

}

