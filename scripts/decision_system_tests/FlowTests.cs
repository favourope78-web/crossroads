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
using Crossroads.Gameplay;

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


        // ---------------------------------------------------------------- 9. GameStateManager + progression attributes
        private static void TestProgressionManager()
        {
            Log.Add("[9] GameStateManager: data-driven player attributes (rep/relationships/resources/skills/unlocks/flags)");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_prog_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);
            var m = GameServices.Progress;

            CheckEq(m.CurrentArea, "hall", "current area defaults to hall");
            Check(!m.AreaUnlocked("annex"), "annex locked initially");
            CheckEq(m.Reputation("choir"), 0, "reputation starts neutral");
            Check(!m.HasAbility("ember_pulse"), "no abilities yet");
            CheckEq(m.Skill("echo_attunement"), 0, "skill level 0");
            Check(!m.HasItem("echo_shard"), "no items yet");
            CheckEq(m.Bond("mara"), 0, "bond starts neutral");
            CheckEq(m.BondTier("mara"), "New", "bond tier label");

            // apply a decision, then read everything through the manager
            var dm = GameServices.Decisions;
            dm.Resolve("dec_c1_hall_first_light", "ember_reach");
            CheckEq(m.Reputation("choir"), -10, "reputation: choir -10");
            CheckEq(m.Reputation("folk"), 5, "reputation: folk +5");
            Check(m.HasAbility("ember_pulse"), "ability unlocked: Ember Pulse");
            CheckEq(m.Skill("echo_attunement"), 1, "skill level +1");
            Check(m.HasDecision("dec_c1_hall_first_light"), "decision recorded");
            CheckEq(m.DecisionOption("dec_c1_hall_first_light"), "ember_reach", "decision exposed");
            CheckEq(m.BondTier("mara"), "Warm", "bond tier reflects the stored bond (5)");
            Check(m.FlagIs("c1_hall_drive", "ember"), "story flag drives state");

            // manager writes also persist + expose
            m.UnlockArea("annex");
            m.SetCurrentArea("annex");
            Check(m.AreaUnlocked("annex"), "area unlock via manager");
            CheckEq(m.CurrentArea, "annex", "current area via manager");
            m.AddItem("echo_shard");
            m.AddReputation("wards", 8);
            m.AddSkill("echo_attunement", 1);
            Check(m.HasItem("echo_shard") && m.ItemCount("echo_shard") == 1, "item added");
            CheckEq(m.Skill("echo_attunement"), 2, "skill raw-levelled");
            CheckEq(m.Reputation("wards"), 8, "rep raw-added");

            // player card for the HUD
            var lines = m.StatusLines();
            Check(lines.Count >= 6, "player card has status lines");
            Check(lines[0].Contains("Ember 10"), "card shows affinities");
            Check(m.Describe().Contains("abilities=1"), "describe() exposes the run summary");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 10. shard encounter (annex loot)
        private static void TestShardFlow()
        {
            Log.Add("[10] Fracture Shard: per-path line + take/leave consequences + re-talk");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_shard_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            // set the drive flag as if choice A was made (data path identical to runtime)
            GameServices.State.SetFlag("c1_hall_drive", "ember");
            GameServices.State.UnlockArea("annex");
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterShard);
            Check(Harness.Lines.Count == 1 && Harness.Lines[0].text.Contains("compass stuck on your name"),
                  "shard line variant A (embeds the first decision)");
            flow.Advance();
            Check(Harness.Prompts.Count == 1 && Harness.Prompts[0].choices.Count == 2, "take/leave prompt (2 choices)");
            flow.SelectChoice("take");
            Check(GameServices.Progress.HasItem("echo_shard"), "shard added to inventory");
            CheckEq(GameServices.Progress.Echoes, 25, "echoes +25");
            CheckEq(GameServices.Progress.Skill("echo_attunement"), 1, "attunement +1");
            Check(!GameServices.State.GetEntity("echo_shard", true), "shard entity switched off (gone from the world)");
            flow.Advance();
            Check(!flow.IsRunning, "take aftermath resolved");

            // re-talk: the shard is gone - quiet-now line, no re-prompt
            flow.Run(StoryContentBuilder.EncounterShard);
            Check(Harness.Prompts.Count == 1, "no re-prompt on re-talk (still only the first prompt)");
            Check(Harness.Lines[Harness.Lines.Count - 1].text.Contains("quiet now"), "re-talk variant: took it");
            flow.Advance(); // final line shown - tap closes
            Check(!flow.IsRunning, "re-talk ended");

            // leave path in a fresh run
            Harness.Reset();
            GameServices.ResetRun();
            GameServices.State.SetFlag("c1_hall_drive", "stone");
            GameServices.State.UnlockArea("annex");
            var flow2 = GameServices.Encounters;
            flow2.Run(StoryContentBuilder.EncounterShard);
            Check(Harness.Lines[0].text.Contains("measure it"), "shard line variant C (stone)");
            flow2.Advance();
            flow2.SelectChoice("leave");
            Check(!GameServices.Progress.HasItem("echo_shard"), "leave: no item");
            flow2.Advance();
            Check(!flow2.IsRunning, "leave aftermath resolved");
            flow2.Run(StoryContentBuilder.EncounterShard);
            Check(Harness.Lines[Harness.Lines.Count - 1].text.Contains("choice stays yours"), "re-talk variant: left it");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 11. Sera (NPC behaviour/dialogue/choices per state)
        private static void TestSeraFlow()
        {
            Log.Add("[11] Sera: behaviour + dialogue + FUTURE CHOICES depend on previous decisions/state");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_sera_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            // ---- tide run: lookout option visible, banner text differs ----
            GameServices.State.SetFlag("c1_hall_drive", "tide");
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterSera);
            Check(Harness.Lines[0].text.Contains("I owe you"), "Sera's opener differs per first choice (tide)");
            flow.Advance();
            var prompt = Harness.Prompts[0];
            var ids = prompt.choices.ConvertAll(c => c.optionId);
            Check(ids.Contains("lookout"), "tide: lookout choice available");
            Check(!ids.Contains("shard_show"), "tide (no shard): shard_show choice hidden");
            Check(ids.Contains("keep_low"), "fallback choice always available");
            flow.SelectChoice("lookout");
            CheckEq(GameServices.Progress.Bond("sera"), 10, "sera bond +10");
            Check(GameServices.State.FlagIs("sera_watch", "1"), "sera_watch flag set");
            Check(GameServices.State.GetEntity("sera_lamp"), "lookout entity spawned");
            flow.Advance(); // aftermath line
            Check(Harness.Lines[Harness.Lines.Count - 1].text.Contains("Nothing crosses"), "aftermath matches the choice");
            flow.Advance();
            Check(!flow.IsRunning, "sera encounter ended");

            // ---- ember run + shard: shard_show becomes available (item-gated) ----
            Harness.Reset();
            GameServices.ResetRun();
            GameServices.State.SetFlag("c1_hall_drive", "ember");
            GameServices.State.AddItem("echo_shard");
            var flow2 = GameServices.Encounters;
            flow2.Run(StoryContentBuilder.EncounterSera);
            Check(Harness.Lines[0].text.Contains("scent"), "Sera's opener differs per first choice (ember)");
            flow2.Advance();
            var ids2 = Harness.Prompts[0].choices.ConvertAll(c => c.optionId);
            Check(ids2.Contains("shard_show"), "shard_show choice appears only AFTER the shard decision (ItemHeld)");
            Check(!ids2.Contains("lookout"), "lookout choice hidden off the tide path");
            flow2.SelectChoice("shard_show");
            CheckEq(GameServices.Progress.Bond("sera"), 5, "sera bond +5 via shard_show");
            flow2.Advance();
            Check(Harness.Lines[Harness.Lines.Count - 1].text.Contains("Archivist"), "shard aftermath line");
            flow2.Advance();
            Check(!flow2.IsRunning, "sera (ember) ended");

            // ---- stone run: only the fallback choice ----
            Harness.Reset();
            GameServices.ResetRun();
            GameServices.State.SetFlag("c1_hall_drive", "stone");
            var flow3 = GameServices.Encounters;
            flow3.Run(StoryContentBuilder.EncounterSera);
            Check(Harness.Lines[0].text.Contains("still as the wall"), "Sera's opener differs per first choice (stone)");
            flow3.Advance();
            var ids3 = Harness.Prompts[0].choices.ConvertAll(c => c.optionId);
            Check(ids3.Count == 1 && ids3[0] == "keep_low", "stone (no shard): exactly the fallback choice");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 12. area gate rules (accessible areas)
        private static void TestGateRules()
        {
            Log.Add("[12] Area gates: ability-gated access + unlock persistence + variant colors");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_gate_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            var rules = new List<GateRuleData>
            {
                new GateRuleData { conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = "ember_pulse" } }, opens = true, text = "Ember text" },
                new GateRuleData { conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = "tide_mend" } }, opens = true, text = "Tide text" },
                new GateRuleData { opens = false, text = "Sealed text" }
            };

            Check(GateRuleEvaluator.FirstMatch(rules, GameServices.State).text == "Sealed text", "no ability -> sealed fallback");
            Check(GateRuleEvaluator.FirstMatch(rules, GameServices.State).opens == false, "fallback keeps the gate shut");

            GameServices.Decisions.Resolve("dec_c1_hall_first_light", "ember_reach");
            var match = GateRuleEvaluator.FirstMatch(rules, GameServices.State);
            Check(match.opens && match.text == "Ember text", "ember ability -> gate opens with its flavor");

            GameServices.Progress.UnlockArea("annex");
            GameServices.State.SetCurrentArea("annex");
            Check(GameServices.Progress.AreaUnlocked("annex") && GameServices.Progress.CurrentArea == "annex",
                  "unlock + current area persisted in state");

            // tide path: the same rules yield the tide flavor
            GameServices.ResetRun();
            GameServices.Decisions.Resolve("dec_c1_hall_first_light", "tide_clear");
            Check(GateRuleEvaluator.FirstMatch(rules, GameServices.State).text == "Tide text", "tide ability -> tide flavor");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 13. restart: full progression persists
        private static void TestRestartProgression()
        {
            Log.Add("[13] Restart: decisions STILL affect the world (abilities/items/rep/areas/skills restored)");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_progrestart_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            // --- session 1: choose A, unlock the gate, take the shard, talk to Sera ---
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterFirstLight);
            flow.Advance(); flow.Advance(); flow.Advance();
            flow.SelectChoice("ember_reach");
            flow.Advance(); flow.Advance();

            GameServices.Progress.UnlockArea("annex");
            GameServices.State.SetCurrentArea("annex");
            flow.Run(StoryContentBuilder.EncounterShard);
            flow.Advance();
            flow.SelectChoice("take");
            flow.Advance(); flow.Advance();
            GameServices.State.SetCurrentArea("hall");
            flow.Run(StoryContentBuilder.EncounterSera);
            flow.Advance();
            flow.SelectChoice("keep_low");
            flow.Advance(); flow.Advance();

            var m = GameServices.Progress;
            Check(m.HasDecision("dec_c1_hall_first_light"), "session1: decision A recorded");
            Check(m.HasDecision("dec_east_shard"), "session1: shard decision recorded");
            Check(m.HasDecision("dec_sera_lookout"), "session1: sera decision recorded");
            Check(m.HasAbility("ember_pulse") && m.AreaUnlocked("annex"), "session1: progression applied");
            Check(m.HasItem("echo_shard"), "session1: item held");
            CheckEq(m.Reputation("choir"), -10, "session1: reputation applied");

            // --- kill the app ---
            Harness.Unsubscribe();
            GameServices.Shutdown(silent: true);
            Harness.Reset();

            // --- session 2: relaunch ---
            NewRun(dir, out content);
            var m2 = GameServices.Progress;
            CheckEq(m2.State.State.decisions.Count, 3, "session2: all 3 decisions restored");
            CheckEq(m2.DecisionOption("dec_c1_hall_first_light"), "ember_reach", "session2: choice A restored");
            Check(m2.HasAbility("ember_pulse"), "session2: ability restored (gate would open)");
            Check(m2.AreaUnlocked("annex"), "session2: area access restored");

            var rules = new List<GateRuleData>
            {
                new GateRuleData { conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = "ember_pulse" } }, opens = true },
                new GateRuleData { opens = false }
            };
            Check(GateRuleEvaluator.FirstMatch(rules, m2.State).opens, "session2: gate evaluates OPEN from the restored ability");

            Check(m2.HasItem("echo_shard"), "session2: item restored");
            CheckEq(m2.Reputation("choir"), -10, "session2: reputation restored");
            CheckEq(m2.Skill("echo_attunement"), 2, "session2: skill levels restored (choice + shard)");
            CheckEq(m2.CurrentArea, "hall", "session2: current area restored");
            Check(!GameServices.State.GetEntity("echo_shard", true), "session2: shard entity stays OFF (already taken)");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 14. post-choice notices + schema upgrade
        private static void TestNoticesAndUpgrade()
        {
            Log.Add("[14] Change notices (brief what-changed) + v1->v2 save upgrade");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_notice_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);
            Harness.Reset(); // ignore boot events

            var evt = GameServices.Decisions.Resolve("dec_c1_hall_first_light", "ember_reach");
            Check(evt.notices.Count >= 3, "notices generated for the choice");
            string joined = string.Join("|", evt.notices.ConvertAll(n => n.text));
            Check(joined.Contains("Ember +10"), "notice: affinity line");
            Check(joined.Contains("Ability: Ember Pulse"), "notice: ability unlock (data-driven name)");
            Check(joined.Contains("The Choir -10"), "notice: reputation group (data-driven name)");
            Check(joined.Contains("Area open") == false, "no area notice on this choice");

            // another run: shard take -> item + resource notices
            GameServices.ResetRun();
            GameServices.State.SetFlag("c1_hall_drive", "tide");
            GameServices.State.UnlockArea("annex");
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterShard);
            flow.Advance();
            var evt2 = GameServices.Decisions.Resolve("dec_east_shard", "take");
            string joined2 = string.Join("|", evt2.notices.ConvertAll(n => n.text));
            Check(joined2.Contains("Fracture Shard"), "notice: item name");
            Check(joined2.Contains("Echoes +25"), "notice: resource");
            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);

            // ---- schema upgrade: hand-written v1 file with legacy fields ----
            Log.Add("[15] SaveSystem upgrades v1 files in memory (progression fields default)");
            string dir2 = Path.Combine(Path.GetTempPath(), "crossroads_test_v1_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir2);
            string v1 = "{\"schemaVersion\":1,\"meta\":{\"slotName\":\"v1 save\",\"timestamp\":\"x\",\"playtimeSec\":0},"
                + "\"scene\":{\"sceneKey\":\"FirstLocation\",\"checkpointId\":\"hall_spawn\"},"
                + "\"gameState\":{\"flags\":[{\"key\":\"c1_hall_drive\",\"value\":\"stone\"}],"
                + "\"decisions\":[{\"decisionId\":\"dec_c1_hall_first_light\",\"optionId\":\"stone_still\",\"summary\":\"s\",\"resolvedAt\":\"t\"}],"
                + "\"stone\":10,\"echoBank\":15}}";
            File.WriteAllText(Path.Combine(dir2, "save_slot_0.json"), v1);
            var save = new SaveSystem(new TestJsonAdapter(), new TempPaths(dir2));
            var data = save.Load(0);
            Check(data != null, "v1 file loads (not refused)");
            Check(data.schemaVersion == SaveData.CurrentSchemaVersion, "upgraded to current schema in memory");
            CheckEq(data.gameState.DecisionOption("dec_c1_hall_first_light"), "stone_still", "legacy decision preserved");
            CheckEq(data.gameState.GetFlag("c1_hall_drive"), "stone", "legacy flag preserved");
            CheckEq(data.gameState.stone, 10, "legacy affinity preserved");
            Check(!data.gameState.HasAbility("ember_pulse") && !data.gameState.IsAreaUnlocked("annex"),
                  "new progression fields default (v1 had none)");
            Directory.Delete(dir2, true);
        }


        // ---------------------------------------------------------------- 16. NPC framework data
        private static void TestNpcContent()
        {
            Log.Add("[16] NPC framework: data-driven definitions, encounters reference, index names");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_npccontent_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            StoryContentData cd = content.Content;
            NpcDefinitionData mara = cd.FindNpc("mara");
            NpcDefinitionData sera = cd.FindNpc("sera");
            Check(mara != null && sera != null, "mara + sera definitions present");
            Check(mara.displayName == "Mara" && mara.sheetRef == "REF-02", "mara identity + CHARACTER_REFERENCE sheet ref");
            Check(mara.behaviour.personality == NpcPersonality.Friendly, "mara personality: Friendly (approaches)");
            Check(mara.states.Count == 1 && mara.states[0].conditions.Count == 1
                  && mara.states[0].conditions[0].type == ConditionType.BondAtLeast, "mara fate state is bond-gated (relationship)");
            Check(sera.behaviour.personality == NpcPersonality.Wary, "sera personality: Wary (keeps distance)");
            CheckEq(sera.states.Count, 3, "sera has one fate state per drive decision");
            Check(sera.FindInteraction("show_shard").conditions[0].type == ConditionType.ItemHeld, "sera shard interaction is item-gated");

            bool allResolve = true;
            for (int i = 0; i < cd.npcs.Count; i++)
                for (int j = 0; j < cd.npcs[i].interactions.Count; j++)
                    if (cd.FindEncounter(cd.npcs[i].interactions[j].encounterId) == null) allResolve = false;
            Check(allResolve, "every NPC interaction resolves to a registered encounter/graph");
            Check(cd.FindGraph("g_c1_hall_mara_confide").Find("confide_promise") != null, "Mara confide graph has its payoff node");
            Check(cd.FindGraph("g_c1_hall_sera_shard").Find("shard_story_tide") != null, "Sera shard graph present");
            CheckEq(GameServices.Progress.Index.NpcName("mara"), "Mara", "ProgressionIndex resolves NPC names from content");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 17. Mara reacts to the earlier decision
        private static void TestNpcMaraReaction()
        {
            Log.Add("[17] Mara: Decision A changes bond -> title, behaviour, available interactions, prompt");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_mara_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            NpcBrain brain = new NpcBrain(content.Content.FindNpc("mara"), GameServices.Progress);
            CheckEq(brain.CurrentTitle, "Mara", "base title before any decision");
            Check(brain.Profile.approach > 1.0f, "Friendly baseline: walks toward the player");
            Check(!brain.InteractionAvailable("confide"), "confide locked at bond 0");
            CheckEq(brain.PromptLabel(), "Talk to Mara", "default prompt before the decision");

            // ---- Decision A: tide (the relationship path) ----
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "tide_clear");
            brain.Reapply(); // NpcAgent calls this on state events; tests drive it directly
            CheckEq(brain.Bond, 10, "Decision A -> bond +10");
            CheckEq(brain.BondTier, "Warm", "tier changes New -> Warm");
            CheckEq(brain.CurrentTitle, "Mara \u00b7 Warm", "title carries the relationship");
            Check(brain.InteractionAvailable("confide"), "confide UNLOCKS at bond >= 8");
            CheckEq(brain.DefaultInteraction().encounterId, "c1_hall_mara_confide", "next conversation is the payoff scene");
            CheckEq(brain.PromptLabel(), "Comfort Mara", "the INTERACT button itself changes");
            Check(brain.Profile.approach < 1.45f, "Warm Mara stands closer (behaviour override)");

            // ---- other paths stay below the gate ----
            GameServices.ResetRun();
            brain = new NpcBrain(content.Content.FindNpc("mara"), GameServices.Progress);
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "stone_still");
            brain.Reapply();
            CheckEq(brain.Bond, 3, "stone path -> bond +3");
            Check(!brain.InteractionAvailable("confide"), "confide stays locked off the tide path");
            CheckEq(brain.PromptLabel(), "Talk to Mara", "prompt unchanged on the stone path");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 18. Sera: behaviour flips per drive
        private static void TestNpcSeraReaction()
        {
            Log.Add("[18] Sera: one NPC, three behaviour/dialogue variants by the earlier decision");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_sera_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            NpcBrain brain = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            Check(brain.Profile.avoid > 0f && brain.Profile.approach <= 0f, "baseline Wary: keeps distance, never approaches");
            CheckEq(brain.PromptLabel(), "Talk to Sera", "prompt = talk");

            // tide: guard drops - the SAME NPC now walks toward you
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "tide_clear");
            brain.Reapply();
            CheckEq(brain.CurrentTitle, "Sera \u00b7 Grateful", "tide -> Grateful title");
            Check(brain.Profile.approach > 0f && brain.Profile.avoid <= 0f, "behaviour FLIPS: approaches instead of backing off");
            Check(!brain.InteractionAvailable("show_shard"), "shard interaction still locked (no item yet)");

            // ember: more guarded
            GameServices.ResetRun();
            brain = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "ember_reach");
            brain.Reapply();
            CheckEq(brain.CurrentTitle, "Sera \u00b7 Watchful", "ember -> Watchful title");
            Check(brain.Profile.avoid > 3.0f, "comfort distance grows on the ember path");

            // stone: curious
            GameServices.ResetRun();
            brain = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "stone_still");
            brain.Reapply();
            CheckEq(brain.CurrentTitle, "Sera \u00b7 Intrigued", "stone -> Intrigued title");
            Check(brain.Profile.approach > 0f && brain.Profile.approach < 1.4f, "curious: approaches slowly, respectfully");

            // item-gated interaction: only after the shard is taken
            GameServices.State.SetFlag(StoryContentBuilder.DriveFlag, "tide");
            GameServices.State.UnlockArea("annex");
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterShard);
            flow.Advance();
            flow.SelectChoice("take");
            flow.Advance(); flow.Advance();
            Check(GameServices.Progress.HasItem(StoryContentBuilder.ItemShard), "shard taken");
            brain.Reapply();
            Check(brain.InteractionAvailable("show_shard"), "show_shard unlocks with the item held");
            CheckEq(brain.PromptLabel(), "Talk to Sera", "talk stays first: the interaction LIST grew, prompt kept");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 19. NpcLogic behaviour FSM
        private class FakeWorld : INpcWorld
        {
            public Point3 Pos;
            public Point3 LastMoveTarget;
            public Point3 LastFaceTarget;
            public int MoveCalls;
            public int FaceCalls;
            public Point3 NpcPosition { get { return Pos; } }
            public void NpcMoveTowards(Point3 target, float speed, float dt)
            {
                MoveCalls++;
                LastMoveTarget = target;
                Pos = Point3.MoveTowards(Pos, target, speed * dt);
            }
            public void NpcFaceTowards(Point3 target, float turnSpeed, float dt)
            {
                FaceCalls++;
                LastFaceTarget = target;
            }
        }

        private static void TestNpcLogic()
        {
            Log.Add("[19] NpcLogic: idle / walking / talking / routine / reacting (pure FSM)");
            var friendly = new NpcProfile { facesPlayer = true, reactRadius = 4.5f, approach = 1.6f, avoid = 0f, talkDistance = 2.0f, moveSpeed = 1.0f, turnSpeed = 6f };
            var wary = new NpcProfile { facesPlayer = true, reactRadius = 4.5f, approach = 0f, avoid = 2.6f, talkDistance = 2.2f, moveSpeed = 0.9f, turnSpeed = 4f };
            var idleProfile = new NpcProfile { facesPlayer = true, reactRadius = 4.5f, approach = 1.6f, avoid = 0f, talkDistance = 2.0f, moveSpeed = 1.0f, turnSpeed = 6f };
            Point3 player = new Point3(4f, 0f, 0f);
            Point3 far = new Point3(30f, 0f, 0f);

            // Friendly: approaches, stops at talking distance, then faces
            var world = new FakeWorld { Pos = new Point3(0, 0, 0) };
            var logic = new NpcLogic(null);
            logic.Tick(world, 0.05f, player, true, friendly, false);
            CheckEq(logic.State, NpcMoodState.Approach, "friendly + player near -> Approach");
            Check(world.MoveCalls > 0 && world.LastMoveTarget.x == 4f, "walks toward the player");
            int guard = 0;
            while (logic.State == NpcMoodState.Approach && guard++ < 500) logic.Tick(world, 0.05f, player, true, friendly, false);
            Check(Point3.Distance(world.Pos, player) <= 2.2f, "stops at the talk distance");
            CheckEq(logic.State, NpcMoodState.ReactFace, "then stands and faces the player");

            // Wary: steps back, growing the distance
            var world2 = new FakeWorld { Pos = new Point3(0, 0, 0) };
            var logic2 = new NpcLogic(null);
            Point3 close = new Point3(1.2f, 0f, 0f);
            logic2.Tick(world2, 0.05f, close, true, wary, false);
            CheckEq(logic2.State, NpcMoodState.Avoid, "wary + player too close -> Avoid");
            Check(world2.Pos.x < -0.001f, "moves AWAY from the player");
            Check(world2.LastMoveTarget.x < -1f, "retreat target keeps the comfort distance");

            // Wary: player at a polite distance -> no backing off, just faces
            var world3 = new FakeWorld { Pos = new Point3(0, 0, 0) };
            var logic3 = new NpcLogic(null);
            Point3 polite = new Point3(3.5f, 0f, 0f);
            logic3.Tick(world3, 0.05f, polite, true, wary, false);
            CheckEq(logic3.State, NpcMoodState.ReactFace, "respectful distance -> ReactFace, no retreat");
            Check(world3.MoveCalls == 0, "no movement when distance is acceptable");

            // Routine: walk -> dwell -> next stop -> loop
            var stops = new List<NpcStopData>
            {
                new NpcStopData { position = new Point3(3f, 0f, 0f), dwellSeconds = 0.5f },
                new NpcStopData { position = new Point3(0f, 0f, 0f), dwellSeconds = 0.5f }
            };
            var world4 = new FakeWorld { Pos = new Point3(0, 0, 0) };
            var logic4 = new NpcLogic(stops);
            logic4.Tick(world4, 0.05f, far, false, idleProfile, false);
            CheckEq(logic4.State, NpcMoodState.RoutineWalk, "routine: walks to stop 1");
            guard = 0;
            while (logic4.Arrivals == 0 && guard++ < 500) logic4.Tick(world4, 0.05f, far, false, idleProfile, false);
            Check(logic4.Arrivals == 1, "routine: reached stop 1");
            CheckEq(logic4.State, NpcMoodState.Dwell, "routine: dwells");
            logic4.Tick(world4, 0.5f, far, false, idleProfile, false);
            CheckEq(logic4.State, NpcMoodState.RoutineWalk, "routine: resumes the loop");
            guard = 0;
            while (logic4.Arrivals < 2 && guard++ < 500) logic4.Tick(world4, 0.05f, far, false, idleProfile, false);
            Check(logic4.Arrivals == 2, "routine: full loop completed");

            // Talking freezes movement (dialogue lock)
            var world5 = new FakeWorld { Pos = new Point3(0, 0, 0) };
            var logic5 = new NpcLogic(null);
            logic5.Tick(world5, 0.05f, player, true, friendly, true);
            CheckEq(logic5.State, NpcMoodState.Talk, "talking -> Talk state");
            Check(world5.MoveCalls == 0, "no walking during conversation");
            Check(world5.FaceCalls > 0, "still turns to face the player");

            // Player far away -> no reaction at all
            var world6 = new FakeWorld { Pos = new Point3(0, 0, 0) };
            var logic6 = new NpcLogic(null);
            logic6.Tick(world6, 0.05f, far, true, friendly, false);
            CheckEq(logic6.State, NpcMoodState.Idle, "player out of react radius -> Idle");
            Check(world6.MoveCalls == 0 && world6.FaceCalls == 0, "no reaction outside the react radius");
        }

        // ---------------------------------------------------------------- 20. the required sequence (two paths)
        private static void TestNpcConsequenceSequence()
        {
            Log.Add("[20] Sequence A/B: Decision A -> NPC reacts later; path B reacts differently");
            // ---- PATH A: tide ----
            string dirA = Path.Combine(Path.GetTempPath(), "crossroads_test_seqA_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource contentA;
            NewRun(dirA, out contentA);
            var flowA = GameServices.Encounters;
            flowA.Run(StoryContentBuilder.EncounterFirstLight);
            flowA.Advance(); flowA.Advance(); flowA.Advance();
            flowA.SelectChoice("tide_clear");
            flowA.Advance(); flowA.Advance();

            var maraA = new NpcBrain(contentA.Content.FindNpc("mara"), GameServices.Progress);
            maraA.Reapply();
            Check(maraA.InteractionAvailable("confide"), "A: the relationship gate opened");

            Harness.Reset();
            flowA.Run(maraA.DefaultInteraction().encounterId); // the later encounter
            int guard = 0;
            while (Harness.Ended.Count == 0 && guard++ < 20) flowA.Advance();
            string maraLines = string.Join("|", Harness.Lines.ConvertAll(l => l.text));
            Check(Harness.Ended.Count > 0, "A: confide conversation ran to the end");
            Check(maraLines.Contains("You got the twins out"), "A: Mara's later conversation REMEMBERS the tide choice");
            Check(!maraLines.Contains("You took the light like it owed you"), "A: not the ember variant");

            Harness.Reset();
            flowA.Run(StoryContentBuilder.EncounterSera);
            guard = 0;
            while (Harness.Ended.Count == 0 && guard++ < 20) flowA.Advance();
            string seraLinesA = string.Join("|", Harness.Lines.ConvertAll(l => l.text));
            Check(seraLinesA.Contains("You got my sisters out"), "A: Sera greets you as the one who saved her sisters");
            flowA.SelectChoice("keep_low"); // close Sera's embedded decision cleanly
            flowA.Advance(); flowA.Advance();

            // ---- PATH B: ember (independent save path) ----
            string dirB = Path.Combine(Path.GetTempPath(), "crossroads_test_seqB_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource contentB;
            NewRun(dirB, out contentB);
            var flowB = GameServices.Encounters;
            flowB.Run(StoryContentBuilder.EncounterFirstLight);
            flowB.Advance(); flowB.Advance(); flowB.Advance();
            flowB.SelectChoice("ember_reach");
            flowB.Advance(); flowB.Advance();

            var maraB = new NpcBrain(contentB.Content.FindNpc("mara"), GameServices.Progress);
            maraB.Reapply();
            Check(!maraB.InteractionAvailable("confide"), "B: confide stays locked (bond 5 < 8)");
            CheckEq(maraB.PromptLabel(), "Talk to Mara", "B: prompt unchanged");

            Harness.Reset();
            flowB.Run(StoryContentBuilder.EncounterSera);
            guard = 0;
            while (Harness.Ended.Count == 0 && guard++ < 20) flowB.Advance();
            string seraLinesB = string.Join("|", Harness.Lines.ConvertAll(l => l.text));
            Check(seraLinesB.Contains("The Choir has your scent now"), "B: Sera warns you instead - DIFFERENT reaction to Decision A/B");
            Check(!seraLinesB.Contains("You got my sisters out"), "B: no gratitude on the ember path");
            flowB.SelectChoice("keep_low"); // close Sera's embedded decision cleanly
            flowB.Advance(); flowB.Advance();

            // B + item -> ember shard story
            Harness.Reset();
            flowB.Run(StoryContentBuilder.EncounterSeraShard);
            guard = 0;
            while (Harness.Ended.Count == 0 && guard++ < 20) flowB.Advance();
            string shardLinesB = string.Join("|", Harness.Lines.ConvertAll(l => l.text));
            Check(shardLinesB.Contains("Bright things pick owners"), "B: shard story stays in her ember tone");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dirA, true);
            Directory.Delete(dirB, true);
        }

        // ---------------------------------------------------------------- 21. restart keeps NPC reactions
        private static void TestNpcRestart()
        {
            Log.Add("[21] Restart: NPC reactions survive (bond/title/behaviour/interactions re-applied)");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_npcrestart_" + Guid.NewGuid().ToString("N"));
            Harness.Reset();
            IEncounterSource content;
            NewRun(dir, out content);

            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterFirstLight);
            flow.Advance(); flow.Advance(); flow.Advance();
            flow.SelectChoice("tide_clear");
            flow.Advance(); flow.Advance();
            GameServices.State.UnlockArea("annex");
            flow.Run(StoryContentBuilder.EncounterShard);
            flow.Advance();
            flow.SelectChoice("take");
            flow.Advance(); flow.Advance();

            Harness.Unsubscribe();
            GameServices.Shutdown(silent: true);
            Harness.Reset();
            NewRun(dir, out content);

            var mara = new NpcBrain(content.Content.FindNpc("mara"), GameServices.Progress);
            var sera = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            CheckEq(mara.CurrentTitle, "Mara \u00b7 Warm", "restart: Mara's relationship title restored");
            Check(mara.InteractionAvailable("confide"), "restart: confide available again");
            CheckEq(mara.PromptLabel(), "Comfort Mara", "restart: prompt restored");
            Check(sera.Profile.approach > 0f, "restart: Sera still approaches (tide behaviour restored)");
            CheckEq(sera.CurrentTitle, "Sera \u00b7 Grateful", "restart: Sera's state restored");
            Check(sera.InteractionAvailable("show_shard"), "restart: item-gated interaction restored");

            // and the later conversation still plays right after restart
            Harness.Reset();
            flow = GameServices.Encounters;
            flow.Run(mara.DefaultInteraction().encounterId);
            int guard = 0;
            while (Harness.Ended.Count == 0 && guard++ < 20) flow.Advance();
            string lines = string.Join("|", Harness.Lines.ConvertAll(l => l.text));
            Check(lines.Contains("You got the twins out"), "restart: the payoff conversation is still the tide one");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
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
            TestProgressionManager();
            TestShardFlow();
            TestSeraFlow();
            TestGateRules();
            TestRestartProgression();
            TestNoticesAndUpgrade();
            TestNpcContent();
            TestNpcMaraReaction();
            TestNpcSeraReaction();
            TestNpcLogic();
            TestNpcConsequenceSequence();
            TestNpcRestart();

            Console.WriteLine("======================================");
            foreach (var line in Log) Console.WriteLine(line);
            Console.WriteLine("======================================");
            Console.WriteLine("RESULT: {0} passed, {1} failed", _passed, _failed);
            return _failed == 0 ? 0 : 1;
        }
    }
}
