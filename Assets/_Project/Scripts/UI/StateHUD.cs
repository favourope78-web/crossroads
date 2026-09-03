using System.Text;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Player-card HUD (top-left): live snapshot of the player's CURRENT state from
    /// GameStateManager - affinity meters, reputation standings, relationship tiers,
    /// unlocked powers, skills, resources, current area + save status. Refreshes on any
    /// state event and periodically (death-free parkour: cheap string build).
    /// Includes a dev-only RESET button (editor/dev builds) to replay the prototype.
    /// </summary>
    public class StateHUD : MonoBehaviour
    {
        private GameObject _root;
        private Text _status;
        private Text _saveState;
        private float _nextRefresh;
        private float _savedFlashUntil;
        private bool _dirty = true;

        public static StateHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<StateHUD>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            var panel = RuntimeMenuFactory.CreatePanel("StateHUD", parent, RuntimeMenuFactory.Panel);
            _root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(40f, -420f);
            rect.offsetMax = new Vector2(40f + 700f, -24f);

            _status = RuntimeMenuFactory.CreateText("Status", rect, "", 28, RuntimeMenuFactory.TextMain, TextAnchor.UpperLeft);
            RuntimeMenuFactory.Stretch(_status.rectTransform, 24f, 24f, 18f, 48f);

            _saveState = RuntimeMenuFactory.CreateText("Save", rect, "", 26, RuntimeMenuFactory.Accent, TextAnchor.MiddleLeft);
            _saveState.rectTransform.anchorMin = new Vector2(0f, 0f);
            _saveState.rectTransform.anchorMax = new Vector2(1f, 0f);
            _saveState.rectTransform.pivot = new Vector2(0.5f, 0f);
            _saveState.rectTransform.offsetMin = new Vector2(24f, 14f);
            _saveState.rectTransform.offsetMax = new Vector2(-24f, 50f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var reset = RuntimeMenuFactory.CreateButton("ResetRun", rect, "✕ RESET DECISIONS", 24, new Color(0.35f, 0.12f, 0.10f, 0.95f), RuntimeMenuFactory.TextMain);
            var rr = ((Image)reset.targetGraphic).rectTransform;
            rr.anchorMin = new Vector2(1f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(1f, 1f);
            rr.offsetMin = new Vector2(-236f, -48f);
            rr.offsetMax = new Vector2(-10f, -8f);
            reset.onClick.AddListener(OnResetPressed);
#endif
            RefreshFromState();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<DecisionResolvedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<AffinityChangedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<BondChangedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<ReputationChangedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<AbilityUnlockedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<SkillChangedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<ItemChangedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<AreaUnlockedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<AreaChangedEvent>(OnAnyStateEvent);
            EventBus.Subscribe<SaveCompletedEvent>(OnSaveCompleted);
            EventBus.Subscribe<StateResetEvent>(OnAnyStateEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<AffinityChangedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<BondChangedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<ReputationChangedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<SkillChangedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<ItemChangedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<AreaUnlockedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<AreaChangedEvent>(OnAnyStateEvent);
            EventBus.Unsubscribe<SaveCompletedEvent>(OnSaveCompleted);
            EventBus.Unsubscribe<StateResetEvent>(OnAnyStateEvent);
        }

        private void OnStateLoaded(StateLoadedEvent e) { _dirty = true; RefreshFromState(); _saveState.text = e.hadSave ? "save loaded ✓" : "no save - fresh run"; }
        private void OnAnyStateEvent(DecisionResolvedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(AffinityChangedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(BondChangedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(ReputationChangedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(AbilityUnlockedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(SkillChangedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(ItemChangedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(AreaUnlockedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(AreaChangedEvent e) { _dirty = true; }
        private void OnAnyStateEvent(StateResetEvent e) { _dirty = true; _saveState.text = "run reset"; }

        private void OnSaveCompleted(SaveCompletedEvent e)
        {
            _saveState.text = e.ok ? "saved ✓ " + System.DateTime.Now.ToString("HH:mm:ss") : "save FAILED ✕ " + e.error;
            _savedFlashUntil = Time.unscaledTime + 6f;
        }

        private void Update()
        {
            if (_dirty || Time.unscaledTime >= _nextRefresh)
            {
                RefreshFromState();
                _nextRefresh = Time.unscaledTime + 0.8f;
            }
            if (_savedFlashUntil > 0f && Time.unscaledTime > _savedFlashUntil)
            {
                _savedFlashUntil = 0f;
                RefreshFromState();
            }
        }

        /// <summary>Pulls the full player card straight from GameStateManager.</summary>
        public void RefreshFromState()
        {
            if (!GameServices.IsInitialized) return;
            _dirty = false;
            var lines = GameServices.Progress.StatusLines();
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Count - 1) sb.Append('\n');
            }
            _status.text = sb.ToString();
        }

        private void OnResetPressed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameServices.ResetRun();
#endif
        }
    }
}
