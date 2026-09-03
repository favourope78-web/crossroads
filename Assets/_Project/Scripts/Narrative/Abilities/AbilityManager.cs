using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Narrative
{
    /// <summary>What an ability currently means for this player (persisted state -> access).</summary>
    public enum AbilityAccessState
    {
        Locked,   // not unlocked (yet) - UI shows the unlock hint
        Blocked,  // explicitly excluded by an earlier decision - cannot be gained this run
        Unlocked  // available; level >= 1
    }

    /// <summary>Result of trying to activate an ability.</summary>
    public enum AbilityActivation
    {
        Ok,
        Unknown,          // no definition with this id
        Locked,           // not unlocked
        Blocked,          // sealed by a decision
        CoolingDown,      // still recharging
        NotEnoughEnergy   // energyCost > echoBank (cost = data, energy pool = echoes §3.3)
    }

    /// <summary>
    /// The power/ability runtime (GAME_DESIGN §3.3 Powers + §4.2 data-driven rules):
    ///   - reads the live GameStateManager (unlocks are persisted decisions)
    ///   - state machine per definition: Locked -> Blocked | Unlocked (level 1..max)
    ///   - activation: validates access/cooldown/cost, publishes AbilityUsedEvent with the
    ///     CURRENT level-row numbers (upgrades genuinely change behaviour), starts cooldown
    ///   - cooldown state is session-only (never persisted) and computed on demand from an
    ///     injected clock - no per-frame processing anywhere
    /// Pure C# - headless-testable. Activated from the mobile ability UI or future combat.
    /// </summary>
    public class AbilityManager
    {
        private readonly List<AbilityDefinitionData> _definitions;
        private readonly GameStateManager _progress;
        private readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();

        /// <summary>Injected time source in seconds (Unity: () => Time.time; tests: manual clock).</summary>
        public Func<float> Now;

        public AbilityManager(List<AbilityDefinitionData> definitions, GameStateManager progress)
        {
            _definitions = definitions ?? new List<AbilityDefinitionData>();
            _progress = progress;
            Now = () => 0f;
        }

        // ---------------------------------------------------------------- registry
        /// <summary>All known abilities (definitions = pure data from the content pipeline).</summary>
        public List<AbilityDefinitionData> Definitions { get { return _definitions; } }

        public AbilityDefinitionData Find(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return null;
            for (int i = 0; i < _definitions.Count; i++)
                if (_definitions[i] != null && _definitions[i].id == abilityId) return _definitions[i];
            return null;
        }

        // ---------------------------------------------------------------- access state (persisted)
        public AbilityAccessState AccessState(string abilityId)
        {
            if (Find(abilityId) == null) return AbilityAccessState.Locked;
            if (_progress.IsAbilityBlocked(abilityId)) return AbilityAccessState.Blocked;
            if (Level(abilityId) >= 1) return AbilityAccessState.Unlocked;
            return AbilityAccessState.Locked;
        }

        public bool IsUnlocked(string abilityId) { return AccessState(abilityId) == AbilityAccessState.Unlocked; }

        /// <summary>Current level (0 = not unlocked; 1..MaxLevel after).</summary>
        public int Level(string abilityId)
        {
            int stored = _progress.AbilityLevel(abilityId);
            if (stored > 0) return stored;
            return _progress.HasAbility(abilityId) ? 1 : 0;
        }

        public int MaxLevel(string abilityId)
        {
            AbilityDefinitionData def = Find(abilityId);
            return def != null ? def.MaxLevel : 1;
        }

        /// <summary>Upgrade gate: unlocked and not yet at max level.</summary>
        public bool CanUpgrade(string abilityId)
        {
            return IsUnlocked(abilityId) && Level(abilityId) < MaxLevel(abilityId);
        }

        // ---------------------------------------------------------------- activation + cooldown
        /// <summary>Cooldown row for the ability's CURRENT level (behavior follows level).</summary>
        public AbilityLevelData CurrentRow(string abilityId)
        {
            AbilityDefinitionData def = Find(abilityId);
            return def != null ? def.LevelRow(Level(abilityId)) : null;
        }

        public float CooldownRemaining(string abilityId)
        {
            float readyAt;
            if (!_readyAt.TryGetValue(abilityId, out readyAt)) return 0f;
            float remaining = readyAt - Now();
            return remaining > 0f ? remaining : 0f;
        }

        public bool OnCooldown(string abilityId) { return CooldownRemaining(abilityId) > 0f; }

        /// <summary>
        /// Activates an ability: validates access -> cost -> cooldown, then publishes
        /// AbilityUsedEvent (payload = the level row: cooldown/power/radius/duration) and
        /// starts the cooldown. Effects in the world (VFX, NPC reactions, UI) subscribe to
        /// the event - the manager knows no Unity types.
        /// </summary>
        public AbilityActivation Activate(string abilityId)
        {
            AbilityDefinitionData def = Find(abilityId);
            if (def == null) return AbilityActivation.Unknown;

            AbilityAccessState access = AccessState(abilityId);
            if (access == AbilityAccessState.Blocked) return AbilityActivation.Blocked;
            if (access != AbilityAccessState.Unlocked) return AbilityActivation.Locked;

            AbilityLevelData row = def.LevelRow(Level(abilityId));
            if (row == null) return AbilityActivation.Unknown;

            if (OnCooldown(abilityId)) return AbilityActivation.CoolingDown;
            if (row.energyCost > 0 && _progress.Echoes < row.energyCost) return AbilityActivation.NotEnoughEnergy;

            _readyAt[abilityId] = Now() + (row.cooldown > 0f ? row.cooldown : 0f);
            EventBus.Publish(new AbilityUsedEvent
            {
                abilityId = abilityId,
                level = Level(abilityId),
                cooldown = row.cooldown,
                power = row.power,
                radius = row.radius,
                duration = row.duration,
                energyCost = row.energyCost
            });
            StoryLog.Log("[ABILITY] " + abilityId + " used (Lv " + Level(abilityId) + ", cd " + row.cooldown + "s)");
            return AbilityActivation.Ok;
        }

        /// <summary>Clears all cooldowns (run reset / new life).</summary>
        public void ResetCooldowns() { _readyAt.Clear(); }
    }
}
