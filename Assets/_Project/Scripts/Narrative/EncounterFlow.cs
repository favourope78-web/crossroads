using System.Collections.Generic;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Dialogue + embedded-decision runner (GAME_DESIGN §4.2: "Runner = coroutine-based
    /// DialogueRunner" - here a pure state machine so it is headless-testable; the UI
    /// drives pacing by calling Advance/SelectChoice).
    ///
    /// Flow: Run(encounterId) -> DialogueLineEvent per node -> the UI typewriter shows it
    /// -> UI taps call Advance() -> ... -> node with decisionId -> DecisionPromptEvent ->
    /// UI shows option cards -> UI calls SelectChoice(optionId) -> DecisionManager.Resolve
    /// (effects + persistence + autosave) -> aftermath branch continues -> DialogueEndedEvent.
    ///
    /// Re-runs: a resolved embedded decision is skipped, so condition-gated aftermath
    /// variants and re-talk openers (DecisionNotMade/DecisionWas) prove the stored choice.
    /// </summary>
    public class EncounterFlow
    {
        private readonly IEncounterSource _content;
        private readonly DecisionManager _decisions;
        private readonly StateMutator _state;

        public bool IsRunning { get; private set; }
        public bool AwaitingChoice { get; private set; }
        public string CurrentEncounterId { get; private set; }
        public string CurrentDecisionId { get; private set; }
        public string CurrentNodeId { get; private set; }

        private DialogueGraphData _graph;
        private string _pendingAfterNext = ""; // node entered only after player advances past the afterText line

        public EncounterFlow(IEncounterSource content, DecisionManager decisions, StateMutator state)
        {
            _content = content;
            _decisions = decisions;
            _state = state;
        }

        // ---------------------------------------------------------------- entry
        public void Run(string encounterId)
        {
            if (IsRunning)
            {
                StoryLog.LogWarning("[CROSSROADS] Run(" + encounterId + ") ignored - an encounter is already running");
                return;
            }

            EncounterDefinitionData enc = _content != null ? _content.GetEncounter(encounterId) : null;
            if (enc == null || string.IsNullOrEmpty(enc.graphId))
            {
                StoryLog.LogWarning("[CROSSROADS] Unknown encounter: " + encounterId);
                EndRun();
                return;
            }
            _graph = _content.GetGraph(enc.graphId);
            if (_graph == null)
            {
                StoryLog.LogWarning("[CROSSROADS] Encounter " + encounterId + " has no graph " + enc.graphId);
                EndRun();
                return;
            }

            CurrentEncounterId = encounterId;
            IsRunning = true;
            AwaitingChoice = false;
            CurrentDecisionId = "";
            _pendingAfterNext = "";
            InputLock.Set(true, "dialogue:" + encounterId);
            EventBus.Publish(new DialogueStartedEvent { encounterId = encounterId });
            StoryLog.Log("[CROSSROADS] Encounter started: " + encounterId);

            EnterNode(enc.startNodeId);
        }

        private void EnterNode(string nodeId)
        {
            DialogueNodeData node = _graph.Find(nodeId);
            if (node == null)
            {
                StoryLog.LogWarning("[CROSSROADS] Graph " + _graph.id + " missing node " + nodeId + " - ending");
                EndRun();
                return;
            }
            CurrentNodeId = node.id;

            // Embedded decision: already resolved -> skip straight to the (condition-gated) aftermath;
            // otherwise present the choice cards.
            if (!string.IsNullOrEmpty(node.decisionId))
            {
                if (_decisions.IsResolved(node.decisionId))
                {
                    StoryLog.Log("[CROSSROADS] Decision " + node.decisionId + " already recorded - skipping to aftermath");
                    RouteTo(nextNodeId(node));
                    return;
                }
                PresentDecision(node);
                return;
            }

            // Silent routing node (no line): walk through without publishing.
            bool silent = string.IsNullOrEmpty(node.text) && string.IsNullOrEmpty(node.speaker);
            if (silent)
            {
                RouteTo(nextNodeId(node));
                return;
            }

            if (node.end)
            {
                EndRun();
                return;
            }

            EventBus.Publish(new DialogueLineEvent
            {
                encounterId = CurrentEncounterId,
                speaker = node.speaker,
                text = node.text,
                hasNext = !(string.IsNullOrEmpty(node.nextId) && string.IsNullOrEmpty(node.branchPrefix) && !node.end)
            });
        }

        private void RouteTo(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) EndRun();
            else EnterNode(nodeId);
        }

        private void PresentDecision(DialogueNodeData node)
        {
            AwaitingChoice = true;
            CurrentDecisionId = node.decisionId;
            DecisionNodeData decision = _decisions.Get(node.decisionId);
            EventBus.Publish(new DecisionPromptEvent
            {
                decisionId = node.decisionId,
                promptText = decision != null ? decision.promptText : "",
                choices = _decisions.Present(node.decisionId),
                timeLimitSeconds = decision != null ? decision.timeLimitSeconds : 0f,
                timeoutOptionIndex = decision != null ? decision.timeoutOptionIndex : 0
            });
            StoryLog.Log("[CROSSROADS] Presenting decision " + node.decisionId);
        }

        // ---------------------------------------------------------------- UI callbacks
        /// <summary>UI taps "continue" after a line was shown (or after the afterText line).</summary>
        public void Advance()
        {
            if (!IsRunning || AwaitingChoice) return;

            if (_pendingAfterNext != null && _pendingAfterNext != "")
            {
                string next = _pendingAfterNext;
                _pendingAfterNext = "";
                RouteTo(next);
                return;
            }

            DialogueNodeData node = _graph != null ? _graph.Find(CurrentNodeId) : null;
            if (node == null) { EndRun(); return; }
            if (node.end) { EndRun(); return; }
            RouteTo(nextNodeId(node));
        }

        /// <summary>UI taps an option card.</summary>
        public void SelectChoice(string optionId)
        {
            if (!IsRunning || !AwaitingChoice) return;
            AwaitingChoice = false;

            DialogueNodeData node = _graph.Find(CurrentNodeId);
            string decisionId = CurrentDecisionId;
            CurrentDecisionId = "";

            DecisionResolvedEvent resolved = _decisions.Resolve(decisionId, optionId);
            if (string.IsNullOrEmpty(resolved.optionId))
            {
                EndRun(); // unknown decision/option - end gracefully, nothing recorded
                return;
            }

            // The chosen option's afterText is the player's/narrator's line following the choice.
            string afterNext = nextNodeId(node);
            DecisionNodeData decision = _decisions.Get(decisionId);
            DecisionOptionData option = decision != null ? decision.FindOption(optionId) : null;
            if (option != null && !string.IsNullOrEmpty(option.afterText))
            {
                _pendingAfterNext = afterNext;
                EventBus.Publish(new DialogueLineEvent
                {
                    encounterId = CurrentEncounterId,
                    speaker = "",
                    text = option.afterText,
                    hasNext = true
                });
            }
            else
            {
                RouteTo(afterNext);
            }
        }

        // ---------------------------------------------------------------- internals
        private string nextNodeId(DialogueNodeData node)
        {
            if (node == null) return "";

            if (!string.IsNullOrEmpty(node.branchPrefix))
            {
                // Branch pool = condition-bearing variants; the FIRST passing variant wins.
                // Sequels without conditions never steal a branch - a bare-prefix node acts
                // as the unconditional fallback instead.
                List<DialogueNodeData> candidates = _graph.FindByPrefix(node.branchPrefix);
                for (int i = 0; i < candidates.Count; i++)
                {
                    DialogueNodeData c = candidates[i];
                    if (c.conditions == null || c.conditions.Count == 0) continue;
                    if (ConditionEvaluator.Evaluate(c.conditions, _state)) return c.id;
                }
                DialogueNodeData fallback = _graph.Find(node.branchPrefix);
                if (fallback != null && fallback != node) return fallback.id;
                return "";
            }
            return node.nextId;
        }

        private void EndRun()
        {
            string ended = CurrentEncounterId;
            IsRunning = false;
            AwaitingChoice = false;
            CurrentEncounterId = "";
            CurrentNodeId = "";
            CurrentDecisionId = "";
            _pendingAfterNext = "";
            _graph = null;
            InputLock.Set(false, "");
            EventBus.Publish(new DialogueEndedEvent { encounterId = ended ?? "" });
            StoryLog.Log("[CROSSROADS] Encounter ended: " + ended);
        }
    }
}
