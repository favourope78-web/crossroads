using System.Text;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Top-left run-state strip (affinity meters Ember/Tide/Stone per §3.2, resolved-decision
    /// count, save status) + a dev-only RESET button (editor/dev builds) so the encounter can
    /// be replayed and persistence can be tested ("stop Play -> Play again -> decision remains").
    /// Hollow stays hidden (design: hidden meter).
    /// </summary>
    public class StateHUD : MonoBehaviour
    {
        private GameObject _root;
        private Text _meters;
        private Text _decisions;
        private Text _saveState;
        private Button _reset;
        private float _savedFlashUntil;

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
            rect.offsetMin = new Vector2(40f, -180f);
            rect.offsetMax = new Vector2(40f + 640f, -24f);

            _meters = RuntimeMenuFactory.CreateText("Meters", rect, "", 32, RuntimeMenuFactory.TextMain, TextAnchor.MiddleLeft);
            _meters.rectTransform.anchorMin = new Vector2(0f, 1f);
            _meters.rectTransform.anchorMax = new Vector2(1f, 1f);
            _meters.rectTransform.pivot = new Vector2(0.5f, 1f);
            _meters.rectTransform.offsetMin = new Vector2(24f, -56f);
            _meters.rectTransform.offsetMax = new Vector2(-24f, -18f);

            _decisions = RuntimeMenuFactory.CreateText("Decisions", rect, "", 28, RuntimeMenuFactory.TextDim, TextAnchor.MiddleLeft);
            _decisions.rectTransform.anchorMin = new Vector2(0f, 1f);
            _decisions.rectTransform.anchorMax = new Vector2(1f, 1f);
            _decisions.rectTransform.pivot = new Vector2(0.5f, 1f);
            _decisions.rectTransform.offsetMin = new Vector2(24f, -92f);
            _decisions.rectTransform.offsetMax = new Vector2(-24f, -58f);

            _saveState = RuntimeMenuFactory.CreateText("Save", rect, "", 28, RuntimeMenuFactory.Accent, TextAnchor.MiddleLeft);
            _saveState.rectTransform.anchorMin = new Vector2(0f, 1f);
            _saveState.rectTransform.anchorMax = new Vector2(1f, 1f);
            _saveState.rectTransform.pivot = new Vector2(0.5f, 1f);
            _saveState.rectTransform.offsetMin = new Vector2(24f, -128f);
            _saveState.rectTransform.offsetMax = new Vector2(-24f, -94f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _reset = RuntimeMenuFactory.CreateButton("ResetRun", rect, "✕ RESET DECISIONS", 26, new Color(0.35f, 0.12f, 0.10f, 0.95f), RuntimeMenuFactory.TextMain);
            var rr = _reset.image.rectTransform;
            rr.anchorMin = new Vector2(1f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(1f, 1f);
            rr.offsetMin = new Vector2(-196f, -46f);
            rr.offsetMax = new Vector2(-10f, -6f);
            _reset.onClick.AddListener(OnResetPressed);
#endif
            RefreshFromState();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Subscribe<SaveCompletedEvent>(OnSaveCompleted);
            EventBus.Subscribe<AffinityChangedEvent>(OnAffinityChanged);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Unsubscribe<SaveCompletedEvent>(OnSaveCompleted);
            EventBus.Unsubscribe<AffinityChangedEvent>(OnAffinityChanged);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void OnStateLoaded(StateLoadedEvent e)
        {
            RefreshFromState();
            _saveState.text = e.hadSave ? "save loaded ✓" : "no save - fresh run";
        }

        private void OnDecisionResolved(DecisionResolvedEvent e)
        {
            RefreshFromState();
        }

        private void OnAffinityChanged(AffinityChangedEvent e)
        {
            RefreshFromState();
        }

        private void OnStateReset(StateResetEvent e)
        {
            RefreshFromState();
            _saveState.text = "run reset";
        }

        private void OnSaveCompleted(SaveCompletedEvent e)
        {
            _saveState.text = e.ok ? "saved ✓ " + System.DateTime.Now.ToString("HH:mm:ss") : "save FAILED ✕ " + e.error;
            _savedFlashUntil = Time.unscaledTime + 6f;
        }

        private void Update()
        {
            if (_savedFlashUntil > 0f && Time.unscaledTime > _savedFlashUntil)
            {
                _savedFlashUntil = 0f;
                RefreshFromState();
            }
        }

        /// <summary>Pulls a snapshot straight from GameServices (boot-time refresh).</summary>
        public void RefreshFromState()
        {
            if (!GameServices.IsInitialized) return;
            var s = GameServices.State;
            var sb = new StringBuilder();
            sb.Append("Ember ").Append(s.GetAffinity("ember"));
            sb.Append("   ·   Tide ").Append(s.GetAffinity("tide"));
            sb.Append("   ·   Stone ").Append(s.GetAffinity("stone"));
            _meters.text = sb.ToString();

            int resolved = s.State.decisions != null ? s.State.decisions.Count : 0;
            int total = GameServices.Decisions != null ? GameServices.Decisions.RegisteredCount : 0;
            _decisions.text = "decisions " + resolved + "/" + total + "   ·   echoes " + s.State.echoBank;
        }

        private void OnResetPressed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameServices.ResetRun();
#endif
        }
    }
}
