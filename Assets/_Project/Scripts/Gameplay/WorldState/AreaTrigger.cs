using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Tracks where the player currently is (persisted as currentArea in GameState).
    /// The current area is shown by the HUD and can gate future content
    /// ("only available in the East Annex"). This is also how accessible-area
    /// consequences become observable state, saved and restored across restarts.
    /// </summary>
    public class AreaTrigger : MonoBehaviour
    {
        [SerializeField] private string areaId = "hall";

        private void OnTriggerEnter(Collider other)
        {
            // The player moves with a CharacterController (the only trigger-capable
            // player collider in the prototype).
            if (other == null || !(other is CharacterController)) return;
            if (!GameServices.IsInitialized) return;
            if (GameServices.Progress.CurrentArea != areaId)
                GameServices.Progress.SetCurrentArea(areaId);
        }
    }
}
