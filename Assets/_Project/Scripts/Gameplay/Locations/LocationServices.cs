using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Location facade (same pattern as CampaignServices): boots the LocationManager over
    /// the initialized GameServices, hooks the autosave (any travel/unlock persists through
    /// the existing save pipeline - unlockAreas/currentArea/flags are all v5 fields), and
    /// exposes read snapshots for the Map UI.
    ///
    /// Boot order: GameServices.Init -> WorldServices.Init -> CampaignServices.Init ->
    /// LocationServices.Init (locations sit on top of the story runtime: a decision can
    /// hand the player an ability that opens a location in the SAME refresh cascade).
    /// </summary>
    public static class LocationServices
    {
        public static bool IsInitialized { get; private set; }

        /// <summary>Location runtime: unlock rules, travel graph, arrivals.</summary>
        public static LocationManager Locations { get; private set; }

        private static bool _autosaveHooked;

        public static void Init()
        {
            Shutdown(silent: true);
            if (!GameServices.IsInitialized || GameServices.Content == null)
            {
                StoryLog.LogWarning("[CROSSROADS] LocationServices.Init: GameServices not initialized");
                return;
            }

            StoryContentData content = GameServices.Content.Content;
            Locations = new LocationManager(content != null ? content.locations : null, GameServices.Progress);
            Locations.BindEvents();
            Locations.Refresh(); // re-derive unlocks/current from the loaded save (idempotent)

            if (!_autosaveHooked)
            {
                _autosaveHooked = true;
                EventBus.Subscribe<LocationArrivedEvent>(OnArrived);
                EventBus.Subscribe<LocationUnlockedEvent>(OnUnlocked);
            }

            IsInitialized = true;
            StoryLog.Log("[CROSSROADS] Location services ready (" + Locations.Describe() + ")");
        }

        /// <summary>Travel is state-first; the scene reacts to LocationArrivedEvent (fade/env).</summary>
        public static bool Travel(string locationId)
        {
            if (!IsInitialized || Locations == null) return false;
            return Locations.Travel(locationId);
        }

        private static void OnArrived(LocationArrivedEvent e) { Persist(); }
        private static void OnUnlocked(LocationUnlockedEvent e) { Persist(); }

        private static void Persist()
        {
            if (!IsInitialized) return;
            GameServices.PersistNow(autosaveMirror: true);
        }

        public static void Shutdown(bool silent = false)
        {
            if (Locations != null) { Locations.UnbindEvents(); Locations = null; }
            IsInitialized = false;
        }

        // ---------------------------------------------------------------- UI snapshot
        public enum MapEntryState { Current, TravelTo, Locked, Sealed }

        public struct MapEntry
        {
            public string id;
            public string name;
            public int kind;              // LocationKind
            public string description;
            public MapEntryState state;
            public string hint;           // lockedHint while locked; rule notice otherwise
        }

        /// <summary>Everything the Map HUD shows, in one call (current/available/locked+requirements).</summary>
        public static List<MapEntry> MapSnapshot()
        {
            var entries = new List<MapEntry>();
            if (!IsInitialized || Locations == null) return entries;

            List<LocationDefinitionData> all = Locations.All;
            for (int i = 0; i < all.Count; i++)
            {
                LocationDefinitionData loc = all[i];
                if (loc == null) continue;

                MapEntryState state;
                if (loc.id == Locations.CurrentLocationId) state = MapEntryState.Current;
                else if (Locations.IsReachable(loc.id)) state = MapEntryState.TravelTo;
                else if (GameServices.State.IsAreaClosed(loc.id)) state = MapEntryState.Sealed;
                else state = MapEntryState.Locked;

                entries.Add(new MapEntry
                {
                    id = loc.id,
                    name = loc.name,
                    kind = loc.kind,
                    description = loc.description,
                    state = state,
                    hint = state == MapEntryState.Locked ? Locations.LockedHint(loc.id) : ""
                });
            }
            return entries;
        }
    }
}
