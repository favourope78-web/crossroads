using Crossroads.Narrative;
using Crossroads.Prototype;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Non-NPC story beat (echo shards, mementos, consoles...): an Interactable that runs a
    /// data-driven encounter graph. Same content pipeline as NPCs - the graph carries the
    /// per-state lines, the embedded decision and the consequences.
    /// </summary>
    public class StoryEventInteractable : Interactable
    {
        [SerializeField] private string encounterId = "";

        public string EncounterId { get { return encounterId; } }

        public override void OnInteract(GameObject player)
        {
            if (string.IsNullOrEmpty(encounterId)) return;
            if (!GameServices.IsInitialized)
            {
                Debug.LogWarning("[CROSSROADS] GameServices not initialized - cannot run " + encounterId);
                return;
            }
            GameServices.Encounters.Run(encounterId);
            Debug.Log("[CROSSROADS] Story event via interactable: " + encounterId);
        }
    }
}
