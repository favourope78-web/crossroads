using System;
using System.Collections.Generic;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// The game's decision brain (task spec: register decisions / store selected choices /
    /// check previous decisions / expose decisions to future gameplay systems).
    /// All content is data (DecisionNodeData); this class never hardcodes a choice.
    /// Pure C# - headless-testable.
    /// </summary>
    public class DecisionManager
    {
        private readonly Dictionary<string, DecisionNodeData> _registered = new Dictionary<string, DecisionNodeData>();
        private readonly StateMutator _state;

        /// <summary>Fired after a choice is committed (before autosave); used to request persistence.</summary>
        public event Action<string, string> Resolved;

        public int RegisteredCount { get { return _registered.Count; } }
        public int ResolvedCount { get { return _state.State.decisions.Count; } }

        public DecisionManager(StateMutator state)
        {
            _state = state;
        }

        // ---------------------------------------------------------------- registration
        public void Register(DecisionNodeData node)
        {
            if (node == null || string.IsNullOrEmpty(node.id)) return;
            _registered[node.id] = node;
        }

        public void RegisterAll(IEnumerable<DecisionNodeData> nodes)
        {
            if (nodes == null) return;
            foreach (var n in nodes) Register(n);
        }

        public void Unregister(string decisionId)
        {
            if (!string.IsNullOrEmpty(decisionId)) _registered.Remove(decisionId);
        }

        public bool IsRegistered(string decisionId) { return !string.IsNullOrEmpty(decisionId) && _registered.ContainsKey(decisionId); }

        // ---------------------------------------------------------------- exposure to future systems
        public DecisionNodeData Get(string decisionId)
        {
            DecisionNodeData node;
            return _registered.TryGetValue(decisionId ?? "", out node) ? node : null;
        }

        public bool IsResolved(string decisionId) { return _state.HasDecision(decisionId); }

        public string ResolvedOption(string decisionId) { return _state.DecisionOption(decisionId); }

        public List<ResolvedDecisionEntry> AllDecisions { get { return _state.State.decisions; } }

        /// <summary>Condition-filtered options currently visible to the player.</summary>
        public List<DecisionOptionData> VisibleOptions(string decisionId)
        {
            var result = new List<DecisionOptionData>();
            DecisionNodeData node = Get(decisionId);
            if (node == null) return result;
            for (int i = 0; i < node.options.Count; i++)
            {
                DecisionOptionData o = node.options[i];
                if (o != null && ConditionEvaluator.Evaluate(o.conditions, _state)) result.Add(o);
            }
            return result;
        }

        /// <summary>Builds the UI payload for a pending decision.</summary>
        public List<DecisionChoiceView> Present(string decisionId)
        {
            var views = new List<DecisionChoiceView>();
            List<DecisionOptionData> visible = VisibleOptions(decisionId);
            for (int i = 0; i < visible.Count; i++)
                views.Add(new DecisionChoiceView(visible[i].id, visible[i].text));
            return views;
        }

        // ---------------------------------------------------------------- resolution
        public DecisionResolvedEvent Resolve(string decisionId, string optionId)
        {
            var evt = new DecisionResolvedEvent { decisionId = decisionId, optionId = optionId };

            DecisionNodeData node = Get(decisionId);
            if (node == null)
            {
                StoryLog.LogWarning("[CROSSROADS] Resolve(" + decisionId + "): unknown decision");
                evt.optionId = ""; // signals failure to the runner
                return evt;
            }
            DecisionOptionData option = node.FindOption(optionId);
            if (option == null)
            {
                StoryLog.LogWarning("[CROSSROADS] Resolve(" + decisionId + "): unknown option " + optionId);
                evt.optionId = "";
                return evt;
            }

            // 1. apply effects through the mutator (single write path)
            evt.affinityDeltas = EffectApplier.Apply(option.effects, _state);
            string summary = EffectApplier.Summarize(option.effects, _state);

            // 2. record the decision (persistent history + future condition checks)
            _state.RecordDecision(decisionId, optionId, summary);
            evt.summary = summary;

            // 3. node-level codex entry
            if (!string.IsNullOrEmpty(node.codexEntryId)) _state.AddCodex(node.codexEntryId);

            // 4. tell the world (UI feedback, world-state appliers, autosave hook)
            EventBus.Publish(evt);
            if (Resolved != null) Resolved(decisionId, optionId);
            StoryLog.Log("[CROSSROADS] Decision " + decisionId + " -> " + optionId + " (" + summary + ")");
            return evt;
        }

        /// <summary>D2 pressure choice: auto-resolve when the timer runs out (design: "hesitate" outcome).</summary>
        public DecisionResolvedEvent ResolveTimeout(string decisionId)
        {
            DecisionNodeData node = Get(decisionId);
            if (node == null || node.options.Count == 0) return new DecisionResolvedEvent();
            int idx = node.timeoutOptionIndex < 0 || node.timeoutOptionIndex >= node.options.Count
                ? 0 : node.timeoutOptionIndex;
            return Resolve(decisionId, node.options[idx].id);
        }
    }
}
