using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// COMPACT OBJECTIVE TRACKER (VISUAL_TARGET §4): top-right, under the mini-map.
    /// Shows the CURRENT objective ("▶ title · 2/3") - one glance, no scrolling lists,
    /// no ids, no phase dumps. The full diagnostic list stays behind the dev gate
    /// (ObjectiveHUD). Pure text derivation lives in TrackerModel (headless-testable).
    /// </summary>
    public class ObjectiveTrackerHUD : MonoBehaviour
    {
        private GameObject _root;
        private Text _title;
        private Text _counter;
        private Image _flashBar;

        private float _flashUntil;

        public static ObjectiveTrackerHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<ObjectiveTrackerHUD>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            var go = new GameObject("ObjectiveTracker");
            _root = go;
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-HudTheme.Corner, -(HudTheme.Corner + HudTheme.SmallButton + 24f) - 260f - 18f);
            rect.sizeDelta = new Vector2(560f, 96f);

            var back = go.AddComponent<Image>();
            back.sprite = UiShapes.RoundRect;
            back.color = HudTheme.Glass;
            back.raycastTarget = false;

            _flashBar = AddShape(rect, UiShapes.Pill, HudTheme.Accent, new Vector2(8f, 72f), new Vector2(-262f, 0f));
            _flashBar.raycastTarget = false;

            _title = RuntimeMenuFactory.CreateText("Title", rect, "", 28, HudTheme.TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            _title.rectTransform.anchorMin = _title.rectTransform.anchorMax = new Vector2(0f, 1f);
            _title.rectTransform.pivot = new Vector2(0f, 1f);
            _title.rectTransform.offsetMin = new Vector2(28f, -56f);
            _title.rectTransform.offsetMax = new Vector2(-24f, -12f);
            _title.raycastTarget = false;

            _counter = RuntimeMenuFactory.CreateText("Counter", rect, "", 24, HudTheme.Accent,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            _counter.rectTransform.anchorMin = _counter.rectTransform.anchorMax = new Vector2(0f, 0f);
            _counter.rectTransform.pivot = new Vector2(0f, 0f);
            _counter.rectTransform.offsetMin = new Vector2(28f, 10f);
            _counter.rectTransform.offsetMax = new Vector2(-24f, 48f);
            _counter.raycastTarget = false;

            Refresh();
        }

        private static Image AddShape(RectTransform parent, Sprite sprite, Color color, Vector2 size, Vector2 pos)
        {
            var shapeGo = new GameObject("Shape");
            var r = shapeGo.AddComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var img = shapeGo.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnObjectiveChanged(ObjectiveChangedEvent e)
        {
            Refresh();
            if (e.phase == ObjectivePhase.Active && e.previousPhase == ObjectivePhase.Available) _flashUntil = Time.unscaledTime + 1.4f;
            if (e.phase == ObjectivePhase.Completed) _flashUntil = Time.unscaledTime + 2.0f;
        }

        private void OnStateLoaded(StateLoadedEvent e) { Refresh(); }
        private void OnStateReset(StateResetEvent e) { Refresh(); }
        private void OnDialogueStarted(DialogueStartedEvent e) { _root.SetActive(false); }
        private void OnDialogueEnded(DialogueEndedEvent e) { _root.SetActive(true); Refresh(); }

        private void Update()
        {
            if (_flashBar == null) return;
            bool flash = _flashUntil > Time.unscaledTime;
            Color c = _flashBar.color;
            c.a = flash ? 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 4f, 1f) : 0.35f;
            _flashBar.color = c;
        }

        public void Refresh()
        {
            if (_title == null) return;
            List<ObjectiveView> active = null, offered = null;
            if (WorldServices.IsInitialized && WorldServices.Objectives != null)
            {
                active = WorldServices.Objectives.ActiveObjectives();
                offered = WorldServices.Objectives.OfferedObjectives();
            }
            string title, counter;
            TrackerModel.Compose(active, offered, out title, out counter);
            _title.text = title;
            _counter.text = counter;
            _root.SetActive(!string.IsNullOrEmpty(title));
        }
    }

    /// <summary>Pure tracker text derivation (headless-testable): current objective only.</summary>
    public static class TrackerModel
    {
        /// <summary>First active objective (else first offered), formatted for the compact tracker.</summary>
        public static void Compose(List<ObjectiveView> active, List<ObjectiveView> offered, out string title, out string counter)
        {
            ObjectiveView pick = null;
            if (active != null && active.Count > 0) pick = active[0];
            else if (offered != null && offered.Count > 0) pick = offered[0];

            if (pick == null)
            {
                title = "";
                counter = "";
                return;
            }
            title = "\u25B6  " + pick.title;
            counter = string.IsNullOrEmpty(pick.counterText) ? "" : pick.counterText;
            if (active == null || active.Count == 0) counter = string.IsNullOrEmpty(counter) ? "available" : counter + "  ·  available";
        }
    }
}
