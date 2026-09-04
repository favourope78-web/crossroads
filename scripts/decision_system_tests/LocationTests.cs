// ============================================================================
// CROSSROADS headless tests of the WORLD EXPANSION / LOCATION system:
//   location contracts · unlocking (data rules) · requirements/hints ·
//   connection graph + travel validation · returning to locations ·
//   first-visit world changes (once) · persistent changes reflected back ·
//   decision-dependent locations · ability-gated hidden interactions ·
//   save/load · restart mid-route · the full vertical flow (task 14).
// Runs the exact same code paths the game uses (LocationManager,
// LocationServices, ConditionEvaluator, EffectApplier, StateMutator,
// CampaignManager, ObjectiveManager, EncounterFlow).
// Invoke from FlowTests.Main (single process, shared counters).
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Gameplay;

namespace Crossroads.Tests
{
    public static class LocationTests
    {
        private static int _passed, _failed;
        private static readonly List<string> Log = new List<string>();
        private static readonly List<DialogueLineEvent> Lines = new List<DialogueLineEvent>();
        private static readonly List<LocationArrivedEvent> Arrivals = new List<LocationArrivedEvent>();
        private static readonly List<LocationUnlockedEvent> Unlocks = new List<LocationUnlockedEvent>();

        private static void Check(bool condition, string what)
        {
            if (condition) { _passed++; Log.Add("  PASS  " + what); }
            else { _failed++; Log.Add("  FAIL  " + what); }
        }

        private static void CheckEq<T>(T actual, T expected, string what)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            if (ok) { _passed++; Log.Add("  PASS  " + what); }
            else { _failed++; Log.Add("  FAIL  " + what + " (expected " + expected + ", got " + actual + ")"); }
        }

        private class TempPaths : IPathProvider
        {
            private readonly string _dir;
            public TempPaths(string dir) { _dir = dir; System.IO.Directory.CreateDirectory(dir); }
            public string Directory { get { return _dir; } }
            public string Resolve(string fileName) { return Path.Combine(_dir, fileName); }
        }

        private class TestJsonAdapter : IJsonSerializer
        {
            public string ToJson(object o, bool prettyPrint) { return TestJson.ToJson(o); }
            public T FromJson<T>(string json) { return TestJson.FromJson<T>(json); }
        }

        private static bool _subscribed;

        private static void NewRun(string dir)
        {
            StoryLog.Info = Console.WriteLine;
            StoryLog.Warn = Console.WriteLine;
            StoryLog.Error = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(dir), new RuntimeContentSource(),
                "FirstLocation", "hall_spawn", 0, loadExisting: true);
            WorldServices.Init();
            CampaignServices.Init();
            LocationServices.Init(); // world expansion runtime over the same state

