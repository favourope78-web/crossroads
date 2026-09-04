using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// The world-expansion runtime (location framework). Like CampaignManager, a PURE
    /// evaluation engine that owns no state:
    ///
    ///   - UNLOCKING derives from data: each location carries unlock rules (the same gate
    ///     language the scene doors use); the first passing opens-rule unlocks the area
    ///     through the EXISTING StateMutator.UnlockArea (persisted since the area system,
    ///     autosaved, restored on load). Empty rules = open from the start (the hub).
    ///   - TRAVEL follows the CONNECTION graph: you can move along edges between unlocked,
    ///     open locations; returning to a visited location is the same path (its world
    ///     state was never rolled back - GameState is the single truth).
    ///   - FIRST ARRIVAL applies the location's worldStateChanges (SpawnEntity /
    ///     SetWorldState / MoveNpc / ...) exactly once, guarded by a loc_visited_ flag.
    ///   - RESTORING is free: current location, unlocks, visits, entities, objectives and
    ///     NPC locations all live in GameState v5 already; Refresh() re-derives the rest.
    ///
    /// Data-driven end to end: a designer adds a location in content (id, rules,
    /// connections, who lives there, env profile) and this manager evaluates it - no code.
    /// </summary>
    public class LocationManager
    {
        private readonly List<LocationDefinitionData> _locations;
        private readonly GameStateManager _progress;

        private readonly List<LocationDefinitionData> _unlocked = new List<LocationDefinitionData>();
        private string _currentId = "";
        private bool _dirty;

        public LocationManager(List<LocationDefinitionData> locations, GameStateManager progress)
        {
            _locations = locations ?? new List<LocationDefinitionData>();
            _progress = progress;
        }

        public GameStateManager Progress { get { return _progress; } }
        public List<LocationDefinitionData> All { get { return _locations; } }
        public List<LocationDefinitionData> Unlocked { get { return _unlocked; } }
        public string CurrentLocationId { get { return _currentId; } }

        public static string VisitedFlag(string locationId) { return "loc_visited_" + locationId; }

        // ---------------------------------------------------------------- lifecycle
        public void BindEvents()
        {
            EventBus.Subscribe<DecisionResolvedEvent>(OnStateChanged);
            EventBus.Subscribe<AbilityUnlockedEvent>(OnStateChanged);
            EventBus.Subscribe<ObjectiveChangedEvent>(OnStateChanged);
            EventBus.Subscribe<ItemChangedEvent>(OnStateChanged);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        public void UnbindEvents()
        {
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnStateChanged);
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnStateChanged);
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<ItemChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void OnStateChanged<T>(T e) { Refresh(); }

        private void OnStateReset(StateResetEvent e)
        {
            _currentId = "";
            Refresh();
        }

        // ---------------------------------------------------------------- derivation
        /// <summary>
        /// Re-derives unlocks from content rules against live state (idempotent; also the
        /// post-load path: a restored ability/decision re-opens its locations).
        /// </summary>
        public void Refresh()
        {
            _dirty = false;

            // 1) auto-unlock: first passing opens-rule wins (OR across rules, AND within)
            for (int i = 0; i < _locations.Count; i++)
            {
                LocationDefinitionData loc = _locations[i];
                if (loc == null || IsLocationOpenByDesign(loc) || _progress.State.IsAreaUnlocked(loc.id)) continue;
                string notice;
                if (UnlockRulePasses(loc, out notice))
                {
                    _progress.State.UnlockArea(loc.id); // existing area system (persisted)
                    EventBus.Publish(new LocationUnlockedEvent { locationId = loc.id, name = loc.name, notice = notice });
                    _dirty = true;
                }
            }

            // 2) unlocked set + current location (State.currentArea is the persisted truth;
            //    fall back to the first Hub when nothing valid is set - e.g. fresh save)
            _unlocked.Clear();
            for (int i = 0; i < _locations.Count; i++)
            {
                LocationDefinitionData loc = _locations[i];
                if (loc != null && IsUnlocked(loc.id)) _unlocked.Add(loc);
            }
            string area = _progress.CurrentArea;
            if (Find(area) == null)
            {
                LocationDefinitionData hub = FindByKind(LocationKind.Hub);
                _currentId = hub != null ? hub.id : (_locations.Count > 0 ? _locations[0].id : "");
                if (!string.IsNullOrEmpty(_currentId) && string.IsNullOrEmpty(area))
                {
                    // fresh save with no area set: seat the player in the hub quietly
                    _progress.State.SetCurrentArea(_currentId);
                    if (Find(_currentId) != null && !_unlocked.Contains(Find(_currentId)))
                        _unlocked.Add(Find(_currentId));
                }
            }
            else _currentId = area;

            if (_dirty) EventBus.Publish(new LocationAvailabilityChangedEvent { currentLocationId = _currentId });
        }

        private bool UnlockRulePasses(LocationDefinitionData loc, out string notice)
        {
            notice = "";
            if (loc.unlockRules == null) return false;
            for (int i = 0; i < loc.unlockRules.Count; i++)
            {
                GateRuleData rule = loc.unlockRules[i];
                if (rule == null || !rule.opens) continue;
                if (ConditionEvaluator.Evaluate(rule.conditions, _progress.State))
                {
                    notice = rule.text;
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- queries
        public LocationDefinitionData Find(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            for (int i = 0; i < _locations.Count; i++)
                if (_locations[i] != null && _locations[i].id == locationId) return _locations[i];
            return null;
        }

        /// <summary>A location with no unlock rules is open from the start (the hub);
        /// gated ones live in the persisted unlockAreas once a rule passes.</summary>
        public bool IsLocationOpenByDesign(LocationDefinitionData loc)
        {
            return loc != null && (loc.unlockRules == null || loc.unlockRules.Count == 0);
        }

        public bool IsUnlocked(string locationId)
        {
            LocationDefinitionData loc = Find(locationId);
            if (loc == null) return false;
            if (IsLocationOpenByDesign(loc)) return true;
            return _progress.State.IsAreaUnlocked(loc.id);
        }

        public LocationDefinitionData FindByKind(LocationKind kind)
        {
            for (int i = 0; i < _locations.Count; i++)
                if (_locations[i] != null && (LocationKind)_locations[i].kind == kind) return _locations[i];
            return null;
        }

        public bool IsVisited(string locationId)
        {
            return _progress.State.GetFlag(VisitedFlag(locationId)) == "1";
        }

        /// <summary>Why not: unknown / locked / sealed / entry conditions / no connection.</summary>
        public enum TravelBlock { None, Unknown, Locked, Sealed, EntryConditions, NotConnected }

        public bool CanTravel(string toId)
        {
            TravelBlock block;
            return CanTravel(toId, out block);
        }

        public bool CanTravel(string toId, out TravelBlock block)
        {
            block = TravelBlock.None;
            LocationDefinitionData to = Find(toId);
            if (to == null) { block = TravelBlock.Unknown; return false; }
            if (!IsUnlocked(to.id)) { block = TravelBlock.Locked; return false; }
            if (_progress.State.IsAreaClosed(to.id)) { block = TravelBlock.Sealed; return false; }
            if (!ConditionEvaluator.Evaluate(to.entryConditions, _progress.State)) { block = TravelBlock.EntryConditions; return false; }
            if (!Connected(_currentId, to.id)) { block = TravelBlock.NotConnected; return false; }
            return true;
        }

        /// <summary>Edge test both ways (connections are authored one-directionally but walked both).</summary>
        public bool Connected(string fromId, string toId)
        {
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return false;
            if (fromId == toId) return false;
            LocationDefinitionData from = Find(fromId);
            if (from != null && from.connections != null && from.connections.Contains(toId)) return true;
            LocationDefinitionData to = Find(toId);
            if (to != null && to.connections != null && to.connections.Contains(fromId)) return true;
            return false;
        }

        /// <summary>Unlocked + open + connected to the current location (the map's "travel" rows).</summary>
        public bool IsReachable(string locationId)
        {
            TravelBlock block;
            CanTravel(locationId, out block);
            return block == TravelBlock.None;
        }

        // ---------------------------------------------------------------- travel
        /// <summary>
        /// Moves the run to a connected, unlocked location. State first (single truth), then
        /// the Arrival event carries everything the scene needs (checkpoint + env profile);
        /// worldStateChanges fire on the FIRST arrival only, guarded by a persisted flag.
        /// </summary>
        public bool Travel(string toId)
        {
            TravelBlock block;
            if (!CanTravel(toId, out block))
            {
                StoryLog.LogWarning("[LOCATIONS] travel to " + toId + " blocked: " + block);
                return false;
            }
            LocationDefinitionData to = Find(toId);
            bool firstVisit = !IsVisited(to.id);

            EventBus.Publish(new LocationDepartedEvent { locationId = _currentId });

            _progress.State.SetCurrentArea(to.id);                    // persisted + AreaChangedEvent
            _progress.State.SetFlag(VisitedFlag(to.id), "1");         // persisted visit marker
            if (firstVisit && to.worldStateChanges != null && to.worldStateChanges.Count > 0)
            {
                EffectApplier.Apply(to.worldStateChanges, _progress.State); // consequences of arrival
            }

            LocationEnvironmentData env = to.environment;
            EventBus.Publish(new LocationArrivedEvent
            {
                locationId = to.id,
                name = to.name,
                sceneKey = to.sceneKey,
                checkpointId = to.checkpointId,
                firstVisit = firstVisit,
                envProfile = env != null ? env.profile : "",
                envAmbient = env != null ? env.ambient : "",
                envFog = env != null ? env.fog : "",
                envFogDensity = env != null ? env.fogDensity : 0f,
                envSun = env != null ? env.sun : "",
                envSunIntensity = env != null ? env.sunIntensity : 1f
            });

            Refresh();
            return true;
        }

        /// <summary>Requirement text the map shows while a location stays locked.</summary>
        public string LockedHint(string locationId)
        {
            LocationDefinitionData loc = Find(locationId);
            return loc != null ? loc.lockedHint : "";
        }

        public string Describe()
        {
            return _locations.Count + " locations, " + _unlocked.Count + " unlocked, at " + _currentId;
        }
    }
}
