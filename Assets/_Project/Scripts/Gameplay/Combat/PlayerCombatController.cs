using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Player-side combat controller (task: basic player combat suitable for Android).
    /// Layers ON TOP of the existing locomotion (PlayerPrototypeController is untouched -
    /// movement/look stay there; this component feeds it a speed multiplier only):
    ///   - Health/defense/statuses via a CombatantState built from CombatSettingsData
    ///     (hp restored from the persisted player_hp var at boot)
    ///   - Basic attack: data-driven melee arc (button/keyboard) -> damage + statuses
    ///   - Ability activation: goes through the EXISTING AbilityManager (POWERS sheet);
    ///     CombatResolution turns the resulting AbilityUsedEvent into real damage
    ///   - Dodge: short dash + immunity frames (dodge_guard status), data-driven numbers
    ///   - Defeat: applies the authored consequences (CombatResolution.DefeatPlayer),
    ///     revives at the checkpoint - the save is never destroyed
    /// Mobile: no per-frame allocations; input = on-screen buttons + F / LeftShift keys.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCombatController : MonoBehaviour
    {
        [Tooltip("Where the player respawns after a defeat (scene checkpoint).")]
        [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 1f, -16f);

        private CombatSettingsData _settings;
        private CombatantState _combatant;
        private CharacterController _cc;
        private float _attackCooldownUntil;
        private float _dodgeCooldownUntil;
        private float _dodgeRemaining;
        private Vector3 _dodgeDirection;
        private bool _defeatHandled;
        private float _playerSpeedMultiplier = 1f;
        private float _nextPersist;

        public CombatantState Combatant { get { return _combatant; } }
        public bool Dodging { get { return _dodgeRemaining > 0f; } }
        public float HealthFraction
        {
            get { return _combatant != null ? _combatant.Health / _combatant.MaxHealth : 1f; }
        }

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            if (GameServices.IsInitialized && GameServices.Content != null && GameServices.Content.Content != null)
            {
                _settings = GameServices.Content.Content.combat;
                if (_settings == null) _settings = new CombatSettingsData();
                _combatant = CombatantState.ForPlayer(_settings);

                // restore persisted health (fresh runs start full)
                float saved = GameServices.State.GetVar(_settings.healthVarKey, -1);
                if (saved >= 0f) _combatant.RestoreHealth(saved);
            }
            else
            {
                _settings = new CombatSettingsData();
                _combatant = CombatantState.ForPlayer(_settings);
            }
        }

        private void Update()
        {
            if (_combatant == null) return;
            float now = Time.time;

            // statuses: dodge-guard immunity, suppression slow, tide soothing...
            _combatant.TickStatuses(Time.deltaTime);

            // feed the locomotion controller (additive hook, no rewrite)
            _playerSpeedMultiplier = _combatant.MoveSpeedMultiplier;
            PlayerPrototypeController.ExternalSpeedMultiplier = _playerSpeedMultiplier;

            // defeat handling (once)
            if (!_combatant.Alive && !_defeatHandled)
            {
                _defeatHandled = true;
                OnDefeated();
            }

            // dodge movement (CharacterController dash)
            if (_dodgeRemaining > 0f)
            {
                float step = (_settings.dodgeDistance / _settings.dodgeDurationSeconds) * Time.deltaTime;
                _cc.Move(_dodgeDirection * step + Vector3.down * 2f * Time.deltaTime);
                _dodgeRemaining -= Time.deltaTime;
            }

            // keyboard fallbacks (mobile uses the CombatHUD buttons)
            if (!InputLock.Active)
            {
                if (ConsumeKeyDown(KeyCode.F)) TryAttack();
                if (ConsumeKeyDown(KeyCode.LeftShift)) TryDodge();
            }

            // throttle hp persistence to at most every 3s while combat breathes
            if (now >= _nextPersist && _settings != null)
            {
                _nextPersist = now + 3f;
                PersistHealth();
            }
        }

        private static bool ConsumeKeyDown(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(key)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (key == KeyCode.F && keyboard.fKey.wasPressedThisFrame) return true;
                if (key == KeyCode.LeftShift && keyboard.leftShiftKey.wasPressedThisFrame) return true;
            }
