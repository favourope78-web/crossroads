using System.Collections.Generic;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Pure, headless-testable NPC state resolver - the "brain" half of the NPC framework.
    /// Connects an NpcDefinitionData to the LIVE player state (GameStateManager + the
    /// decision system): it answers, at any moment,
    ///   - which fate-state is active (first matching conditions -> title + mood line)
    ///   - the behaviour profile to run (base behaviour + state overrides)
    ///   - which interactions are available right now (condition-gated list)
    ///   - the relationship value (bond + tier) with the player
    /// Reapply() is called on Start/load AND on every relevant state event, so NPCs that
    /// "notice" an earlier decision update the moment the state changes (NpcAgent does the
    /// presentation part: movement, materials, events).
    /// No Unity types - directly unit-testable.
    /// </summary>
    public class NpcBrain
    {
        public readonly NpcDefinitionData Definition;
        private readonly GameStateManager _progress;

        /// <summary>Display title (definition display-name, or the active state's title).</summary>
        public string CurrentTitle { get; private set; }

        /// <summary>Authored one-liner describing this state's reaction (toasts/logs).</summary>
        public string MoodLine { get; private set; }

        /// <summary>The active fate state (null = base behaviour/title).</summary>
        public NpcStateData ActiveState { get; private set; }

        /// <summary>Resolved behaviour numbers for the active configuration.</summary>
        public NpcProfile Profile { get; private set; }

        public NpcDefinitionData DefinitionData { get { return Definition; } }
        public string DisplayName
        {
            get { return !string.IsNullOrEmpty(Definition.displayName) ? Definition.displayName : Definition.id; }
        }

        public int Bond { get { return _progress.Bond(Definition.id); } }
        public string BondTier { get { return _progress.BondTier(Definition.id); } }
        public NpcPersonality Personality
        {
            get { return Definition.behaviour != null ? Definition.behaviour.personality : NpcPersonality.Reserved; }
        }

        public NpcBrain(NpcDefinitionData definition, GameStateManager progress)
        {
            Definition = definition ?? new NpcDefinitionData();
            _progress = progress;
            Reapply();
        }

        /// <summary>
        /// Re-resolves against the current game state. Returns true when the resolved
        /// fate-state changed (callers use it to decide whether to publish/toast/move).
        /// </summary>
        public bool Reapply()
        {
            NpcBehaviourData b = Definition.behaviour ?? new NpcBehaviourData();
            NpcStateData prev = ActiveState;

            NpcStateData chosen = null;
            if (Definition.states != null && _progress != null && _progress.State != null)
            {
                for (int i = 0; i < Definition.states.Count; i++)
                {
                    NpcStateData candidate = Definition.states[i];
                    if (candidate == null) continue;
                    if (ConditionEvaluator.Evaluate(candidate.conditions, _progress.State))
                    {
                        chosen = candidate;
                        break;
                    }
                }
            }
            ActiveState = chosen;

            CurrentTitle = chosen != null && !string.IsNullOrEmpty(chosen.title)
                ? chosen.title
                : DisplayName;
            MoodLine = chosen != null ? chosen.moodLine : "";

            Profile = new NpcProfile
            {
                facesPlayer = b.facesPlayer,
                reactRadius = OverrideOr(chosen != null ? chosen.reactRadius : -1f, b.reactRadius),
                approach = OverrideOr(chosen != null ? chosen.approachDistance : -1f, b.approachDistance),
                avoid = OverrideOr(chosen != null ? chosen.avoidDistance : -1f, b.avoidDistance),
                talkDistance = b.talkDistance,
                moveSpeed = OverrideOr(chosen != null ? chosen.moveSpeed : -1f, b.moveSpeed),
                turnSpeed = b.turnSpeed
            };

            return prev != chosen;
        }

        /// <summary>All interactions whose conditions currently pass, in authored order.</summary>
        public List<NpcInteractionData> AvailableInteractions()
        {
            var list = new List<NpcInteractionData>();
            if (Definition.interactions == null) return list;
            for (int i = 0; i < Definition.interactions.Count; i++)
            {
                NpcInteractionData it = Definition.interactions[i];
                if (it == null) continue;
                if (_progress == null || ConditionEvaluator.Evaluate(it.conditions, _progress.State)) list.Add(it);
            }
            return list;
        }

        /// <summary>The interaction the INTERACT button offers (first available).</summary>
        public NpcInteractionData DefaultInteraction()
        {
            List<NpcInteractionData> list = AvailableInteractions();
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>INTERACT button label ("" when no interaction is currently available).</summary>
        public string PromptLabel()
        {
            NpcInteractionData it = DefaultInteraction();
            return it != null ? it.label : "";
        }

        /// <summary>True if an interaction with this id is available right now.</summary>
        public bool InteractionAvailable(string interactionId)
        {
            List<NpcInteractionData> list = AvailableInteractions();
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == interactionId) return true;
            return false;
        }

        private static float OverrideOr(float value, float fallback) { return value >= 0f ? value : fallback; }
    }
}
