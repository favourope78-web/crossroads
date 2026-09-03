using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Contextual [INTERACT] button bottom-left (GAME_DESIGN §8.1 layout): appears when the
    /// player is close enough to an interactable, above the virtual stick zone. Big touch
    /// target (>= 88dp). Labels update per target ("Talk to Mara"). Hides during dialogue.
    /// </summary>
    public class InteractionHUD : MonoBehaviour
    {
        private GameObject _root;
        private Text _label;
        private Button _button;
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
            var panel = RuntimeMenuFactory.CreatePanel("InteractButton", parent, RuntimeMenuFactory.Panel);
            _root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = new Vector2(60f, 150f);
            rect.offsetMax = new Vector2(60f + 320f, 150f + 150f);

            _button = panel.gameObject.AddComponent<Button>();
            _button.targetGraphic = panel;
            var colors = _button.colors;
            colors.highlightedColor = new Color(RuntimeMenuFactory.Accent.r, RuntimeMenuFactory.Accent.g, RuntimeMenuFactory.Accent.b, 0.5f);
            colors.pressedColor = new Color(RuntimeMenuFactory.Accent.r * 0.7f, RuntimeMenuFactory.Accent.g * 0.7f, RuntimeMenuFactory.Accent.b * 0.7f, 1f);
            _button.colors = colors;
            _button.onClick.AddListener(OnPressed);

            _label = RuntimeMenuFactory.CreateText("Label", rect, "INTERACT", 40, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_label.rectTransform, 24f, 24f, 20f, 20f);
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

        public void Hide()
        {
            _currentTargetId = "";
            _root.SetActive(false);
        }
    }
}
