// CROSSROADS - campaign content pass tests (headless, single-assembly).
//
// Proves the COMPLETE playable campaign as data: prologue -> fracture -> becoming -> reckoning
// -> epilogue, driven only through the public runtime services (GameServices / WorldServices /
// CampaignServices / LocationServices) the way the scene does. Every playthrough below is the
// headless equivalent of "new game -> ending": it travels the location graph, runs the
// encounters, resolves the decisions, defeats the enemies through CombatResolution, and asserts
// the chapter/objective/world-state consequences the design promises.
using System;
using System.Collections.Generic;
using System.IO;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Gameplay;

namespace Crossroads.Tests
{
    public static class CampaignContentTests
    {
        private static readonly List<string> Log = new List<string>();
        private static readonly List<DialogueLineEvent> Lines = new List<DialogueLineEvent>();
        private static int _passed, _failed;
        public static List<string> GetLog() { return Log; }

        private static void Check(bool ok, string what)
        {
            if (ok) { _passed++; Log.Add("  PASS  " + what); }
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
            StoryLog.Info = delegate { };
            StoryLog.Warn = Console.WriteLine;
            StoryLog.Error = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(dir), new RuntimeContentSource(),
                "FirstLocation", "hall_spawn", 0, loadExisting: true);
            WorldServices.Init();
            CampaignServices.Init();
            LocationServices.Init();
            if (!_subscribed)
            {
                _subscribed = true;
                EventBus.Subscribe<DialogueLineEvent>(e => Lines.Add(e));
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
            return Path.Combine(Path.GetTempPath(), "crossroads_campaign_" + tag + "_" + Guid.NewGuid().ToString("N"));
        }

        private static StoryContentData Content { get { return GameServices.Content.Content; } }
        private static StateMutator S { get { return GameServices.State; } }

        /// <summary>Runs an encounter to completion, picking the given option at its decision (if any).</summary>
        private static bool Play(string encounterId, string optionId = "")
        {
            var flow = GameServices.Encounters;
            flow.Run(encounterId);
            int guard = 0;
            while (flow.IsRunning && guard++ < 60)
            {
                if (flow.AwaitingChoice)
                {
                    if (string.IsNullOrEmpty(optionId)) return false;
                    flow.SelectChoice(optionId);
                }
                else flow.Advance();
            }
            return !flow.IsRunning;
        }

        private static bool Travel(string id)
        {
            bool ok = LocationServices.Travel(id);
            if (!ok) Console.WriteLine("  [travel blocked] -> " + id + " from " + LocationServices.Locations.CurrentLocationId);
            return ok;
        }

        private static void Kill(string enemyId, int times = 1)
        {
            for (int i = 0; i < times; i++) CombatResolution.DefeatEnemy(Content.FindEnemy(enemyId), S);
        }

        private static bool LinesHave(string fragment)
        {
            for (int i = 0; i < Lines.Count; i++)
                if (Lines[i].text != null && Lines[i].text.Contains(fragment)) return true;
            return false;
        }

        private static bool JournalHas(string fragment)
        {
            var journal = S.State.campaignJournal;
            if (journal == null) return false;
            for (int i = 0; i < journal.Count; i++) if (journal[i].Contains(fragment)) return true;
            return false;
        }

        private static ObjectivePhase Phase(string id) { return WorldServices.Objectives.PhaseOf(id); }

        // ---------------------------------------------------------------- shared playthrough segments
        /// <summary>Chapter one (existing content) on the ember path, completed so ch1_complete is set.</summary>
        private static void PlayChapterOneEmber()
        {
            Play(StoryContentBuilder.EncounterFirstLight, "ember_reach");
            S.SetFlag("beacon_silenced", "1");            // the annex beacon world action
            S.SetFlag("ember_cache_opened", "1");
            Kill("choir_warden");
            Check(S.GetFlag("ch1_complete") == "1", "chapter one (existing) completes on the ember path");
        }