            if (!_subscribed)
            {
                _subscribed = true;
                EventBus.Subscribe<DialogueLineEvent>(e => Lines.Add(e));
                EventBus.Subscribe<LocationArrivedEvent>(e => Arrivals.Add(e));
                EventBus.Subscribe<LocationUnlockedEvent>(e => Unlocks.Add(e));
            }
        }

        private static void Shutdown()
        {
            LocationServices.Shutdown(silent: true);
            CampaignServices.Shutdown(silent: true);
            WorldServices.Shutdown(silent: true);
            GameServices.Shutdown(silent: true);
        }

        private static string TempDir(string tag)
        {
            return Path.Combine(Path.GetTempPath(), "crossroads_locations_" + tag + "_" + Guid.NewGuid().ToString("N"));
        }

        private static void PlayFirstLight(string optionId)
        {
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterFirstLight);
            int guard = 0;
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            flow.SelectChoice(optionId);
            flow.Advance();
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
        }

        private static bool JournalHas(string fragment)
        {
            var journal = GameServices.State.State.campaignJournal;
            if (journal == null) return false;
            for (int i = 0; i < journal.Count; i++) if (journal[i].Contains(fragment)) return true;
            return false;
        }

        private static bool LinesHave(string fragment)
        {
            for (int i = 0; i < Lines.Count; i++)
                if (Lines[i].text != null && Lines[i].text.Contains(fragment)) return true;
            return false;
        }

        private static int LinesClear(string fragment)
        {
            int n = 0;
            for (int i = 0; i < Lines.Count; i++)
                if (Lines[i].text != null && Lines[i].text.Contains(fragment)) n++;
            return n;
        }

        private static LocationServices.MapEntry? Entry(string id)
        {
            var snap = LocationServices.MapSnapshot();
            for (int i = 0; i < snap.Count; i++) if (snap[i].id == id) return snap[i];
            return null;
        }

        // ================================================================ 64. location content contracts
        private static void TestContentContracts()
        {
            Log.Add("[64] Locations: content contracts (ids, kinds, graph, references, env profiles)");
            string dir = TempDir("content");
            NewRun(dir);
            StoryContentData c = GameServices.Content.Content;

            CheckEq(c.locations.Count, 3, "three prototype locations");
            LocationDefinitionData hall = c.FindLocation("hall");
            LocationDefinitionData annex = c.FindLocation("annex");
            LocationDefinitionData tidewell = c.FindLocation("tidewell");
            Check(hall != null && annex != null && tidewell != null, "hall/annex/tidewell registered");

            CheckEq((LocationKind)hall.kind, LocationKind.Hub, "hall is the Hub (exploration/story)");
            CheckEq((LocationKind)annex.kind, LocationKind.Combat, "annex is the Combat location");
            CheckEq((LocationKind)tidewell.kind, LocationKind.Npc, "tidewell is the NPC location");

            CheckEq(hall.unlockRules.Count, 0, "hub has no unlock rules (open from the start)");
            CheckEq(annex.unlockRules.Count, 3, "annex: one rule per route ability (OR across rules)");
            CheckEq(tidewell.unlockRules.Count, 1, "tidewell: single decision rule");
            CheckEq(tidewell.unlockRules[0].conditions[0].type, ConditionType.DecisionWas,
                "tidewell rule reads the trunk DECISION");
            Check(!string.IsNullOrEmpty(annex.lockedHint) && !string.IsNullOrEmpty(tidewell.lockedHint),
                "gated locations carry a requirement hint for the map");

            Check(hall.connections.Contains("annex") && annex.connections.Contains("hall"),
                "hall <-> annex connected (symmetric)");
            Check(hall.connections.Contains("tidewell") && tidewell.connections.Contains("hall"),
                "hall <-> tidewell connected (symmetric)");
            Check(!annex.connections.Contains("tidewell"),
                "annex <-> tidewell NOT connected (travel routes through the hub)");

            Check(hall.npcs.Contains("mara") && hall.npcs.Contains("sera"), "hall lists its NPCs");
            Check(tidewell.npcs.Contains("sera"), "tidewell lists Sera (she keeps the shrine)");
            Check(annex.objectives.Contains("obj_ember_beacon") && annex.objectives.Contains("obj_warden_hunt"),
                "annex owns the beacon + warden objectives");
            Check(tidewell.objectives.Contains("obj_tide_keepsake"), "tidewell owns the keepsake objective");

            for (int i = 0; i < c.locations.Count; i++)
            {
                LocationDefinitionData loc = c.locations[i];
                if (loc == null) continue;
                Check(!string.IsNullOrEmpty(loc.sceneKey) && !string.IsNullOrEmpty(loc.checkpointId),
                    loc.id + " has sceneKey + checkpointId");
                Check(loc.environment != null && !string.IsNullOrEmpty(loc.environment.profile),
                    loc.id + " has an environment profile");
                for (int e = 0; e < loc.encounters.Count; e++)
                    Check(c.FindEncounter(loc.encounters[e]) != null,
                        loc.id + " encounter '" + loc.encounters[e] + "' exists");
                for (int o = 0; o < loc.objectives.Count; o++)
                    Check(c.FindObjective(loc.objectives[o]) != null,
                        loc.id + " objective '" + loc.objectives[o] + "' exists");
                for (int n = 0; n < loc.npcs.Count; n++)
                    Check(c.FindNpc(loc.npcs[n]) != null, loc.id + " npc '" + loc.npcs[n] + "' exists");
            }

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 65. unlocking + requirements
        private static void TestUnlocking()
        {
            Log.Add("[65] Locations: data-driven unlocking (abilities + decisions) and locked hints");
            string dir = TempDir("unlock");
            NewRun(dir);
            LocationManager loc = LocationServices.Locations;

            Check(loc.IsUnlocked("hall") && loc.CurrentLocationId == "hall",
                "fresh run: hub unlocked and current");
            LocationManager.TravelBlock block;
            loc.CanTravel("annex", out block);
            CheckEq(block, LocationManager.TravelBlock.Locked, "annex locked before any route ability");
            loc.CanTravel("tidewell", out block);
            CheckEq(block, LocationManager.TravelBlock.Locked, "tidewell locked before the tide decision");
            Check(!string.IsNullOrEmpty(loc.LockedHint("tidewell")),
                "the map can show WHY tidewell is locked (hint from content)");
            CheckEq(Entry("annex").Value.state, LocationServices.MapEntryState.Locked,
                "map row for annex starts Locked");

            Unlocks.Clear();
            PlayFirstLight("ember_reach"); // grants ember_pulse -> rule 1 passes
            Check(loc.IsUnlocked("annex"), "ember decision unlocked the annex (ability rule)");
            bool announced = false;
            for (int i = 0; i < Unlocks.Count; i++) if (Unlocks[i].locationId == "annex") announced = true;
            Check(announced, "LocationUnlockedEvent fired (with the rule's notice text)");
            Check(!loc.IsUnlocked("tidewell"),
                "tidewell STAYS locked for the ember player (decision-gated)");

            Shutdown();
            Directory.Delete(dir, true);

            // stone route opens the annex through a DIFFERENT rule (OR across rules)
            string dir2 = TempDir("unlock_stone");
            NewRun(dir2);
            LocationManager loc2 = LocationServices.Locations;
            PlayFirstLight("stone_still");
            Check(loc2.IsUnlocked("annex"), "stone decision unlocked the annex too (third rule)");
            Check(!loc2.IsUnlocked("tidewell"), "stone player: tidewell still locked");
            Shutdown();
            Directory.Delete(dir2, true);
        }

        // ================================================================ 66. travel graph + validation
        private static void TestTravelValidation()
        {
            Log.Add("[66] Locations: travel follows the connection graph (and rejects the rest)");
            string dir = TempDir("travel");
            NewRun(dir);
            PlayFirstLight("tide_clear"); // unlocks BOTH annex (tide_mend rule) and tidewell (decision)
            LocationManager loc = LocationServices.Locations;
            Check(loc.IsUnlocked("annex") && loc.IsUnlocked("tidewell"),
                "tide player: annex via ability rule, tidewell via decision rule");

            LocationManager.TravelBlock block;
            loc.CanTravel("nowhere", out block);
            CheckEq(block, LocationManager.TravelBlock.Unknown, "unknown location id -> Unknown");

            Arrivals.Clear();
            Check(loc.Travel("annex"), "travel hall -> annex succeeds");
            CheckEq(GameServices.Progress.CurrentArea, "annex", "current area moved to the annex");
            CheckEq(GameServices.State.GetFlag("loc_visited_annex"), "1", "visit marker persisted as a flag");
            Check(Arrivals.Count > 0 && Arrivals[Arrivals.Count - 1].locationId == "annex",
                "LocationArrivedEvent published for the annex");
            Check(Arrivals[Arrivals.Count - 1].firstVisit, "first arrival flagged");
            Check(Arrivals[Arrivals.Count - 1].checkpointId == "annex_spawn",
                "arrival carries the checkpoint anchor id (scene side)");
            CheckEq(Arrivals[Arrivals.Count - 1].envProfile, "ember_low",
                "arrival carries the environment profile (content -> event -> scene)");

            loc.CanTravel("tidewell", out block);
            CheckEq(block, LocationManager.TravelBlock.NotConnected,
                "annex -> tidewell refused: no edge between them");
            CheckEq(Entry("annex").Value.state, LocationServices.MapEntryState.Current,
                "map marks the annex as current");

            Check(loc.Travel("hall"), "annex -> hall works (the shared edge)");
            CheckEq(Entry("tidewell").Value.state, LocationServices.MapEntryState.TravelTo,
                "from the hall the tidewell is a TravelTo row (reachability is per-current)");
            Check(loc.Travel("tidewell"), "hall -> tidewell travel succeeds for the tide player");
            CheckEq(GameServices.State.GetWorldState("tidewell", ""), "lit",
                "first arrival at the tidewell lit it (worldStateChanges)");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 67. returning + first-visit effects once
        private static void TestReturning()
        {
            Log.Add("[67] Locations: returning to a visited location; world changes fire on first arrival only");
            string dir = TempDir("return");
            NewRun(dir);
            PlayFirstLight("ember_reach");
            LocationManager loc = LocationServices.Locations;

            Check(loc.Travel("annex"), "travel to the annex");
            CheckEq(GameServices.State.GetWorldState("annex", ""), "reached",
                "first arrival applied the annex world-state change (worldStates.annex=reached)");

            Check(loc.Travel("hall"), "return to the hall");
            CheckEq(GameServices.Progress.CurrentArea, "hall", "current area back at the hall");
            Check(loc.IsVisited("annex"), "visit marker tracks the annex");

            Arrivals.Clear();
            Check(loc.Travel("annex"), "travel to the annex a second time");
            Check(Arrivals.Count > 0 && !Arrivals[Arrivals.Count - 1].firstVisit,
                "second arrival is NOT firstVisit (effects guarded by the persisted flag)");

            Check(loc.Travel("hall"), "and back again (returns are unlimited)");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 68. persistent world changes
        private static void TestPersistentChanges()
        {
            Log.Add("[68] Locations: the hall REMEMBERS what happened in the annex (and vice versa)");
            string dir = TempDir("memory");
            NewRun(dir);
            PlayFirstLight("ember_reach");
            LocationManager loc = LocationServices.Locations;

            Check(loc.Travel("annex"), "in the annex");
            // the beacon world action (same effects the scene object carries)
            GameServices.State.SetFlag("beacon_silenced", "1");
            GameServices.State.SetEntity("choir_beacon", false);
            Check(GameServices.State.ObjectiveWasCompleted("obj_ember_beacon"),
                "beacon objective completed in the annex");
            CheckEq(GameServices.State.GetNpcLocation("sera", ""), "annex_gate",
                "objective consequence relocated Sera (global NPC memory, not per-location)");
            Check(JournalHas("The beacon is quiet"), "campaign journaled the annex outcome");

            Check(loc.Travel("hall"), "return to the hall");
            CheckEq(GameServices.State.GetFlag("beacon_silenced"), "1",
                "the flag survived the location change (single GameState truth)");
            CheckEq(GameServices.State.GetEntity("choir_beacon", true), false,
                "the beacon entity state survived too");

            Lines.Clear();
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterMaraConfide);
            int guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
            Check(LinesHave("The north went quiet"),
                "Mara (hall NPC) reacts to the annex change - the hall reflects it");

            // hidden interaction: the ember cache needs the route ability too
            var interactions = WorldServices.World.AvailableInteractions();
            bool cacheVisible = false;
            for (int i = 0; i < interactions.Count; i++) if (interactions[i].key == "ember_cache_open") cacheVisible = true;
            Check(cacheVisible, "ember player sees the hidden cache (flag + ability)");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 69. decision-dependent locations
        private static void TestDecisionDependencies()
        {
            Log.Add("[69] Locations: two players, different decisions -> different location sets");
            string dirA = TempDir("playerA");
            NewRun(dirA);
            PlayFirstLight("ember_reach"); // player A: ember
            CheckEq(Entry("annex").Value.state, LocationServices.MapEntryState.TravelTo, "A: annex reachable");
            CheckEq(Entry("tidewell").Value.state, LocationServices.MapEntryState.Locked, "A: tidewell locked");
            CheckEq(GameServices.State.GetNpcLocation("sera", ""), "",
                "A: Sera has not moved (no tide decision)");
            Shutdown();
            Directory.Delete(dirA, true);

            string dirB = TempDir("playerB");
            NewRun(dirB);
            PlayFirstLight("tide_clear"); // player B: tide
            CheckEq(Entry("annex").Value.state, LocationServices.MapEntryState.TravelTo, "B: annex reachable (ability rule)");
            CheckEq(Entry("tidewell").Value.state, LocationServices.MapEntryState.TravelTo,
                "B: tidewell reachable (decision rule) - a location A can NEVER enter this run");
            CheckEq(GameServices.State.GetNpcLocation("sera", ""), "tidewell",
                "B: Sera relocated to the tidewell (MoveNpc consequence of the same decision)");

            // ability-gated hidden interaction: without ember_pulse the cache stays hidden
            GameServices.State.SetFlag("beacon_silenced", "1"); // flag alone is not enough
            var interactions = WorldServices.World.AvailableInteractions();
            bool cacheVisible = false;
            for (int i = 0; i < interactions.Count; i++) if (interactions[i].key == "ember_cache_open") cacheVisible = true;
            Check(!cacheVisible, "B: hidden cache NOT visible - the ability gates it, not the flag");

            Shutdown();
            Directory.Delete(dirB, true);
        }

        // ================================================================ 70. save/load + restart
        private static void TestSaveLoadRestart()
        {
            Log.Add("[70] Locations: unlocks/current/visits/world changes survive save + restart");
            string dir = TempDir("saveload");
            NewRun(dir);
            PlayFirstLight("ember_reach");
            Check(LocationServices.Locations.Travel("annex"), "travel to the annex");
            GameServices.State.SetEntity("choir_beacon", false);
            GameServices.PersistNow(autosaveMirror: true);
            Shutdown();

            NewRun(dir); // restart the "app"
            LocationManager loc = LocationServices.Locations;
            CheckEq(loc.CurrentLocationId, "annex", "restart: current location restored (no travel needed)");
            Check(loc.IsUnlocked("annex") && loc.IsUnlocked("hall"), "restart: unlocks restored");
            Check(loc.IsVisited("annex"), "restart: visit marker restored");
            CheckEq(GameServices.State.GetEntity("choir_beacon", true), false,
                "restart: entity change still applied");
            CheckEq(GameServices.State.GetWorldState("annex", ""), "reached",
                "restart: first-arrival world state restored");
            CheckEq(Entry("annex").Value.state, LocationServices.MapEntryState.Current,
                "restart: map shows the annex as current");
            Check(loc.Travel("hall"), "restart: travel still works after restore");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 71. the full vertical flow (task 14)
        private static void TestFullFlow()
        {
            Log.Add("[71] Full flow: hall -> decision -> annex unlocks -> travel -> encounter -> objective ->");
            Log.Add("     world changes -> return -> hall reflects it -> save -> restart -> route restored");
            string dir = TempDir("fullflow");
            NewRun(dir);

            // Start: exploration in the hall -> NPC encounter -> dialogue -> decision
            CheckEq(LocationServices.Locations.CurrentLocationId, "hall", "flow starts in the hall");
            PlayFirstLight("ember_reach");

            // Decision -> location unlocks
            Check(LocationServices.Locations.IsUnlocked("annex"), "decision unlocked the annex");

            // Travel to B
            Check(LocationServices.Locations.Travel("annex"), "travelled to the annex");

            // Encounter inside the annex (the echo shrine lives there - resolve its choice)
            Lines.Clear();
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterShrine);
            int guard = 0;
            while (!flow.AwaitingChoice && flow.IsRunning && guard++ < 20) flow.Advance();
            if (flow.AwaitingChoice) flow.SelectChoice("leave");
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
            Check(Lines.Count > 0, "annex encounter ran (echo shrine graph)");

            // Objective + action: silence the beacon (world change) + drive the Warden off
            GameServices.State.SetFlag("beacon_silenced", "1");
            GameServices.State.SetEntity("choir_beacon", false);
            GameServices.State.SetVar("warden_driven_off", 1);
            Check(GameServices.State.ObjectiveWasCompleted("obj_ember_beacon"),
                "annex objective completed (world changed)");
            Check(GameServices.State.ObjectiveWasCompleted("obj_warden_hunt"),
                "warden driven off - the annex combat objective resolved");

            // Return to A -> A reflects the previous change
            Check(LocationServices.Locations.Travel("hall"), "returned to the hall");
            Lines.Clear();
            flow.Run(StoryContentBuilder.EncounterMaraConfide);
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
            Check(LinesHave("The north went quiet"),
                "hall NPC dialogue reflects the annex change (persistent world state)");

            // Save -> restart -> correct branch/location restored
            GameServices.PersistNow(autosaveMirror: true);
            Shutdown();
            NewRun(dir);
            LocationManager loc = LocationServices.Locations;
            CheckEq(loc.CurrentLocationId, "hall", "restart: back-in-hall position restored");
            Check(loc.IsUnlocked("annex") && loc.IsVisited("annex"), "restart: annex still open + visited");
            CheckEq(GameServices.State.GetFlag("beacon_silenced"), "1",
                "restart: the world change survived");
            CheckEq(GameServices.State.DecisionOption(StoryContentBuilder.DecisionFirstLight), "ember_reach",
                "restart: the decision that shaped the world survived");
            Lines.Clear();
            flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterMaraConfide);
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
            Check(LinesClear("The north went quiet") == 1,
                "restart: the hall STILL reflects the annex change (route fully restored)");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- entry
        public static void RunAll(out int passed, out int failed)
        {
            _passed = 0; _failed = 0;
            Log.Clear(); Lines.Clear(); Arrivals.Clear(); Unlocks.Clear();

            TestContentContracts();
            TestUnlocking();
            TestTravelValidation();
            TestReturning();
            TestPersistentChanges();
            TestDecisionDependencies();
            TestSaveLoadRestart();
            TestFullFlow();

            passed = _passed;
            failed = _failed;
        }

        public static List<string> GetLog() { return Log; }
    }
}
