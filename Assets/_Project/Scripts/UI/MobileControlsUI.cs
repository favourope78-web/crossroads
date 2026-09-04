using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Gameplay;
using Crossroads.Gameplay.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// The mobile control rig (GAME_DESIGN §8.1): virtual joystick + look pad + PAUSE +
    /// ATTACK/DODGE, laid out so nothing overlaps (INTERACT lives above the stick zone in
    /// InteractionHUD, POWERS/abilities top-right in AbilityHUD). Configurable through
    /// InputSettings (scale, opacity, left-handed mirror) and gated:
    ///   - ATTACK/DODGE appear only while a live enemy is engaged (task 7)
    ///   - INTERACT availability is driven by the interaction system's prompts (task 5)
    /// All widgets produce into the InputBus; nothing here polls per frame except a
    /// 4 Hz combat-presence tick (no allocations).
    /// </summary>
    public class MobileControlsUI : MonoBehaviour
    {
        private RectTransform _root;
        private CanvasGroup _group;
        private VirtualJoystick _joystick;
        private TouchLookPad _lookPad;
        private GameObject _attack;
        private GameObject _dodge;
        private float _nextPresenceTick;
        private bool _combatActive;

        public static MobileControlsUI Attach(RectTransform parent)
        {
            var rig = parent.gameObject.AddComponent<MobileControlsUI>();
            rig.Build(parent);
            return rig;
        }

        private void Build(RectTransform parent)
        {
            var rootGo = new GameObject("ControlRig");
            _root = rootGo.AddComponent<RectTransform>();
            _root.SetParent(parent, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _group = rootGo.AddComponent<CanvasGroup>();

            Rebuild();

            EventBus.Subscribe<InteractPromptEvent>(OnInteractPrompt);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        /// <summary>(Re)creates the widgets for the current settings (handedness/scale/opacity).</summary>
        public void Rebuild()
        {
            InputSettings s = InputSettingsStore.Current;
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                Destroy(_root.GetChild(i).gameObject);
            }

            // look pad FIRST (lowest) so every later control wins its touches
            _lookPad = TouchLookPad.Build(_root, s.leftHanded);

            // joystick on the player's dominant thumb side
            _joystick = VirtualJoystick.Build(_root, !s.leftHanded ? false : true);

            // ---- PAUSE (top-center-right, small, always available) ----
            var pause = RuntimeMenuFactory.CreateButton("PauseButton", _root, "II", 34,
                new Color(0.05f, 0.07f, 0.10f, 0.85f), RuntimeMenuFactory.TextMain);
            var prect = ((Image)pause.targetGraphic).rectTransform;
            prect.anchorMin = prect.anchorMax = new Vector2(0.5f, 1f);
            prect.pivot = new Vector2(0.5f, 1f);
            prect.anchoredPosition = new Vector2(s.leftHanded ? -770f : 770f, -18f);
            prect.sizeDelta = new Vector2(110f, 110f);
            pause.onClick.AddListener(OnPausePressed);

            // ---- ATTACK + DODGE (bottom cluster on the look-pad side; hidden until combat) ----
            _attack = BuildActionButton("AttackButton", "ATK", 210f,
                new Color(0.42f, 0.12f, 0.08f, 0.92f), OnAttackPressed);
            _dodge = BuildActionButton("DodgeButton", "DODGE", 150f,
                new Color(0.10f, 0.24f, 0.32f, 0.92f), OnDodgePressed);
            _attack.SetActive(false);
            _dodge.SetActive(false);
            _combatActive = false;

            ApplySettings();
        }

        private GameObject BuildActionButton(string name, string label, float size, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            InputSettings s = InputSettingsStore.Current;
            var btn = RuntimeMenuFactory.CreateButton(name, _root, label, size >= 200f ? 44 : 32, bg, RuntimeMenuFactory.TextMain);
            var rect = ((Image)btn.targetGraphic).rectTransform;
            float x = s.leftHanded ? -1f : 1f;
            rect.anchorMin = rect.anchorMax = new Vector2(x < 0f ? 0f : 1f, 0f);
            rect.pivot = new Vector2(x < 0f ? 0f : 1f, 0f);
            float margin = size >= 200f ? 70f : 300f;
            float lift = size >= 200f ? 190f : 64f;
            rect.anchoredPosition = new Vector2(x * margin, lift);
            rect.sizeDelta = new Vector2(size, size);
            btn.onClick.AddListener(onClick);
            return btn.gameObject;
        }

        /// <summary>Applies scale/opacity/visibility without rebuilding (called live by the pause menu).</summary>
        public void ApplySettings()
        {
            InputSettings s = InputSettingsStore.Current;
            _group.alpha = s.controlOpacity;
            _root.localScale = new Vector3(s.buttonScale, s.buttonScale, 1f);
            // showTouchControls: 0 Auto (touch-or-desktop-testing -> shown), 1 Always, 2 Never
            _root.gameObject.SetActive(s.showTouchControls != 2);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<InteractPromptEvent>(OnInteractPrompt);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        // ---------------------------------------------------------------- gating wiring
        private void OnInteractPrompt(InteractPromptEvent e)
        {
            // task 5: the interaction system decides whether a mobile action button makes sense
            InputBus.SetAvailable(MobileButton.Interact, e.visible);
        }

        private void OnStateReset(StateResetEvent e)
        {
            InputBus.Reset();
            _combatActive = false;
            _attack.SetActive(false);
            _dodge.SetActive(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPresenceTick) return;
            _nextPresenceTick = Time.unscaledTime + 0.25f; // 4 Hz combat poll - cheap, no allocations

            bool combat = CombatPresence.HasLiveEnemy(CombatDirector.LiveEnemies);
            if (combat != _combatActive)
            {
                _combatActive = combat;
                _attack.SetActive(combat);
                _dodge.SetActive(combat);
                InputBus.SetAvailable(MobileButton.Attack, combat);
                InputBus.SetAvailable(MobileButton.Dodge, combat);
            }
        }

        // ---------------------------------------------------------------- button handlers
        private void OnPausePressed()
        {
            InputBus.SetPressed(MobileButton.Pause);
            var menu = FindFirstObjectByType<PauseMenuUI>();
            if (menu != null) menu.Open();
        }

        private void OnAttackPressed()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var controller = player.GetComponent<PlayerCombatController>();
            if (controller != null) controller.TryAttack();
        }

        private void OnDodgePressed()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var controller = player.GetComponent<PlayerCombatController>();
            if (controller != null) controller.TryDodge();
        }
    }
}