        private static void PlayPrologue(string kite, string pier, string summer)
        {
            Check(Travel("last_summer"), "hub -> The Last Summer (memory pier, open by design)");
            Check(Phase("obj_tut_move") == ObjectivePhase.Active, "tutorial objective active on arrival");
            S.SetFlag("tut_moved", "1");                  // kite pickup world action
            CheckEq(Phase("obj_tut_move"), ObjectivePhase.Completed, "movement tutorial completes on the kite pickup");
            CheckEq(Phase("obj_tut_talk"), ObjectivePhase.Active, "talk tutorial follows up automatically");
            NpcBrain mara = new NpcBrain(Content.FindNpc("mara_young"), GameServices.Progress);
            CheckEq(mara.DefaultInteraction().encounterId, "p1_kite", "young Mara offers the kite scene first");
            Check(Play("p1_kite", kite), "kite scene plays to the end");
            mara.Reapply();
            CheckEq(mara.DefaultInteraction().encounterId, "p1_pier", "then the pier race");
            Check(Play("p1_pier", pier), "pier scene plays");
            Check(Play("p1_summer_end", summer), "summer end scene plays");
            CheckEq(Phase("obj_tut_talk"), ObjectivePhase.Completed, "talk tutorial completes after the three prologue choices");
            Check(S.GetFlag("prologue_complete") == "1", "prologue flag set");
            Check(S.State.CampaignChapterCompleted("ch_prologue"), "Prologue chapter completes");
            Check(S.GetFlag("mara_fate") == "civilian", "Mara's fate starts as Civilian");
            Check(S.GetWorldState("market", "") == "intact", "Old Market: Intact (prologue variant)");
        }

        private static void PlayFracture(string mentor, string advice, string spar)
        {
            Check(Travel("hall") && Travel("fracture_night"), "hub -> Night of the Fracture (opens after prologue + chapter one)");
            Check(CampaignServices.Campaign.ActiveChapters.Exists(c => c.id == "ch_fracture"), "Fracture chapter live");
            int before = S.GetAffinity(mentor == "kael" ? "ember" : mentor == "odalys" ? "tide" : "stone");
            Check(Play("c1_fracture_open", mentor), "mentor choice cutscene plays");
            int after = S.GetAffinity(mentor == "kael" ? "ember" : mentor == "odalys" ? "tide" : "stone");
            CheckEq(after - before, 20, "mentor pick grants +20 starting affinity to their line");
            string lineAbility = mentor == "kael" ? "cinder_burst" : mentor == "odalys" ? "riptide" : "tremor_stomp";
            Check(S.HasAbility(lineAbility), "mentor's line ability unlocked (" + lineAbility + ")");
            Check(!S.HasAbility("riptide") || mentor == "odalys", "the other mentors' abilities stay locked");
            Check(GameServices.Abilities.Activate(lineAbility) == AbilityActivation.Ok, "the new ability activates through the AbilityManager");
            Check(Play("c1_mentor_lesson", advice), "mentor lesson (combat tutorial) plays with the advice decision");
            Check(Play("c1_dax_spar", spar), "Dax sparring scene");
            CheckEq(S.GetFlag("dax_fate"), spar == "spare" ? "wary" : "rival", "Dax fate after the spar");
            Check(Phase("obj_fn_arenas") == ObjectivePhase.Active, "street arenas objective active");
            EnemyDefinitionData grunt = Content.FindEnemy("choir_grunt");
            Check(ConditionEvaluator.Evaluate(grunt.activationConditions, S), "Choir grunts activate once the street is visited");
            Kill("choir_grunt", 2); Kill("choir_charger", 2);
            CheckEq(Phase("obj_fn_arenas"), ObjectivePhase.Completed, "three arenas cleared -> objective complete");
            Check(S.GetWorldState("market", "") == "contested", "Old Market: Contested after the Fracture night");
            S.SetFlag("fn_family_saved", "1");
            CheckEq(Phase("obj_fn_civilians"), ObjectivePhase.Completed, "side objective: family saved");
            Check(Travel("under_spire"), "-> Under the Spire (opens after the arenas)");
            Check(!ConditionEvaluator.Evaluate(Content.FindEnemy("first_echo").activationConditions, S), "First Echo dormant until the cordon breaks");
            Kill("choir_caster", 2); Kill("choir_bruiser");
            Check(ConditionEvaluator.Evaluate(Content.FindEnemy("first_echo").activationConditions, S), "First Echo activates after three kills");
            Play("c1_first_echo_intro");
            Kill("first_echo");
            Check(S.GetFlag("first_echo_defeated") == "1", "First Echo defeated");
            CheckEq(Phase("obj_us_descent"), ObjectivePhase.Completed, "Under the Spire objective completes");
            Check(Play("c1_first_echo_fallen"), "boss-fallen cutscene plays (mentor-flavoured)");
            Check(S.State.CampaignChapterCompleted("ch_fracture"), "Fracture chapter completes");
            Check(S.GetFlag("c2_open") == "1", "Becoming opens");
        }

