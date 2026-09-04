using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Scene-level combat hub (drop one in the scene; no serialized fields):
    ///   - keeps the live enemy registry (register/unregister from EnemyAgent)
    ///   - owns the player's CombatantState reference (PlayerCombatController publishes it)
    ///   - turns EXISTING ability activations into attacks: AbilityUsedEvent (raised by the
    ///     data-driven AbilityManager) -> gather enemies inside the level-row radius
    ///     (registry iteration - no physics, no allocations) -> CombatResolution applies
    ///     damage/heal/statuses from the AbilityCombatData row
    ///   - player position/queries for agents (single cached transform)
    /// Everything is event-driven; per frame the director does nothing at all.
    /// </summary>
    public class CombatDirector : MonoBehaviour
    {
        private static readonly List<EnemyAgent> Enemies = new List<EnemyAgent>(8);
        private static Transform _playerTransform;
        private static bool _bound;

        public static IReadOnlyList<EnemyAgent> LiveEnemies { get { return Enemies; } }

        private void Start()
        {
            Bind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public static void Bind()
        {
            if (_bound) return;
            _bound = true;
            EventBus.Subscribe<AbilityUsedEvent>(OnAbilityUsed);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        public static void Unbind()
        {
            if (!_bound) return;
            _bound = false;
            EventBus.Unsubscribe<AbilityUsedEvent>(OnAbilityUsed);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
            Enemies.Clear();
            _playerController = null;
            _playerTransform = null;
        }

        private static void OnStateReset(StateResetEvent e)
        {
            Enemies.Clear();
            _playerController = null;
            _playerTransform = null;
        }

        // ---------------------------------------------------------------- registry
        public static void Register(EnemyAgent agent)
        {
            if (agent != null && !Enemies.Contains(agent)) Enemies.Add(agent);
        }

        public static void Unregister(EnemyAgent agent)
        {
            Enemies.Remove(agent);
        }

        /// <summary>Called by EnemyAgent when its combatant died (persistence + feedback).</summary>
        public static void OnEnemyDefeated(EnemyAgent agent)
        {
            if (_playerController == null) ResolvePlayer();
            if (_playerController != null) _playerController.PersistHealth();
            GameServices.PersistNow(autosaveMirror: true);
        }

        // ---------------------------------------------------------------- player bridge
        private static PlayerCombatController _playerController;

        private static void ResolvePlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            _playerController = player.GetComponent<PlayerCombatController>();
            if (_playerController != null) _playerTransform = _playerController.transform;
        }

        /// <summary>Player combatant (resolved lazily, cached; null-safe every frame).</summary>
        public static CombatantState PlayerCombatant
        {
            get
            {
                if (_playerController == null) ResolvePlayer();
                return _playerController != null ? _playerController.Combatant : null;
            }
        }

        // keep player position cheap after first resolve
        public static Point3 PlayerPosition()
        {
            if (_playerTransform == null) ResolvePlayer();
            if (_playerTransform != null)
            {
                Vector3 p = _playerTransform.position;
                return new Point3(p.x, p.y, p.z);
            }
            return new Point3(0f, 0f, 0f);
        }

        // ---------------------------------------------------------------- queries (allocation-free)
        /// <summary>Enemies within a melee arc (reuses a static buffer - never allocates).</summary>
        public static List<EnemyAgent> QueryEnemies(Point3 origin, float facingDegrees, float range, float arcDegrees)
        {
            _queryBuffer.Clear();
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                EnemyAgent agent = Enemies[i];
                if (agent == null || !agent.isActiveAndEnabled || agent.Combatant == null || !agent.Combatant.Alive) continue;
                if (CombatResolution.InMeleeArc(origin, ToP3(agent.transform.position), facingDegrees, range, arcDegrees))
                    _queryBuffer.Add(agent);
            }
            return _queryBuffer;
        }

        private static readonly List<EnemyAgent> _queryBuffer = new List<EnemyAgent>(8);

        /// <summary>Enemies within a radius pulse (ability attacks).</summary>
        public static List<EnemyAgent> QueryEnemiesInRadius(Point3 origin, float radius)
        {
            _radiusBuffer.Clear();
            float r2 = radius * radius;
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                EnemyAgent agent = Enemies[i];
                if (agent == null || !agent.isActiveAndEnabled || agent.Combatant == null || !agent.Combatant.Alive) continue;
                Point3 p = ToP3(agent.transform.position);
                float dx = p.x - origin.x, dz = p.z - origin.z;
                if (dx * dx + dz * dz <= r2) _radiusBuffer.Add(agent);
            }
            return _radiusBuffer;
        }

        private static readonly List<EnemyAgent> _radiusBuffer = new List<EnemyAgent>(8);

        // ---------------------------------------------------------------- ability -> attack
        /// <summary>The ONE place abilities become attacks: consumes the existing manager's
        /// event (level-row numbers) + the AbilityCombatData row; applies via pure resolution.</summary>
        private static void OnAbilityUsed(AbilityUsedEvent e)
        {
            if (GameServices.Content == null || GameServices.Content.Content == null) return;
            AbilityCombatData combat = GameServices.Content.Content.FindAbilityCombat(e.abilityId);
            if (combat == null) return; // not a combat ability (utility powers stay harmless)

            if (_playerController == null) ResolvePlayer();
            CombatantState playerState = _playerController != null ? _playerController.Combatant : null;

            List<EnemyAgent> inRadius = QueryEnemiesInRadius(PlayerPosition(), Mathf.Max(e.radius, 1.5f));
            _targetStates.Clear();
            for (int i = 0; i < inRadius.Count; i++) _targetStates.Add(inRadius[i].Combatant);

            CombatResolution.ResolveAbilityAttack(e, combat, GameServices.Content.Content.statusEffects,
                playerState, _targetStates);

            if (_playerController != null) _playerController.PersistHealth();
        }

        private static readonly List<CombatantState> _targetStates = new List<CombatantState>(8);

        private static Point3 ToP3(Vector3 v) { return new Point3(v.x, v.y, v.z); }
    }
}
