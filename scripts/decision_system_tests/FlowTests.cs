// ============================================================================
// CROSSROADS headless test of the decision-system foundation:
//   walk -> approach -> interaction prompt -> dialogue -> choices -> select ->
//   decision saved -> consequence/state change -> RESTART -> decision remains.
// Compiles the pure C# system (Core + Narrative + Unity stub) and runs the
// exact same code paths the game uses (EventBus, StateMutator, DecisionManager,
// EncounterFlow, SaveSystem, ProximitySelector, StoryContentBuilder).
// Run:  mcs -out:FlowTests.exe UnityStub.cs TestJson.cs FlowTests.cs <core+narrative .cs> && mono FlowTests.exe
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Tests
{
    public static class FlowTests
    {
        private static int _passed, _failed;
        private static readonly List<string> Log = new List<string>();

        // ---------------------------------------------------------------- helpers
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

        private static class Harness
        {
            public static readonly List<DialogueLineEvent> Lines = new List<DialogueLineEvent>();
            public static readonly List<DecisionPromptEvent> Prompts = new List<DecisionPromptEvent>();
            public static readonly List<DecisionResolvedEvent> Resolved = new List<DecisionResolvedEvent>();
            public static readonly List<DialogueEndedEvent> Ended = new List<DialogueEndedEvent>();
            public static readonly List<SaveCompletedEvent> Saves = new List<SaveCompletedEvent>();

            private static bool _subscribed;
            public static void Subscribe()
            {
                if (_subscribed) return;
                _subscribed = true;
                EventBus.Subscribe<DialogueLineEvent>(OnLine);
                EventBus.Subscribe<DecisionPromptEvent>(OnPrompt);
                EventBus.Subscribe<DecisionResolvedEvent>(OnResolved);
                EventBus.Subscribe<DialogueEndedEvent>(OnEnded);
                EventBus.Subscribe<SaveCompletedEvent>(OnSave);
            }
            public static void Unsubscribe()
            {
                if (!_subscribed) return;
                _subscribed = false;
                EventBus.Unsubscribe<DialogueLineEvent>(OnLine);
                EventBus.Unsubscribe<DecisionPromptEvent>(OnPrompt);
                EventBus.Unsubscribe<DecisionResolvedEvent>(OnResolved);
                EventBus.Unsubscribe<DialogueEndedEvent>(OnEnded);
                EventBus.Unsubscribe<SaveCompletedEvent>(OnSave);
            }
            private static void OnLine(DialogueLineEvent e) { Lines.Add(e); }
            private static void OnPrompt(DecisionPromptEvent e) { Prompts.Add(e); }
            private static void OnResolved(DecisionResolvedEvent e) { Resolved.Add(e); }
            private static void OnEnded(DialogueEndedEvent e) { Ended.Add(e); }
            private static void OnSave(SaveCompletedEvent e) { Saves.Add(e); }
            public static void Reset()
            {
                Lines.Clear(); Prompts.Clear(); Resolved.Clear(); Ended.Clear(); Saves.Clear();
            }
        }

        private static void NewRun(string dir, out IEncounterSource content)
        {
            content = new RuntimeContentSource(); // code-built mirror of the shipped .asset
            StoryLog.Info = Console.WriteLine;
            StoryLog.Warn = Console.WriteLine;
            StoryLog.Error = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(dir), content,
                "FirstLocation", "hall_spawn", 0, loadExisting: true);
            Harness.Subscribe();
        }

        private class TestJsonAdapter : IJsonSerializer
        {
            public string ToJson(object o, bool prettyPrint) { return TestJson.ToJson(o); }
            public T FromJson<T>(string json) { return TestJson.FromJson<T>(json); }
        }

        // ---------------------------------------------------------------- 1. proximity
        private static void TestProximity()
        {
            Log.Add("[1] Proximity: walk -> prompt appears -> prompt disappears");
            var mara = new ProximityTarget("Mara_NPC", new Point3(6.5f, 0f, -8f), 3.2f, 20f);
            var door = new ProximityTarget("SM_Door_000", new Point3(0f, 0f, -19.9f), 2.5f, 100f);
            var candidates = new List<ProximityTarget> { mara, door };

            var pick = ProximitySelector.Pick(new Point3(0f, 0f, -16f), candidates);
            Check(pick == null, "spawn is out of range of every target (no prompt)");

            pick = ProximitySelector.Pick(new Point3(5.2f, 0f, -8.4f), candidates);
            Check(pick == mara, "walked near Mara -> prompt targets Mara");

            // tie-break: equal distance, lower priority wins
            var clone = new ProximityTarget("Clone", new Point3(5.0f, 0f, -8.0f), 3.0f, 5f);
            candidates.Add(clone);
            var near = new Point3(5.0f, 0f, -8.0f);
            pick = ProximitySelector.Pick(near, candidates);
            Check(pick == clone, "equal distance -> priority breaks the tie");

            // out of range again
            pick = ProximitySelector.Pick(new Point3(0f, 0f, -16f), candidates);
            Check(pick == null, "walked away -> prompt disappears");

            // in-range API
            Check(ProximitySelector.InRange(new Point3(6.0f, 0f, -8.5f), mara), "InRange helper agrees");
        }

        // ---------------------------------------------------------------- 2/4/5. full flows
        private static void RunEncounterScript(string dir, string expectedAftermathContains, string optionId,
                                               Action<StateMutator> asserts, Action<GameState> finalAsserts)
        {
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);
            var flow = GameServices.Encounters;

            flow.Run(StoryContentBuilder.EncounterFirstLight);
            Check(flow.IsRunning, "encounter running");
            Check(Harness.Lines.Count == 1, "first intro line shown");
            Check(GameServices.State.HasFlag("c1_hall_drive") == false, "no flag before the choice");

            flow.Advance(); flow.Advance();
            Check(Harness.Lines.Count == 3, "three intro lines shown (Mara), one per tap");
            flow.Advance();
            Check(Harness.Prompts.Count == 1, "decision prompt presented");
            Check(Harness.Prompts[0].choices.Count == 3, "exactly 3 choices");
            Check(flow.AwaitingChoice, "runner waits for the choice");
            Check(InputLock.Active, "movement locked during choice");

            flow.SelectChoice(optionId);
            Check(!flow.AwaitingChoice, "choice consumed");
            Check(Harness.Resolved.Count == 1, "DecisionResolvedEvent fired");
            Check(Harness.Resolved[0].optionId == optionId, "resolved event carries the option");
            // afterText narration line
            int lastIdx = Harness.Lines.Count - 1;
            Check(lastIdx >= 0 && Harness.Lines[lastIdx].speaker == "", "afterText published as narration (no speaker)");

            asserts(GameServices.State);

            // aftermath variant (condition-gated by the just-set flag)
            flow.Advance();
            Check(Harness.Lines[Harness.Lines.Count - 1].text.Contains(expectedAftermathContains),
                  "aftermath dialogue differs per choice (contains '" + expectedAftermathContains + "')");
            flow.Advance(); // end of line
            Check(!flow.IsRunning && Harness.Ended.Count == 1, "encounter ended");
            Check(!InputLock.Active, "movement unlocked after the encounter");
            Check(Harness.Saves.Count >= 1 && Harness.Saves[Harness.Saves.Count - 1].ok, "autosave after decision");

            finalAsserts(GameServices.State.State);
            GameServices.Shutdown(silent: true);
            Harness.Unsubscribe();
        }

        private static void TestEmberFlow()
        {
            Log.Add("[2] Flow A (Ember): reach the light");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_a_" + Guid.NewGuid().ToString("N"));
            RunEncounterScript(dir, "burned red", "ember_reach", s =>
            {
                CheckEq(s.GetAffinity("ember"), 10, "ember +10");
                CheckEq(s.GetAffinity("tide"), 0, "tide unchanged");
                CheckEq(s.GetBond("mara"), 5, "mara bond +5");
                CheckEq(s.GetWorldState("hall"), "ember", "hall world state = ember");
                Check(s.GetEntity("ember_marker"), "ember marker entity ON");
                Check(!s.GetEntity("tide_marker"), "tide marker entity OFF");
                Check(!s.GetEntity("tide_bystanders"), "bystanders OFF (not rescued)");
            }, gs =>
            {
                CheckEq(gs.echoBank, 15, "echoes granted (+15)");
                Check(gs.HasCodex("c1_echo_first_light") && gs.HasCodex("c1_echo_ember"), "codex entries added");
                CheckEq(gs.decisions.Count, 1, "one decision recorded");
                CheckEq(gs.DecisionOption("dec_c1_hall_first_light"), "ember_reach", "decision stored");
            });
            Directory.Delete(dir, true);
        }

        private static void TestTideFlow()
        {
            Log.Add("[3] Flow B (Tide): get the others out");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_b_" + Guid.NewGuid().ToString("N"));
            RunEncounterScript(dir, "twins clear", "tide_clear", s =>
            {
                CheckEq(s.GetAffinity("tide"), 10, "tide +10");
                CheckEq(s.GetAffinity("ember"), 0, "ember unchanged");
                CheckEq(s.GetBond("mara"), 10, "mara bond +10");
                CheckEq(s.GetWorldState("hall"), "tide", "hall world state = tide");
                Check(s.GetEntity("tide_marker"), "tide marker ON");
                Check(s.GetEntity("tide_bystanders"), "bystanders ON (rescued)");
                Check(!s.GetEntity("ember_marker"), "ember marker OFF");
            }, gs => CheckEq(gs.echoBank, 20, "echoes granted (+20)"));
            Directory.Delete(dir, true);
        }

        private static void TestStoneFlow()
        {
            Log.Add("[4] Flow C (Stone): stay still");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_c_" + Guid.NewGuid().ToString("N"));
            RunEncounterScript(dir, "third time", "stone_still", s =>
            {
                CheckEq(s.GetAffinity("stone"), 10, "stone +10");
                CheckEq(s.GetBond("mara"), 3, "mara bond +3");
                CheckEq(s.GetWorldState("hall"), "stone", "hall world state = stone");
                Check(s.GetEntity("stone_marker"), "stone marker ON");
                Check(!s.GetEntity("tide_bystanders"), "bystanders OFF");
            }, gs => CheckEq(gs.echoBank, 15, "echoes granted (+15)"));
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 3. restart
        private static void TestRestartPersistence()
        {
            Log.Add("[5] Restart: decision survives a full re-init (app kill -> relaunch)");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_restart_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);
            var flow = GameServices.Encounters;

            flow.Run(StoryContentBuilder.EncounterFirstLight);
            flow.Advance(); flow.Advance(); flow.Advance();
            flow.SelectChoice("ember_reach");
            flow.Advance(); flow.Advance();
            Check(!flow.IsRunning, "first run finished");
            string savedPath = GameServices.Save.SavePath;
            Check(File.Exists(savedPath), "save file exists at " + savedPath);
            string json = File.ReadAllText(savedPath);
            Check(json.Contains("\"dec_c1_hall_first_light\""), "save JSON contains the decision id");

            // ---- kill the app: shutdown everything ----
            Harness.Unsubscribe();
            GameServices.Shutdown(silent: true);
            Harness.Reset();

            // ---- relaunch: fresh services, load from disk ----
            NewRun(dir, out content);
            CheckEq(GameServices.State.State.decisions.Count, 1, "decision restored from disk");
            CheckEq(GameServices.State.GetFlag("c1_hall_drive"), "ember", "flag restored");
            CheckEq(GameServices.State.GetAffinity("ember"), 10, "affinity restored");
            CheckEq(GameServices.State.GetBond("mara"), 5, "bond restored");
            Check(GameServices.State.GetEntity("ember_marker"), "entity state restored (marker ON)");

            // world-state applier equivalent (what StoryWorldState does at boot)
            CheckEq(GameServices.State.GetWorldState("hall"), "ember", "world state restored");

            // re-talk the same NPC: no decision prompt, variant aftermath, no double-record
            var flow2 = GameServices.Encounters;
            flow2.Run(StoryContentBuilder.EncounterFirstLight);
            Check(flow2.IsRunning, "re-run starts");
            Check(Harness.Prompts.Count == 0, "already-resolved decision is NOT re-presented");
            string firstLine = Harness.Lines[0].text;
            Check(firstLine.Contains("still standing"), "re-talk opener differs (DecisionWas condition)");
            flow2.Advance(); // -> decide node (already resolved) -> skipped straight into aftermath
            string lastLine = Harness.Lines[Harness.Lines.Count - 1].text;
            Check(lastLine.Contains("burned red"), "aftermath variant still matches the stored choice");
            flow2.Advance();
            Check(!flow2.IsRunning, "re-run ended cleanly");
            CheckEq(GameServices.State.State.decisions.Count, 1, "no duplicate decision on re-run");
            CheckEq(GameServices.Decisions.ResolvedOption("dec_c1_hall_first_light"), "ember_reach",
                    "DecisionManager exposes the stored choice (future systems can gate on it)");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 6. manager API + conditions
        private static void TestDecisionManagerApi()
        {
            Log.Add("[6] DecisionManager: register / condition-gate / expose");
            StoryLog.Info = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(Path.Combine(Path.GetTempPath(), "crossroads_test_api_" + Guid.NewGuid().ToString("N"))),
                new RuntimeContentSource(), "FirstLocation", "hall_spawn", 0, loadExisting: false);

            var dm = GameServices.Decisions;
            Check(dm.IsRegistered(StoryContentBuilder.DecisionFirstLight), "first-light decision registered from content");
            Check(!dm.IsResolved(StoryContentBuilder.DecisionFirstLight), "not resolved yet");

            // future content: a node gated on a threshold the player hasn't reached
            var future = new DecisionNodeData
            {
                id = "dec_future_ember_gate",
                promptText = "The gate asks for proof of fire.",
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData { id = "yes", text = "Show them.",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AffinityAtLeast, key = "ember", amount = 30 } },
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "future_opened", value = "1" } } }
                }
            };
            dm.Register(future);
            Check(dm.VisibleOptions("dec_future_ember_gate").Count == 0, "gated option hidden (ember < 30)");
            GameServices.State.AddAffinity("ember", 35);
            Check(dm.VisibleOptions("dec_future_ember_gate").Count == 1, "gated option visible after threshold");
            dm.Resolve("dec_future_ember_gate", "yes");
            Check(GameServices.State.FlagIs("future_opened", "1"), "condition-gated effect applied");
            Check(dm.IsResolved("dec_future_ember_gate"), "manager checked the resolution");
            Check(dm.AllDecisions.Count == 1, "all decisions exposed (history for later systems)");

            // D2 pressure choice: timeout auto-resolve
            var timed = new DecisionNodeData
            {
                id = "dec_timed",
                promptText = "Hurry.",
                timeLimitSeconds = 5f,
                timeoutOptionIndex = 1,
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData { id = "a", text = "Chase", effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "choice", value = "chased" } } },
                    new DecisionOptionData { id = "b", text = "Shield", effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "choice", value = "shielded" } } }
                }
            };
            dm.Register(timed);
            dm.ResolveTimeout("dec_timed");
            CheckEq(GameServices.State.GetFlag("choice"), "shielded", "D2 timeout resolved to the hesitate option");

            GameServices.Shutdown(silent: true);
        }

        // ---------------------------------------------------------------- 7. save resilience
        private static void TestSaveResilience()
        {
            Log.Add("[7] SaveSystem: atomic write, corrupt-file tolerance, autosave mirror");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_save_" + Guid.NewGuid().ToString("N"));
            var paths = new TempPaths(dir);
            var json = new TestJsonAdapter();

            var save = new SaveSystem(json, paths);
            save.StartSession("probe", "FirstLocation", "hall_spawn", new GameState(), 0);
            save.Current.gameState.flags.Add(new StringEntry("k", "v"));
            var report = save.Persist(autosaveMirror: true);
            Check(report.ok, "atomic save ok");
            Check(File.Exists(Path.Combine(dir, "save_slot_0.json")), "slot file written");
            Check(File.Exists(Path.Combine(dir, "autosave.json")), "autosave mirror written");

            var save2 = new SaveSystem(json, paths);
            var loaded = save2.Load(0);
            Check(loaded != null && loaded.gameState != null, "round-trip load ok");
            CheckEq(loaded.gameState.GetFlag("k"), "v", "state survived the JSON round-trip");

            File.WriteAllText(Path.Combine(dir, "save_slot_0.json"), "{corrupt!!");
            var save3 = new SaveSystem(json, paths);
            Check(save3.Load(0) == null, "corrupt save -> null (no crash)");

            var del = save3.Delete(0);
            Check(del.ok && !save3.Exists(0), "delete clears slot + autosave");
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 8. content contracts
        private static void TestContentContracts()
        {
            Log.Add("[8] Content: the authored data satisfies the runner contracts");
            var content = StoryContentBuilder.CreateFirstLightContent();
            var enc = content.FindEncounter(StoryContentBuilder.EncounterFirstLight);
            Check(enc != null && enc.startNodeId == "start", "encounter definition ok");
            var graph = content.FindGraph(enc.graphId);
            Check(graph != null, "graph resolves by id");
            var dec = content.FindDecision("dec_c1_hall_first_light");
            Check(dec != null && dec.options.Count == 3, "decision has 3 options");
            Check(graph.Find("start") != null && graph.Find("end") != null, "graph entry/exit nodes present");
            Check(graph.Find("after_ember") != null && graph.Find("after_tide") != null && graph.Find("after_stone") != null,
                  "three condition-gated aftermath variants present");

            // unknown encounter ends cleanly (no throw)
            GameServices.Init(new TestJsonAdapter(), new TempPaths(Path.Combine(Path.GetTempPath(), "crossroads_test_unknown_" + Guid.NewGuid().ToString("N"))),
                new RuntimeContentSource(content), "FirstLocation", "hall_spawn", 0, loadExisting: false);
            GameServices.Encounters.Run("does_not_exist");
            Check(!GameServices.Encounters.IsRunning, "unknown encounter: clean no-op");
            GameServices.Shutdown(silent: true);
        }

        public static int Main(string[] args)
        {
            Console.WriteLine("CROSSROADS decision-system flow tests");
            Console.WriteLine("======================================");
            // NOTE: content param flows through IEncounterSource wrapper
            TestProximity();
            TestEmberFlow();
            TestTideFlow();
            TestStoneFlow();
            TestRestartPersistence();
            TestDecisionManagerApi();
            TestSaveResilience();
            TestContentContracts();

            Console.WriteLine("======================================");
            foreach (var line in Log) Console.WriteLine(line);
            Console.WriteLine("======================================");
            Console.WriteLine("RESULT: {0} passed, {1} failed", _passed, _failed);
            return _failed == 0 ? 0 : 1;
        }
    }
}
