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
            var sheetPanel = RuntimeMenuFactory.CreatePanel("DialogueSheet", parent, RuntimeMenuFactory.Panel);
            _sheet = sheetPanel.rectTransform;
            _sheet.anchorMin = new Vector2(0f, 0f);
            _sheet.anchorMax = new Vector2(1f, 0f);
            _sheet.pivot = new Vector2(0.5f, 0f);
            _sheet.offsetMin = new Vector2(36f, 28f);
            _sheet.offsetMax = new Vector2(-36f, 0f);
            _sheet.sizeDelta = new Vector2(0f, 400f);

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

            HideSilently();
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
            _sheet.gameObject.SetActive(true);
            _sheet.sizeDelta = new Vector2(0f, 400f);
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
            _hint.text = "";
            _timer.text = e.timeLimitSeconds > 0f ? "⏱ " + e.timeLimitSeconds.ToString("0.0") : "";
            SetBodyMode(true);
            _body.text = e.promptText;
            BuildChoices(e.choices);
        }

        private void OnEnded(DialogueEndedEvent e)
        {
            _running = false;
            _decisionMode = false;
            HideSilently();
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
                    _timer.text = "⏱ " + _timeLimit.ToString("0.0");
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
                var btn = RuntimeMenuFactory.CreateButton("Choice_" + choice.optionId, _choiceArea, choice.text, 34,
                    RuntimeMenuFactory.PanelSoft, RuntimeMenuFactory.TextMain);
                var rect = ((Image)btn.targetGraphic).rectTransform;
                float y = 100f + i * (slotH + gap);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.offsetMin = new Vector2(44f, y);
                rect.offsetMax = new Vector2(-44f, y + slotH);
                string optionId = choice.optionId;
                btn.onClick.AddListener(() => OnChoice(optionId));
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
            _sheet.gameObject.SetActive(false);
            _running = false;
        }
    }
}
