using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Enemy behaviour FSM states (task: Idle, Detection, Approach, Attack, Take damage,
    /// Defeat). Mirrors NpcMoodState style - a plain enum the UI can label from data.
    /// </summary>
    public enum EnemyState
    {
        Dormant = 0,     // activation conditions unmet (story gate) - stands cold
        Idle = 1,        // active, player not detected
        Alert = 2,       // player detected (Detection) - published once for feedback
        Approach = 3,    // closing the distance
        AttackWindup = 4,// telegraphing its strike
        AttackRecover = 5,// struck; cooldown before it may act again
        Stagger = 6,     // Took a hit - brief stagger
        Defeat = 7       // destroyed; consequences applied by CombatResolution
    }

    /// <summary>Result of one brain tick (the agent acts on Strike).</summary>
    public enum EnemyTickResult
    {
        None = 0,
        Strike = 1       // windup finished: resolve the attack NOW if target still valid
    }

    /// <summary>
    /// Movement/pose sink for the enemy FSM (same pattern as INpcWorld - the Unity agent
    /// implements it with its transform; tests inject a fake).
    /// </summary>
    public interface IEnemyWorld
    {
        Point3 Position { get; }
        void MoveTowards(Point3 target, float speed, float dt);
        void FaceTowards(Point3 target, float turnSpeed, float dt);
    }

    /// <summary>
    /// Pure-C# enemy behaviour brain (task: ONE basic enemy prototype). One tick = one
    /// transition + at most one movement step; everything is a couple of distance/angle
    /// comparisons - no allocations, no raycasts, no pathfinding (mobile budget).
    ///
    /// Transitions:
    ///   Dormant  -> (activation flag raised externally) Idle
    ///   Idle     -> Alert      when player within detectionRadius
    ///   Alert    -> Approach   immediately (Alert exists so the world can react once)
    ///   Approach -> AttackWindup within attackRange; -> Idle beyond leashRadius
    ///   Windup   -> [Strike]   after windupSeconds (agent resolves the hit)
    ///             -> Recover   after the strike
    ///   Recover  -> Approach   after cooldown (slowed by statuses via the combatant)
    ///   any      -> Stagger   when damaged (agent calls OnDamaged)
    ///   Stagger  -> Approach  after staggerSeconds
    ///   any      -> Defeat    when the combatant dies (agent calls OnDefeated)
    /// Statuses on the enemy itself (echo burn, suppression) modify speed and cooldown
    /// through the shared CombatantState - behaviour consequences, not visuals.
    /// </summary>
    public class EnemyBrain
    {
        private readonly EnemyDefinitionData _def;
        private readonly CombatantState _combatant;
        private float _windupTimer;
        private float _recoverTimer;
        private float _staggerTimer;

        public EnemyState State { get; private set; }
        public float PlayerDistance { get; private set; }

        public EnemyBrain(EnemyDefinitionData def, CombatantState combatant)
        {
            _def = def ?? new EnemyDefinitionData();
            _combatant = combatant;
            State = EnemyState.Dormant;
        }

        /// <summary>Story gate lifted (activation conditions passed) - the construct wakes.</summary>
        public void Activate()
        {
            if (State == EnemyState.Dormant && _combatant.Alive) State = EnemyState.Idle;
        }

        /// <summary>Called by the agent when the combatant took a hit (Take damage reaction).</summary>
        public void OnDamaged()
        {
            if (!_combatant.Alive) { OnDefeated(); return; }
            if (State == EnemyState.Dormant || State == EnemyState.Defeat) return;
            _staggerTimer = _def.staggerSeconds > 0f ? _def.staggerSeconds : 0.3f;
            State = EnemyState.Stagger;
        }

        /// <summary>Terminal state (consequences are applied by CombatResolution).</summary>
        public void OnDefeated()
        {
            State = EnemyState.Defeat;
        }

        /// <summary>One behaviour step. talking=true (dialogue lock) freezes the enemy politely.</summary>
        public EnemyTickResult Tick(IEnemyWorld world, float dt, Point3 playerPos, bool playerActive, bool talking)
        {
            if (world == null || State == EnemyState.Dormant || State == EnemyState.Defeat)
                return EnemyTickResult.None;

            if (talking) return EnemyTickResult.None; // encounters hold during dialogue (§4.5)

            PlayerDistance = Point3.Distance(world.Position, playerPos);
            float speed = _def.moveSpeed * (_combatant != null ? _combatant.MoveSpeedMultiplier : 1f);
            float attackRange = MathfMax(_def.attackRange, 0.5f);

            switch (State)
            {
                case EnemyState.Idle:
                    if (playerActive && PlayerDistance <= _def.detectionRadius)
                    {
                        State = EnemyState.Alert; // detection moment (feedback event from agent)
                    }
                    break;

                case EnemyState.Alert:
                    if (playerActive && PlayerDistance > attackRange)
                    {
                        State = EnemyState.Approach;
                    }
                    else if (playerActive)
                    {
                        BeginWindup();
                    }
                    else State = EnemyState.Idle;
                    break;

                case EnemyState.Approach:
                    if (!playerActive || PlayerDistance > _def.leashRadius)
                    {
                        State = EnemyState.Idle; // lost interest
                        break;
                    }
                    if (PlayerDistance <= attackRange)
                    {
                        BeginWindup();
                        break;
                    }
                    world.FaceTowards(playerPos, _def.turnSpeed, dt);
                    world.MoveTowards(playerPos, speed, dt);
                    break;

                case EnemyState.AttackWindup:
                    world.FaceTowards(playerPos, _def.turnSpeed, dt); // tracks during telegraph
                    _windupTimer -= dt;
                    if (_windupTimer <= 0f)
                    {
                        State = EnemyState.AttackRecover;
                        _recoverTimer = AttackCooldown();
                        return EnemyTickResult.Strike;
                    }
                    break;

                case EnemyState.AttackRecover:
                    _recoverTimer -= dt;
                    if (_recoverTimer <= 0f)
                    {
                        if (!playerActive || PlayerDistance > _def.leashRadius) State = EnemyState.Idle;
                        else if (PlayerDistance <= attackRange) BeginWindup();
                        else State = EnemyState.Approach;
                    }
                    break;

                case EnemyState.Stagger:
                    _staggerTimer -= dt;
                    if (_staggerTimer <= 0f)
                    {
                        State = playerActive && PlayerDistance <= _def.leashRadius
                            ? EnemyState.Approach
                            : EnemyState.Idle;
                    }
                    break;
            }
            return EnemyTickResult.None;
        }

        private void BeginWindup()
        {
            State = EnemyState.AttackWindup;
            _windupTimer = _def.attack != null && _def.attack.windupSeconds > 0f ? _def.attack.windupSeconds : 0.25f;
        }

        private float AttackCooldown()
        {
            float cd = _def.attack != null && _def.attack.cooldownSeconds > 0f ? _def.attack.cooldownSeconds : 1.5f;
            return cd * (_combatant != null ? _combatant.AttackRateMultiplier : 1f);
        }

        private static float MathfMax(float a, float b) { return a > b ? a : b; }
    }
}