#endif
            return false;
        }

        // ---------------------------------------------------------------- actions
        /// <summary>Basic attack (ATTACK button / F): data-driven melee arc in facing direction.</summary>
        public bool TryAttack()
        {
            if (_combatant == null || !_combatant.Alive || InputLock.Active) return false;
            if (Time.time < _attackCooldownUntil) return false;
            AttackDefinitionData attack = _settings.basicAttack;
            _attackCooldownUntil = Time.time + attack.cooldownSeconds;

            Point3 origin = ToPoint3(transform.position);
            float facing = transform.eulerAngles.y;

            List<EnemyAgent> hits = CombatDirector.QueryEnemies(origin, facing, attack.range, attack.arcDegrees);
            if (hits.Count == 0)
            {
                EventBus.Publish(new NoticeRequestEvent { text = "Your strike cuts empty air" });
                return true; // the swing happened; it just missed
            }

            var lib = GameServices.Content != null && GameServices.Content.Content != null
                ? GameServices.Content.Content.statusEffects : null;
            for (int i = 0; i < hits.Count; i++)
            {
                CombatantState target = hits[i].Combatant;
                if (target == null || !target.Alive) continue;
                var r = target.ApplyDamage(attack.damageType, attack.baseDamage);
                if (r.dodged) continue;
                ApplyAttackStatuses(attack, lib, target);
                EventBus.Publish(new NoticeRequestEvent
                { text = "Hit " + hits[i].name + " for " + r.amount.ToString("0") });
            }
            return true;
        }

        /// <summary>Dodge (DODGE button / Shift): dash along facing + immunity frames.</summary>
        public bool TryDodge()
        {
            if (_combatant == null || !_combatant.Alive || InputLock.Active) return false;
            if (Time.time < _dodgeCooldownUntil || Dodging) return false;
            _dodgeCooldownUntil = Time.time + _settings.dodgeCooldownSeconds;
            _dodgeRemaining = _settings.dodgeDurationSeconds;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            _dodgeDirection = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

            var guard = GameServices.Content != null && GameServices.Content.Content != null
                ? GameServices.Content.Content.FindStatusEffect(_settings.dodgeStatusId) : null;
            if (guard != null) _combatant.ApplyStatus(guard);
            EventBus.Publish(new NoticeRequestEvent { text = "You flow aside" });
            return true;
        }

        private static void ApplyAttackStatuses(AttackDefinitionData attack, List<StatusEffectDefinitionData> lib, CombatantState target)
        {
            if (attack.applyStatusIds == null || lib == null) return;
            for (int i = 0; i < attack.applyStatusIds.Count; i++)
                for (int j = 0; j < lib.Count; j++)
                    if (lib[j] != null && lib[j].id == attack.applyStatusIds[i])
                    {
                        target.ApplyStatus(lib[j]);
                        break;
                    }
        }

        // ---------------------------------------------------------------- defeat / persistence
        private void OnDefeated()
        {
            CombatResolution.DefeatPlayer(_settings, GameServices.State);
            EventBus.Publish(new NoticeRequestEvent { text = "The hall goes dark - and puts you back on your feet" });

            // revive at the checkpoint, full health (save untouched beyond the authored effects)
            _combatant.ReviveFull();
            _defeatHandled = false;
            if (_cc != null) _cc.enabled = false;
            transform.position = respawnPosition;
            if (_cc != null) _cc.enabled = true;
            PersistHealth();
            GameServices.PersistNow(autosaveMirror: true);
        }

        /// <summary>Health persists in the existing vars table (throttled; also on combat ends).</summary>
        public void PersistHealth()
        {
            if (_combatant == null || _settings == null || !GameServices.IsInitialized) return;
            GameServices.State.SetVar(_settings.healthVarKey, Mathf.RoundToInt(_combatant.Health));
        }

        private static Point3 ToPoint3(Vector3 v) { return new Point3(v.x, v.y, v.z); }
    }
}
