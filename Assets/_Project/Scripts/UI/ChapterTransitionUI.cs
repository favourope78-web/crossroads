using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Crossroads.Core;

namespace Crossroads.UI
{
    /// <summary>
    /// Chapter title cards (art production pass). Subscribes to the existing
    /// CampaignChapterStartedEvent and shows a non-blocking, pointer-transparent card:
    /// kicker line, chapter title, rule lines - then fades. Pure presentation; the
    /// card formats are static and unit-tested.
    /// </summary>
    public class ChapterTransitionUI : MonoBehaviour
    {
        private const float HoldSeconds = 1.9f;
        private const float FadeSeconds = 0.55f;

        private CanvasGroup _group;
        private Text _kicker;
        private Text _title;
        private GameObject _rootGo;
        private Coroutine _running;

        public static ChapterTransitionUI Attach(RectTransform parent)
        {
            var ct = parent.gameObject.AddComponent<ChapterTransitionUI>();
            ct.Build(parent);
            return ct;
        }

        private void Build(RectTransform parent)
        {
            var rootGo = new GameObject("ChapterCard");
            var rect = rootGo.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _group = rootGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // soft letterbox band so the card reads over any scene
            RuntimeMenuFactory.CreatePanel("Band", rect, new Color(0.008f, 0.014f, 0.024f, 0.55f));

            _kicker = RuntimeMenuFactory.CreateText("Kicker", rect, "", 30, HudTheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _kicker.rectTransform.anchorMin = _kicker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _kicker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _kicker.rectTransform.sizeDelta = new Vector2(900f, 42f);
            _kicker.rectTransform.anchoredPosition = new Vector2(0f, 78f);

            _title = RuntimeMenuFactory.CreateText("Title", rect, "", 74, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            _title.rectTransform.anchorMin = _title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _title.rectTransform.sizeDelta = new Vector2(1500f, 110f);

            // rule lines above/below the title (drawn as thin panels - no font dependency)
            var ruleA = RuntimeMenuFactory.CreatePanel("RuleA", rect, HudTheme.Stroke);
            ruleA.rectTransform.anchorMin = ruleA.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ruleA.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ruleA.rectTransform.sizeDelta = new Vector2(430f, 2f);
            ruleA.rectTransform.anchoredPosition = new Vector2(0f, 58f);
            var ruleB = RuntimeMenuFactory.CreatePanel("RuleB", rect, HudTheme.Stroke);
            ruleB.rectTransform.anchorMin = ruleB.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ruleB.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ruleB.rectTransform.sizeDelta = new Vector2(430f, 2f);
            ruleB.rectTransform.anchoredPosition = new Vector2(0f, -58f);

            _rootGo = rootGo;
            rootGo.SetActive(false);

            EventBus.Subscribe<CampaignChapterStartedEvent>(OnChapterStarted);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CampaignChapterStartedEvent>(OnChapterStarted);
        }

        private void OnChapterStarted(CampaignChapterStartedEvent e)
        {
            Show(e.title);
        }

        public void Show(string title)
        {
            if (_rootGo == null) return;
            _kicker.text = KickerText(title);
            _title.text = TitleText(title);
            _rootGo.SetActive(true);
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(HoldAndFade());
        }

        private IEnumerator HoldAndFade()
        {
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.deltaTime;
                _group.alpha = Mathf.Clamp01(t / FadeSeconds);
                yield return null;
            }
            _group.alpha = 1f;
            yield return new WaitForSeconds(HoldSeconds);
            t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.deltaTime;
                _group.alpha = Mathf.Clamp01(1f - t / FadeSeconds);
                yield return null;
            }
            _group.alpha = 0f;
            _rootGo.SetActive(false);
            _running = null;
        }

        // ---------------------------------------------------------------- pure logic (tested)
        /// <summary>Chapter titles arrive as "Chapter 3 - The Long Wall"; the card splits kicker/title at the dash.</summary>
        public static string KickerText(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return "CHAPTER";
            int dash = rawTitle.IndexOf('-');
            return dash > 0 ? rawTitle.Substring(0, dash).Trim().ToUpperInvariant() : "CHAPTER";
        }

        public static string TitleText(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return "Untitled";
            int dash = rawTitle.IndexOf('-');
            return dash > 0 && dash + 1 < rawTitle.Length ? rawTitle.Substring(dash + 1).Trim() : rawTitle.Trim();
        }

        public static float CardDurationSeconds { get { return HoldSeconds + 2f * FadeSeconds; } }
    }
}
