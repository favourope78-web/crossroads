using System;
using System.Collections.Generic;
using System.Text;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// The reusable, data-driven WORLD STATE system (task spec: track open/closed
    /// areas, changed objects, NPC locations/states, story flags, completed
    /// objectives, unlocked interactions, ability-dependent interactions).
    ///
    /// It is a thin, event-driven façade over the authoritative GameState (all writes
    /// through StateMutator - the project's single write path) plus a condition-gated
    /// interaction-unlock registry (WorldInteractionData rows from the story library):
    /// when a row's conditions first pass - for THIS player, because of THEIR choices
    /// and abilities - the unlock key is persisted and InteractionUnlockedEvent fires.
    /// Two players on different paths keep different unlock sets, saved and restored.
    /// Pure C# - headless-testable; scene appliers (StoryWorldState, NpcRelocator,
    /// AreaGate, ObjectiveHUD) react to the events instead of polling.
    /// </summary>
    public class WorldStateSystem
    {
        private readonly StateMutator _state;
        private readonly GameStateManager _progress;
        private readonly List<WorldInteractionData> _interactions;
        private bool _subscribed;

        public WorldStateSystem(StateMutator state, GameStateManager progress,
                                List<WorldInteractionData> worldInteractions)
        {
            _state = state;
            _progress = progress;
            _interactions = worldInteractions ?? new List<WorldInteractionData>();
        }

        // ---------------------------------------------------------------- areas
        /// <summary>An area is open when it was unlocked AND not re-sealed afterwards.</summary>
        public bool IsOpen(string areaId) { return _state.IsAreaOpen(areaId); }
        public bool IsClosed(string areaId) { return _state.IsAreaClosed(areaId); }
        public bool IsUnlocked(string areaId) { return _state.IsAreaUnlocked(areaId); }

        public void OpenArea(string areaId) { _state.UnlockArea(areaId); }
        public void CloseArea(string areaId) { _state.CloseArea(areaId); }
        public void ReopenArea(string areaId) { _state.ReopenArea(areaId); }

        /// <summary>All area ids currently open (unlocked + not sealed).</summary>
        public List<string> OpenAreas()
        {
            var open = new List<string>();
            var areas = _state.State.unlockAreas;
            for (int i = 0; i < areas.Count; i++)
            {
                if (areas[i] == null) continue;
                if (!_state.IsAreaClosed(areas[i].key)) open.Add(areas[i].key);
            }
            return open;
        }

        // ---------------------------------------------------------------- changed objects
        /// <summary>Persisted object toggles (spawn/hide world objects; StoryWorldState applies).</summary>
        public void SetObjectState(string entityKey, bool active) { _state.SetEntity(entityKey, active); }
        public bool ObjectActive(string entityKey, bool fallback = false) { return _state.GetEntity(entityKey, fallback); }

        // ---------------------------------------------------------------- NPC locations & states
        /// <summary>Relocates an NPC by location key (persisted; NpcRelocatedEvent for the scene).</summary>
        public void MoveNpc(string npcId, string locationKey) { _state.SetNpcLocation(npcId, locationKey); }
        public string NpcLocation(string npcId, string fallback = "") { return _state.GetNpcLocation(npcId, fallback); }

        /// <summary>NPC state (title/mood/behaviour) is resolved data-side from the same
        /// state by NpcBrain - objective-driven fate rows use ObjectiveCompleted etc.</summary>
        public string NpcStateHint(string npcId)
        {
            return _progress != null ? _progress.BondTier(npcId) : "New";
        }

        // ---------------------------------------------------------------- story flags
        public void SetFlag(string key, string value) { _state.SetFlag(key, value); }
        public bool FlagIs(string key, string value) { return _state.FlagIs(key, value); }
        public bool HasFlag(string key) { return _state.HasFlag(key); }

        // ---------------------------------------------------------------- objectives
        public bool ObjectiveCompleted(string objectiveId) { return _state.ObjectiveWasCompleted(objectiveId); }
        public bool ObjectiveFailed(string objectiveId) { return _state.ObjectiveFailed(objectiveId); }
        public bool ObjectiveActive(string objectiveId) { return _state.ObjectiveIsActive(objectiveId); }
        public ObjectivePhase ObjectivePhase(string objectiveId) { return _state.GetObjectivePhase(objectiveId); }

        // ---------------------------------------------------------------- world variants
        public void SetWorldVariant(string areaKey, string variantKey) { _state.SetWorldState(areaKey, variantKey); }
        public string WorldVariant(string areaKey, string fallback = "") { return _state.GetWorldState(areaKey, fallback); }

        // ---------------------------------------------------------------- interaction unlocks
        /// <summary>
        /// Re-evaluates every world-interaction row: rows whose conditions newly pass
        /// are persisted as unlocked (once) and announced. Event-driven via BindEvents;
        /// also called once at boot after the save is loaded.
        /// </summary>
        public int SyncInteractionUnlocks()
        {
            int newly = 0;
            for (int i = 0; i < _interactions.Count; i++)
            {
                WorldInteractionData row = _interactions[i];
                if (row == null || string.IsNullOrEmpty(row.key)) continue;
                if (_state.HasInteractionUnlock(row.key)) continue;
                if (!ConditionEvaluator.Evaluate(row.conditions, _state)) continue;
                if (_state.UnlockInteraction(row.key, row.label)) newly++;
            }
            return newly;
        }

        public bool InteractionUnlocked(string unlockKey) { return _state.HasInteractionUnlock(unlockKey); }

        /// <summary>All currently-passing interaction rows for this player (their possible world).</summary>
        public List<WorldInteractionData> AvailableInteractions()
        {
            var list = new List<WorldInteractionData>();
            for (int i = 0; i < _interactions.Count; i++)
            {
                WorldInteractionData row = _interactions[i];
                if (row == null || !_state.HasInteractionUnlock(row.key)) continue;
                list.Add(row);
            }
            return list;
        }

        // ---------------------------------------------------------------- event wiring
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
            EventBus.Subscribe<NpcRelocatedEvent>(OnWorldChanged);
            EventBus.Subscribe<StateLoadedEvent>(OnWorldChanged);
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
            EventBus.Unsubscribe<NpcRelocatedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<StateLoadedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<StateResetEvent>(OnWorldChanged);
        }

        private void OnWorldChanged<T>(T evt)
        {
            SyncInteractionUnlocks();
        }

        // ---------------------------------------------------------------- summary (HUD/tests)
        /// <summary>Compact world summary lines (HUD/debug/tests).</summary>
        public List<string> SummaryLines()
        {
            var lines = new List<string>();
            var open = OpenAreas();
            lines.Add("Open areas  " + (open.Count > 0 ? string.Join(", ", open.ToArray()) : "none yet"));

            var changed = _state.State.entities;
            int changedCount = 0;
            for (int i = 0; i < changed.Count; i++) if (changed[i] != null && changed[i].value) changedCount++;
            lines.Add("World changes  " + changedCount + " object(s)");

            var relocated = _state.State.npcLocations;
            if (relocated.Count > 0)
            {
                var sb = new StringBuilder("NPC locations  ");
                for (int i = 0; i < relocated.Count; i++)
                {
                    if (relocated[i] == null) continue;
                    if (sb.Length > 16) sb.Append(" · ");
                    sb.Append(relocated[i].key).Append(" -> ").Append(relocated[i].value);
                }
                lines.Add(sb.ToString());
            }

            var unlocked = AvailableInteractions();
            lines.Add("Possibilities  " + unlocked.Count + " of " + _interactions.Count);
            return lines;
        }

        public string Describe()
        {
            var sb = new StringBuilder("[world]");
            sb.Append(" open=").Append(string.Join("|", OpenAreas().ToArray()));
            sb.Append(" unlocks=").Append(_state.State.interactionUnlocks.Count);
            sb.Append(" npcLoc=").Append(_state.State.npcLocations.Count);
            return sb.ToString();
        }
    }
}
