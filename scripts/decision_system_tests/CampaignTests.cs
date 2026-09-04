// ============================================================================
// CROSSROADS headless tests of the CORE BRANCHING CAMPAIGN system:
//   chapter progression · three-way branch selection · condition evaluation ·
//   failure-as-a-route · NPC/ability/objective-dependent branches · the echo
//   second encounter · decision persistence · save/load v5 · v4->v5 migration ·
//   restarting in the correct branch.
// Runs the exact same code paths the game uses (CampaignManager, CampaignServices,
// ConditionEvaluator, EffectApplier, ObjectiveManager, EncounterFlow, StateMutator).
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
    public static class CampaignTests
    {
        private static int _passed, _failed;
        private static readonly List<string> Log = new List<string>();
        private static readonly List<DialogueLineEvent> Lines = new List<DialogueLineEvent>();

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

        private static void NewRun(string dir, out IEncounterSource content)
        {
            content = new RuntimeContentSource();
            StoryLog.Info = Console.WriteLine;
            StoryLog.Warn = Console.WriteLine;
            StoryLog.Error = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(dir), content,
                "FirstLocation", "hall_spawn", 0, loadExisting: true);
            WorldServices.Init();
            CampaignServices.Init(); // boots the branching-story runtime over the same state

            if (!_subscribed)
            {
                _subscribed = true;
                EventBus.Subscribe<DialogueLineEvent>(e => Lines.Add(e));
            }
        }

        private static void Shutdown()
        {
            CampaignServices.Shutdown(silent: true);
            WorldServices.Shutdown(silent: true);
            GameServices.Shutdown(silent: true);
        }

        private static string TempDir(string tag)
        {
            return Path.Combine(Path.GetTempPath(), "crossroads_campaign_" + tag + "_" + Guid.NewGuid().ToString("N"));
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

        private static bool BeatAvailable(string id)
        {
            var beats = CampaignServices.Campaign.AvailableBeats;
            for (int i = 0; i < beats.Count; i++) if (beats[i].id == id) return true;
            return false;
        }

        private static bool BranchTaken(string id) { return GameServices.State.State.CampaignBranchTaken(id); }

        private static bool JournalHas(string fragment)
        {
            var j = GameServices.State.State.campaignJournal;
            if (j == null) return false;
            for (int i = 0; i < j.Count; i++) if (j[i].Contains(fragment)) return true;
            return false;
        }

        public static void RunAll(out int passed, out int failed)
        {
            Console.WriteLine();
            TestCampaignContent();
            TestThreeWayBranch();
            TestChapterCompletion();
            TestFailureRoute();
            TestDependentBranches();
            TestSaveLoadRestart();
            TestV4Migration();
            passed = _passed;
            failed = _failed;
        }

        public static IEnumerable<string> GetLog() { return Log; }

        // ================================================================ 57. campaign content contracts
        private static void TestCampaignContent()
        {
            Log.Add("[57] Campaign content: chapters/beats/branches present, references valid, ch1 starts at boot");
            string dir = TempDir("content");
            NewRun(dir, out IEncounterSource content);
            StoryContentData c = content.Content;

            Check(c.FindChapter("ch_first_light") != null, "chapter one defined");
            Check(c.FindChapter("ch_whispers") != null, "chapter two teaser defined");
            CampaignChapterData owner;
            Check(c.FindBeat("beat_arrival", out owner) != null && owner.id == "ch_first_light",
                  "beats are findable and owned by their chapter");

            CampaignChapterData ch1 = c.FindChapter("ch_first_light");
            CheckEq(ch1.beats.Count, 11, "chapter one carries eleven beats");
            CheckEq(ch1.branches.Count, 11, "chapter one carries eleven branches");

            // every branch points at a real beat (or is a leaf) - same integrity rule the validator enforces
            var ids = new HashSet<string>();
            foreach (CampaignChapterData ch in c.chapters)
                foreach (StoryBeatData b in ch.beats) ids.Add(b.id);
            bool refsOk = true;
            foreach (CampaignChapterData ch in c.chapters)
                foreach (CampaignBranchData br in ch.branches)
                    if ((br.fromBeatId != null && !ids.Contains(br.fromBeatId)) ||
                        (!string.IsNullOrEmpty(br.toBeatId) && !ids.Contains(br.toBeatId))) refsOk = false;
            Check(refsOk, "every branch fromBeatId/toBeatId references a real beat");

            // chapter one is live the moment the game boots (no entry conditions)
            Check(CampaignServices.IsInitialized && CampaignServices.Campaign.ActiveChapters.Count == 1
                  && CampaignServices.Campaign.ActiveChapters[0].id == "ch_first_light",
                  "chapter one active at boot");
            Check(JournalHas("Chapter One"), "chapter start wrote a journal line");
            Check(CampaignServices.Campaign.CurrentBeat != null && CampaignServices.Campaign.CurrentBeat.id == "beat_arrival",
                  "current beat points at the arrival question");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 58. the three-branch trunk
        private static void RunTrunk(string option, string branch, string pathFlag, string pathBeat,
                                     string pathObjective, string otherFlag1, string otherFlag2)
        {
            PlayFirstLight(option);
            Check(GameServices.State.State.CampaignBeatResolved("beat_arrival"), "arrival beat resolved (" + option + ")");
            Check(BranchTaken(branch), "branch taken: " + branch);
            Check(!BranchTaken(otherFlag1) && !BranchTaken(otherFlag2), "the other trunk branches were NOT taken");
            CheckEq(GameServices.State.GetFlag(pathFlag), "1", "path flag set: " + pathFlag);
            Check(BeatAvailable(pathBeat), "path beat available: " + pathBeat);
            CheckEq((int)WorldServices.Objectives.PhaseOf(pathObjective), (int)ObjectivePhase.Active,
                  "path objective active: " + pathObjective);
            // non-linear proof: several beats are simultaneously available, not one forced sequence
            Check(BeatAvailable("beat_sera_echo") && BeatAvailable("beat_warden"),
                  "echo + warden beats available in parallel (non-linear)");
            Check(JournalHas("Path of"), "journal records the chosen path label");
        }

        private static void TestThreeWayBranch()
        {
            Log.Add("[58] Branch selection: three players, three routes, three different objectives");
            Lines.Clear();

            string dir = TempDir("trunk_ember");
            NewRun(dir, out IEncounterSource _);
            RunTrunk("ember_reach", "br_trode_ember", "path_ember", "beat_ember_path",
                     "obj_ember_beacon", "br_trode_tide", "br_trode_stone");
            // ember also demonstrates the ability-dependent beat (ember_pulse was unlocked by the decision)
            Check(GameServices.State.State.CampaignBeatResolved("beat_ember_mastery"),
                  "ability-dependent beat resolved the moment the ember power was owned");
            Check(BranchTaken("br_second_door"), "ability-dependent branch taken (The Ember Widens)");
            Shutdown();
            Directory.Delete(dir, true);

            dir = TempDir("trunk_tide");
            NewRun(dir, out _);
            RunTrunk("tide_clear", "br_trode_tide", "path_tide", "beat_tide_path",
                     "obj_tide_keepsake", "br_trode_ember", "br_trode_stone");
            Check(GameServices.State.HasAbility("tide_mend"), "tide player owns tide_mend instead");
            Shutdown();
            Directory.Delete(dir, true);

            dir = TempDir("trunk_stone");
            NewRun(dir, out _);
            RunTrunk("stone_still", "br_trode_stone", "path_stone", "beat_stone_path",
                     "obj_stone_barricade", "br_trode_ember", "br_trode_tide");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 59. chapter completion chain
        private static void TestChapterCompletion()
        {
            Log.Add("[59] Chapter progression: decision -> path objective -> settle branch -> finale -> chapter 2 chains");
            string dir = TempDir("complete");
            NewRun(dir, out IEncounterSource content);

            PlayFirstLight("ember_reach");

            // the world action's effect completes the path objective (as the beacon interaction does in-game)
            GameServices.State.SetFlag("beacon_silenced", "1");

            Check(GameServices.State.State.CampaignBeatResolved("beat_ember_path"),
                  "path beat resolved by the objective completing");
            Check(BranchTaken("br_ember_settled"), "settle branch routed to the finale");
            Check(GameServices.State.State.CampaignBeatResolved("beat_council"), "finale beat resolved");
            Check(GameServices.State.State.CampaignChapterCompleted("ch_first_light"),
                  "chapter one completed on its completion conditions");
            CheckEq(GameServices.State.GetFlag("ch1_complete"), "1", "completion flag set (unlocks next chapter)");
            Check(GameServices.State.HasCodex("c1_ch1_complete"), "completion codex entry granted");

            // chapter two chained through pure data (entry = ch1_complete flag) -
            // it started AND completed in the same cascade, so prove it by record+journal
            Check(GameServices.State.State.CampaignChapterCompleted("ch_whispers")
                  && JournalHas("Chapter Two"),
                  "chapter two teaser started+resolved (chapters chain via content data)");
            Check(GameServices.State.GetFlag("ch2_teaser") == "1", "teaser beat resolved and flagged");
            Check(GameServices.State.State.CampaignChapterCompleted("ch_whispers"), "teaser chapter stamped complete");

            // the UI snapshot reflects the story
            CampaignServices.CampaignSnapshot snap = CampaignServices.Snapshot();
            Check(snap.pathLabels.Contains("Path of Ember"), "snapshot exposes the taken path label");
            Check(snap.journal.Count >= 4, "snapshot journal carries the story log");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 60. failure is a route
        private static void TestFailureRoute()
        {
            Log.Add("[60] Failure branch: the barricade falls -> different beat -> recovery -> chapter still completes");
            string dir = TempDir("failure");
            NewRun(dir, out IEncounterSource content);

            PlayFirstLight("stone_still");
            Check(BeatAvailable("beat_stone_path"), "stone player can still hold the line");

            // the shrine seal topples the barricade (the authored fail condition)
            GameServices.State.SetFlag("c1_echo_sealed", "1");

            CheckEq((int)WorldServices.Objectives.PhaseOf("obj_stone_barricade"), (int)ObjectivePhase.Failed,
                  "barricade objective FAILED");
            Check(!GameServices.State.State.CampaignBeatResolved("beat_stone_path"),
                  "the success beat stayed unresolved");
            Check(GameServices.State.State.CampaignBeatResolved("beat_stone_fell"),
                  "the FAILURE beat resolved (failure is a first-class trigger)");
            Check(BranchTaken("br_line_fell"), "the Line Fell branch routed the run");
            CheckEq(GameServices.State.GetFlag("path_fell"), "1", "path_fell flag set");
            CheckEq(GameServices.State.GetFlag("path_resolved"), "", "chapter NOT completed by failing");
            Check(!GameServices.State.State.CampaignChapterCompleted("ch_first_light"),
                  "the chapter stays open - the story continues through recovery");
            Check(BeatAvailable("beat_recovery"), "recovery beat available (different future content)");

            // rebuild: clearing the rubble completes the recovery objective
            GameServices.State.SetVar("rubble_count", 2);
            Check(GameServices.State.State.CampaignBeatResolved("beat_recovery"),
                  "recovery beat resolved by the rebuild objective");
            Check(BranchTaken("br_line_reheld"), "The Line Held Again branch taken");
            Check(GameServices.State.State.CampaignChapterCompleted("ch_first_light"),
                  "chapter completes through the recovery route too");
            Check(JournalHas("The Line Fell") && JournalHas("The Line Held Again"),
                  "failure journals differ from the success route");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 61. NPC/ability/dialogue-dependent branches
        private static void TestDependentBranches()
        {
            Log.Add("[61] Dependent branches: NPC bond gate, echo encounter reacts to the decision, branch dialogue");
            string dir = TempDir("dependent");
            NewRun(dir, out IEncounterSource content);
            Lines.Clear();

            PlayFirstLight("ember_reach");

            // ---- second encounter: Sera's dialogue is BRANCH-CONDITIONED on the earlier decision ----
            var flow = GameServices.Encounters;
            flow.Run("camp_sera_echo");
            int guard = 0;
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            bool sawEmberLine = false;
            foreach (DialogueLineEvent l in Lines)
                if (l.text != null && l.text.Contains("Ember still hums")) sawEmberLine = true;
            Check(sawEmberLine, "ember player hears the ember-specific sera line (conversation differs by branch)");
            flow.SelectChoice("tell_her");
            flow.Advance();
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();

            CheckEq(GameServices.State.GetFlag("sera_echo_seen"), "1", "echo decision left its flag");
            CheckEq(GameServices.State.GetBond("sera"), 2, "telling sera raised the bond");
            Check(GameServices.State.State.CampaignBeatResolved("beat_sera_echo"), "echo beat resolved");
            Check(BranchTaken("br_told_sera"), "truthful echo branch taken");
            Check(!BranchTaken("br_deflected"), "deflect branch not taken");

            // ---- NPC-dependent beat: sera only confides at bond >= 7 (5 from the warden + 2 told) ----
            Check(!GameServices.State.State.CampaignBeatResolved("beat_sera_confide"),
                  "confide beat still hidden at bond 2 (NPC-dependent gate holds)");
            CombatResolution.DefeatEnemy(content.Content.FindEnemy("choir_warden"), GameServices.State); // sera +5
            Check(GameServices.State.State.CampaignBeatResolved("beat_sera_confide"),
                  "confide beat resolved once sera's bond reached 7 (NPC-dependent branch)");
            CheckEq(GameServices.State.GetFlag("waystation_key"), "1", "confide beat unlocked the waystation key");
            Check(GameServices.Progress.State.State.echoBank >= 15, "confide + warden paid echoes");

            Shutdown();
            Directory.Delete(dir, true);

            // ---- the deflecting player gets the OTHER echo branch ----
            dir = TempDir("deflect");
            NewRun(dir, out content);
            Lines.Clear();
            PlayFirstLight("tide_clear");
            flow = GameServices.Encounters;
            flow.Run("camp_sera_echo");
            guard = 0;
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            bool sawTideLine = false;
            foreach (DialogueLineEvent l in Lines)
                if (l.text != null && l.text.Contains("went into the water")) sawTideLine = true;
            Check(sawTideLine, "tide player hears the tide-specific sera line");
            flow.SelectChoice("deflect");
            flow.Advance();
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
            Check(BranchTaken("br_deflected") && !BranchTaken("br_told_sera"),
                  "deflecting takes the other branch (Some Doors Stay Shut)");
            CheckEq(GameServices.State.GetBond("sera"), 0, "deflecting earned no bond");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 62. save/load: restart in the correct branch
        private static void TestSaveLoadRestart()
        {
            Log.Add("[62] Save/load: the whole route (beats, branches, chapters, journal) survives a restart");
            string dir = TempDir("saveload");
            NewRun(dir, out IEncounterSource content);

            PlayFirstLight("ember_reach");
            var flow = GameServices.Encounters;
            flow.Run("camp_sera_echo");                     // side beat FIRST (natural play order -
            int guard = 0;                                  // beats stop cascading once the chapter
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();   // completes)
            flow.SelectChoice("tell_her");
            flow.Advance();
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
            GameServices.State.SetFlag("beacon_silenced", "1");      // path settles -> chapter completes
            GameServices.PersistNow(autosaveMirror: true);

            // RESTART the "app"
            Shutdown();
            NewRun(dir, out content);

            CheckEq(GameServices.State.DecisionOption(StoryContentBuilder.DecisionFirstLight), "ember_reach",
                  "restart: the decision persists");
            Check(BranchTaken("br_trode_ember") && !BranchTaken("br_trode_tide"),
                  "restart: the taken branch persists (correct branch restored)");
            Check(BranchTaken("br_ember_settled") && BranchTaken("br_told_sera"),
                  "restart: later branches persist too");
            Check(GameServices.State.State.CampaignChapterCompleted("ch_first_light"),
                  "restart: chapter completion persists");
            Check(GameServices.State.State.CampaignBeatResolved("beat_council"), "restart: beats persist");
            Check(JournalHas("Path of Ember"), "restart: the journal persists");
            Check(GameServices.State.State.CampaignChapterCompleted("ch_whispers") && JournalHas("Whispers Under the Hall"),
                  "restart: chapter two's state restored (the branch's consequence)");
            CheckEq(GameServices.State.GetFlag("path_tide"), "", "restart: the other paths stay untouched");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 63. v4 -> v5 migration
        private static void TestV4Migration()
        {
            Log.Add("[63] Save migration: a v4 file (pre-campaign) loads and the route re-derives live");
            string dir = TempDir("migrate");
            var paths = new TempPaths(dir);

            // hand-write a v4 save: objectives exist, campaign fields do not
            string v4 = "{\"schemaVersion\":4,\"meta\":{\"slotName\":\"old\",\"timestamp\":\"2026-09-01T00:00:00\",\"playtimeSec\":0}," +
                        "\"scene\":{\"sceneKey\":\"FirstLocation\",\"checkpointId\":\"hall_spawn\"}," +
                        "\"gameState\":{\"decisions\":[{\"decisionId\":\"dec_c1_hall_first_light\",\"optionId\":\"tide_clear\",\"summary\":\"\"}]," +
                        "\"flags\":[],\"vars\":[],\"bonds\":[]}}";
            File.WriteAllText(paths.Resolve(SaveSystem.SlotPrefix.Replace("{0}", "0")), v4);

            NewRun(dir, out IEncounterSource content);

            Check(GameServices.State.HasDecision("dec_c1_hall_first_light"), "v4 decisions still load");
            Check(GameServices.State.State.campaignBeats.Count == 0 || GameServices.State.State.CampaignBeatResolved("beat_arrival"),
                  "campaign fields normalized (no crash on missing v5 sections)");
            // the live campaign re-derives the route from the restored decision
            Check(GameServices.State.State.CampaignBeatResolved("beat_arrival"),
                  "arrival beat re-derived from the v4 decision");
            Check(BranchTaken("br_trode_tide"), "the tide branch re-derived live (correct route restored)");
            CheckEq(GameServices.State.GetFlag("path_tide"), "1", "path flag re-applied");
            Check(BeatAvailable("beat_tide_path"), "tide path beat available after migration");

            Shutdown();
            Directory.Delete(dir, true);
        }
    }
}
