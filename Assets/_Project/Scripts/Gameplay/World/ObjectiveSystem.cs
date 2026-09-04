using System;
using System.Collections.Generic;
using System.Text;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// UI-agnostic view of one objective instance (checklist + counter + phase),
    /// pulled by the ObjectiveHUD and the headless tests.
    /// </summary>
    public class ObjectiveView
    {
        public string id = "";
        public string title = "";
        public string description = "";
        public ObjectiveType type = ObjectiveType.Main;
        public ObjectivePhase phase = ObjectivePhase.Hidden;
        public string counterText = "";     // "Braces set 1/2" ("" when no counter)
        public int progress;                // units passed (steps + counter)
        public int target;                  // total units (0 = no measurable progress)
        public List<string> steps = new List<string>();      // display lines ("[x] ..." / "[ ] ...")
        public List<string> openSteps = new List<string>();  // only the unfinished lines
        public string followUpHint = "";    // "Next: <title>" when a follow-up is authored
    }

    /// <summary>
    /// The reusable, data-driven objective/mission runtime (task: unique ID, title,
    /// description, type, requirements, completion, failure, consequences, follow-ups).
    ///
    /// Design rules (all existing systems, none rewritten):
    ///   - mission content is DATA (ObjectiveDefinitionData rows in the story library);
    ///     this class never hardcodes a mission
    ///   - lifecycle Hidden -> Available -> Active -> Completed/Failed/Cancelled is
    ///     persisted through StateMutator (single write path, ObjectiveChangedEvent)
    ///   - EVENT-DRIVEN, not per-frame: the manager subscribes to the state-change
    ///     events (decision, flag, var, item, ability, area, entity, bond, relocation,
    ///     interaction unlock) and re-evaluates only when the world actually changed
    ///   - consequences/failureConsequences go through EffectApplier, so completing a
    ///     mission can do anything a decision can (open/lock areas, spawn/hide objects,
    ///     grant items/abilities, move NPCs, unlock interactions, set world variants)
    ///   - objectives react to previous decisions via offerConditions (the same
    ///     condition whitelist the dialogue system uses) and chain through follow-ups
    /// Pure C# - headless-testable.
    /// </summary>
    public class ObjectiveManager
    {
        private readonly Dictionary<string, ObjectiveDefinitionData> _definitions = new Dictionary<string, ObjectiveDefinitionData>();
        private readonly List<ObjectiveDefinitionData> _ordered = new List<ObjectiveDefinitionData>();
        private readonly StateMutator _state;
        private bool _subscribed;
        private bool _evaluating;

        /// <summary>Fired (in addition to ObjectiveChangedEvent) for the toast UI. Text is data.</summary>
        public event Action<string> Notice;

        public ObjectiveManager(List<ObjectiveDefinitionData> definitions, StateMutator state)
        {
            _state = state;
            RegisterAll(definitions);
        }

        // ---------------------------------------------------------------- registration
        public void Register(ObjectiveDefinitionData definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return;
            if (!_definitions.ContainsKey(definition.id)) _ordered.Add(definition);
            _definitions[definition.id] = definition;
        }

        public void RegisterAll(List<ObjectiveDefinitionData> definitions)
        {
            if (definitions == null) return;
            for (int i = 0; i < definitions.Count; i++) Register(definitions[i]);
        }

        public ObjectiveDefinitionData Find(string objectiveId)
        {
            ObjectiveDefinitionData def;
            return objectiveId != null && _definitions.TryGetValue(objectiveId, out def) ? def : null;
        }

        public int RegisteredCount { get { return _definitions.Count; } }

        // ---------------------------------------------------------------- event wiring
        /// <summary>Subscribes to every state-change event that can move an objective.</summary>
        public void BindEvents()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventBus.Subscribe<DecisionResolvedEvent>(OnWorldChanged);
            EventBus.Subscribe<FlagChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<VarChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<EntityStateChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<ItemChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<AbilityUnlockedEvent>(OnWorldChanged);
            EventBus.Subscribe<AbilityBlockedEvent>(OnWorldChanged);
            EventBus.Subscribe<AreaUnlockedEvent>(OnWorldChanged);
            EventBus.Subscribe<AreaClosedEvent>(OnWorldChanged);
            EventBus.Subscribe<AreaReopenedEvent>(OnWorldChanged);
            EventBus.Subscribe<BondChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<ReputationChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<SkillChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<NpcRelocatedEvent>(OnWorldChanged);
            EventBus.Subscribe<InteractionUnlockedEvent>(OnWorldChanged);
            EventBus.Subscribe<StateResetEvent>(OnWorldChanged);
        }

        public void UnbindEvents()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<FlagChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<VarChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<EntityStateChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<ItemChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<AbilityBlockedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<AreaUnlockedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<AreaClosedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<AreaReopenedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<BondChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<ReputationChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<SkillChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<NpcRelocatedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<InteractionUnlockedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<StateResetEvent>(OnWorldChanged);
        }

        private void OnWorldChanged<T>(T evt)
        {
            Evaluate();
        }

        // ---------------------------------------------------------------- evaluation
        /// <summary>
        /// Re-evaluates every objective against the live state (called on state events
        /// and after a save load - never per frame). Consequences applied inside may
        /// publish more events; the reentrancy guard defers them into the same pass.
        /// </summary>
        public void Evaluate()
        {
            if (_evaluating) return; // cascaded events are covered by this pass
            _evaluating = true;
            try
            {
                for (int i = 0; i < _ordered.Count; i++) EvaluateOne(_ordered[i]);
            }
            finally
            {
                _evaluating = false;
            }
        }

        private void EvaluateOne(ObjectiveDefinitionData def)
        {
            if (def == null || _state == null) return;
            ObjectivePhase phase = _state.GetObjectivePhase(def.id);

            switch (phase)
            {
                case ObjectivePhase.Hidden:
                case ObjectivePhase.Available:
                case ObjectivePhase.Cancelled:
                    // failure can pre-empt an offered-but-untracked objective (rare, allowed);
                    // an EMPTY failConditions list means unfailable (never "always fails")
                    if (HasFailConditions(def) && EvaluateAll(def.failConditions))
                    {
                        Fail(def);
                        return;
                    }
                    if (phase != ObjectivePhase.Available && EvaluateAll(def.offerConditions))
                    {
                        _state.UpdateObjective(def.id, ObjectivePhase.Available, 0);
                        AnnounceOffer(def);
                    }
                    phase = _state.GetObjectivePhase(def.id);
                    if (phase == ObjectivePhase.Available && def.autoActivate)
                    {
                        _state.UpdateObjective(def.id, ObjectivePhase.Active, 0);
                    }
                    break;

                case ObjectivePhase.Active:
                    if (HasFailConditions(def) && EvaluateAll(def.failConditions))
                    {
                        Fail(def);
                        return;
                    }
                    if (IsComplete(def))
                    {
                        Complete(def);
                        return;
                    }
                    UpdateProgress(def);
                    break;

                default:
                    break; // Completed/Failed are terminal for the run
            }
        }

        private bool EvaluateAll(List<DecisionConditionData> conditions)
        {
            return ConditionEvaluator.Evaluate(conditions, _state);
        }

        /// <summary>Unfailable objectives author an empty failConditions list.</summary>
        private static bool HasFailConditions(ObjectiveDefinitionData def)
        {
            return def.failConditions != null && def.failConditions.Count > 0;
        }

        /// <summary>Completion = explicit conditions + every step + the counter target.</summary>
        public bool IsComplete(ObjectiveDefinitionData def)
        {
            if (!EvaluateAll(def.completeConditions)) return false;
            if (def.UsesCounter && _state.GetVar(def.counterVar, 0) < def.counterTarget) return false;
            for (int i = 0; i < def.steps.Count; i++)
                if (!EvaluateAll(def.steps[i].conditions)) return false;
            return true;
        }

        private void Complete(ObjectiveDefinitionData def)
        {
            int progress = ProgressOf(def);
            EffectApplier.Apply(def.consequences, _state);
            _state.UpdateObjective(def.id, ObjectivePhase.Completed, progress);
            StoryLog.Log("[CROSSROADS] Objective completed: " + def.id + " - " + def.title);
            Notify(def, def.completionNotice, "Objective complete - " + def.title);
        }

        private void Fail(ObjectiveDefinitionData def)
        {
            int progress = ProgressOf(def);
            EffectApplier.Apply(def.failureConsequences, _state);
            _state.UpdateObjective(def.id, ObjectivePhase.Failed, progress);
            StoryLog.Log("[CROSSROADS] Objective failed: " + def.id + " - " + def.title);
            Notify(def, def.failureNotice, "Objective failed - " + def.title);
        }

        private void UpdateProgress(ObjectiveDefinitionData def)
        {
            int progress = ProgressOf(def);
            if (progress != _state.GetObjectiveProgress(def.id))
                _state.UpdateObjective(def.id, ObjectivePhase.Active, progress);
        }

        private void AnnounceOffer(ObjectiveDefinitionData def)
        {
            Notify(def, "", "New objective - " + def.title);
        }

        private void Notify(ObjectiveDefinitionData def, string authoredText, string fallback)
        {
            string text = !string.IsNullOrEmpty(authoredText) ? authoredText : fallback;
            EventBus.Publish(new NoticeRequestEvent { text = text });
            if (Notice != null) Notice(text);
        }

        // ---------------------------------------------------------------- queries (UI / tests)
        public ObjectivePhase PhaseOf(string objectiveId)
        {
            return _state != null ? _state.GetObjectivePhase(objectiveId) : ObjectivePhase.Hidden;
        }

        /// <summary>Measurable units passed right now (steps + counter).</summary>
        public int ProgressOf(ObjectiveDefinitionData def)
        {
            int passed = 0;
            for (int i = 0; i < def.steps.Count; i++)
                if (EvaluateAll(def.steps[i].conditions)) passed++;
            if (def.UsesCounter) passed += Math.Min(_state.GetVar(def.counterVar, 0), def.counterTarget);
            return passed;
        }

        public int TargetOf(ObjectiveDefinitionData def)
        {
            int target = def.steps.Count;
            if (def.UsesCounter) target += def.counterTarget;
            return target;
        }

        /// <summary>Tracked objectives in authored order (the HUD's "current" list).</summary>
        public List<ObjectiveView> ActiveObjectives()
        {
            return Collect(ObjectivePhase.Active);
        }

        public List<ObjectiveView> CompletedObjectives() { return Collect(ObjectivePhase.Completed); }
        public List<ObjectiveView> FailedObjectives() { return Collect(ObjectivePhase.Failed); }
        public List<ObjectiveView> OfferedObjectives() { return Collect(ObjectivePhase.Available); }

        private List<ObjectiveView> Collect(ObjectivePhase phase)
        {
            var list = new List<ObjectiveView>();
            for (int i = 0; i < _ordered.Count; i++)
                if (_state.GetObjectivePhase(_ordered[i].id) == phase) list.Add(View(_ordered[i]));
            return list;
        }

        /// <summary>Full UI view of one objective (checklist lines + counter + phase).</summary>
        public ObjectiveView View(ObjectiveDefinitionData def)
        {
            var view = new ObjectiveView
            {
                id = def.id,
                title = def.title,
                description = def.description,
                type = def.type,
                phase = _state.GetObjectivePhase(def.id),
                progress = ProgressOf(def),
                target = TargetOf(def)
            };
            for (int i = 0; i < def.steps.Count; i++)
            {
                bool done = EvaluateAll(def.steps[i].conditions);
                string line = (done ? "[x] " : "[ ] ") + def.steps[i].text;
                view.steps.Add(line);
                if (!done) view.openSteps.Add(def.steps[i].text);
            }
            if (def.UsesCounter)
            {
                int value = Math.Min(_state.GetVar(def.counterVar, 0), def.counterTarget);
                view.counterText = (string.IsNullOrEmpty(def.counterText) ? def.counterVar : def.counterText)
                    + " " + value + "/" + def.counterTarget;
            }
            if (def.followUps != null)
                for (int i = 0; i < def.followUps.Count; i++)
                {
                    ObjectiveDefinitionData next = Find(def.followUps[i]);
                    if (next != null) { view.followUpHint = "Next: " + next.title; break; }
                }
            return view;
        }

        public ObjectiveView ViewOf(string objectiveId)
        {
            ObjectiveDefinitionData def = Find(objectiveId);
            return def != null ? View(def) : null;
        }

        /// <summary>One-line debug/test description of the mission state.</summary>
        public string Describe()
        {
            var sb = new StringBuilder("[objectives]");
            for (int i = 0; i < _ordered.Count; i++)
            {
                ObjectivePhase phase = _state.GetObjectivePhase(_ordered[i].id);
                if (phase == ObjectivePhase.Hidden) continue;
                sb.Append(' ').Append(_ordered[i].id).Append('=').Append(phase);
            }
            return sb.ToString();
        }
    }
}
