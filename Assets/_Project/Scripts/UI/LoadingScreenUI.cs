using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Crossroads.Core;

namespace Crossroads.UI
{
    /// <summary>
    /// Boot loading screen (art production pass): the FIRST thing a player sees - keyart,
    /// CROSSROADS wordmark, an honest animated progress bar and a rotating tip. Covers
    /// service boot + first-frame render, then fades to reveal the main menu. Travel
    /// between locations keeps using LocationTransitionFader; this owns the front door.
    ///
    /// Pure-logic parts (progress curve, tip rotation, readiness gating) are static and
    /// unit-tested headlessly.
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        private const float MinSeconds = 1.6f;
        private const float FadeSeconds = 0.6f;
        private const float FillSeconds = 1.1f;

        private Image _barFill;
        private CanvasGroup _group;
        private float _elapsed;
        private bool _servicesReady;
        private bool _done;
        private int _tipIndex = -1;

        public static LoadingScreenUI Attach(RectTransform parent)
        {
            var ls = parent.gameObject.AddComponent<LoadingScreenUI>();
            ls.Build(parent);
            return ls;
        }

        private void Build(RectTransform parent)
        {
            var rootGo = new GameObject("LoadingScreen");
            var rect = rootGo.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // deep backdrop + keyart (art library; graceful without it)
            RuntimeMenuFactory.CreatePanel("Backdrop", rect, new Color(0.016f, 0.023f, 0.037f, 1f));
            if (ArtLibrary.HasKeyart)
            {
                var art = ArtLibrary.CreateArt("Keyart", rect, ArtLibrary.SpriteKind.KeyartMenu,
                    new Color(0.82f, 0.86f, 0.92f, 0.38f));
                art.SetAsFirstSibling();
            }

            _group = rootGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = true;

            // wordmark (logo sprite when present; typographic otherwise)
            var logo = ArtLibrary.Get(ArtLibrary.SpriteKind.Logo);
            if (logo != null)
            {
                var lgo = RuntimeMenuFactory.CreatePanel("Logo", rect, Color.white);
                var limg = lgo;
                limg.sprite = logo;
                limg.preserveAspect = true;
                var lr = limg.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.62f);
                lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(860f, 258f);
            }
            else
            {
                var title = RuntimeMenuFactory.CreateText("Title", rect, "CROSSROADS", 96, RuntimeMenuFactory.TextMain,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
                title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                title.rectTransform.sizeDelta = new Vector2(900f, 160f);
            }

            // progress rail
            var railGo = RuntimeMenuFactory.CreatePanel("Rail", rect, new Color(0.05f, 0.075f, 0.11f, 0.9f));
            var rail = railGo.rectTransform;
            rail.anchorMin = rail.anchorMax = new Vector2(0.5f, 0.30f);
            rail.pivot = new Vector2(0.5f, 0.5f);
            rail.sizeDelta = new Vector2(620f, 10f);
            var fillGo = RuntimeMenuFactory.CreatePanel("Fill", rail, HudTheme.Accent);
            _barFill = fillGo;
            var fr = fillGo.rectTransform;
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
            fr.sizeDelta = new Vector2(0f, 6f);
            fr.anchoredPosition = Vector2.zero;

            var tip = RuntimeMenuFactory.CreateText("Tip", rect, "", 26, RuntimeMenuFactory.TextDim, TextAnchor.MiddleCenter);
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0.5f, 0.24f);
            tip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tip.rectTransform.sizeDelta = new Vector2(1200f, 44f);
            _tip = tip;
        }

        private Text _tip;

        private void Update()
        {
            if (_done) return;
            _elapsed += Time.deltaTime;
            if (_tipIndex != TipIndexFor(_elapsed))
            {
                _tipIndex = TipIndexFor(_elapsed);
                _tip.text = TipText(_tipIndex);
            }
            float p = ProgressFor(_elapsed, _servicesReady);
            if (_barFill != null) _barFill.rectTransform.sizeDelta = new Vector2(614f * p, 6f);
            if (ReadyToDismiss(_elapsed, _servicesReady))
                StartCoroutine(Dismiss());
        }

        private IEnumerator Dismiss()
        {
            if (_done) yield break;
            _done = true;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.deltaTime;
                _group.alpha = Mathf.Clamp01(1f - t / FadeSeconds);
                yield return null;
            }
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        /// <summary>Services finished booting - the bar may complete and the screen may leave.</summary>
        public void MarkServicesReady() { _servicesReady = true; }

        // ---------------------------------------------------------------- pure logic (tested)
        /// <summary>Ease-out progress: fills to ~85% over FillSeconds, completes only when services are ready.</summary>
        public static float ProgressFor(float elapsed, bool servicesReady)
        {
            if (servicesReady && elapsed >= MinSeconds) return 1f;
            float raw = Mathf.Clamp01(elapsed / FillSeconds);
            return 0.85f * (1f - (1f - raw) * (1f - raw));
        }

        public static bool ReadyToDismiss(float elapsed, bool servicesReady)
        {
            return servicesReady && elapsed >= MinSeconds + FadeSeconds;
        }

        public static int TipIndexFor(float elapsed)
        {
            return Mathf.FloorToInt(elapsed / 4f);
        }

        public static string TipText(int index)
        {
            string[] tips =
            {
                "Choices are remembered - by the city, and by the people in it.",
                "Hold the left stick to walk. Push it to the rim to run.",
                "The Fracture Hall monument remembers every answer you give it.",
                "Abilities share one ring: ember burns, tide moves, stone holds.",
                "Your journal records the roads you did not take.",
            };
            return tips[((index % tips.Length) + tips.Length) % tips.Length];
        }
    }
}
