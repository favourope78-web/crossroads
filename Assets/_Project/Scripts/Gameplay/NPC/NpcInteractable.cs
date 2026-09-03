using Crossroads.Prototype;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Interaction entry point of an NPC (drop on the same GameObject as NpcAgent).
    /// The prompt label and the conversation are data-driven: the agent resolves the
    /// first AVAILABLE interaction (condition-gated), so what the INTERACT button says
    /// - and what happens when you press it - changes with the player's state.
    /// The proximity/prompt system (PlayerInteraction) is unchanged.
    /// </summary>
    public class NpcInteractable : Interactable
    {
        [SerializeField] private NpcAgent npc;

        public NpcAgent Agent { get { return npc; } }

        public override string PromptText
        {
            get
            {
                if (npc == null) return base.PromptText;
                string label = npc.PromptLabel();
                return string.IsNullOrEmpty(label) ? base.PromptText : label;
            }
        }

        public override void OnInteract(GameObject player)
        {
            if (npc == null) return;
            npc.Interact(player);
        }
    }
}
