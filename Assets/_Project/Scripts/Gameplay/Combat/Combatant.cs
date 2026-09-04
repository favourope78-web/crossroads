using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>Outcome of one damage application (feedback + tests).</summary>
    public struct DamageResult
    {
        public bool dodged;        // immunity frames absorbed everything
        public float amount;       // final post-mitigation damage dealt
        public float multiplier;   // resistance multiplier applied (feedback: "resisted")
        public float remainingHealth;
        public bool defeated;
    }

    /// <summary>One active timed status on a combatant (runtime mirror of the data row).</summary>
    public class ActiveStatus
    {
        public string statusId = "";
        public StatusEffectDefinitionData definition;
        public float remaining;
        public float tickTimer;
    }

    /// <summary>
    /// Pure-C# combatant: health, defense, damage-type resistances and timed status
    /// effects (task: reusable Health + Damage receiving + Defense components - the Unity
    /// hosts PlayerCombatController/EnemyAgent own one of these). Every mutation goes
    /// through here and publishes a combat event, so the whole game reacts to damage
    /// without polling. No Unity types - headless-testable, mobile-cheap: ticks are a few
    /// float ops; allocations happen only when a status is applied (never per frame).
    /// </summary>
    public class CombatantState
    {
        public readonly string Id;
        public readonly bool IsPlayer;
        public string DisplayName = "";
        public readonly float MaxHealth;
        public float Health;
        public readonly float Defense;
        public readonly List<DamageResistEntry> Resistances;
        private readonly List<ActiveStatus> _statuses = new List<ActiveStatus>();

        public bool Alive { get { return Health > 0f; } }
        public IReadOnlyList<ActiveStatus> Statuses { get { return _statuses; } }

        public CombatantState(string id, bool isPlayer, string displayName, float maxHealth,
                              float defense, List<DamageResistEntry> resistances)
        {
            Id = id ?? "";
            IsPlayer = isPlayer;
            DisplayName = displayName ?? id;
            MaxHealth = Math.Max(1f, maxHealth);
            Health = MaxHealth;
            Defense = defense;
            Resistances = resistances ?? new List<DamageResistEntry>();
        }

        public static CombatantState ForPlayer(CombatSettingsData settings)
        {
            return new CombatantState("player", true, "Ari",
                settings != null ? settings.playerMaxHealth : 100f,
                settings != null ? settings.playerDefense : 0f,
                settings != null ? settings.playerResistances : null);
        }

        public static CombatantState ForEnemy(EnemyDefinitionData def)
        {
            return new CombatantState(def != null ? def.id : "enemy", false,
                def != null ? def.displayName : "enemy",
                def != null ? def.maxHealth : 30f,
                def != null ? def.defense : 0f,
                def != null ? def.resistances : null);
        }

        // ---------------------------------------------------------------- damage / healing
        /// <summary>Applies one hit: immunity -> resistances -> flat defense -> floor.</summary>
        public DamageResult ApplyDamage(DamageType type, float rawAmount)
        {
            var result = new DamageResult { remainingHealth = Health };
            if (!Alive || rawAmount <= 0f) return result;

            if (IsImmune) // dodge/ward frames: the hit whiffs
            {
                result.dodged = true;
                PublishDamage(result, type, rawAmount);
                return result;
            }

            float multiplier = DamageCalculator.ResistanceFor(Resistances, type);
            result.multiplier = multiplier;
            result.amount = DamageCalculator.Compute(rawAmount, multiplier, Defense);
            Health = Math.Max(0f, Health - result.amount);
            result.remainingHealth = Health;
            result.defeated = !Alive;
            PublishDamage(result, type, rawAmount);

            if (result.defeated)
                EventBus.Publish(new CombatantDefeatedEvent
                {
                    combatantId = Id, isPlayer = IsPlayer,
                    enemyId = IsPlayer ? "" : Id, displayName = DisplayName
                });
            return result;
        }

        private void PublishDamage(DamageResult result, DamageType type, float raw)
        {
            EventBus.Publish(new CombatantDamagedEvent
            {
                combatantId = Id, isPlayer = IsPlayer, enemyId = IsPlayer ? "" : Id,
                displayName = DisplayName, damageType = type,
                amount = result.amount, rawAmount = raw, dodged = result.dodged,
                remainingHealth = result.remainingHealth, maxHealth = MaxHealth,
                defeated = result.defeated
            });
        }

        public void Heal(float amount)
        {
            if (!Alive || amount <= 0f) return;
            float before = Health;
            Health = Math.Min(MaxHealth, Health + amount);
            EventBus.Publish(new CombatantHealedEvent
            {
                combatantId = Id, isPlayer = IsPlayer,
                amount = Health - before, remainingHealth = Health, maxHealth = MaxHealth
            });
        }

        /// <summary>Full restore (respawn / encounter reset) - used by defeat handling.</summary>
        public void ReviveFull()
        {
            Health = MaxHealth;
            _statuses.Clear();
        }

        /// <summary>Boot-time restore from a persisted value (clamped; statuses start clear).</summary>
        public void RestoreHealth(float value)
        {
            Health = Math.Max(1f, Math.Min(MaxHealth, value));
            _statuses.Clear();
        }

        // ---------------------------------------------------------------- status effects
        /// <summary>Applies (or refreshes) a status from its definition. Allocates once per application.</summary>
        public void ApplyStatus(StatusEffectDefinitionData definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id) || !Alive) return;
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i].statusId == definition.id)
                {
                    _statuses[i].remaining = definition.durationSeconds; // refresh
                    _statuses[i].tickTimer = definition.tickIntervalSeconds;
                    return;
                }
            }
            _statuses.Add(new ActiveStatus
            {
                statusId = definition.id,
                definition = definition,
                remaining = definition.durationSeconds,
                tickTimer = definition.tickIntervalSeconds
            });
            EventBus.Publish(new StatusChangedEvent
            { combatantId = Id, isPlayer = IsPlayer, statusId = definition.id, active = true });
        }

        public bool HasStatus(string statusId)
        {
            for (int i = 0; i < _statuses.Count; i++) if (_statuses[i].statusId == statusId) return true;
            return false;
        }

        /// <summary>Advances all statuses (dt seconds): periodic health deltas + expiry.
        /// Returns true when a periodic health change happened (caller may persist).</summary>
        public bool TickStatuses(float dt)
        {
            bool healthChanged = false;
            for (int i = _statuses.Count - 1; i >= 0; i--)
            {
                ActiveStatus s = _statuses[i];
                s.remaining -= dt;
                if (s.definition.tickIntervalSeconds > 0f)
                {
                    s.tickTimer -= dt;
                    // "remaining + dt" = the remaining time at the START of this frame: a status
                    // whose duration ends exactly now still delivers its final tick.
                    while (s.tickTimer <= 0f && s.remaining + dt > 0f && Alive)
                    {
                        s.tickTimer += s.definition.tickIntervalSeconds;
                        int perTick = s.definition.healthPerTick;
                        if (perTick < 0) { ApplyDamage(DamageType.Kinetic, -perTick); healthChanged = true; }
                        else if (perTick > 0) { Heal(perTick); healthChanged = true; }
                        if (!Alive) return healthChanged;
                    }
                }
                if (s.remaining <= 0f)
                {
                    _statuses.RemoveAt(i);
                    EventBus.Publish(new StatusChangedEvent
                    { combatantId = Id, isPlayer = IsPlayer, statusId = s.statusId, active = false });
                }
            }
            return healthChanged;
        }

        // ---------------------------------------------------------------- derived modifiers
        /// <summary>Product of active movement modifiers (suppression slows, etc.).</summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < _statuses.Count; i++)
                    m *= _statuses[i].definition.moveSpeedMultiplier;
                return m;
            }
        }

        /// <summary>Product of active attack-rate modifiers (enemy cooldowns).</summary>
        public float AttackRateMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < _statuses.Count; i++)
                    m *= _statuses[i].definition.attackRateMultiplier;
                return m;
            }
        }

        /// <summary>True while a grantsImmunity status is active (dodge guard).</summary>
        public bool IsImmune
        {
            get
            {
                for (int i = 0; i < _statuses.Count; i++)
                    if (_statuses[i].definition.grantsImmunity) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Deterministic damage math (task: damage calculation). Formula:
    ///   final = max(1, raw * resistanceMultiplier - defense)
    /// Resistances are per damage type (1 = normal, &lt;1 resisted, &gt;1 vulnerable);
    /// defense is flat AFTER the percentage; the floor keeps fights resolvable.
    /// No RNG anywhere - tests and replays are stable.
    /// </summary>
    public static class DamageCalculator
    {
        public const float MinimumDamage = 1f;

        public static float Compute(float rawAmount, float resistanceMultiplier, float defense)
        {
            if (rawAmount <= 0f) return 0f;
            float mitigated = rawAmount * resistanceMultiplier - defense;
            return Math.Max(MinimumDamage, mitigated);
        }

        public static float ResistanceFor(List<DamageResistEntry> resistances, DamageType type)
        {
            if (resistances == null) return 1f;
            for (int i = 0; i < resistances.Count; i++)
                if (resistances[i] != null && resistances[i].type == type)
                    return resistances[i].multiplier;
            return 1f;
        }
    }
}
