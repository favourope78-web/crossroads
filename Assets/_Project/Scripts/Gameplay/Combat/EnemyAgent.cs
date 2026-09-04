using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Unity host of ONE enemy (same pattern as NpcAgent): owns the data definition, a
    /// CombatantState (health/defense/statuses) and the pure EnemyBrain FSM; bridges them
    /// to the scene:
    ///   - Dormant until the archetype's activationConditions pass (story-gated encounters),
    ///     re-checked on state events - so combat encounters start because of decisions
    ///   - ticks the brain (skips while dialogue locks the world), moves via transform
    ///   - Strike: resolves the data attack against the player (range + arc, immunity
    ///     respected) - damage flows through CombatantState (single combat write path)
    ///   - damage: hit-flash + stagger; defeat: CombatResolution.DefeatEnemy applies the
    ///     authored effects (objective counters, world entities, NPCs) then the body sinks
    /// Mobile: no physics queries, no per-frame allocations (registry iteration + math only).
    /// </summary>
    public class EnemyAgent : MonoBehaviour
    {
        [SerializeField] private string enemyId = "";
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material hitMaterial;          // damage flash
        [SerializeField] private float sinkSeconds = 1.2f;      // defeat animation time

        private EnemyDefinitionData _def;
        private CombatantState _combatant;
        private EnemyBrain _brain;
        private EnemyWorld _world;
        private bool _defeatApplied;
        private float _flashTimer;
        private float _sinkTimer;
        private EnemyState _lastPublished;
        private bool _strikeArcDebug;

        public string EnemyId { get { return enemyId; } }

        // ---- headless test seams (single-assembly tests; never called by gameplay code) ----
        internal void SetDefeatedForTests() { _defeatApplied = true; }
        internal void SetEnabledForTests(bool value) { enabled = value; }
        public bool IsDefeated { get { return _defeatApplied; } }
        public CombatantState Combatant { get { return _combatant; } }
        public EnemyDefinitionData Definition { get { return _def; } }

        private void Start()
        {
            if (GameServices.IsInitialized && GameServices.Content != null && GameServices.Content.Content != null)
            {
                _def = GameServices.Content.Content.FindEnemy(enemyId);
                if (_def == null)
                {
                    StoryLog.LogWarning("[COMBAT] EnemyAgent '" + enemyId + "' has no definition in the story content");
                    return;
                }
                _combatant = CombatantState.ForEnemy(_def);
                _brain = new EnemyBrain(_def, _combatant);
                _world = new EnemyWorld(transform);
                CombatDirector.Register(this);

                if (ConditionEvaluator.Evaluate(_def.activationConditions, GameServices.State)) _brain.Activate();
            }
            Subscribe(true);
        }

        private void OnDestroy()
        {
            Subscribe(false);
            CombatDirector.Unregister(this);
        }

        private void Subscribe(bool on)
        {
            if (on)
            {
                EventBus.Subscribe<DecisionResolvedEvent>(OnStateEvent);
                EventBus.Subscribe<FlagChangedEvent>(OnStateEvent);
                EventBus.Subscribe<AreaUnlockedEvent>(OnStateEvent);
                EventBus.Subscribe<ObjectiveChangedEvent>(OnStateEvent);
                EventBus.Subscribe<StateLoadedEvent>(OnStateEvent);
                EventBus.Subscribe<CombatantDamagedEvent>(OnCombatantDamaged);
            }
            else
            {
                EventBus.Unsubscribe<DecisionResolvedEvent>(OnStateEvent);
                EventBus.Unsubscribe<FlagChangedEvent>(OnStateEvent);
                EventBus.Unsubscribe<AreaUnlockedEvent>(OnStateEvent);
                EventBus.Unsubscribe<ObjectiveChangedEvent>(OnStateEvent);
                EventBus.Unsubscribe<StateLoadedEvent>(OnStateEvent);
                EventBus.Unsubscribe<CombatantDamagedEvent>(OnCombatantDamaged);
            }
        }

        private void OnStateEvent<T>(T e)
        {
            // story state moved: (re)check the activation gate
            if (_def != null && _brain != null && _brain.State == EnemyState.Dormant && GameServices.IsInitialized)
                if (ConditionEvaluator.Evaluate(_def.activationConditions, GameServices.State))
                    _brain.Activate();
        }

        private void OnCombatantDamaged(CombatantDamagedEvent e)
        {
            if (_combatant == null || e.combatantId != _combatant.Id) return;

            // damage feedback: material flash
            if (bodyRenderer != null && hitMaterial != null)
            {
                bodyRenderer.sharedMaterial = hitMaterial;
                _flashTimer = 0.12f;
            }

            if (e.defeated) return; // defeat handled below
            if (_brain != null) _brain.OnDamaged();
        }

        private void Update()
        {
            if (_brain == null || _combatant == null) return;

            // defeat sequence: apply once, then sink the body
            if (_defeatApplied)
            {
                _sinkTimer -= Time.deltaTime;
                if (_sinkTimer <= 0f && gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            if (!_combatant.Alive)
            {
                OnDefeat();
                return;
            }

            // hit flash restore (cheap timer, no allocations)
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && bodyRenderer != null && baseMaterial != null)
                    bodyRenderer.sharedMaterial = baseMaterial;
            }

            _combatant.TickStatuses(Time.deltaTime);

            CombatantState player = CombatDirector.PlayerCombatant;
            bool playerActive = player != null && player.Alive;
            Point3 playerPos = CombatDirector.PlayerPosition();

            EnemyTickResult result = _brain.Tick(_world, Time.deltaTime, playerPos, playerActive, InputLock.Active);

            PublishStateChange();

            if (result == EnemyTickResult.Strike) Strike(player, playerPos);
        }

        /// <summary>The windup finished: resolve the data-driven attack against the player.</summary>
        private void Strike(CombatantState player, Point3 playerPos)
        {
            if (player == null || !player.Alive || _def.attack == null) return;

            // still in range AND inside the telegraphed arc (dodging sideways helps)
            bool inRange = Point3.Distance(_world.Position, playerPos) <= _def.attackRange + 0.4f;
            if (!inRange) return;

            var r = player.ApplyDamage(_def.attack.damageType, _def.attack.baseDamage);
            if (r.dodged)
            {
                EventBus.Publish(new NoticeRequestEvent { text = "You slip under the " + _def.displayName + "'s strike" });
                return;
            }
            // attack-borne statuses on the player (suppression)
            if (GameServices.Content != null && GameServices.Content.Content != null)
            {
                var lib = GameServices.Content.Content.statusEffects;
                for (int i = 0; i < _def.attack.applyStatusIds.Count; i++)
                    for (int j = 0; j < lib.Count; j++)
                        if (lib[j] != null && lib[j].id == _def.attack.applyStatusIds[i])
                        {
                            player.ApplyStatus(lib[j]);
                            break;
                        }
            }
        }

        private void OnDefeat()
        {
            if (_defeatApplied) return;
            _defeatApplied = true;
            _sinkTimer = sinkSeconds;
            PublishState(EnemyState.Defeat);
            CombatResolution.DefeatEnemy(_def, GameServices.State); // effects -> objectives/world
            CombatDirector.OnEnemyDefeated(this);
        }

        private void PublishStateChange()
        {
            if (_brain.State != _lastPublished) PublishState(_brain.State);
        }

        private void PublishState(EnemyState state)
        {
            _lastPublished = state;
            EventBus.Publish(new EnemyStateChangedEvent
            {
                enemyId = enemyId,
                state = state,
                stateLabel = StateLabel(state)
            });
        }

        /// <summary>Data-free tiny label map (HUD chips). Kept local: labels belong to UI copy.</summary>
        private static string StateLabel(EnemyState s)
        {
            switch (s)
            {
                case EnemyState.Dormant: return "dormant";
                case EnemyState.Idle: return "watching";
                case EnemyState.Alert: return "alert!";
                case EnemyState.Approach: return "closing in";
                case EnemyState.AttackWindup: return "striking";
                case EnemyState.AttackRecover: return "recovering";
                case EnemyState.Stagger: return "staggered";
                case EnemyState.Defeat: return "destroyed";
                default: return "";
            }
        }

        /// <summary>Transform bridge for the pure brain (allocation-free movement).</summary>
        private class EnemyWorld : IEnemyWorld
        {
            private readonly Transform _transform;
            public EnemyWorld(Transform t) { _transform = t; }

            public Point3 Position
            {
                get
                {
                    Vector3 p = _transform.position;
                    return new Point3(p.x, p.y, p.z);
                }
            }

            public void MoveTowards(Point3 target, float speed, float dt)
            {
                Vector3 pos = _transform.position;
                Vector3 to = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
                float magnitude = to.magnitude;
                if (magnitude < 0.001f) return;
                Vector3 step = to * (speed * dt / magnitude);
                if (step.sqrMagnitude > to.sqrMagnitude) _transform.position = new Vector3(target.x, pos.y, target.z);
                else _transform.position = pos + step;
            }

            public void FaceTowards(Point3 target, float turnSpeed, float dt)
            {
                Vector3 pos = _transform.position;
                Vector3 dir = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
                if (dir.sqrMagnitude < 0.0001f) return;
                Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, look, Mathf.Clamp01(turnSpeed * dt));
            }
        }
    }
}
