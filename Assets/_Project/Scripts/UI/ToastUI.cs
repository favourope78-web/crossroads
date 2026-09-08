using System.Collections;
using System.Text;
using Crossroads.Core;
using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>
    /// Brief "what changed" toast (required after every choice): shows up to 5 short lines
    /// built from the decision's ChangeNotices (data-driven labels), then the save result.
    /// Also shows one-shot world notices (locked gates, area arrivals, pickups).
    /// Short and clear by design - details always live in the state HUD.
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        private UnityEngine.UI.Text _text;
        private UnityEngine.UI.Image _panel;
        private CanvasGroup _group;
        private Coroutine _fade;
        private string _pendingBody = "";
        private float _lift;                 // 0 hidden .. 1 resting
        private float _liftTarget;
        private const float LiftSeconds = 0.16f;

        public static ToastUI Attach(RectTransform parent)
        {
            var toast = parent.gameObject.AddComponent<ToastUI>();
            toast.Build(parent);
            return toast;
        }

        private void Build(RectTransform parent)
        {
            var panel = RuntimeMenuFactory.CreatePanel("Toast", parent, new Color(0.04f, 0.065f, 0.10f, 0.96f));
            panel.sprite = UiShapes.RoundRect; // rounded glass (visual pass)
            var rect = panel.rectTransform;
            // top-centre, under the chapter pill - never fights the dialogue sheet at the bottom
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(-560f, -300f);
            rect.offsetMax = new Vector2(560f, -104f);
            _text = RuntimeMenuFactory.CreateText("Text", rect, "", 30, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter);
            RuntimeMenuFactory.Stretch(_text.rectTransform, 28f, 28f, 14f, 14f);
            _panel = panel;
            _group = panel.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Subscribe<SaveCompletedEvent>(OnSaveCompleted);
            EventBus.Subscribe<NoticeRequestEvent>(OnNotice);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Unsubscribe<SaveCompletedEvent>(OnSaveCompleted);
            EventBus.Unsubscribe<NoticeRequestEvent>(OnNotice);
        }

        private void OnDecisionResolved(DecisionResolvedEvent e)
        {
            var sb = new StringBuilder();
            sb.Append("locked in");
            int shown = 0;
            if (e.notices != null)
            {
                for (int i = 0; i < e.notices.Count && shown < 5; i++)
                {
                    ChangeNotice n = e.notices[i];
                    if (string.IsNullOrEmpty(n.text)) continue;
                    sb.Append('\n').Append(n.text);
                    shown++;
                }
            }
            _pendingBody = sb.ToString();
            Show(_pendingBody + "\n...saving");
        }

        private void OnSaveCompleted(SaveCompletedEvent e)
        {
            string body = _pendingBody;
            _pendingBody = "";
            Show(string.IsNullOrEmpty(body)
                ? (e.ok ? "saved ✓" : "save failed ✕")
                : body + "\n" + (e.ok ? "saved ✓" : "save failed ✕"));
        }

        private void OnNotice(NoticeRequestEvent e)
        {
            _pendingBody = "";
            Show(e.text);
        }

        private void Show(string message)
        {
            if (_text == null) return;
            _text.text = message;
            _text.color = RuntimeMenuFactory.TextMain;
            if (_panel != null && !_panel.gameObject.activeSelf) _panel.gameObject.SetActive(true);
            if (_fade != null) StopCoroutine(_fade);
            _liftTarget = 1f;
            _fade = StartCoroutine(FadeAndHide());
        }

        private IEnumerator FadeAndHide()
        {
            yield return new WaitForSecondsRealtime(3.4f);
            if (_text != null) _text.color = RuntimeMenuFactory.TextDim;
            yield return new WaitForSecondsRealtime(0.4f);
            _liftTarget = 0f; // Update fades/drops the card, then deactivates it
            _fade = null;
        }

        private void Update()
        {
            if (_group == null || _lift == _liftTarget) return;
            float step = (Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f) / LiftSeconds;
            _lift = _liftTarget > _lift ? Mathf.Min(_liftTarget, _lift + step) : Mathf.Max(_liftTarget, _lift - step);
            float ease = 1f - (1f - _lift) * (1f - _lift);
            _group.alpha = _lift;
            var rect = _panel.rectTransform;
            // slides DOWN from under the chapter pill when arriving (top-centre anchor)
            rect.offsetMin = new Vector2(-560f, -300f - 26f * (1f - ease));
            rect.offsetMax = new Vector2(560f, -104f - 26f * (1f - ease));
            if (_lift <= 0f && _liftTarget <= 0f) _panel.gameObject.SetActive(false);
        }
    }
}
