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

            // ---- PAUSE + MAP (top corner cluster, always available) ----
            float side = s.leftHanded ? -1f : 1f;
            _pauseButton = BuildCornerButton("PauseButton", "\u275A\u275A", side, HudTheme.Corner, OnPausePressed);
            _mapButton = BuildCornerButton("MapButton", "\u25A4", side, HudTheme.Corner + HudTheme.SmallButton + 14f, OnMapPressed);

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

        private GameObject _pauseButton;
        private GameObject _mapButton;

        /// <summary>Round glass corner button (pause / map) with an accent ring. edge = distance from that screen edge.</summary>
        private GameObject BuildCornerButton(string name, string glyph, float side, float edge, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(_root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(side < 0f ? 0f : 1f, 1f);
            rect.pivot = new Vector2(side < 0f ? 0f : 1f, 1f);
            rect.anchoredPosition = new Vector2(side * edge, -HudTheme.Corner);
            rect.sizeDelta = new Vector2(HudTheme.SmallButton, HudTheme.SmallButton);

            var back = go.AddComponent<Image>();
            back.sprite = UiShapes.Circle;
            back.color = new Color(0.05f, 0.075f, 0.11f, 0.85f);
            back.raycastTarget = true;

            var ring = new GameObject("Ring");
            var rrect = ring.AddComponent<RectTransform>();
            rrect.SetParent(rect, false);
            rrect.anchorMin = Vector2.zero;
            rrect.anchorMax = Vector2.one;
            rrect.offsetMin = Vector2.zero;
            rrect.offsetMax = Vector2.zero;
            var ringImg = ring.AddComponent<Image>();
            ringImg.sprite = UiShapes.Ring;
            ringImg.color = HudTheme.Stroke;
            ringImg.raycastTarget = false;

            var label = RuntimeMenuFactory.CreateText("Glyph", rect, glyph, 34, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.raycastTarget = false;

            var button = go.AddComponent<Button>();
            button.targetGraphic = back;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.30f, 0.85f, 0.95f, 0.35f);
            colors.pressedColor = new Color(0.30f, 0.85f, 0.95f, 0.55f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return go;
        }

        private void OnMapPressed()
        {
            var bootstrap = FindFirstObjectByType<GameUIBootstrap>();
            if (bootstrap != null) bootstrap.ToggleWorldMap();
        }

        private GameObject BuildActionButton(string name, string label, float size, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            InputSettings s = InputSettingsStore.Current;
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(_root, false);
            float x = s.leftHanded ? -1f : 1f;
            rect.anchorMin = rect.anchorMax = new Vector2(x < 0f ? 0f : 1f, 0f);
            rect.pivot = new Vector2(x < 0f ? 0f : 1f, 0f);
            float margin = size >= 200f ? 70f : 300f;
            float lift = size >= 200f ? 190f : 64f;
            rect.anchoredPosition = new Vector2(x * margin, lift);
            rect.sizeDelta = new Vector2(size, size);

            var back = go.AddComponent<Image>();
            back.sprite = UiShapes.Circle;
            back.color = bg;
            back.raycastTarget = true;

            var ring = new GameObject("Ring");
            var rrect = ring.AddComponent<RectTransform>();
            rrect.SetParent(rect, false);
            rrect.anchorMin = Vector2.zero;
            rrect.anchorMax = Vector2.one;
            rrect.offsetMin = Vector2.zero;
            rrect.offsetMax = Vector2.zero;
            var ringImg = ring.AddComponent<Image>();
            ringImg.sprite = UiShapes.Ring;
            ringImg.color = new Color(1f, 1f, 1f, 0.22f);
            ringImg.raycastTarget = false;

            var text = RuntimeMenuFactory.CreateText("Label", rect, label, size >= 200f ? 44 : 30,
                RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            var button = go.AddComponent<Button>();
            button.targetGraphic = back;
            var colors = button.colors;
            colors.highlightedColor = new Color(RuntimeMenuFactory.Accent.r, RuntimeMenuFactory.Accent.g, RuntimeMenuFactory.Accent.b, 0.35f);
            colors.pressedColor = new Color(RuntimeMenuFactory.Accent.r * 0.8f, RuntimeMenuFactory.Accent.g * 0.8f, RuntimeMenuFactory.Accent.b * 0.8f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return go;
        }

        /// <summary>GameUIBootstrap hides the whole rig behind the main menu (visual pass).</summary>
        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            InputSettings s = InputSettingsStore.Current;
            bool shown = visible && s.showTouchControls != 2;
            if (_root.gameObject.activeSelf != shown) _root.gameObject.SetActive(shown);
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
        private PauseMenuUI _pauseMenu;
        private PlayerCombatController _playerCombat;

        private void OnPausePressed()
        {
            InputBus.SetPressed(MobileButton.Pause);
            if (_pauseMenu == null) _pauseMenu = FindFirstObjectByType<PauseMenuUI>();
            if (_pauseMenu != null) _pauseMenu.Open();
        }

        /// <summary>Tag lookup + GetComponent cached after the first press (a hot path under thumb-mashing).</summary>
        private PlayerCombatController PlayerCombat()
        {
            if (_playerCombat != null) return _playerCombat;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return null;
            _playerCombat = player.GetComponent<PlayerCombatController>();
            return _playerCombat;
        }

        private void OnAttackPressed()
        {
            var controller = PlayerCombat();
            if (controller != null) controller.TryAttack();
        }

        private void OnDodgePressed()
        {
            var controller = PlayerCombat();
            if (controller != null) controller.TryDodge();
        }
    }
}
