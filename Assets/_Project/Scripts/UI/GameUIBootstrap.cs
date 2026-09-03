using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>
    /// Scene component that builds the whole story UI at runtime (canvas, dialogue sheet,
    /// interaction prompt, state HUD). No UI assets/prefabs needed - prototype-safe and
    /// mobile-first. Subscribes to Core events; state snapshots are pulled via GameServices.
    /// </summary>
    public class GameUIBootstrap : MonoBehaviour
    {
        private InteractionHUD _interaction;
        private DialogueUI _dialogue;
        private StateHUD _state;
        private ToastUI _toast;

        private void Awake()
        {
            var canvas = RuntimeMenuFactory.CreateRoot("GameUI");
            var safe = RuntimeMenuFactory.CreateSafeArea("SafeArea", canvas.transform);

            _interaction = InteractionHUD.Attach(safe);
            _dialogue = DialogueUI.Attach(safe);
            _state = StateHUD.Attach(safe);
            _toast = ToastUI.Attach(safe);

            Debug.Log("[CROSSROADS] Game UI ready (canvas + dialogue + HUD)");
        }

        private void Start()
        {
            // Events published before this UI subscribed (service boot) are replayed by snapshot
            _state.RefreshFromState();
            _interaction.Hide();
            _dialogue.HideSilently();
        }
    }
}
