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
        private Coroutine _fade;
        private string _pendingBody = "";

        public static ToastUI Attach(RectTransform parent)
        {
            var toast = parent.gameObject.AddComponent<ToastUI>();
            toast.Build(parent);
            return toast;
        }

        private void Build(RectTransform parent)
        {
            var panel = RuntimeMenuFactory.CreatePanel("Toast", parent, new Color(RuntimeMenuFactory.Panel.r, RuntimeMenuFactory.Panel.g, RuntimeMenuFactory.Panel.b, 0.96f));
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(-560f, 440f);
            rect.offsetMax = new Vector2(560f, 700f);
            _text = RuntimeMenuFactory.CreateText("Text", rect, "", 30, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter);
            RuntimeMenuFactory.Stretch(_text.rectTransform, 28f, 28f, 14f, 14f);
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
            if (gameObject.activeSelf)
            {
                if (_fade != null) StopCoroutine(_fade);
            }
            else
            {
                gameObject.SetActive(true);
            }
            _fade = StartCoroutine(FadeAndHide());
        }

        private IEnumerator FadeAndHide()
        {
            yield return new WaitForSecondsRealtime(3.4f);
            if (_text != null) _text.color = RuntimeMenuFactory.TextDim;
            yield return new WaitForSecondsRealtime(0.4f);
            gameObject.SetActive(false);
            _fade = null;
        }
    }
}
