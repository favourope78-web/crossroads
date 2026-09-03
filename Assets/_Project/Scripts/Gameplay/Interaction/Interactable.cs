using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>
    /// Base interactable for the CROSSROADS interaction system (GAME_DESIGN §8.3).
    /// Proximity volumes (doors, shrines, codex motes, NPCs, levers...): the nearest valid
    /// target gets the [INTERACT] prompt; priority breaks ties (quest > NPC > shrine > collectible).
    /// Subclasses override OnInteract (e.g. DoorInteractable, StoryEncounterNPC).
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [SerializeField] private string promptLabel = "Inspect";
        [SerializeField] protected float interactRadius = 2.5f;
        [Tooltip("Lower wins ties between equally-close targets (design §8.3 priority order).")]
        [SerializeField] protected float priority = 100f;

        public string Label { get { return promptLabel; } }
        public float Radius { get { return interactRadius; } }
        public float Priority { get { return priority; } }

        /// <summary>Text shown on the contextual INTERACT button (override for NPCs: "Talk to Mara").</summary>
        public virtual string PromptText { get { return promptLabel; } }

        /// <summary>Gate for "can this be interacted with right now" (e.g. dialogue running).</summary>
        public virtual bool CanInteract(GameObject player)
        {
            return enabled && !Crossroads.Core.InputLock.Active;
        }

        public bool InRange(Vector3 playerPos)
        {
            return Vector3.Distance(playerPos, transform.position) <= interactRadius;
        }

        public virtual void OnInteract(GameObject player)
        {
            Debug.Log("[CROSSROADS] Inspect: " + name);
        }
    }
}