        private static void PlayBecoming(string path, string maraChoice, string daxChoice, string duelEnd)
        {
            Check(Travel("interlude_becoming"), "-> Interlude: Becoming");
            Check(Play("i2_archivist", path), "Archivist offers the three paths; one chosen");
            CheckEq(S.GetFlag("c2_path"), path, "path flag");
            string arena = path;
            Check(Travel(arena), "-> " + arena + " (the chosen path unlocks only that location)");
            Check(!LocationServices.Locations.IsUnlocked(path == "docks" ? "sanctuary" : "docks"), "the other paths stay locked");
            Check(ConditionEvaluator.Evaluate(Content.FindEnemy("choir_sentinel").activationConditions, S), "C2 skins activate on the path");
            Kill("choir_sentinel", 4); Kill("choir_lancer", 4);
            Check(ConditionEvaluator.Evaluate(Content.FindEnemy("choir_elite").activationConditions, S), "the Elite activates after six kills");
            Kill("choir_elite");
            Check(S.GetFlag("c2_elite_down") == "1", "Elite down");
            if (path == "docks") Play("c2_docks_shed", "breach");
            string obj = path == "docks" ? "obj_docks_assault" : path == "sanctuary" ? "obj_sanctuary_hold" : "obj_wall_hold";
            CheckEq(Phase(obj), ObjectivePhase.Completed, "path objective complete (" + obj + ")");
            string capstone = path == "docks" ? "phoenix_reckoning" : path == "sanctuary" ? "call_ally" : "bulwark";
            Check(S.HasAbility(capstone), "path capstone ability unlocked (" + capstone + ")");
            // the timed D2 pressure choice
            DecisionNodeData pressure = Content.FindDecision("dec_save_mara");
            Check(pressure.timeLimitSeconds >= 5f && pressure.timeLimitSeconds <= 10f && pressure.options[pressure.timeoutOptionIndex].id == "hesitate",
                  "save-Mara choice is a 5-10 s D2 with 'hesitate' as the timeout outcome");
            Check(Play("c2_mara_pressure", maraChoice), "crane cable scene resolved (" + maraChoice + ")");
            Check(Travel("dax_arena"), "-> Dax Confrontation (after the path + the crane)");
            Check(Play("c2_dax_confront", daxChoice), "Dax confrontation choice");
            if (daxChoice == "duel")
            {
                Check(ConditionEvaluator.Evaluate(Content.FindEnemy("dax_rival").activationConditions, S), "duel: Dax activates as a boss");
                Kill("dax_rival");
                Check(Play("c2_dax_duel_end", duelEnd), "duel end choice (" + duelEnd + ")");
            }
            else
            {
                Check(ConditionEvaluator.Evaluate(Content.FindEnemy("choir_hunter").activationConditions, S), "truce: the Choir Hunter activates instead");
                Kill("choir_hunter");
                Play("c2_dax_hunter_fallen");
            }
            CheckEq(Phase("obj_dax"), ObjectivePhase.Completed, "Dax objective completes");
            Check(S.State.CampaignChapterCompleted("ch_becoming"), "Becoming chapter completes");
        }

        private static void PlayReckoningToChoirmaster()
        {
            Check(Travel("interlude_reckoning"), "-> Interlude: Reckoning");
            Check(Play("i3_archivist"), "Archivist reads the world state back");
            Check(S.GetFlag("dominant") != "", "dominant line evaluated at the reveal (" + S.GetFlag("dominant") + ")");
            Check(S.GetFlag("mentor_fate") != "", "mentor fate evaluated (" + S.GetFlag("mentor_fate") + ")");
            Check(Travel("market"), "-> The Old Market");
            Kill("hollow_husk", 3);
            Check(ConditionEvaluator.Evaluate(Content.FindEnemy("choir_cantor").activationConditions, S), "Cantor activates after the husks");
            Kill("choir_cantor");
            CheckEq(Phase("obj_market"), ObjectivePhase.Completed, "market objective completes; Cantor's voice taken");
            Check(S.HasItem("cantor_voice"), "Cantor's Voice item held");
            Check(Travel("spire_ascent"), "-> Ascent of the Spire");
            S.AddVar("anomaly_count", 2);
            Kill("spire_warden", 2);
            CheckEq(Phase("obj_ascent"), ObjectivePhase.Completed, "ascent objective completes");
            Check(Travel("choirmaster"), "-> The Choirmaster");
            Play("c3_cm_open");
            Kill("choirmaster_p1");
            Check(S.GetFlag("cm_p1") == "1", "phase one broken");
        }

