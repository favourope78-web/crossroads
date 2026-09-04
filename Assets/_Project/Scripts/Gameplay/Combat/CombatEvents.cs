using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    // ------------------------------------------------------------------ combat events
    // Raised from the single combat write path (CombatantState/CombatResolution) so UI,
    // agents and the world/objective systems react to combat without polling.

    /// <summary>Fired whenever a combatant takes damage (HUD flash, enemy stagger, feedback).</summary>
    public struct CombatantDamagedEvent
    {
        public string combatantId;
        public bool isPlayer;
        public string enemyId;        // "" for the player
        public string displayName;
        public DamageType damageType;
        public float amount;          // final post-mitigation amount (0 = dodged/immune)
        public float rawAmount;       // pre-mitigation (feedback "resisted")
        public bool dodged;           // immunity frames absorbed it
        public float remainingHealth;
        public float maxHealth;
        public bool defeated;
    }

    /// <summary>Fired when a combatant is healed.</summary>
    public struct CombatantHealedEvent
    {
        public string combatantId;
        public bool isPlayer;
        public float amount;
        public float remainingHealth;
        public float maxHealth;
    }

    /// <summary>
    /// Fired exactly once per combatant death. Enemy defeats route their authored
    /// onDefeatEffects through EffectApplier (see CombatResolution), which is what lets a
    /// fight move objectives, world state and NPCs.
    /// </summary>
    public struct CombatantDefeatedEvent
    {
        public string combatantId;
        public bool isPlayer;
        public string enemyId;
        public string displayName;
    }

    /// <summary>Fired when a status effect is applied or expires (HUD chips).</summary>
    public struct StatusChangedEvent
    {
        public string combatantId;
        public bool isPlayer;
        public string statusId;
        public bool active;           // true = applied, false = expired
    }

    /// <summary>Fired on every enemy FSM transition (task: enemy state feedback).</summary>
    public struct EnemyStateChangedEvent
    {
        public string enemyId;
        public EnemyState state;
        public string stateLabel;
    }
}
