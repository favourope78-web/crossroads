using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// CONTEXTUAL INTERACT PROMPT (VISUAL_TARGET §4/§9): appears near the right action
    /// cluster the moment an interactable is in range. Round touch target (>= 88dp) with
    /// accent ring + contextual label ("TALK TO MARA", "OPEN", "IGNITE"). Hides during
    /// dialogue. Presentation-only - PlayerInteraction owns the range/priority logic.
    /// </summary>
    public class InteractionHUD : MonoBehaviour
    {
        private GameObject _root;
        private GameObject _labelRoot;
        private Text _label;
        private Button _button;
        private Image _ring;
        private float _pulse;
        private string _currentTargetId = "";

        public static InteractionHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<InteractionHUD>();
            hud.Build(parent);
            hud.Hide();
            return hud;
        }

        private void Build(RectTransform parent)
        {
            var go = new GameObject("InteractButton");
            _root = go;
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-64f, 640f);
            rect.sizeDelta = new Vector2(168f, 168f);

            var back = go.AddComponent<Image>();
            back.sprite = UiShapes.Circle;
            back.color = new Color(0.05f, 0.09f, 0.12f, 0.92f);
            back.raycastTarget = true;

            var ringGo = new GameObject("Ring");
            var rrect = ringGo.AddComponent<RectTransform>();
            rrect.SetParent(rect, false);
            rrect.anchorMin = Vector2.zero;
            rrect.anchorMax = Vector2.one;
            rrect.offsetMin = Vector2.zero;
            rrect.offsetMax = Vector2.zero;
            _ring = ringGo.AddComponent<Image>();
            _ring.sprite = UiShapes.Ring;
            _ring.color = new Color(0.30f, 0.85f, 0.95f, 0.9f);
            _ring.raycastTarget = false;

            var glyph = RuntimeMenuFactory.CreateText("Glyph", rect, "\u2726", 52, HudTheme.Accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            glyph.rectTransform.anchorMin = Vector2.zero;
            glyph.rectTransform.anchorMax = Vector2.one;
            glyph.rectTransform.offsetMin = Vector2.zero;
            glyph.rectTransform.offsetMax = Vector2.zero;
            glyph.raycastTarget = false;

            // contextual label pill to the LEFT of the button
            var labelGo = new GameObject("InteractLabel");
            _labelRoot = labelGo;
            var lrect = labelGo.AddComponent<RectTransform>();
            lrect.SetParent(parent, false);
            lrect.anchorMin = lrect.anchorMax = new Vector2(1f, 0f);
            lrect.pivot = new Vector2(1f, 0f);
            lrect.anchoredPosition = new Vector2(-252f, 694f);
            lrect.sizeDelta = new Vector2(520f, 60f);
            var pill = labelGo.AddComponent<Image>();
            pill.sprite = UiShapes.Pill;
            pill.color = new Color(0.045f, 0.065f, 0.095f, 0.9f);
            pill.raycastTarget = false;
            _label = RuntimeMenuFactory.CreateText("Label", lrect, "INTERACT", 30, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            _label.rectTransform.anchorMin = Vector2.zero;
            _label.rectTransform.anchorMax = Vector2.one;
            _label.rectTransform.offsetMin = new Vector2(26f, 0f);
            _label.rectTransform.offsetMax = new Vector2(-26f, 0f);
            _label.raycastTarget = false;

            _button = go.AddComponent<Button>();
            _button.targetGraphic = back;
            var colors = _button.colors;
            colors.highlightedColor = new Color(RuntimeMenuFactory.Accent.r, RuntimeMenuFactory.Accent.g, RuntimeMenuFactory.Accent.b, 0.5f);
            colors.pressedColor = new Color(RuntimeMenuFactory.Accent.r * 0.7f, RuntimeMenuFactory.Accent.g * 0.7f, RuntimeMenuFactory.Accent.b * 0.7f, 1f);
            _button.colors = colors;
            _button.onClick.AddListener(OnPressed);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InteractPromptEvent>(OnPrompt);
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractPromptEvent>(OnPrompt);
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnPrompt(InteractPromptEvent e)
        {
            if (e.visible)
            {
                _currentTargetId = e.interactableId;
                _label.text = string.IsNullOrEmpty(e.label) ? "INTERACT" : e.label.ToUpperInvariant();
                _root.SetActive(true);
                _labelRoot.SetActive(true);
                _pulse = 0f;
            }
            else Hide();
        }

        private void OnDialogueStarted(DialogueStartedEvent e) { Hide(); }
        private void OnDialogueEnded(DialogueEndedEvent e) { /* PlayerInteraction republishes the prompt on unlock */ }

        private void OnPressed()
        {
            // find the player's interaction component and trigger
            var interaction = FindFirstObjectByType<PlayerInteraction>();
            if (interaction != null) interaction.Interact();
        }

        private void Update()
        {
            // gentle availability pulse so the prompt reads as "alive"
            if (_root == null || !_root.activeSelf || _ring == null) return;
            _pulse += Time.unscaledDeltaTime;
            float k = 0.75f + 0.25f * Mathf.Sin(_pulse * 3.2f);
            Color c = _ring.color;
            c.a = 0.55f + 0.35f * k;
            _ring.color = c;
        }

        public void Hide()
        {
            _currentTargetId = "";
            if (_root != null) _root.SetActive(false);
            if (_labelRoot != null) _labelRoot.SetActive(false);
        }
    }
}
