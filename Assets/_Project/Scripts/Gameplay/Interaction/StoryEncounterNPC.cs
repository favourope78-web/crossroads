using Crossroads.Narrative;
using Crossroads.Prototype;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Story NPC: an Interactable bound to a data-driven encounter (StoryContentLibrary).
    /// Interacting runs the encounter's dialogue graph, whose embedded decision node
    /// presents the choices; effects, persistence and aftermath are handled by the
    /// EncounterFlow/DecisionManager (Narrative) - this component only knows its ID.
    /// Per GAME_DESIGN §9.2 one NPC = one prefab + a state driver; the fate/dialogue
    /// variants are selected by the graph's condition-gated nodes, not by new code.
    /// </summary>
    public class StoryEncounterNPC : Interactable
    {
        [SerializeField] private string encounterId = "";
        [SerializeField] private string npcDisplayName = "?";

        public string EncounterId { get { return encounterId; } }
        public string NpcDisplayName { get { return npcDisplayName; } }

        public override string PromptText
        {
            get { return string.IsNullOrEmpty(npcDisplayName) || npcDisplayName == "?" ? "Talk" : "Talk to " + npcDisplayName; }
        }

        public override void OnInteract(GameObject player)
        {
            if (string.IsNullOrEmpty(encounterId)) return;
            if (!GameServices.IsInitialized)
            {
                Debug.LogWarning("[CROSSROADS] GameServices not initialized - cannot run " + encounterId);
                return;
            }
            // Behavior/title consequences come from the NpcFateDriver if present (state -> title).
            var fate = GetComponent<Gameplay.NpcFateDriver>();
            GameServices.Encounters.Run(encounterId, fate != null ? fate.CurrentTitle : "");
            Debug.Log("[CROSSROADS] Encounter started via NPC: " + encounterId);
        }
    }
}
