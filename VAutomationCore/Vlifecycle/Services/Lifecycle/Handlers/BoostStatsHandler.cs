using Unity.Entities;
using ProjectM;
using VAuto.Core;

namespace VAuto.Core.Lifecycle.Handlers
{
    /// <summary>
    /// Overwrites character stats to maximum values for arena sandbox mode.
    /// Boosts: Health, Level, GearLevel, Blood Quality, Resistances.
    /// </summary>
    public class BoostStatsHandler : LifecycleActionHandler
    {
        private const string LogSource = "BoostStatsHandler";

        public bool Execute(LifecycleAction action, LifecycleContext context)
        {
            var em = VAutoCore.EntityManager;
            var character = context.CharacterEntity;

            if (character == Entity.Null)
            {
                VAutoLogger.LogWarning($"[{LogSource}] Character entity is null");
                return false;
            }

            try
            {
                // Max health
                if (em.HasComponent<Health>(character))
                {
                    var health = em.GetComponentData<Health>(character);
                    health.Value = 9999f;
                    health.MaxHealth = 9999f;
                    em.SetComponentData(character, health);
                    VAutoLogger.LogInfo($"[{LogSource}] Health maxed to 9999");
                }

                // Max level/gear level
                if (em.HasComponent<UnitLevel>(character))
                {
                    var level = em.GetComponentData<UnitLevel>(character);
                    level.GearLevel = 99;
                    level.Level = 80;
                    em.SetComponentData(character, level);
                    VAutoLogger.LogInfo($"[{LogSource}] Level/GearLevel maxed");
                }

                // Max blood quality
                if (em.HasComponent<Blood>(character))
                {
                    var blood = em.GetComponentData<Blood>(character);
                    blood.Quality = 100f;
                    em.SetComponentData(character, blood);
                    VAutoLogger.LogInfo($"[{LogSource}] Blood quality maxed to 100");
                }

                // Max resistances
                if (em.HasComponent<Resistances>(character))
                {
                    var resist = em.GetComponentData<Resistances>(character);
                    resist.SunResistance = 100f;
                    resist.FireResistance = 100f;
                    resist.HolyResistance = 100f;
                    resist.DecayResistance = 100f;
                    resist.BleedResistance = 100f;
                    em.SetComponentData(character, resist);
                    VAutoLogger.LogInfo($"[{LogSource}] All resistances maxed");
                }

                // Max physical damage/defense if components exist
                TryBoostCombatStats(character, em);

                VAutoLogger.LogInfo($"[{LogSource}] ✅ All stats boosted for arena");
                return true;
            }
            catch (System.Exception ex)
            {
                VAutoLogger.LogException(ex);
                VAutoLogger.LogError($"[{LogSource}] Failed: {ex.Message}");
                return false;
            }
        }

        private void TryBoostCombatStats(Entity character, EntityManager em)
        {
            // Physical Damage
            if (em.HasComponent<PhysicalDamage>(character))
            {
                var dmg = em.GetComponentData<PhysicalDamage>(character);
                dmg.Value = 5000f;
                em.SetComponentData(character, dmg);
            }

            // Weapon Damage
            if (em.HasComponent<WeaponDamage>(character))
            {
                var wdmg = em.GetComponentData<WeaponDamage>(character);
                wdmg.BaseDamage = 5000f;
                wdmg.BonusDamage = 5000f;
                em.SetComponentData(character, wdmg);
            }

            // Armor/Defense
            if (em.HasComponent<Armor>(character))
            {
                var armor = em.GetComponentData<Armor>(character);
                armor.Value = 5000f;
                em.SetComponentData(character, armor);
            }

            VAutoLogger.LogDebug($"[{LogSource}] Combat stats boosted");
        }
    }
}
