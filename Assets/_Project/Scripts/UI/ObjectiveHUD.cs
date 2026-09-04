using System.Text;
using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Mobile-friendly objective HUD (task spec: current objective, progress, completed
    /// objectives, important changes). Top-right panel, runtime-built (no assets),
    /// event-driven: refreshes on ObjectiveChangedEvent (offered/completed/failed/
    /// progress) and on save load/reset - never per frame. Important changes are also
    /// toasted by the ObjectiveManager (NoticeRequestEvent -> ToastUI).
    /// Layout: active objectives (title + counter + checklist) over the completed list
    /// (dimmed ✓ lines), with a brief accent flash when something important changes.
    /// </summary>
    public class ObjectiveHUD : MonoBehaviour
    {
        private Text _body;
        private Image _panel;
        private float _flashUntil;

        public static ObjectiveHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<ObjectiveHUD>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            _panel = RuntimeMenuFactory.CreatePanel("ObjectiveHUD", parent, RuntimeMenuFactory.Panel);
            var rect = _panel.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(-1920f + 220f, -470f);
            rect.offsetMax = new Vector2(-40f, -24f);

            _body = RuntimeMenuFactory.CreateText("Objectives", rect, "", 27, RuntimeMenuFactory.TextMain, TextAnchor.UpperLeft);
            RuntimeMenuFactory.Stretch(_body.rectTransform, 24f, 24f, 16f, 44f);
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void OnStateLoaded(StateLoadedEvent e) { Refresh(); }
        private void OnStateReset(StateResetEvent e) { Refresh(); }

        private void OnObjectiveChanged(ObjectiveChangedEvent e)
        {
            Refresh();
            // important transitions (offered/completed/failed) flash the panel border
            if (e.phase == ObjectivePhase.Active && e.previousPhase == ObjectivePhase.Available) _flashUntil = Time.unscaledTime + 1.6f;
            if (e.phase == ObjectivePhase.Completed || e.phase == ObjectivePhase.Failed) _flashUntil = Time.unscaledTime + 2.2f;
        }

        private void Update()
        {
            bool flash = _flashUntil > 0f && Time.unscaledTime < _flashUntil;
            if (_panel != null)
                _panel.color = flash
                    ? new Color(RuntimeMenuFactory.Accent.r, RuntimeMenuFactory.Accent.g, RuntimeMenuFactory.Accent.b, 0.28f)
                    : RuntimeMenuFactory.Panel;
        }

        /// <summary>Pulls the mission snapshot from the ObjectiveManager (data-driven).</summary>
        public void Refresh()
        {
            if (_body == null) return;
            if (!WorldServices.IsInitialized || WorldServices.Objectives == null)
            {
                _body.text = "";
                return;
            }

            var sb = new StringBuilder();
            sb.Append("<b>OBJECTIVES</b>");

            var active = WorldServices.Objectives.ActiveObjectives();
            var offered = WorldServices.Objectives.OfferedObjectives();
            var completed = WorldServices.Objectives.CompletedObjectives();
            var failed = WorldServices.Objectives.FailedObjectives();

            if (active.Count == 0 && offered.Count == 0)
                sb.Append("\n<color=#99a6b3>Nothing calls to you yet.</color>");

            for (int i = 0; i < active.Count; i++)
            {
                ObjectiveView o = active[i];
                sb.Append("\n<color=#4dd9f2>▶ ").Append(o.title).Append("</color>");
                if (!string.IsNullOrEmpty(o.counterText)) sb.Append("  (").Append(o.counterText).Append(")");
                if (!string.IsNullOrEmpty(o.description)) sb.Append('\n').Append(o.description);
                for (int s = 0; s < o.steps.Count; s++)
                    sb.Append('\n').Append(o.steps[s].StartsWith("[x]")
                        ? "<color=#7fdca8>" + o.steps[s] + "</color>"
                        : o.steps[s]);
            }

            for (int i = 0; i < offered.Count; i++)
                sb.Append("\n<color=#d8c67a>◇ ").Append(offered[i].title).Append(" (available)</color>");

            if (completed.Count > 0 || failed.Count > 0)
            {
                sb.Append("\n<color=#99a6b3>──────</color>");
                for (int i = 0; i < completed.Count; i++)
                    sb.Append("\n<color=#7fdca8>✓ ").Append(completed[i].title).Append("</color>");
                for (int i = 0; i < failed.Count; i++)
                    sb.Append("\n<color=#e08a7a>✗ ").Append(failed[i].title).Append("</color>");
            }

            _body.supportRichText = true;
            _body.text = sb.ToString();
        }
    }
}
