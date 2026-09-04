// ============================================================================
// CROSSROADS headless tests of the WORLD STATE + OBJECTIVE/MISSION system:
//   objective unlocking by decision · completion · failure + recovery ·
//   ability-gated interactions · NPC reactions to objectives · world-state
//   changes (areas/objects/npc locations/interaction unlocks) · save/load
//   persistence · different decision paths -> different worlds.
// Runs the exact same code paths the game uses (WorldServices over
// GameServices: StateMutator, EventBus, EffectApplier, ObjectiveManager,
// WorldStateSystem, WorldActionInteractable, NpcBrain, EncounterFlow).
// Invoke from FlowTests.Main (single process, shared counters).
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Gameplay;

namespace Crossroads.Tests
{
    public static class WorldTests
    {
        private static int _passed, _failed;
        private static readonly List<string> Log = new List<string>();

        // captured world/objective events (proof the systems are event-driven)
        private static readonly List<ObjectiveChangedEvent> ObjectiveEvents = new List<ObjectiveChangedEvent>();
        private static readonly List<NpcRelocatedEvent> Relocations = new List<NpcRelocatedEvent>();
        private static readonly List<InteractionUnlockedEvent> InteractionUnlocks = new List<InteractionUnlockedEvent>();
        private static readonly List<VarChangedEvent> VarEvents = new List<VarChangedEvent>();
        private static readonly List<NoticeRequestEvent> Notices = new List<NoticeRequestEvent>();

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

        // ---------------------------------------------------------------- harness
        private static bool _subscribed;

        private static void NewRun(string dir, out IEncounterSource content)
        {
            content = new RuntimeContentSource();
            StoryLog.Info = Console.WriteLine;
            StoryLog.Warn = Console.WriteLine;
            StoryLog.Error = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(dir), content,
                "FirstLocation", "hall_spawn", 0, loadExisting: true);
            WorldServices.Init(); // exactly what StoryModeBootstrap does after GameServices

            if (!_subscribed)
            {
                _subscribed = true;
                EventBus.Subscribe<ObjectiveChangedEvent>(e => ObjectiveEvents.Add(e));
                EventBus.Subscribe<NpcRelocatedEvent>(e => Relocations.Add(e));
                EventBus.Subscribe<InteractionUnlockedEvent>(e => InteractionUnlocks.Add(e));
                EventBus.Subscribe<VarChangedEvent>(e => VarEvents.Add(e));
                EventBus.Subscribe<NoticeRequestEvent>(e => Notices.Add(e));
            }
        }

        private static void ClearCaptures()
        {
            ObjectiveEvents.Clear(); Relocations.Clear(); InteractionUnlocks.Clear(); VarEvents.Clear(); Notices.Clear();
        }

        private static void Shutdown()
        {
            WorldServices.Shutdown(silent: true);
            GameServices.Shutdown(silent: true);
        }

        private static string TempDir(string tag)
        {
            return Path.Combine(Path.GetTempPath(), "crossroads_world_" + tag + "_" + Guid.NewGuid().ToString("N"));
        }

        /// <summary>Resolves the First Light encounter and picks an option (the branch).</summary>
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

