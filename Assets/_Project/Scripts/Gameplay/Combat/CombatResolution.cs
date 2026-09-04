using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Pure combat resolution (no Unity types):
    ///   - Ability attacks: consumes the EXISTING AbilityManager's AbilityUsedEvent payload
    ///     (level-row power/radius - upgrades genuinely change combat) + the AbilityCombatData
    ///     row and applies damage/heal/statuses. The ability system itself is untouched.
    ///   - Defeats: applies authored effect lists through EffectApplier (the same single
    ///     write path decisions and objectives use), so an enemy's death can increment
    ///     objective counters, spawn wreckage, move NPCs or unlock interactions - and a
    ///     PLAYER defeat applies its consequences without ever destroying the save.
    /// Headless-testable end to end.
    /// </summary>
    public static class CombatResolution
    {
        // ---------------------------------------------------------------- ability attacks
        /// <summary>
        /// Resolves one ability activation against the enemies already gathered inside the
        /// level-row radius (the director does the gathering). Damage/heal scale with the
        /// payload's power; statuses come from the data row.
        /// </summary>
        public static int ResolveAbilityAttack(AbilityUsedEvent evt, AbilityCombatData data,
                                               List<StatusEffectDefinitionData> statusLibrary,
                                               CombatantState player, List<CombatantState> targets)
        {
            if (data == null) return 0;

            // player-side payload (Tide Mend heals the caster, applies soothing)
            if (player != null && player.Alive && data.healPlayerPerPower > 0f)
                player.Heal(data.healPlayerPerPower * evt.power);
            if (player != null && player.Alive)
                ApplyStatuses(data.applyStatusToPlayer, statusLibrary, player);

            int hits = 0;
            if (targets == null) return 0;
            for (int i = 0; i < targets.Count; i++)
            {
                CombatantState target = targets[i];
                if (target == null || !target.Alive) continue;
                target.ApplyDamage(data.damageType, data.damagePerPower * evt.power);
                ApplyStatuses(data.applyStatusToTargets, statusLibrary, target);
                hits++;
            }
            if (hits > 0)
                StoryLog.Log("[COMBAT] " + evt.abilityId + " hit " + hits + " target(s) for " + (data.damagePerPower * evt.power) + " " + data.damageType);
            return hits;
        }

        private static void ApplyStatuses(List<string> statusIds, List<StatusEffectDefinitionData> library, CombatantState target)
        {
            if (statusIds == null || library == null) return;
            for (int i = 0; i < statusIds.Count; i++)
            {
                for (int j = 0; j < library.Count; j++)
                    if (library[j] != null && library[j].id == statusIds[i])
                    {
                        target.ApplyStatus(library[j]);
                        break;
                    }
            }
        }

        // ---------------------------------------------------------------- defeats
        /// <summary>
        /// Enemy defeated: applies the archetype's onDefeatEffects through EffectApplier
        /// (vars/world/entities/NPCs/objectives all react through the existing event graph).
        /// </summary>
        public static void DefeatEnemy(EnemyDefinitionData def, StateMutator state)
        {
            if (def == null) return;
            if (state != null) EffectApplier.Apply(def.onDefeatEffects, state);
            EventBus.Publish(new CombatantDefeatedEvent
            { combatantId = def.id, isPlayer = false, enemyId = def.id, displayName = def.displayName });
            StoryLog.Log("[COMBAT] Enemy defeated: " + def.id + " (" + def.displayName + ")");
        }

        /// <summary>
        /// Player defeated: NEVER destroys progress - applies the authored consequences
        /// (a counter, a bond, flags...) through the same write path, then the caller
        /// revives the player at the checkpoint with full health.
        /// </summary>
        public static void DefeatPlayer(CombatSettingsData settings, StateMutator state)
        {
            if (state == null) return;
            EffectApplier.Apply(settings != null ? settings.onPlayerDefeat : null, state);
            EventBus.Publish(new CombatantDefeatedEvent
            { combatantId = "player", isPlayer = true, enemyId = "", displayName = "Ari" });
            StoryLog.Log("[COMBAT] Player defeated - consequences applied, save intact");
        }

        /// <summary>Melee-arc helper shared by the player strike and tests: pure math.</summary>
        public static bool InMeleeArc(Point3 origin, Point3 target, float facingDegreesY,
                                      float range, float arcDegrees)
        {
            float dx = target.x - origin.x;
            float dz = target.z - origin.z;
            float distanceSq = dx * dx + dz * dz;
            if (distanceSq > range * range) return false;
            if (distanceSq < 0.0001f) return true; // standing inside each other

            // facing on the XZ plane (Unity convention: 0 deg = +Z, growing clockwise)
            float rad = facingDegreesY * DegToRad;
            float fx = Sin(rad);
            float fz = Cos(rad);
            float dot = (dx * fx + dz * fz) / Sqrt(distanceSq);
            float cosHalfArc = Cos(arcDegrees * 0.5f * DegToRad);
            return dot >= cosHalfArc;
        }

        private const float DegToRad = 0.0174532925f;
        private static float Sin(float r) { return (float)System.Math.Sin(r); }
        private static float Cos(float r) { return (float)System.Math.Cos(r); }
        private static float Sqrt(float v) { return (float)System.Math.Sqrt(v); }
    }
}
