using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Crossroads.Core;

namespace Crossroads.UI
{
    /// <summary>
    /// Story introduction (art production pass): New Game plays a short, tappable
    /// still-and-caption sequence (original keyart beats, crossfaded, typewriter
    /// captions) before the hall opens. Presentation only - it publishes nothing and
    /// changes no state; the caller keeps running the existing new-game flow after.
    ///
    /// Pure-logic beat navigation is static and unit-tested.
    /// </summary>
    public class IntroCinematicUI : MonoBehaviour
    {
        private const float CrossfadeSeconds = 0.7f;
        private const float TypeSeconds = 0.026f;

        private CanvasGroup _group;
        private Image _art;
        private Text _label;
        private Text _caption;
        private Text _hint;
        private int _index = -1;
        private int _shownChars;
        private float _t;
        private bool _advancing;
        private System.Action _onDone;

        public static IntroCinematicUI Attach(RectTransform parent)
        {
            var ic = parent.gameObject.AddComponent<IntroCinematicUI>();
            ic.Build(parent);
            return ic;
        }

        private void Build(RectTransform parent)
        {
            var rootGo = new GameObject("IntroCinematic");
            var rect = rootGo.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _group = rootGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = true;

            RuntimeMenuFactory.CreatePanel("Backdrop", rect, new Color(0.012f, 0.018f, 0.03f, 1f));
            _art = RuntimeMenuFactory.CreatePanel("Art", rect, new Color(0.9f, 0.92f, 0.95f, 0.16f));
            RuntimeMenuFactory.Stretch(_art.rectTransform, 120f, 60f, 200f, 60f);
            _art.preserveAspect = true;

            _label = RuntimeMenuFactory.CreateText("Label", rect, "", 30, HudTheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _label.rectTransform.anchorMin = _label.rectTransform.anchorMax = new Vector2(0.5f, 0.16f);
            _label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _label.rectTransform.sizeDelta = new Vector2(600f, 44f);

            _caption = RuntimeMenuFactory.CreateText("Caption", rect, "", 38, RuntimeMenuFactory.TextMain, TextAnchor.UpperCenter);
            _caption.rectTransform.anchorMin = _caption.rectTransform.anchorMax = new Vector2(0.5f, 0.12f);
            _caption.rectTransform.pivot = new Vector2(0.5f, 0f);
            _caption.rectTransform.sizeDelta = new Vector2(1400f, 170f);

            _hint = RuntimeMenuFactory.CreateText("Hint", rect, "tap to continue", 26, RuntimeMenuFactory.TextDim, TextAnchor.MiddleCenter);
            _hint.rectTransform.anchorMin = _hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            _hint.rectTransform.sizeDelta = new Vector2(600f, 40f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, 26f);

            var skip = RuntimeMenuFactory.CreateButton("Skip", rect, "SKIP  \u25B8", 28,
                new Color(0.06f, 0.09f, 0.13f, 0.85f), RuntimeMenuFactory.TextMain);
            var sr = ((Image)skip.targetGraphic).rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(1f, 1f);
            sr.anchoredPosition = new Vector2(-40f, -40f);
            sr.sizeDelta = new Vector2(200f, 74f);
            skip.onClick.AddListener(Finish);

            rootGo.SetActive(false);
            _rootGo = rootGo;
        }

        private GameObject _rootGo;

        /// <summary>Plays the intro, then invokes onDone (once, guaranteed - including after SKIP).</summary>
        public void Play(System.Action onDone)
        {
            _onDone = onDone;
            _index = -1;
            _rootGo.SetActive(true);
            InputLock.Set(true, "intro");
            Advance();
        }

        private void Advance()
        {
            if (_advancing) return;
            _advancing = true;
            StartCoroutine(CrossfadeThenType());
        }

        private IEnumerator CrossfadeThenType()
        {
            int next = _index + 1;
            if (next >= ArtLibrary.IntroBeats.Length) { Finish(); yield break; }
            _index = next;
            var beat = ArtLibrary.IntroBeats[_index];

            float t = 0f;
            var from = _art.color;
            var to = new Color(from.r, from.g, from.b, 0f);
            while (t < CrossfadeSeconds)
            {
                t += Time.deltaTime;
                _art.color = Color.Lerp(from, to, t / CrossfadeSeconds);
                yield return null;
            }
            var sprite = ArtLibrary.Get(beat.still);
            if (sprite != null) _art.sprite = sprite;
            _label.text = beat.label;
            _shownChars = 0;
            _caption.text = "";
            t = 0f;
            to = new Color(from.r, from.g, from.b, 0.16f);
            while (t < CrossfadeSeconds)
            {
                t += Time.deltaTime;
                _art.color = Color.Lerp(new Color(from.r, from.g, from.b, 0f), to, t / CrossfadeSeconds);
                yield return null;
            }
            _advancing = false;
        }

        private void Update()
        {
            if (!_rootGo.activeSelf || _index < 0 || _advancing) return;
            var beat = ArtLibrary.IntroBeats[_index];
            if (_shownChars < beat.caption.Length)
            {
                _t += Time.deltaTime;
                while (_t >= TypeSeconds && _shownChars < beat.caption.Length)
                {
                    _t -= TypeSeconds;
                    _shownChars++;
                }
                _caption.text = beat.caption.Substring(0, _shownChars);
                _hint.gameObject.SetActive(false);
            }
            else
            {
                _hint.gameObject.SetActive(true);
                if (Input.GetMouseButtonDown(0)) Advance();
            }
        }

        /// <summary>Ends the intro early (SKIP path / tests). Fires the completion callback once.</summary>
        public void FinishPublic() { Finish(); }

        private void Finish()
        {
            if (!_rootGo.activeSelf) return;
            StopAllCoroutines();
            _advancing = false;
            _rootGo.SetActive(false);
            InputLock.Set(false, "");
            var cb = _onDone;
            _onDone = null;
            if (cb != null) cb();
        }

        // ---------------------------------------------------------------- pure logic (tested)
        public static int BeatCount { get { return ArtLibrary.IntroBeats.Length; } }

        public static string LabelFor(int index)
        {
            return ArtLibrary.IntroBeats[ClampIndex(index)].label;
        }

        public static string CaptionFor(int index)
        {
            return ArtLibrary.IntroBeats[ClampIndex(index)].caption;
        }

        private static int ClampIndex(int index)
        {
            return Mathf.Clamp(index, 0, ArtLibrary.IntroBeats.Length - 1);
        }
    }
}