        /// <summary>Builds the scene-side world action headlessly via reflection (same class
        /// the scene components use) so tests cover the real gating logic.</summary>
        private static WorldActionInteractable MakeAction(string name,
            List<DecisionConditionData> conditions, List<DecisionEffectData> effects,
            string useCountVar, int maxUses, string consumeEntityKey)
        {
            var action = new WorldActionInteractable();
            action.name = name;
            action.enabled = true;
            typeof(WorldActionInteractable).GetField("conditions", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(action, conditions ?? new List<DecisionConditionData>());
            typeof(WorldActionInteractable).GetField("perUseEffects", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(action, effects ?? new List<DecisionEffectData>());
            typeof(WorldActionInteractable).GetField("useCountVar", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(action, useCountVar ?? "");
            typeof(WorldActionInteractable).GetField("maxUses", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(action, maxUses);
            typeof(WorldActionInteractable).GetField("consumeEntityKey", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(action, consumeEntityKey ?? "");
            return action;
        }

        private static DecisionConditionData Cond(ConditionType type, string key, string value = "", int amount = 0)
        {
            return new DecisionConditionData { type = type, key = key, value = value, amount = amount };
        }

        private static DecisionEffectData Eff(EffectType type, string key, string value = "", int amount = 0)
        {
            return new DecisionEffectData { type = type, key = key, value = value, amount = amount };
        }

        // ================================================================ 30. objective content contracts
        private static void TestObjectiveContent()
        {
            Log.Add("[30] Objectives: data-driven definitions satisfy the manager contracts");
            var content = StoryContentBuilder.CreateFirstLightContent();
            CheckEq(content.objectives.Count, 6, "six authored objectives (3 paths + 3 follow-ups)");
            Check(content.FindObjective(StoryContentBuilder.ObjectiveEmberBeacon) != null, "ember path objective present");
            Check(content.FindObjective(StoryContentBuilder.ObjectiveTideKeepsake) != null, "tide path objective present");
            Check(content.FindObjective(StoryContentBuilder.ObjectiveStoneBarricade) != null, "stone path objective present");

            foreach (var o in content.objectives)
            {
                Check(!string.IsNullOrEmpty(o.id) && !string.IsNullOrEmpty(o.title) && !string.IsNullOrEmpty(o.description),
                      o.id + ": id/title/description populated");
                Check(o.offerConditions.Count > 0, o.id + ": has offer requirements");
                Check(o.completeConditions.Count > 0, o.id + ": has completion conditions");
            }

            // follow-up chains resolve to registered objectives
            bool chainsResolve = true;
            for (int i = 0; i < content.objectives.Count; i++)
                for (int f = 0; f < content.objectives[i].followUps.Count; f++)
                    if (content.FindObjective(content.objectives[i].followUps[f]) == null) chainsResolve = false;
            Check(chainsResolve, "every follow-up resolves to a registered objective");

            // each path objective offers on a DIFFERENT first decision option
            CheckEq(content.FindObjective(StoryContentBuilder.ObjectiveEmberBeacon).offerConditions[0].value, "ember_reach",
                  "ember objective reacts to Decision A (ember_reach)");
            CheckEq(content.FindObjective(StoryContentBuilder.ObjectiveTideKeepsake).offerConditions[0].value, "tide_clear",
                  "tide objective reacts to Decision B (tide_clear)");
            CheckEq(content.FindObjective(StoryContentBuilder.ObjectiveStoneBarricade).offerConditions[0].value, "stone_still",
                  "stone objective reacts to Decision C (stone_still)");

            // failure only where appropriate: exactly the stone crisis is failable
            Check(content.FindObjective(StoryContentBuilder.ObjectiveStoneBarricade).failConditions.Count == 1,
                  "stone crisis objective has failure conditions");
            Check(content.FindObjective(StoryContentBuilder.ObjectiveEmberBeacon).failConditions.Count == 0
                  && content.FindObjective(StoryContentBuilder.ObjectiveTideKeepsake).failConditions.Count == 0,
                  "ember/tide objectives are unfailable");

            CheckEq(content.worldInteractions.Count, 7, "seven world interaction rows (incl. ability-gated)");

            string dir = TempDir("content");
            IEncounterSource src;
            NewRun(dir, out src);
            Check(WorldServices.IsInitialized && WorldServices.Objectives.RegisteredCount == 6,
                  "booted manager sees the six authored objectives");
            Check(WorldServices.World != null, "world-state system bootstrapped");
            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 31. objective unlocking by decision
        private static void TestObjectiveUnlocking()
        {
            Log.Add("[31] Unlocking: Decision A -> Objective A available+active; other paths stay hidden");
            string dir = TempDir("unlock");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);

            // fresh run: everything hidden, no objective events fired at boot
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Hidden,
                  "fresh run: ember objective hidden");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideKeepsake), ObjectivePhase.Hidden,
                  "fresh run: tide objective hidden");

            PlayFirstLight("ember_reach");

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Active,
                  "Decision A (ember) -> Objective A auto-tracked");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideKeepsake), ObjectivePhase.Hidden,
                  "Decision A does NOT unlock Objective B (tide)");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveStoneBarricade), ObjectivePhase.Hidden,
                  "Decision A does NOT unlock Objective C (stone)");
            CheckEq(WorldServices.Objectives.ActiveObjectives().Count, 1, "exactly one tracked objective");

            bool sawOffer = false, sawActivate = false;
            for (int i = 0; i < ObjectiveEvents.Count; i++)
            {
                if (ObjectiveEvents[i].objectiveId == StoryContentBuilder.ObjectiveEmberBeacon)
                {
                    if (ObjectiveEvents[i].phase == ObjectivePhase.Available) sawOffer = true;
                    if (ObjectiveEvents[i].phase == ObjectivePhase.Active) sawActivate = true;
                }
            }
            Check(sawOffer && sawActivate, "ObjectiveChangedEvent fired for offer + activation (event-driven)");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 32. ember path: completion + consequences + follow-up
        private static void TestEmberCompletion()
        {
            Log.Add("[32] Ember: complete the beacon objective -> world changes + follow-up chain");
            string dir = TempDir("ember");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);

            PlayFirstLight("ember_reach");
            ClearCaptures();

            // the beacon world action (same conditions the scene object carries):
            var beacon = MakeAction("ChoirBeacon",
                new List<DecisionConditionData> { Cond(ConditionType.AbilityOwned, "ember_pulse") },
                new List<DecisionEffectData> { Eff(EffectType.SetFlag, "beacon_silenced", "1") },
                "beacon_uses", 1, "choir_beacon");

            Check(beacon.ConditionsPass, "ember player passes the ability-gated beacon conditions");
            beacon.OnInteract(null);

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Completed,
                  "beacon used -> objective completed");

            // world-state consequences (all through EffectApplier - the single write path)
            CheckEq(GameServices.State.GetWorldState("annex"), "quiet", "world variant: annex remembers 'quiet'");
            CheckEq(GameServices.State.GetEntity("ember_cache", false), true, "ember cache spawned");
            CheckEq(GameServices.State.GetReputation("choir"), -20, "choir standing dropped (-10 D1, -10 objective)");
            CheckEq(GameServices.State.GetBond("sera"), 4, "sera bond rose from the objective consequence");
            CheckEq(GameServices.State.GetNpcLocation("sera", ""), "annex_gate", "sera relocated (persisted npc location)");
            Check(Relocations.Count == 1 && Relocations[0].npcId == "sera", "NpcRelocatedEvent fired for sera");

            // follow-up offered + auto-tracked
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberCache), ObjectivePhase.Active,
                  "follow-up (Claim the Ember Cache) unlocked by completion");

            // complete the follow-up
            var cache = MakeAction("EmberCache",
                new List<DecisionConditionData> { Cond(ConditionType.FlagIs, "beacon_silenced", "1") },
                new List<DecisionEffectData>
                {
                    Eff(EffectType.SetFlag, "ember_cache_opened", "1"),
                    Eff(EffectType.AddItem, "ember_core")
                },
                "cache_uses", 1, "ember_cache");
            cache.OnInteract(null);
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberCache), ObjectivePhase.Completed,
                  "follow-up completed through the cache world action");
            Check(GameServices.State.HasItem("ember_core"), "ember core item granted");

            // Sera's fate state changed because of the OBJECTIVE (NPC reacts to missions)
            var sera = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            CheckEq(sera.CurrentTitle, "Sera · Vanguard", "sera title reacts to the completed objective");
            Check(sera.Profile.approach > 0f, "sera approaches now (objective changed her behaviour)");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 33. ability-gated interaction
        private static void TestAbilityGating()
        {
            Log.Add("[33] Abilities gate the world: the beacon refuses hands without ember");
            string dir = TempDir("ability");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);

            // tide player: ability check fails -> interaction unavailable
            PlayFirstLight("tide_clear");
            var beacon = MakeAction("ChoirBeacon",
                new List<DecisionConditionData> { Cond(ConditionType.AbilityOwned, "ember_pulse") },
                new List<DecisionEffectData> { Eff(EffectType.SetFlag, "beacon_silenced", "1") },
                "beacon_uses", 1, "choir_beacon");
            Check(!beacon.ConditionsPass, "tide player fails the ember-gated conditions");
            beacon.OnInteract(null);
            Check(!GameServices.State.HasFlag("beacon_silenced"), "no effect applied (interaction truly unavailable)");
            Check(Notices.Count > 0, "locked feedback notice shown");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Hidden,
                  "ember objective never offered on the tide path");
            Check(!WorldServices.World.InteractionUnlocked("choir_beacon_channel"),
                  "world registry: beacon channel NOT unlocked without the ability");

            // ember player who SEALED the echo (ability blocked): interaction dies mid-run
            ClearCaptures();
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterShrine);
            int guard = 0;
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            flow.SelectChoice("seal_ember"); // BlockAbility(ember_pulse) via the shrine decision
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();

            Check(!beacon.ConditionsPass, "sealed ember player: conditions no longer pass (blocked ability)");
            beacon.OnInteract(null);
            Check(!GameServices.State.HasFlag("beacon_silenced"), "sealed player cannot complete the beacon");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 34. tide path: checklist progress + NPC-driven completion
        private static void TestTideChecklist()
        {
            Log.Add("[34] Tide: two-step checklist, item delivery, objective completes through dialogue");
            string dir = TempDir("tide");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);

            PlayFirstLight("tide_clear");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideKeepsake), ObjectivePhase.Active,
                  "Decision B -> Objective B tracked");

            ObjectiveView view = WorldServices.Objectives.ViewOf(StoryContentBuilder.ObjectiveTideKeepsake);
            CheckEq(view.steps.Count, 2, "checklist has two steps");
            Check(view.steps[0].StartsWith("[ ]") && view.steps[1].StartsWith("[ ]"), "both steps start unchecked");

            // step 1: find the keepsake (crate world action grants item + flag)
            var crate = MakeAction("KeepsakeCrate",
                new List<DecisionConditionData> { Cond(ConditionType.FlagIs, StoryContentBuilder.DriveFlag, "tide") },
                new List<DecisionEffectData>
                {
                    Eff(EffectType.AddItem, "twins_keepsake"),
                    Eff(EffectType.SetFlag, "keepsake_found", "1")
                },
                "crate_uses", 1, "keepsake_crate");
            crate.OnInteract(null);

            view = WorldServices.Objectives.ViewOf(StoryContentBuilder.ObjectiveTideKeepsake);
            Check(view.steps[0].StartsWith("[x]"), "step 1 ticks after finding the keepsake");
            Check(view.steps[1].StartsWith("[ ]"), "step 2 still open");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideKeepsake), ObjectivePhase.Active,
                  "objective still active mid-checklist");
            CheckEq(view.progress, 1, "progress = 1 of 2");
            Check(WorldServices.World.InteractionUnlocked("keepsake_return"),
                  "carrying the keepsake unlocked the return interaction (registry)");

            // step 2: deliver it to the twins
            var deliver = MakeAction("TwinsReturn",
                new List<DecisionConditionData> { Cond(ConditionType.ItemHeld, "twins_keepsake") },
                new List<DecisionEffectData>
                {
                    Eff(EffectType.RemoveItem, "twins_keepsake"),
                    Eff(EffectType.SetFlag, "keepsake_returned", "1")
                },
                "deliver_uses", 1, "");
            deliver.OnInteract(null);

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideKeepsake), ObjectivePhase.Completed,
                  "delivery completed the objective");
            Check(!GameServices.State.HasItem("twins_keepsake"), "keepsake handed over (item consumed)");
            CheckEq(GameServices.State.GetEntity("tide_bystanders", true), false, "anxious twins despawned");
            CheckEq(GameServices.State.GetEntity("tide_calm", false), true, "relieved twins spawned (world object changed)");
            CheckEq(GameServices.State.GetWorldState("hall"), "twins_blessed", "hall variant remembers the deed");

            // NPC connection: Mara now offers the report interaction; the follow-up completes VIA DIALOGUE
            var mara = new NpcBrain(content.Content.FindNpc("mara"), GameServices.Progress);
            Check(mara.InteractionAvailable("report"), "mara offers 'report' after the objective (NPC<->objective link)");
            CheckEq(mara.DefaultInteraction().id, "report", "report is now mara's prompt interaction");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideReport), ObjectivePhase.Active,
                  "follow-up (Tell Mara) unlocked");

            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterMaraReport);
            int guard = 0;
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            flow.SelectChoice("tell_all");
            flow.Advance();
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideReport), ObjectivePhase.Completed,
                  "dialogue decision completed the objective");
            mara.Reapply();
            CheckEq(mara.CurrentTitle, "Mara · Heartened", "mara's state changed from the completed report objective");
            CheckEq(WorldServices.Objectives.CompletedObjectives().Count, 2, "two objectives completed on the tide path");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 35. stone path: counter, failure, recovery
        private static void TestStoneFailureAndRecovery()
        {
            Log.Add("[35] Stone: counter progress -> failure by a later decision -> recovery follow-up");
            string dir = TempDir("stone");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);

            PlayFirstLight("stone_still");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveStoneBarricade), ObjectivePhase.Active,
                  "Decision C -> Objective C tracked");

            var brace = MakeAction("Barricade",
                new List<DecisionConditionData> { Cond(ConditionType.FlagIs, StoryContentBuilder.DriveFlag, "stone") },
                new List<DecisionEffectData>(), // use-counting on brace_count drives the objective counter
                "brace_count", 2, "");
            brace.OnInteract(null);

            ObjectiveView view = WorldServices.Objectives.ViewOf(StoryContentBuilder.ObjectiveStoneBarricade);
            CheckEq(view.progress, 1, "one brace set (counter progress 1/2)");
            Check(view.counterText.Contains("1/2"), "counter text shows 1/2");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveStoneBarricade), ObjectivePhase.Active,
                  "still active after one brace");

            // the ability shortcut: stone_ward wedges the line in ONE use (abilities change HOW)
            var wedge = MakeAction("WardStone",
                new List<DecisionConditionData> { Cond(ConditionType.AbilityOwned, "stone_ward") },
                new List<DecisionEffectData> { Eff(EffectType.SetVar, "brace_count", "", 2) },
                "wedge_uses", 1, "");
            Check(wedge.ConditionsPass, "stone player passes the ability-gated wedge conditions");
            wedge.OnInteract(null);
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveStoneBarricade), ObjectivePhase.Completed,
                  "ability wedge completed the objective in one use");
            CheckEq(GameServices.State.GetWorldState("hall"), "barricade_held", "hall variant remembers it held");
            CheckEq(GameServices.State.GetReputation("wards"), 16, "wardens standing rose (8 D1 + 8 objective)");

            var sera = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            CheckEq(sera.CurrentTitle, "Sera · Steadied", "sera reacts to the completed barricade objective");

            // ---- now the FAILURE variant on a fresh stone run ----
            Shutdown();
            Directory.Delete(dir, true);
            dir = TempDir("stonefail");
            ClearCaptures();
            NewRun(dir, out content);

            PlayFirstLight("stone_still");
            brace = MakeAction("Barricade",
                new List<DecisionConditionData> { Cond(ConditionType.FlagIs, StoryContentBuilder.DriveFlag, "stone") },
                new List<DecisionEffectData>(),
                "brace_count", 2, "");
            brace.OnInteract(null);

            // seal the echo at the shrine -> the barricade's failure condition fires
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterShrine);
            int guard = 0;
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            flow.SelectChoice("seal_stone");
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveStoneBarricade), ObjectivePhase.Failed,
                  "sealing the echo FAILED the active objective");
            CheckEq(GameServices.State.GetEntity("barricade", true), false, "barricade object removed (fell)");
            CheckEq(GameServices.State.GetEntity("barricade_rubble", false), true, "rubble spawned");
            CheckEq(GameServices.State.GetWorldState("hall"), "barricade_fell", "hall variant remembers it fell");
            CheckEq(GameServices.State.GetReputation("wards"), 2, "wardens standing dropped (8 - 6)");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveStoneRebuild), ObjectivePhase.Active,
                  "recovery objective offered by the failure");

            // area consequence: the sweep seals the annex
            GameServices.State.UnlockArea("annex");
            Check(!WorldServices.World.IsOpen("annex"), "annex closed by the failure consequence");
            Check(WorldServices.World.IsClosed("annex"), "closed-area tracking works");

            // clear the rubble -> recovery completes -> annex reopens
            var rubble = MakeAction("Rubble",
                new List<DecisionConditionData> { Cond(ConditionType.WorldStateIs, "hall", "barricade_fell") },
                new List<DecisionEffectData>(),
                "rubble_count", 2, "");
            Check(rubble.ConditionsPass, "rubble action is world-state-gated (only after the fall)");
            rubble.OnInteract(null);
            rubble.OnInteract(null);
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveStoneRebuild), ObjectivePhase.Completed,
                  "recovery objective completed");
            Check(WorldServices.World.IsOpen("annex"), "annex reopened by the recovery consequence");
            CheckEq(GameServices.State.GetWorldState("hall"), "passage_cleared", "hall variant remembers the repair");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 36. world-state system API + events
        private static void TestWorldStateSystem()
        {
            Log.Add("[36] WorldStateSystem: areas, objects, npc locations, flags, unlocks, events");
            string dir = TempDir("worldapi");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);

            WorldStateSystem world = WorldServices.World;

            // areas
            Check(!world.IsOpen("annex") && !world.IsUnlocked("annex"), "annex starts closed");
            world.OpenArea("annex");
            Check(world.IsOpen("annex") && world.OpenAreas().Contains("annex"), "opened + listed");
            world.CloseArea("annex");
            Check(!world.IsOpen("annex") && world.IsClosed("annex"), "re-sealed");
            world.ReopenArea("annex");
            Check(world.IsOpen("annex"), "re-opened");

            // objects + flags
            world.SetObjectState("test_rubble", true);
            Check(world.ObjectActive("test_rubble"), "object state persisted");
            world.SetFlag("test_flag", "yes");
            Check(world.FlagIs("test_flag", "yes"), "flag passthrough");

            // npc locations
            world.MoveNpc("mara", "east_columns");
            CheckEq(world.NpcLocation("mara"), "east_columns", "npc location persisted");
            Check(Relocations.FindAll(r => r.npcId == "mara").Count == 1, "relocation event fired once");

            // var events (objective counters react to these - no polling)
            int varEventsBefore = VarEvents.Count;
            GameServices.State.SetVar("probe_var", 5);
            CheckEq(VarEvents.Count, varEventsBefore + 1, "VarChangedEvent published on var write");

            // interaction unlock registry is idempotent and condition-gated
            world.SyncInteractionUnlocks();
            int unlocksBefore = InteractionUnlocks.Count;
            world.SyncInteractionUnlocks();
            CheckEq(InteractionUnlocks.Count, unlocksBefore, "unlock sync is idempotent (no duplicate events)");
            Check(!world.InteractionUnlocked("choir_beacon_channel"), "ember channel still locked on fresh run");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 37. save/load persistence
        private static void TestPersistence()
        {
            Log.Add("[37] Persistence: objectives, unlocks, npc locations, world variants survive restart");
            string dir = TempDir("persist");
            ClearCaptures();

            // ---- run 1: ember path, beacon done, cache NOT opened ----
            IEncounterSource content;
            NewRun(dir, out content);
            PlayFirstLight("ember_reach");

            var beacon = MakeAction("ChoirBeacon",
                new List<DecisionConditionData> { Cond(ConditionType.AbilityOwned, "ember_pulse") },
                new List<DecisionEffectData> { Eff(EffectType.SetFlag, "beacon_silenced", "1") },
                "beacon_uses", 1, "choir_beacon");
            beacon.OnInteract(null);

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Completed,
                  "run 1: beacon objective completed");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberCache), ObjectivePhase.Active,
                  "run 1: cache follow-up active (mid-way point for the restart)");
            int consequencesBefore = GameServices.State.State.entities.Count;

            Shutdown();

            // ---- run 2: same slot -> everything restored, nothing re-applied ----
            ClearCaptures();
            NewRun(dir, out content);

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Completed,
                  "restart: completed objective stays completed");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberCache), ObjectivePhase.Active,
                  "restart: in-progress follow-up restored as active");
            CheckEq(GameServices.State.GetWorldState("annex"), "quiet", "restart: world variant restored");
            CheckEq(GameServices.State.GetEntity("ember_cache", false), true, "restart: spawned cache restored");
            CheckEq(GameServices.State.GetNpcLocation("sera", ""), "annex_gate", "restart: sera still at the annex gate");
            Check(WorldServices.World.InteractionUnlocked("choir_beacon_channel"), "restart: ability unlock registry restored");
            CheckEq(GameServices.State.State.entities.Count, consequencesBefore,
                  "restart: consequences NOT re-applied (exactly the same world)");

            // finish the follow-up AFTER the restart (continue where the save left off)
            var cache = MakeAction("EmberCache",
                new List<DecisionConditionData> { Cond(ConditionType.FlagIs, "beacon_silenced", "1") },
                new List<DecisionEffectData>
                {
                    Eff(EffectType.SetFlag, "ember_cache_opened", "1"),
                    Eff(EffectType.AddItem, "ember_core")
                },
                "cache_uses", 1, "ember_cache");
            cache.OnInteract(null);
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberCache), ObjectivePhase.Completed,
                  "restart: follow-up completable from the restored state");

            // ---- mid-checklist persistence (tide) ----
            Shutdown();
            Directory.Delete(dir, true);
            dir = TempDir("persist2");
            NewRun(dir, out content);
            PlayFirstLight("tide_clear");
            GameServices.State.SetFlag("keepsake_found", "1");
            GameServices.State.AddItem("twins_keepsake");
            CheckEq(WorldServices.Objectives.ViewOf(StoryContentBuilder.ObjectiveTideKeepsake).progress, 1, "run 1: step 1 of 2");
            Shutdown();

            NewRun(dir, out content);
            ObjectiveView view = WorldServices.Objectives.ViewOf(StoryContentBuilder.ObjectiveTideKeepsake);
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveTideKeepsake), ObjectivePhase.Active,
                  "restart: mid-checklist objective restored active");
            Check(view.steps[0].StartsWith("[x]") && view.steps[1].StartsWith("[ ]"), "restart: checklist progress restored");
            Check(GameServices.State.HasItem("twins_keepsake"), "restart: carried item restored");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 38. different paths -> different worlds
        private static void TestDifferentPaths()
        {
            Log.Add("[38] Acceptance: two players, two decisions -> different objectives, NPCs and worlds");

            // player A: ember
            string dirA = TempDir("pathA");
            IEncounterSource contentA;
            NewRun(dirA, out contentA);
            PlayFirstLight("ember_reach");
            var beacon = MakeAction("ChoirBeacon",
                new List<DecisionConditionData> { Cond(ConditionType.AbilityOwned, "ember_pulse") },
                new List<DecisionEffectData> { Eff(EffectType.SetFlag, "beacon_silenced", "1") },
                "beacon_uses", 1, "choir_beacon");
            beacon.OnInteract(null);
            var titlesA = WorldServices.Objectives.ActiveObjectives().ConvertAll(o => o.title);
            var unlocksA = WorldServices.World.Describe();
            var seraA = new NpcBrain(contentA.Content.FindNpc("sera"), GameServices.Progress).CurrentTitle;
            Shutdown();
            Directory.Delete(dirA, true);

            // player B: tide
            string dirB = TempDir("pathB");
            IEncounterSource contentB;
            NewRun(dirB, out contentB);
            PlayFirstLight("tide_clear");
            GameServices.State.SetFlag("keepsake_found", "1");
            GameServices.State.SetFlag("keepsake_returned", "1"); // complete the checklist objective
            var titlesB = WorldServices.Objectives.ActiveObjectives().ConvertAll(o => o.title);
            var unlocksB = WorldServices.World.Describe();
            var seraB = new NpcBrain(contentB.Content.FindNpc("sera"), GameServices.Progress).CurrentTitle;
            Shutdown();
            Directory.Delete(dirB, true);

            // player C: stone (failed + recovering)
            string dirC = TempDir("pathC");
            IEncounterSource contentC;
            NewRun(dirC, out contentC);
            PlayFirstLight("stone_still");
            GameServices.State.SetFlag("c1_echo_sealed", "1"); // fires the failure conditions
            var titlesC = WorldServices.Objectives.ActiveObjectives().ConvertAll(o => o.title);
            var worldC = GameServices.State.GetWorldState("hall");
            Shutdown();
            Directory.Delete(dirC, true);

            Check(titlesA.Contains("Claim the Ember Cache") && !titlesB.Contains("Claim the Ember Cache"),
                  "player A has an objective player B can never see");
            Check(titlesB.Contains("Tell Mara What the Light Did") && !titlesA.Contains("Tell Mara What the Light Did"),
                  "player B has an objective player A can never see");
            Check(titlesC.Contains("Clear the Fallen Barricade"), "player C sees the recovery objective");
            Check(unlocksA != unlocksB, "the two players' interaction-unlock sets differ");
            Check(seraA != seraB, "sera's fate differs between the paths (Vanguard vs Grateful)");
            CheckEq(worldC, "barricade_fell", "player C's hall is a different place (fallen barricade)");
        }

        // ================================================================ 39. v3 save migration
        private static void TestSaveMigration()
        {
            Log.Add("[39] Save migration: v3 file (pre-objectives) loads with an empty mission state");
            string dir = TempDir("migrate");
            var paths = new TempPaths(dir);

            // hand-write a v3 save (no objectives/npcLocations/... fields at all)
            string v3 = "{\"schemaVersion\":3,\"meta\":{\"slotName\":\"old\",\"timestamp\":\"2026-09-01T00:00:00\",\"playtimeSec\":0}," +
                        "\"scene\":{\"sceneKey\":\"FirstLocation\",\"checkpointId\":\"hall_spawn\"}," +
                        "\"gameState\":{\"chapterId\":0,\"levelId\":0," +
                        "\"flags\":[{\"key\":\"c1_hall_drive\",\"value\":\"ember\"}]," +
                        "\"decisions\":[{\"decisionId\":\"dec_c1_hall_first_light\",\"optionId\":\"ember_reach\",\"summary\":\"flag\",\"resolvedAt\":\"2026-09-01T00:00:00\"}],\"codex\":[]}}";
            File.WriteAllText(paths.Resolve(SaveSystem.SlotPrefix.Replace("{0}", "0")), v3);

            IEncounterSource content;
            NewRun(dir, out content);

            Check(GameServices.State.HasDecision("dec_c1_hall_first_light"), "v3 decisions still load");
            // v3 file had no objectives field: it normalized to empty, then the live systems
            // evaluated against the restored decision and offered exactly the ember objective
            Check(GameServices.State.State.objectives != null
                  && GameServices.State.State.objectives.Count == 1
                  && GameServices.State.State.objectives[0].id == StoryContentBuilder.ObjectiveEmberBeacon,
                  "v3 objectives normalized empty, then live-upgraded by evaluation");
            // the objective system then offers against the restored decision - live upgrade
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Active,
                  "v3 save upgraded in-memory: objectives evaluate against restored decisions");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ entry
        public static int RunAll(out int passed, out int failed)
        {
            Console.WriteLine();
            TestObjectiveContent();
            TestObjectiveUnlocking();
            TestEmberCompletion();
            TestAbilityGating();
            TestTideChecklist();
            TestStoneFailureAndRecovery();
            TestWorldStateSystem();
            TestPersistence();
            TestDifferentPaths();
            TestSaveMigration();
            passed = _passed;
            failed = _failed;
            return _failed;
        }

        public static List<string> GetLog() { return Log; }
    }

    /// <summary>Test-only read accessor for persisted objective entries.</summary>
    internal static class WorldTestExtensions
    {
        internal static List<ObjectiveProgressEntry> GetObjectiveEntriesForTest(this GameState state)
        {
            return state.objectives != null ? state.objectives : new List<ObjectiveProgressEntry>();
        }
    }
}
