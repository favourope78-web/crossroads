using System.Collections;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Mobile dialogue + decision sheet (GAME_DESIGN §4.5/§8.1):
    ///  - typewriter body text, tap to fast-complete, tap again to advance
    ///  - speaker name chip (narration lines omit it)
    ///  - decision mode: 2-3 full-width choice cards (>= ~100dp tall), affinity feedback AFTER selection
    ///  - D2 pressure choices show a countdown (timeLimitSeconds > 0)
    /// Pacing is driven by EncounterFlow (Advance/SelectChoice) - the UI never interprets content.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private float charsPerSecond = 55f;

        private RectTransform _sheet;
        private Text _speaker;
        private Text _titleChip;
        private Text _body;
        private Text _hint;
        private Text _timer;
        private int _shownTenths = -1;
        private CanvasGroup _sheetGroup;      // sheet slide/fade (unscaled, 0.18 s)
        private Image _speakerStrip;          // accent strip coloured by speaker line
        private float _slide;                 // 0 hidden .. 1 shown
        private float _slideTarget;
        private float _choiceReveal;          // staggered decision-card reveal timer
        private const float SlideSeconds = 0.18f;
        private const float CardStagger = 0.07f;
        private Button _advanceTarget;
        private RectTransform _choiceArea;
        private readonly List<Button> _choiceButtons = new List<Button>();

        private Coroutine _typewriter;
        private bool _typing;
        private bool _decisionMode;
        private float _timeLimit;
        private int _timeoutIndex;
        private bool _timedOut;
        private bool _running;

        public static DialogueUI Attach(RectTransform parent)
        {
            var ui = parent.gameObject.AddComponent<DialogueUI>();
            ui.Build(parent);
            return ui;
        }

        private void Build(RectTransform parent)
        {
            var sheetPanel = RuntimeMenuFactory.CreatePanel("DialogueSheet", parent, new Color(0.035f, 0.055f, 0.085f, 0.95f));
            sheetPanel.sprite = UiShapes.RoundRect; // rounded glass (visual pass)
            _sheet = sheetPanel.rectTransform;
            _sheet.anchorMin = new Vector2(0f, 0f);
            _sheet.anchorMax = new Vector2(1f, 0f);
            _sheet.pivot = new Vector2(0.5f, 0f);
            _sheet.offsetMin = new Vector2(36f, 28f);
            _sheet.offsetMax = new Vector2(-36f, 0f);
            _sheet.sizeDelta = new Vector2(0f, 400f);

            _sheetGroup = sheetPanel.gameObject.AddComponent<CanvasGroup>();
            _sheetGroup.alpha = 0f;

            // speaker accent strip (left edge): Ari cyan, Mara tide, Dax stone, Archivist white, Choir hollow
            _speakerStrip = RuntimeMenuFactory.CreatePanel("SpeakerStrip", _sheet, RuntimeMenuFactory.Accent);
            var strip = _speakerStrip.rectTransform;
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.offsetMin = new Vector2(0f, 0f);
            strip.offsetMax = new Vector2(10f, 0f);
            _speakerStrip.raycastTarget = false;

            // tap surface: whole sheet advances the dialogue (disabled in decision mode)
            var tap = RuntimeMenuFactory.CreateButton("TapToAdvance", _sheet, "", 12, new Color(0f, 0f, 0f, 0f), Color.white);
            RuntimeMenuFactory.Stretch(((Image)tap.targetGraphic).rectTransform);
            tap.onClick.AddListener(OnAdvanceTap);
            _advanceTarget = tap;

            _speaker = RuntimeMenuFactory.CreateText("Speaker", _sheet, "", 34, RuntimeMenuFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            _speaker.rectTransform.anchorMin = new Vector2(0f, 1f);
            _speaker.rectTransform.anchorMax = new Vector2(1f, 1f);
            _speaker.rectTransform.pivot = new Vector2(0f, 1f);
            _speaker.rectTransform.offsetMin = new Vector2(44f, -18f);
            _speaker.rectTransform.offsetMax = new Vector2(-44f, -74f);

            // relation/state chip (e.g. "Mara · Warm") - consequence of state on dialogue framing
            _titleChip = RuntimeMenuFactory.CreateText("TitleChip", _sheet, "", 26, RuntimeMenuFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            _titleChip.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleChip.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleChip.rectTransform.pivot = new Vector2(0f, 1f);
            _titleChip.rectTransform.offsetMin = new Vector2(44f, -56f);
            _titleChip.rectTransform.offsetMax = new Vector2(-44f, -92f);

            _body = RuntimeMenuFactory.CreateText("Body", _sheet, "", 40, RuntimeMenuFactory.TextMain, TextAnchor.UpperLeft);
            _body.rectTransform.anchorMin = Vector2.zero;
            _body.rectTransform.anchorMax = Vector2.one;
            _body.rectTransform.offsetMin = new Vector2(44f, 110f);
            _body.rectTransform.offsetMax = new Vector2(-44f, -96f);

            _hint = RuntimeMenuFactory.CreateText("Hint", _sheet, "tap to continue  ▼", 30, RuntimeMenuFactory.TextDim, TextAnchor.MiddleRight);
            _hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            _hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            _hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            _hint.rectTransform.offsetMin = new Vector2(-340f, 30f);
            _hint.rectTransform.offsetMax = new Vector2(-44f, 74f);

            _timer = RuntimeMenuFactory.CreateText("Timer", _sheet, "", 30, RuntimeMenuFactory.Stone, TextAnchor.MiddleLeft);
            _timer.rectTransform.anchorMin = new Vector2(0f, 0f);
            _timer.rectTransform.anchorMax = new Vector2(1f, 0f);
            _timer.rectTransform.pivot = new Vector2(0.5f, 0f);
            _timer.rectTransform.offsetMin = new Vector2(44f, 30f);
            _timer.rectTransform.offsetMax = new Vector2(340f, 74f);

            _choiceArea = RuntimeMenuFactory.CreateRect("Choices", _sheet);
            RuntimeMenuFactory.Stretch(_choiceArea, 44f, 44f, 250f, 100f);

            // ---- letterbox bars (cinematic dialogue framing, visual pass) ----
            _letterTop = RuntimeMenuFactory.CreatePanel("LetterTop", parent, new Color(0.008f, 0.014f, 0.024f, 0.72f));
            _letterTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            _letterTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            _letterTop.rectTransform.pivot = new Vector2(0.5f, 1f);
            _letterTop.rectTransform.offsetMin = new Vector2(0f, -72f);
            _letterTop.rectTransform.offsetMax = new Vector2(0f, 0f);
            _letterTop.raycastTarget = false;

            _letterBottom = RuntimeMenuFactory.CreatePanel("LetterBottom", parent, new Color(0.008f, 0.014f, 0.024f, 0.72f));
            _letterBottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            _letterBottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            _letterBottom.rectTransform.pivot = new Vector2(0.5f, 0f);
            _letterBottom.rectTransform.offsetMin = new Vector2(0f, 0f);
            _letterBottom.rectTransform.offsetMax = new Vector2(0f, 56f);
            _letterBottom.raycastTarget = false;

            _letterTop.gameObject.SetActive(false);
            _letterBottom.gameObject.SetActive(false);

            HideSilently();
        }

        private Image _letterTop;
        private Image _letterBottom;

        private void SetLetterbox(bool on)
        {
            if (_letterTop != null) _letterTop.gameObject.SetActive(on);
            if (_letterBottom != null) _letterBottom.gameObject.SetActive(on);
        }

        // ------------------------------------------------------------------ events
        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnStarted);
            EventBus.Subscribe<DialogueLineEvent>(OnLine);
            EventBus.Subscribe<DecisionPromptEvent>(OnDecisionPrompt);
            EventBus.Subscribe<DialogueEndedEvent>(OnEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnStarted);
            EventBus.Unsubscribe<DialogueLineEvent>(OnLine);
            EventBus.Unsubscribe<DecisionPromptEvent>(OnDecisionPrompt);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnEnded);
        }

        private void OnStarted(DialogueStartedEvent e)
        {
            _running = true;
            _decisionMode = false;
            _timedOut = false;
            _timeLimit = 0f;
            ClearChoices();
            SetLetterbox(true); // cinematic framing while talking (visual pass)
            _sheet.gameObject.SetActive(true);
            _sheet.sizeDelta = new Vector2(0f, 400f);
            _slideTarget = 1f;
            if (_slide <= 0f) ApplySlide(0f);
            if (_speakerStrip != null) _speakerStrip.color = RuntimeMenuFactory.Accent;
            SetBodyMode(false);
            _speaker.text = "";
            _titleChip.text = string.IsNullOrEmpty(e.npcTitle) ? "" : "· " + e.npcTitle;
            _body.text = "";
            _hint.text = "";
            _timer.text = "";
        }

        private void OnLine(DialogueLineEvent e)
        {
            if (!_running) return;
            _decisionMode = false;
            ClearChoices();
            _speaker.text = string.IsNullOrEmpty(e.speaker) ? "" : e.speaker;
            Color line = SpeakerColor(e.speaker);
            _speaker.color = line;
            if (_speakerStrip != null) _speakerStrip.color = line;
            _titleChip.text = "";
            _timer.text = "";
            _hint.text = e.hasNext ? "tap to continue  ▼" : "tap  ▼";
            _sheet.sizeDelta = new Vector2(0f, 400f);
            SetBodyMode(false);
            StartTypewriter(e.text);
        }

        private void OnDecisionPrompt(DecisionPromptEvent e)
        {
            if (!_running) return;
            _decisionMode = true;
            _timedOut = false;
            _timeLimit = e.timeLimitSeconds;
            _timeoutIndex = e.timeoutOptionIndex;
            _speaker.text = "◆  The decision is yours";
            _speaker.color = RuntimeMenuFactory.Stone;
            if (_speakerStrip != null) _speakerStrip.color = RuntimeMenuFactory.Stone;
            _choiceReveal = 0f;
            _hint.text = "";
            _shownTenths = -1;
            _timer.text = e.timeLimitSeconds > 0f ? "⏱ " + e.timeLimitSeconds.ToString("0.0") : "";
            SetBodyMode(true);
            _body.text = e.promptText;
            BuildChoices(e.choices);
        }

        private void OnEnded(DialogueEndedEvent e)
        {
            _running = false;
            _decisionMode = false;
            _slideTarget = 0f; // Update slides the sheet out, then deactivates it
            SetLetterbox(false);
        }

        /// <summary>Speaker -> line colour (UI parity with the character palette; narration = cyan).</summary>
        public static Color SpeakerColor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return RuntimeMenuFactory.Accent;
            string s = speaker.ToLowerInvariant();
            if (s.StartsWith("ari")) return RuntimeMenuFactory.Accent;
            if (s.StartsWith("mara")) return RuntimeMenuFactory.Tide;
            if (s.StartsWith("dax")) return RuntimeMenuFactory.Stone;
            if (s.StartsWith("archivist") || s.StartsWith("system")) return RuntimeMenuFactory.TextMain;
            if (s.StartsWith("kael")) return RuntimeMenuFactory.Ember;
            if (s.StartsWith("odalys")) return RuntimeMenuFactory.Tide;
            if (s.StartsWith("bran")) return RuntimeMenuFactory.Stone;
            if (s.Contains("choir") || s.Contains("hollow") || s.Contains("cantor") || s.Contains("warden")) return new Color(0.62f, 0.32f, 0.78f, 1f);
            return RuntimeMenuFactory.TextDim;
        }

        private void ApplySlide(float t)
        {
            _slide = t;
            if (_sheetGroup != null) _sheetGroup.alpha = t;
            // ease-out slide from 60 px below the resting offset
            float ease = 1f - (1f - t) * (1f - t);
            _sheet.offsetMin = new Vector2(36f, 28f - 60f * (1f - ease));
        }

        // ------------------------------------------------------------------ input
        private void OnAdvanceTap()
        {
            if (!_running) return;
            if (_decisionMode) return; // choices are separate buttons
            if (_typing) { CompleteTypewriter(); return; } // first tap finishes the line
            GameServices.Encounters.Advance();
        }

        private void Update()
        {
            // sheet slide/fade (unscaled so a paused game still settles the UI)
            if (_slide != _slideTarget && _sheet.gameObject.activeSelf)
            {
                float step = (Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f) / SlideSeconds;
                float next = _slideTarget > _slide ? Mathf.Min(_slideTarget, _slide + step) : Mathf.Max(_slideTarget, _slide - step);
                ApplySlide(next);
                if (_slide <= 0f && _slideTarget <= 0f) _sheet.gameObject.SetActive(false);
            }
            // decision cards reveal one after another (top to bottom)
            if (_decisionMode && _choiceButtons.Count > 0 && _choiceReveal < _choiceButtons.Count * CardStagger + 0.01f)
            {
                _choiceReveal += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
                for (int i = 0; i < _choiceButtons.Count; i++)
                {
                    var b = _choiceButtons[i];
                    if (b == null) continue;
                    bool on = _choiceReveal >= i * CardStagger;
                    if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on);
                }
            }

            if (_decisionMode && _timeLimit > 0f && !_timedOut)
            {
                _timeLimit -= Time.deltaTime;
                if (_timeLimit <= 0f)
                {
                    _timeLimit = 0f;
                    _timedOut = true;
                    _timer.text = "⏱ 0.0";
                    GameServices.Encounters.SelectChoice(TimeOutOptionId());
                }
                else
                {
                    // one string per visible tenth instead of one per frame (GC pressure on Android)
                    int tenths = (int)(_timeLimit * 10f + 0.999f);
                    if (tenths != _shownTenths)
                    {
                        _shownTenths = tenths;
                        _timer.text = "⏱ " + (tenths / 10) + "." + (tenths % 10);
                    }
                }
            }
        }

        private string TimeOutOptionId()
        {
            if (!GameServices.IsInitialized) return "";
            var decision = GameServices.Decisions.Get(GameServices.Encounters.CurrentDecisionId ?? "");
            if (decision == null || decision.options.Count == 0) return "";
            int idx = _timeoutIndex < 0 || _timeoutIndex >= decision.options.Count ? 0 : _timeoutIndex;
            return decision.options[idx].id;
        }

        // ------------------------------------------------------------------ choices
        private void BuildChoices(List<DecisionChoiceView> choices)
        {
            ClearChoices();
            if (choices == null) return;

            float slotH = 128f;
            float gap = 14f;
            int n = choices.Count;
            if (n == 0)
            {
                _timer.text = "no choices available";
                return;
            }
            _sheet.sizeDelta = new Vector2(0f, 380f + n * (slotH + gap));
            for (int i = 0; i < n; i++)
            {
                DecisionChoiceView choice = choices[i];
                var btn = RuntimeMenuFactory.CreateButton("Choice_" + choice.optionId, _choiceArea, "", 34,
                    new Color(0.075f, 0.105f, 0.15f, 0.97f), RuntimeMenuFactory.TextMain);
                var img = (Image)btn.targetGraphic;
                img.sprite = UiShapes.RoundRect; // rounded decision card (visual pass)
                var rect = img.rectTransform;
                float y = 100f + i * (slotH + gap);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.offsetMin = new Vector2(44f, y);
                rect.offsetMax = new Vector2(-44f, y + slotH);

                // decision-coloured edge + arrow glyph: reads as a selectable path, not a dev list
                var edge = RuntimeMenuFactory.CreatePanel("Edge", rect, new Color(0.85f, 0.68f, 0.32f, 0.9f));
                var erect = edge.rectTransform;
                erect.anchorMin = erect.anchorMax = new Vector2(0f, 0.5f);
                erect.pivot = new Vector2(0f, 0.5f);
                erect.sizeDelta = new Vector2(7f, slotH - 22f);
                erect.anchoredPosition = new Vector2(16f, 0f);
                edge.raycastTarget = false;

                var arrow = RuntimeMenuFactory.CreateText("Arrow", rect, "\u25B8", 34, new Color(0.85f, 0.68f, 0.32f, 1f),
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
                arrow.rectTransform.offsetMin = new Vector2(-90f, -34f);
                arrow.rectTransform.offsetMax = new Vector2(-30f, 34f);
                arrow.raycastTarget = false;

                var label = RuntimeMenuFactory.CreateText("Label", rect, choice.text, 34, RuntimeMenuFactory.TextMain,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.offsetMin = new Vector2(48f, -52f);
                label.rectTransform.offsetMax = new Vector2(-110f, 52f);
                label.raycastTarget = false;

                string optionId = choice.optionId;
                btn.onClick.AddListener(() => OnChoice(optionId));
                if (i > 0) btn.gameObject.SetActive(false); // revealed by the stagger in Update
                _choiceButtons.Add(btn);
            }
        }

        private void ClearChoices()
        {
            for (int i = 0; i < _choiceButtons.Count; i++)
                if (_choiceButtons[i] != null) Destroy(_choiceButtons[i].gameObject);
            _choiceButtons.Clear();
        }

        private void OnChoice(string optionId)
        {
            if (!_decisionMode) return;
            _decisionMode = false;
            _timedOut = true; // stop the timer
            if (!GameServices.IsInitialized) return;
            GameServices.Encounters.SelectChoice(optionId);
        }

        // ------------------------------------------------------------------ typewriter
        private void StartTypewriter(string fullText)
        {
            if (_typewriter != null) StopCoroutine(_typewriter);
            _typing = true;
            _body.text = "";
            _typewriter = StartCoroutine(TypeRoutine(fullText));
        }

        private IEnumerator TypeRoutine(string fullText)
        {
            float chars = Mathf.Max(1f, charsPerSecond);
            int shown = 0;
            while (shown < fullText.Length)
            {
                shown = Mathf.Min(fullText.Length, shown + Mathf.Max(1, Mathf.RoundToInt(chars * Time.deltaTime)));
                _body.text = fullText.Substring(0, shown);
                yield return null;
            }
            _body.text = fullText;
            _typing = false;
            _typewriter = null;
        }

        private void CompleteTypewriter()
        {
            if (_typewriter != null) { StopCoroutine(_typewriter); _typewriter = null; }
            _typing = false;
        }

        /// <summary>Line mode: body text fills the sheet. Decision mode: prompt pinned to the top.</summary>
        private void SetBodyMode(bool decisionMode)
        {
            var body = _body.rectTransform;
            if (decisionMode)
            {
                body.anchorMin = new Vector2(0f, 1f);
                body.anchorMax = new Vector2(1f, 1f);
                body.pivot = new Vector2(0.5f, 1f);
                body.offsetMin = new Vector2(44f, -140f);
                body.offsetMax = new Vector2(-44f, -18f);
            }
            else
            {
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.pivot = new Vector2(0.5f, 0.5f);
                body.offsetMin = new Vector2(44f, 110f);
                body.offsetMax = new Vector2(-44f, -96f);
            }
        }

        public void HideSilently()
        {
            _slideTarget = 0f;
            ApplySlide(0f);
            _sheet.gameObject.SetActive(false);
            _running = false;
            SetLetterbox(false);
        }
    }
}
