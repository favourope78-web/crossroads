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
using Crossroads.UI;

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
            public static readonly List<AbilityUsedEvent> AbilityUses = new List<AbilityUsedEvent>();

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
                EventBus.Subscribe<AbilityUsedEvent>(OnAbilityUsed);
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
                EventBus.Unsubscribe<AbilityUsedEvent>(OnAbilityUsed);
            }
            private static void OnLine(DialogueLineEvent e) { Lines.Add(e); }
            private static void OnPrompt(DecisionPromptEvent e) { Prompts.Add(e); }
            private static void OnResolved(DecisionResolvedEvent e) { Resolved.Add(e); }
            private static void OnEnded(DialogueEndedEvent e) { Ended.Add(e); }
            private static void OnSave(SaveCompletedEvent e) { Saves.Add(e); }
            private static void OnAbilityUsed(AbilityUsedEvent e) { AbilityUses.Add(e); }
            public static void Reset()
            {
                Lines.Clear(); Prompts.Clear(); Resolved.Clear(); Ended.Clear(); Saves.Clear(); AbilityUses.Clear();
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


        // ---------------------------------------------------------------- 22. ability content contracts
        private static void TestAbilityContent()
        {
            Log.Add("[22] Abilities: pure-data definitions satisfy the manager contracts");
            var content = StoryContentBuilder.CreateFirstLightContent();
            var defs = content.progression.abilities;
            Check(defs.Count >= 3 && defs.Count == 3 + StoryContentBuilder.CampaignAbilityCount, "three chapter-one abilities + the campaign lines (" + defs.Count + ")");

            string[] ids = { "ember_pulse", "tide_mend", "stone_ward" };
            string[] lines = { "ember", "tide", "stone" };
            float[] cds = { 12f, 9f, 6f };
            for (int i = 0; i < ids.Length; i++)
            {
                AbilityDefinitionData def = defs[i];
                CheckEq(def.id, ids[i], "ability id " + i);
                CheckEq(def.category, AbilityCategory.Active, def.id + " is an active power");
                CheckEq(def.line, lines[i], def.id + " line family");
                Check(!string.IsNullOrEmpty(def.name) && !string.IsNullOrEmpty(def.description) && !string.IsNullOrEmpty(def.unlockHint),
                      def.id + " name/description/unlockHint populated");
                Check(!string.IsNullOrEmpty(def.vfxRef) && !string.IsNullOrEmpty(def.sfxRef), def.id + " visual/audio refs set");
                CheckEq(def.echoCostPerLevel, 10, def.id + " upgrade cost rule");
                CheckEq(def.MaxLevel, 3, def.id + " has 3 upgrade levels");
                CheckEq(def.unlockConditions.Count, 1, def.id + " exactly one unlock condition");
                CheckEq(def.unlockConditions[0].type, ConditionType.DecisionWas, def.id + " unlock keyed on the first decision");
                Check(LevelRowX(def, 1).cooldown == 12f && LevelRowX(def, 2).cooldown == 9f && LevelRowX(def, 3).cooldown == 6f,
                      def.id + " upgrades genuinely change cooldowns (12/9/6)");
                Check(LevelRowX(def, 2).radius > LevelRowX(def, 1).radius && LevelRowX(def, 3).power > LevelRowX(def, 2).power,
                      def.id + " upgrades change radius/power behaviour");
            }
            // different paths -> different abilities (mapping table)
            Check(defs[0].unlockConditions[0].value == "ember_reach" && defs[1].unlockConditions[0].value == "tide_clear"
                  && defs[2].unlockConditions[0].value == "stone_still", "decision options map 1:1 to abilities");

            // a real booted session exposes the same definitions through GameServices
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_abcontent_" + Guid.NewGuid().ToString("N"));
            IEncounterSource booted;
            NewRun(dir, out booted);
            Check(GameServices.Abilities != null, "AbilityManager bootstrapped with GameServices");
            CheckEq(GameServices.Abilities.Definitions.Count, content.progression.abilities.Count, "manager sees the authored definitions");
            Check(GameServices.Abilities.Find("ember_pulse") != null && GameServices.Abilities.Find("nothing") == null,
                  "Find resolves ids");
            CheckEq(GameServices.Abilities.Level("ember_pulse"), 0, "level 0 while locked");
            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        private static AbilityLevelData LevelRowX(AbilityDefinitionData def, int level)
        {
            return def.LevelRow(level);
        }

        // ---------------------------------------------------------------- 23. decision -> ability paths
        private static void TestAbilityUnlockPaths()
        {
            Log.Add("[23] Decision->ability mapping: each path unlocks exactly its own ability");
            string[] options = { "ember_reach", "tide_clear", "stone_still" };
            string[] owned = { "ember_pulse", "tide_mend", "stone_ward" };
            for (int i = 0; i < options.Length; i++)
            {
                string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_abpath_" + Guid.NewGuid().ToString("N"));
                IEncounterSource content;
                NewRun(dir, out content);
                GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, options[i]);

                Check(GameServices.Progress.HasAbility(owned[i]), "option " + options[i] + " -> " + owned[i] + " unlocked");
                CheckEq(GameServices.Abilities.AccessState(owned[i]), AbilityAccessState.Unlocked, owned[i] + " usable");
                CheckEq(GameServices.Abilities.Level(owned[i]), 1, owned[i] + " starts at level 1");
                Check(!GameServices.Progress.IsAbilityBlocked(owned[i]), owned[i] + " not blocked");

                for (int j = 0; j < owned.Length; j++)
                {
                    if (j == i) continue;
                    Check(!GameServices.Progress.HasAbility(owned[j]), owned[j] + " stays locked on the " + options[i] + " path");
                    CheckEq(GameServices.Abilities.AccessState(owned[j]), AbilityAccessState.Locked, owned[j] + " access = Locked");
                }

                // sheet rows mirror the same verdicts (UI model = manager + data, no hardcoded ids)
                var rows = AbilitySheetModel.Build(GameServices.Abilities);
                CheckEq(rows.Count, GameServices.Abilities.Definitions.Count, "sheet lists every known ability");
                bool sawOwner = false, sawLocked = false;
                for (int r = 0; r < rows.Count; r++)
                {
                    if (rows[r].abilityId == owned[i])
                    {
                        sawOwner = true;
                        Check(rows[r].canActivateNow, "owner row can activate");
                        Check(rows[r].stateText.Contains("Lv 1"), "owner row shows level");
                    }
                    else
                    {
                        sawLocked = true;
                        Check(rows[r].stateText.StartsWith("LOCKED"), rows[r].abilityId + " row shows the unlock hint");
                        Check(!rows[r].canActivateNow, "locked row cannot activate");
                    }
                }
                Check(sawOwner && sawLocked, "sheet shows both the owned and the locked abilities");

                GameServices.Shutdown(silent: true);
                Directory.Delete(dir, true);
            }
        }

        // ---------------------------------------------------------------- 24. activation + cooldown state machine
        private static void TestAbilityActivationAndCooldown()
        {
            Log.Add("[24] Activation: event payload, cooldown state machine, access gates");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_abact_" + Guid.NewGuid().ToString("N"));
            IEncounterSource content;
            NewRun(dir, out content);
            float now = 0f;
            GameServices.Abilities.Now = () => now;
            Harness.Reset();

            CheckEq(GameServices.Abilities.Activate("ember_pulse"), AbilityActivation.Locked, "activate locked -> response");
            CheckEq(GameServices.Abilities.Activate("no_such_ability"), AbilityActivation.Unknown, "unknown id -> response");
            CheckEq(GameServices.Abilities.Activate(""), AbilityActivation.Unknown, "empty id -> response");

            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "ember_reach");
            CheckEq(GameServices.Abilities.Activate("ember_pulse"), AbilityActivation.Ok, "first activation succeeds");
            CheckEq(Harness.AbilityUses.Count, 1, "AbilityUsedEvent fired once");
            if (Harness.AbilityUses.Count == 1)
            {
                AbilityUsedEvent u0 = Harness.AbilityUses[0];
                CheckEq(u0.abilityId, "ember_pulse", "event carries the id");
                CheckEq(u0.level, 1, "event carries the CURRENT level");
                CheckEq(u0.cooldown, 12f, "event carries the level row's cooldown");
                CheckEq(u0.power, 1f, "event carries the level row's power");
                CheckEq(u0.radius, 3.5f, "event carries the level row's radius");
                CheckEq(u0.duration, 1f, "event carries the level row's duration");
            }
            CheckEq(GameServices.Abilities.Activate("ember_pulse"), AbilityActivation.CoolingDown, "second use blocked by cooldown");
            Check(GameServices.Abilities.OnCooldown("ember_pulse"), "ability reports cooldown");
            Check(GameServices.Abilities.CooldownRemaining("ember_pulse") > 11.9f && GameServices.Abilities.CooldownRemaining("ember_pulse") <= 12f,
                  "remaining ~= full cooldown");

            now += 12f;
            CheckEq(GameServices.Abilities.CooldownRemaining("ember_pulse"), 0f, "cooldown expires on the clock");
            Check(!GameServices.Abilities.OnCooldown("ember_pulse"), "no longer on cooldown");
            CheckEq(GameServices.Abilities.Activate("ember_pulse"), AbilityActivation.Ok, "usable again after cooldown");

            // energy cost gate (crafted definition with a real cost; authored prototype waves cost)
            var costly = new AbilityDefinitionData
            {
                id = "test_cost", name = "Test Cost", line = "ember", category = AbilityCategory.Active,
                levels = new List<AbilityLevelData>
                {
                    new AbilityLevelData { level = 1, cooldown = 1f, power = 1f, radius = 1f, duration = 1f, energyCost = 5 }
                }
            };
            var freshState = new StateMutator(new GameState());
            freshState.UnlockAbility("test_cost");
            freshState.SetAbilityLevel("test_cost", 1);
            var mgr = new AbilityManager(new List<AbilityDefinitionData> { costly },
                new GameStateManager(freshState, content));
            mgr.Now = () => 0f;
            CheckEq(mgr.Activate("test_cost"), AbilityActivation.NotEnoughEnergy, "cost gate: not enough echoes");
            freshState.GrantEchoes(5);
            CheckEq(mgr.Activate("test_cost"), AbilityActivation.Ok, "cost gate: paid -> use");
            CheckEq(mgr.Activate("test_cost"), AbilityActivation.CoolingDown, "cost gate: still on cooldown after pay");

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 25. upgrades (shrine decision)
        private static void TestAbilityUpgrade()
        {
            Log.Add("[25] Upgrade: shrine decision raises levels, gates repeat buys, changes behaviour");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_abup_" + Guid.NewGuid().ToString("N"));
            IEncounterSource content;
            NewRun(dir, out content);

            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "ember_reach");
            CheckEq(GameServices.Progress.Echoes, 15, "echo income from the first decision");

            // shrine options are condition-gated per ability + cost + max level
            var first = GameServices.Decisions.VisibleOptions(StoryContentBuilder.DecisionShrine);
            bool sawOwn = false, sawLeave = false, sawOther = false;
            for (int i = 0; i < first.Count; i++)
            {
                if (first[i].id == "deep_ember") sawOwn = true;
                if (first[i].id == "leave") sawLeave = true;
                if (first[i].id == "deep_tide" || first[i].id == "deep_stone") sawOther = true;
            }
            Check(sawOwn && sawLeave, "shrine offers the OWNED ability's upgrade + leave");
            Check(!sawOther, "shrine never offers upgrades for abilities you do not own");

            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionShrine, "deep_ember");
            CheckEq(GameServices.Abilities.Level("ember_pulse"), 2, "upgrade -> level 2");
            CheckEq(GameServices.Progress.Echoes, 5, "upgrade costs 10 echoes");
            CheckEq(GameServices.Progress.Skill("echo_attunement"), 2, "upgrade deepens the attunement skill");
            CheckEq(GameServices.Abilities.CurrentRow("ember_pulse").cooldown, 9f, "behaviour changed: cooldown 12 -> 9");
            CheckEq(GameServices.Abilities.CurrentRow("ember_pulse").radius, 4.5f, "behaviour changed: radius 3.5 -> 4.5");
            Check(GameServices.Abilities.CanUpgrade("ember_pulse"), "can upgrade again");
            CheckEq(GameServices.Abilities.AccessState("ember_pulse"), AbilityAccessState.Unlocked, "still usable after upgrade");

            // repeat upgrade is gated by echoes (5 < 10)
            var second = GameServices.Decisions.VisibleOptions(StoryContentBuilder.DecisionShrine);
            bool sawDeep = false;
            for (int i = 0; i < second.Count; i++) if (second[i].id == "deep_ember") sawDeep = true;
            Check(!sawDeep, "upgrade option hidden without the 10 echoes");

            GameServices.State.GrantEchoes(30);
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionShrine, "deep_ember");
            CheckEq(GameServices.Abilities.Level("ember_pulse"), 3, "second upgrade -> level 3 (max)");
            CheckEq(GameServices.Abilities.CurrentRow("ember_pulse").cooldown, 6f, "max level cooldown 6s");
            Check(!GameServices.Abilities.CanUpgrade("ember_pulse"), "max level: no further upgrade");
            var third = GameServices.Decisions.VisibleOptions(StoryContentBuilder.DecisionShrine);
            bool sawDeepAtMax = false;
            for (int i = 0; i < third.Count; i++) if (third[i].id == "deep_ember") sawDeepAtMax = true;
            Check(!sawDeepAtMax, "upgrade option hidden at max level (AbilityLevelBelow gate)");

            // sheet model reflects the level + ready state
            var rows = AbilitySheetModel.Build(GameServices.Abilities);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].abilityId == "ember_pulse")
                {
                    Check(rows[i].stateText.Contains("Lv 3"), "sheet shows the upgraded level");
                    Check(rows[i].stateText.Contains("MAX"), "sheet marks the max level");
                    CheckEq(rows[i].maxLevel, 3, "sheet carries the max level");
                }
            }

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 26. blocking + NPC reactions
        private static void TestAbilityBlockAndNpcReaction()
        {
            Log.Add("[26] Blocking: a future decision seals an ability (persisted) and NPCs react");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_abblock_" + Guid.NewGuid().ToString("N"));
            IEncounterSource content;
            NewRun(dir, out content);

            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "ember_reach");
            NpcBrain sera = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            sera.Reapply();
            CheckEq(sera.CurrentTitle, "Sera \u00b7 Watchful", "ember path: Sera watchful (drive state)");

            // deepen the bind once -> attunement 2 -> Sera notices the power level
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionShrine, "deep_ember");
            sera.Reapply();
            CheckEq(sera.CurrentTitle, "Sera \u00b7 Attuned", "NPC detects the deepened bind (ability level -> state)");

            // seal: the harsher consequence
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionShrine, "seal_ember");
            CheckEq(GameServices.Abilities.AccessState("ember_pulse"), AbilityAccessState.Blocked, "sealed after the choice");
            CheckEq(GameServices.Abilities.Activate("ember_pulse"), AbilityActivation.Blocked, "activation refused while sealed");
            Check(GameServices.Progress.IsAbilityBlocked("ember_pulse"), "blocked persisted in state");
            Check(!GameServices.Progress.HasAbility("ember_pulse"), "sealed ability leaves the owned list");
            CheckEq(GameServices.Progress.AbilityLevel("ember_pulse"), 0, "sealed ability loses its level");
            sera.Reapply();
            CheckEq(sera.CurrentTitle, "Sera \u00b7 Warded", "NPC detects the sealed echo (new data-driven state)");

            // a later decision can still restore it (unlock wins over block)
            GameServices.State.UnlockAbility("ember_pulse");
            Check(!GameServices.Progress.IsAbilityBlocked("ember_pulse"), "re-unlock clears the seal");
            GameServices.State.SetAbilityLevel("ember_pulse", 2);
            GameServices.Abilities.Now = () => 0f;
            CheckEq(GameServices.Abilities.Activate("ember_pulse"), AbilityActivation.Ok, "restored ability works again");
            CheckEq(GameServices.Abilities.Level("ember_pulse"), 2, "restored level kept");

            // sheet states: locked hint / sealed / ready
            var rows = AbilitySheetModel.Build(GameServices.Abilities);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].abilityId == "tide_mend")
                    Check(rows[i].stateText.StartsWith("LOCKED") && rows[i].stateText.Contains("Put others first"),
                          "locked row carries the data-driven unlock hint");
            }

            GameServices.Shutdown(silent: true);
            Directory.Delete(dir, true);
        }

        // ---------------------------------------------------------------- 27. persistence across restarts
        private static void TestAbilityPersistence()
        {
            Log.Add("[27] Persistence: unlock + level + block survive a full restart; cooldown does not");
            string dir = Path.Combine(Path.GetTempPath(), "crossroads_test_abpersist_" + Guid.NewGuid().ToString("N"));

            IEncounterSource content;
            NewRun(dir, out content);
            float now = 0f;
            GameServices.Abilities.Now = () => now;
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionFirstLight, "ember_reach");
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionShrine, "deep_ember"); // Lv 2
            CheckEq(GameServices.Abilities.Activate("ember_pulse"), AbilityActivation.Ok, "activate before saving");
            now += 2f;
            Check(GameServices.Abilities.CooldownRemaining("ember_pulse") > 0f, "cooldown running before save");
            GameServices.PersistNow();
            GameServices.Shutdown(silent: true);

            NewRun(dir, out content);
            Check(GameServices.Progress.HasAbility("ember_pulse"), "ability still owned after restart");
            CheckEq(GameServices.Progress.AbilityLevel("ember_pulse"), 2, "level restored after restart");
            CheckEq(GameServices.Abilities.Level("ember_pulse"), 2, "manager reads the restored level");
            CheckEq(GameServices.Abilities.AccessState("ember_pulse"), AbilityAccessState.Unlocked, "usable after restart");
            Check(!GameServices.Progress.HasAbility("tide_mend"), "other path's ability not granted");
            CheckEq(GameServices.Abilities.CooldownRemaining("ember_pulse"), 0f, "cooldown is session-only (not persisted)");

            // seal persists too
            GameServices.Decisions.Resolve(StoryContentBuilder.DecisionShrine, "seal_ember");
            GameServices.PersistNow();
            GameServices.Shutdown(silent: true);

            NewRun(dir, out content);
            Check(GameServices.Progress.IsAbilityBlocked("ember_pulse"), "seal state restored after restart");
            CheckEq(GameServices.Abilities.AccessState("ember_pulse"), AbilityAccessState.Blocked, "access restored after restart");
            Check(!GameServices.Progress.HasAbility("ember_pulse"), "owned list restored without the sealed ability");
            CheckEq(GameServices.Abilities.Level("ember_pulse"), 0, "level cleared by the seal is restored as cleared");
            GameServices.Shutdown(silent: true);

            // ---- schema: a v2 file lacking the power-system collections loads safely (in-memory v3 migration)
            string dir2 = Path.Combine(Path.GetTempPath(), "crossroads_test_abv2_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir2);
            string v2 = "{\"schemaVersion\":2,\"meta\":{\"slotName\":\"v2 save\",\"timestamp\":\"x\",\"playtimeSec\":0},"
                + "\"scene\":{\"sceneKey\":\"FirstLocation\",\"checkpointId\":\"hall_spawn\"},"
                + "\"gameState\":{\"flags\":[{\"key\":\"c1_hall_drive\",\"value\":\"ember\"}],"
                + "\"abilities\":[{\"key\":\"ember_pulse\",\"value\":\"1\"}],"
                + "\"skills\":[{\"key\":\"echo_attunement\",\"value\":1}],\"echoBank\":5}}";
            File.WriteAllText(Path.Combine(dir2, "save_slot_0.json"), v2);
            var save = new SaveSystem(new TestJsonAdapter(), new TempPaths(dir2));
            var data = save.Load(0);
            Check(data != null, "v2 file loads (not refused)");
            Check(data.schemaVersion == SaveData.CurrentSchemaVersion, "v2 upgraded to current schema in memory");
            Check(data.gameState.HasAbility("ember_pulse"), "v2 ability preserved");
            Check(data.gameState.blockedAbilities != null && data.gameState.abilityLevels != null,
                  "missing power-system collections normalized (never null)");
            CheckEq(data.gameState.blockedAbilities.Count, 0, "no phantom blocks after migration");
            CheckEq(data.gameState.GetAbilityLevel("ember_pulse", 0), 0, "v2 file has no level -> defaults to 0");

            Directory.Delete(dir2, true);
            Directory.Delete(dir, true);
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
            Check(mara.states.Count == 2 && mara.states[1].conditions.Count == 1
                  && mara.states[1].conditions[0].type == ConditionType.BondAtLeast, "mara bond fate state present (relationship)");
            Check(mara.states[0].conditions.Count == 1
                  && mara.states[0].conditions[0].type == ConditionType.ObjectiveCompleted, "mara objective-driven fate state present (reacts to missions)");
            Check(mara.FindInteraction("report") != null
                  && mara.FindInteraction("report").conditions[0].type == ConditionType.ObjectiveCompleted,
                  "mara 'report' interaction is objective-gated");
            Check(sera.behaviour.personality == NpcPersonality.Wary, "sera personality: Wary (keeps distance)");
            CheckEq(sera.states.Count, 8, "sera has drive + ability + objective + combat-reaction states");
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
            // campaign phase: after the first-light decision sera's DEFAULT became the
            // branch-reactive echo talk (prepended, condition-gated); plain talk stays available
            CheckEq(brain.PromptLabel(), "Talk about what happened",
                  "post-decision default is the branch-reactive echo talk");
            Check(brain.InteractionAvailable("talk"), "plain talk still available in the list");

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
            // the tide+shard flow reaches attunement 2, so the ability-reaction state owns the title
            CheckEq(sera.CurrentTitle, "Sera \u00b7 Attuned", "restart: ability-derived state restored from saved skills");
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
            TestAbilityContent();
            TestAbilityUnlockPaths();
            TestAbilityActivationAndCooldown();
            TestAbilityUpgrade();
            TestAbilityBlockAndNpcReaction();
            TestAbilityPersistence();

            // world state + objective/mission system (Gameplay/World)
            int worldPassed, worldFailed;
            WorldTests.RunAll(out worldPassed, out worldFailed);
            _passed += worldPassed;
            _failed += worldFailed;

            // core action & combat system (Gameplay/Combat)
            int combatPassed, combatFailed;
            CombatTests.RunAll(out combatPassed, out combatFailed);
            _passed += combatPassed;
            _failed += combatFailed;

            // mobile player experience (Gameplay/Input + touch rig + settings)
            int mobilePassed, mobileFailed;
            MobileExperienceTests.RunAll(out mobilePassed, out mobileFailed);
            _passed += mobilePassed;
            _failed += mobileFailed;

            // core branching campaign (Gameplay/Campaign)
            int campaignPassed, campaignFailed;
            CampaignTests.RunAll(out campaignPassed, out campaignFailed);
            _passed += campaignPassed;
            _failed += campaignFailed;

            // world expansion (Gameplay/Locations)
            int locationPassed, locationFailed;
            LocationTests.RunAll(out locationPassed, out locationFailed);
            _passed += locationPassed;
            _failed += locationFailed;

            Console.WriteLine("======================================");
            foreach (var line in Log) Console.WriteLine(line);
            foreach (var line in WorldTests.GetLog()) Console.WriteLine(line);
            foreach (var line in CombatTests.GetLog()) Console.WriteLine(line);
            foreach (var line in MobileExperienceTests.GetLog()) Console.WriteLine(line);
            foreach (var line in CampaignTests.GetLog()) Console.WriteLine(line);
            foreach (var line in LocationTests.GetLog()) Console.WriteLine(line);
            Console.WriteLine("======================================");
            Console.WriteLine("RESULT: {0} passed, {1} failed", _passed, _failed);
            return _failed == 0 ? 0 : 1;
        }
    }
}