        private static void FinishChoirmaster(string transition)
        {
            Check(Play("c3_cm_transition", transition), "phase transition choice (" + transition + ")");
            if (transition == "refuse") return;
            Check(ConditionEvaluator.Evaluate(Content.FindEnemy("choirmaster_p2").activationConditions, S), "phase two activates on press");
            Kill("choirmaster_p2");
            Check(ConditionEvaluator.Evaluate(Content.FindEnemy("choirmaster_p3").activationConditions, S), "phase three activates");
            Kill("choirmaster_p3");
            Check(S.GetFlag("choirmaster_defeated") == "1", "Choirmaster defeated");
        }

        private static void PlayEpilogue(string expectedEnding)
        {
            CheckEq(S.GetFlag("ending"), expectedEnding, "ending recorded: " + expectedEnding);
            Check(S.State.CampaignChapterCompleted("ch_reckoning"), "Reckoning chapter completes");
            Check(Travel("epilogue"), "-> Epilogue");
            Lines.Clear();
            Check(Play("ep_epilogue", "close"), "epilogue narration plays and closes");
            Check(S.GetFlag("epilogue_seen") == "1" && S.State.CampaignChapterCompleted("ch_epilogue"), "Epilogue chapter completes");
            Check(S.GetFlag("campaign_complete") == "1", "campaign_complete flag - THE END");
            Check(JournalHas("THE END"), "journal closes with THE END");
        }

        // ================================================================ 80. content contracts
        private static void TestContentContracts()
        {
            Log.Add("[80] Campaign content: GAME_DESIGN coverage (13 locations, 7 chapters, roster, lines, endings)");
            string dir = TempDir("contracts");
            NewRun(dir);
            StoryContentData c = Content;

            foreach (string id in StoryContentBuilder.CampaignLocationIds)
                Check(c.FindLocation(id) != null, "location present: " + id);
            CheckEq(StoryContentBuilder.CampaignLocationIds.Length, 13, "thirteen playable campaign scenes (GAME_DESIGN §11.2)");
            CheckEq(c.chapters.Count, 7, "seven chapters (first light, whispers, prologue, fracture, becoming, reckoning, epilogue)");
            foreach (string n in new[] { "mara_young", "dax", "kael", "odalys", "bran", "archivist", "mara_c2", "mara_c3" })
                Check(c.FindNpc(n) != null, "npc present: " + n);
            CheckEq(c.FindNpc("dax").sheetRef, "REF-03", "Dax uses the canonical REF-03 sheet");
            CheckEq(c.FindNpc("archivist").sheetRef, "REF-05", "Archivist uses REF-05");
            Check(c.FindNpc("mara_young").sheetRef == "REF-02" && c.FindNpc("mara_c2").sheetRef == "REF-02" && c.FindNpc("mara_c3").sheetRef == "REF-02",
                  "every Mara appearance shares the REF-02 canonical sheet");
            foreach (string line in new[] { "ember", "tide", "stone", "hollow" })
            {
                int n = 0;
                foreach (var a in c.progression.abilities) if (a.line == line) n++;
                Check(n >= 2, "ability line '" + line + "' has at least two powers (" + n + ")");
            }
            foreach (var a in c.progression.abilities)
                Check(c.FindAbilityCombat(a.id) != null, "ability has a combat payload: " + a.id);
            foreach (string e in new[] { "choir_grunt", "choir_charger", "choir_caster", "choir_bruiser", "choir_elite", "first_echo", "dax_rival", "choir_cantor", "choirmaster_p1", "choirmaster_p2", "choirmaster_p3" })
                Check(c.FindEnemy(e) != null, "enemy present: " + e);
            DecisionNodeData ending = c.FindDecision("dec_ending");
            CheckEq(ending.options.Count, 7, "final decision offers the seven endings");
            foreach (string id in StoryContentBuilder.EndingIds)
                Check(ending.FindOption(id) != null, "ending option: " + id);
            Check(ending.FindOption("long_way_home").conditions.Count == 0, "The Long Way Home is always available");
            Check(ending.FindOption("hollow_throne").conditions.Exists(x => x.type == ConditionType.AffinityAtLeast && x.key == "hollow" && x.amount == 25),
                  "Hollow Throne requires Hollow >= 25");
            Check(ending.FindOption("tides_embrace").conditions.Exists(x => x.type == ConditionType.BondAtLeast && x.key == "mara" && x.amount == 50),
                  "Tide's Embrace requires Mara Bonded (>= 50)");

            // every reference resolves (the same integrity rule the merge script enforced)
            bool ok = true;
            foreach (var loc in c.locations)
            {
                foreach (string e in loc.encounters) if (c.FindEncounter(e) == null) ok = false;
                foreach (string o in loc.objectives) if (c.FindObjective(o) == null) ok = false;
                foreach (string n in loc.npcs) if (c.FindNpc(n) == null) ok = false;
                foreach (string x in loc.connections) if (c.FindLocation(x) == null) ok = false;
            }
            Check(ok, "every location encounter/objective/npc/connection resolves");
            // the location graph is connected from the hub
            var seen = new HashSet<string> { "hall" };
            var stack = new Stack<string>(); stack.Push("hall");
            while (stack.Count > 0)
            {
                string cur = stack.Pop();
                foreach (var loc in c.locations)
                    if (LocationServices.Locations.Connected(cur, loc.id) && seen.Add(loc.id)) stack.Push(loc.id);
            }
            CheckEq(seen.Count, c.locations.Count, "every location is reachable from the hub through the connection graph");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 81. full playthrough: Ember / Kael / press / docks / pursue / duel-finish -> Ashen Crown
        private static void TestPlaythroughAshenCrown()
        {
            Log.Add("[81] Playthrough A: Ember line, Kael, Dax pressed+finished, Docks burned -> ASHEN CROWN");
            string dir = TempDir("ashen");
            NewRun(dir);
            PlayChapterOneEmber();
            PlayPrologue("fly", "jump", "hero");
            PlayFracture("kael", "obey", "press");
            PlayBecoming("docks", "pursue", "duel", "finish");
            Check(S.GetFlag("mara_fate") == "lost" && S.GetFlag("mara_alive") == "0", "Mara: Lost (pursued the ledger)");
            Check(S.GetFlag("dax_fate") == "dead" && S.GetFlag("dax_alive") == "0", "Dax: dead (finished in the duel)");
            PlayReckoningToChoirmaster();
            CheckEq(S.GetFlag("dominant"), "ember", "dominant line: Ember");
            FinishChoirmaster("press");
            var visible = GameServices.Decisions.VisibleOptions("dec_ending");
            Check(visible.Exists(o => o.id == "ashen_crown"), "Ashen Crown offered (Ember >= 60, Dax dead)");
            Check(!visible.Exists(o => o.id == "tides_embrace"), "Tide's Embrace hidden (Mara lost)");
            Check(!visible.Exists(o => o.id == "balance"), "Balance hidden (a dominant line exists)");
            Check(visible.Exists(o => o.id == "martyrs_dawn") && visible.Exists(o => o.id == "long_way_home"), "Martyr's Dawn + Long Way Home always reachable here");
            Check(Play("c3_final_decision", "ashen_crown"), "final decision resolved");
            PlayEpilogue("ashen_crown");
            Check(LinesHave("ASHEN CROWN"), "epilogue narrates the Ashen Crown variant");
            Check(LinesHave("Mara: Lost"), "epilogue reflects Mara's fate");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 82. full playthrough: Tide / Odalys / spare / sanctuary / save / truce -> Tide's Embrace
        private static void TestPlaythroughTidesEmbrace()
        {
            Log.Add("[82] Playthrough B: Tide line, Odalys, Dax spared+truce, Sanctuary held, Mara saved -> TIDE'S EMBRACE");
            string dir = TempDir("tide");
            NewRun(dir);
            PlayChapterOneEmber();
            PlayPrologue("give", "stay", "healer");
            PlayFracture("odalys", "obey", "spare");
            PlayBecoming("sanctuary", "save", "truce", "");
            Check(S.GetFlag("mara_fate") == "ally", "Mara: Ally (saved at high bond)");
            Check(S.GetFlag("dax_fate") == "truce" && S.GetFlag("dax_alive") == "1", "Dax: Truce, alive");
            Check(S.GetWorldState("docks", "") == "flooded" && S.GetWorldState("market", "") == "rebuilt", "world: Docks flooded sanctum, Market rebuilt");
            PlayReckoningToChoirmaster();
            CheckEq(S.GetFlag("dominant"), "tide", "dominant line: Tide");
            CheckEq(S.GetFlag("mentor_fate"), "alive", "Odalys alive (bond >= 20)");
            FinishChoirmaster("press");
            Check(S.State.CampaignBeatResolved("beat_rk_ins_mara_ally"), "phase two insert: Mara fights beside you");
            Check(Play("c3_final_decision", "tides_embrace"), "Tide's Embrace chosen");
            PlayEpilogue("tides_embrace");
            Check(LinesHave("TIDE'S EMBRACE") && LinesHave("Mara: Ally"), "epilogue narrates Tide's Embrace with Mara as Ally");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 83. Stone / Bran / long wall / yield -> The Unmoved; then Hollow + refusal + Martyr variants
        private static void TestPlaythroughUnmovedAndRefusal()
        {
            Log.Add("[83] Playthrough C: Stone line, Bran, Long Wall, Dax yielded -> THE UNMOVED; refusal -> THE LONG WAY HOME");
            string dir = TempDir("stone");
            NewRun(dir);
            PlayChapterOneEmber();
            PlayPrologue("keep", "refuse", "wall");
            PlayFracture("bran", "obey", "spare");
            PlayBecoming("long_wall", "save", "duel", "yield");
            Check(S.GetFlag("dax_fate") == "redeemed", "Dax: Redeemed (yielded at positive bond)");
            Check(S.GetVar("districts_saved", 0) >= 2, "two districts saved (family + the wall)");
            PlayReckoningToChoirmaster();
            CheckEq(S.GetFlag("dominant"), "stone", "dominant line: Stone");
            FinishChoirmaster("press");
            Check(S.State.CampaignBeatResolved("beat_rk_ins_dax_ally"), "phase two insert: Dax, redeemed, stands with you");
            Check(GameServices.Decisions.VisibleOptions("dec_ending").Exists(o => o.id == "the_unmoved"), "The Unmoved offered (Stone >= 60, 2 districts)");
            Check(Play("c3_final_decision", "the_unmoved"), "The Unmoved chosen");
            PlayEpilogue("the_unmoved");
            Shutdown();

            // refusal route: a fresh run that walks out at the transition
            string dir2 = TempDir("refuse");
            NewRun(dir2);
            PlayChapterOneEmber();
            PlayPrologue("give", "stay", "healer");
            PlayFracture("kael", "defy", "spare");
            PlayBecoming("docks", "save", "truce", "");
            PlayReckoningToChoirmaster();
            FinishChoirmaster("refuse");
            Check(S.GetFlag("campaign_ended") == "1" && S.GetFlag("ending") == "long_way_home", "refusing the call ends the campaign: The Long Way Home");
            PlayEpilogue("long_way_home");
            Check(LinesHave("THE LONG WAY HOME"), "epilogue narrates the refusal ending");
            Shutdown();
            Directory.Delete(dir, true);
            Directory.Delete(dir2, true);
        }

        // ================================================================ 84. Hollow Throne (secret) + Balance + Martyr's Dawn + save/restore mid-campaign
        private static void TestHollowBalanceMartyrAndPersistence()
        {
            Log.Add("[84] Hollow Throne (absorb Dax), Balance (hybrid), Martyr's Dawn, and a mid-campaign restart");
            string dir = TempDir("hollow");
            NewRun(dir);
            PlayChapterOneEmber();
            PlayPrologue("fly", "jump", "hero");
            PlayFracture("kael", "defy", "press");             // hollow +15 so far
            Check(S.GetAffinity("hollow") >= 15, "Hollow accrues from cruelty/defiance (" + S.GetAffinity("hollow") + ")");
            PlayBecoming("docks", "pursue", "duel", "absorb"); // pursue +15 -> absorb allowed (>= 25)
            Check(S.HasAbility("hollow_throne") && S.HasItem("dax_echo"), "absorbing Dax unlocks the Hollow ultimate + the echo item");
            Check(Travel("interlude_reckoning"), "-> reckoning shrine");
            Check(Play("i3_hollow_shrine", "drink"), "the dark plinth accepts a Hollow >= 25 player");
            Check(S.HasAbility("drain_touch"), "Drain Touch unlocked");
            Check(S.GetWorldState("spire", "") == "collapsed", "Spire: Collapsed (Hollow >= 25)");
            Play("i3_archivist");
            Check(Travel("market"), "-> market"); Kill("hollow_husk", 3); Kill("choir_cantor");
            Check(Travel("spire_ascent"), "-> ascent"); S.AddVar("anomaly_count", 2); Kill("spire_warden", 2);
            Check(Travel("choirmaster"), "-> choirmaster"); Kill("choirmaster_p1");
            FinishChoirmaster("press");
            Check(GameServices.Decisions.VisibleOptions("dec_ending").Exists(o => o.id == "hollow_throne"), "Hollow Throne offered");
            Check(Play("c3_final_decision", "hollow_throne"), "Hollow Throne taken");
            PlayEpilogue("hollow_throne");
            Shutdown();

            // Balance: deliberate hybrid (no line >= 60), Mara AND Dax alive
            string dir2 = TempDir("balance");
            NewRun(dir2);
            PlayChapterOneEmber();
            PlayPrologue("give", "stay", "wall");
            PlayFracture("bran", "defy", "spare");
            PlayBecoming("docks", "save", "truce", "");
            PlayReckoningToChoirmaster();
            CheckEq(S.GetFlag("dominant"), "none", "no line reached 60: deliberate hybrid");
            FinishChoirmaster("press");
            var vis = GameServices.Decisions.VisibleOptions("dec_ending");
            Check(vis.Exists(o => o.id == "balance"), "Balance offered (hybrid, Mara and Dax alive)");
            Check(!vis.Exists(o => o.id == "martyrs_dawn"), "Martyr's Dawn needs a dominant line");
            Check(Play("c3_final_decision", "balance"), "Balance chosen");
            PlayEpilogue("balance");
            Shutdown();

            // Martyr's Dawn + persistence: stop in the middle of Becoming, boot again from disk, finish
            string dir3 = TempDir("martyr");
            NewRun(dir3);
            PlayChapterOneEmber();
            PlayPrologue("fly", "jump", "hero");
            PlayFracture("kael", "obey", "spare");
            Check(Travel("interlude_becoming") && Play("i2_archivist", "docks"), "path chosen before the restart");
            GameServices.PersistNow(autosaveMirror: true);
            Shutdown();
            NewRun(dir3);
            CheckEq(LocationServices.Locations.CurrentLocationId, "interlude_becoming", "restart: location restored mid-campaign");
            Check(S.GetFlag("mentor") == "kael" && S.HasAbility("cinder_burst"), "restart: mentor + line ability restored");
            Check(S.State.CampaignChapterCompleted("ch_fracture") && CampaignServices.Campaign.ActiveChapters.Exists(c => c.id == "ch_becoming"),
                  "restart: chapter progress restored (Fracture done, Becoming live)");
            Check(Travel("docks"), "restart: the chosen path is still open");
            Kill("choir_sentinel", 4); Kill("choir_lancer", 4); Kill("choir_elite"); Play("c2_docks_shed", "breach");
            Play("c2_mara_pressure", "save");
            Check(Travel("dax_arena") && Play("c2_dax_confront", "truce"), "truce with Dax");
            Kill("choir_hunter");
            PlayReckoningToChoirmaster();
            FinishChoirmaster("mentor_shield");
            CheckEq(S.GetFlag("mentor_fate"), "fallen", "the mentor fell holding the Choirmaster");
            Check(Play("c3_final_decision", "martyrs_dawn"), "Martyr's Dawn chosen");
            PlayEpilogue("martyrs_dawn");
            Check(LinesHave("Kael: Fallen"), "epilogue: the fallen mentor's memorial line");
            Shutdown();
            Directory.Delete(dir, true);
            Directory.Delete(dir2, true);
            Directory.Delete(dir3, true);
        }

        // ================================================================ 85. failure routes + NPC fate states + world variants
        private static void TestFailureRoutesAndFates()
        {
            Log.Add("[85] Crisis failure -> recovery route; hesitation timeout; Dax final enemy; NPC state titles");
            string dir = TempDir("fail");
            NewRun(dir);
            PlayChapterOneEmber();
            PlayPrologue("keep", "jump", "hero");
            PlayFracture("odalys", "obey", "press");
            Check(Travel("interlude_becoming") && Play("i2_archivist", "sanctuary") && Travel("sanctuary"), "sanctuary path");
            S.AddVar("sanctuary_breaches", 3);
            CheckEq(Phase("obj_sanctuary_hold"), ObjectivePhase.Failed, "three breaches -> the Sanctuary crisis FAILS");
            Check(S.GetWorldState("market", "") == "ruined", "Old Market: Ruined (C2 failure variant)");
            CheckEq(Phase("obj_sanctuary_recover"), ObjectivePhase.Active, "recovery objective offered after the failure");
            Kill("choir_sentinel", 4); Kill("choir_lancer", 4); Kill("choir_elite");
            CheckEq(Phase("obj_sanctuary_recover"), ObjectivePhase.Completed, "recovery completes when the Elite falls");
            Check(S.HasAbility("call_ally"), "capstone still granted through the recovery route");
            Check(S.State.CampaignBeatResolved("beat_bc_sanctuary_lost"), "campaign took the failure beat");
            // D2 timeout = hesitate
            var flow = GameServices.Encounters;
            flow.Run("c2_mara_pressure");
            int guard = 0; while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            GameServices.Decisions.ResolveTimeout("dec_save_mara");
            CheckEq(GameServices.Decisions.ResolvedOption("dec_save_mara"), "hesitate", "timer expiry resolves to 'hesitate'");
            while (flow.IsRunning && guard++ < 40) { if (flow.AwaitingChoice) flow.SelectChoice("hesitate"); else flow.Advance(); }
            Check(S.GetFlag("mara_hurt") == "1", "Mara hurt on hesitation");
            Check(Travel("dax_arena"), "-> dax arena");
            Check(!GameServices.Decisions.VisibleOptions("dec_dax_confront").Exists(o => o.id == "truce"), "truce hidden at negative Dax bond (pressed at 16)");
            Play("c2_dax_confront", "duel"); Kill("dax_rival"); Play("c2_dax_duel_end", "yield");
            CheckEq(S.GetFlag("dax_fate"), "final_enemy", "yielding to a hostile Dax makes him the Final Enemy");
            NpcBrain dax = new NpcBrain(Content.FindNpc("dax"), GameServices.Progress);
            Check(dax.CurrentTitle.Contains("Rival"), "Dax NPC state title reflects the rivalry (" + dax.CurrentTitle + ")");
            NpcBrain mara = new NpcBrain(Content.FindNpc("mara_c2"), GameServices.Progress);
            Check(mara.CurrentTitle.Contains("Warm") || mara.CurrentTitle == "Mara", "Mara C2 title derives from bond (" + mara.CurrentTitle + ")");
            PlayReckoningToChoirmaster();
            FinishChoirmaster("press");
            Check(S.State.CampaignBeatResolved("beat_rk_ins_dax_final") && ConditionEvaluator.Evaluate(Content.FindEnemy("dax_final").activationConditions, S),
                  "phase two insert: Dax as Final Enemy activates");
            Kill("dax_final");
            Check(S.GetFlag("dax_alive") == "0", "Dax falls in phase two");
            Check(Play("c3_final_decision", "long_way_home"), "walk away");
            PlayEpilogue("long_way_home");
            Shutdown();
            Directory.Delete(dir, true);
        }

        public static void RunAll(out int passed, out int failed)
        {
            Console.WriteLine();
            TestContentContracts();
            TestPlaythroughAshenCrown();
            TestPlaythroughTidesEmbrace();
            TestPlaythroughUnmovedAndRefusal();
            TestHollowBalanceMartyrAndPersistence();
            TestFailureRoutesAndFates();
            passed = _passed;
            failed = _failed;
        }
    }
}
