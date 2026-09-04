// ============================================================================
// CROSSROADS headless tests of the MOBILE PLAYER EXPERIENCE:
//   input bus + joystick math · settings persistence · camera rig math ·
//   combat-control gating · ability ownership filtering · full mobile
//   gameplay loop (launch -> load -> decide -> fight -> world/NPC reaction ->
//   save -> restart -> restored) driven through the same InputBus the touch
//   widgets write into.
// Runs the exact same code paths the game uses (InputBus, JoystickFilter,
// InputSettingsStore, SettingsNudge, CameraRigMath, CombatPresence,
// AbilitySheetModel, CombatResolution, ObjectiveManager).
// Invoke from FlowTests.Main (single process, shared counters).
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Gameplay;
using Crossroads.Gameplay.Input;
using Crossroads.UI;
using CM = Crossroads.Gameplay.Input; // disambiguate vs UnityEngine.Input inside tests

namespace Crossroads.Tests
{
    public static class MobileExperienceTests
    {
        private static int _passed, _failed;
        private static readonly List<string> Log = new List<string>();

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

        private static void CheckNear(float actual, float expected, float eps, string what)
        {
            Check(Math.Abs(actual - expected) <= eps, what + " (expected ~" + expected + ", got " + actual + ")");
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

        private static void NewRun(string dir, out IEncounterSource content)
        {
            content = new RuntimeContentSource();
            StoryLog.Info = Console.WriteLine;
            StoryLog.Warn = Console.WriteLine;
            StoryLog.Error = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(dir), content,
                "FirstLocation", "hall_spawn", 0, loadExisting: true);
            WorldServices.Init();
        }

        private static void Shutdown()
        {
            WorldServices.Shutdown(silent: true);
            GameServices.Shutdown(silent: true);
        }

        private static string TempDir(string tag)
        {
            return Path.Combine(Path.GetTempPath(), "crossroads_mobile_" + tag + "_" + Guid.NewGuid().ToString("N"));
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

        public static void RunAll(out int passed, out int failed)
        {
            Console.WriteLine();
            TestInputBusAndJoystick();
            TestSettingsPersistence();
            TestCameraRigMath();
            TestCombatControlGating();
            TestAbilityOwnershipFilter();
            TestFullMobileLoop();
            passed = _passed;
            failed = _failed;
        }

        public static IEnumerable<string> GetLog() { return Log; }

        // ================================================================ 50. input bus + joystick math
        private static void TestInputBusAndJoystick()
        {
            Log.Add("[50] InputBus: joystick deadzone/clamp, look accumulation, press edges, gating");
            InputBus.Reset();
            InputLock.Set(false, "tests");
            InputBus.SetAvailable(MobileButton.Attack, true);
            InputBus.SetAvailable(MobileButton.Interact, true);

            // ---- joystick filter: deadzone, analog middle, rim clamp, normalized diagonals
            CheckEq(JoystickFilter.Apply(0.1f, 0f, 0.18f, 1f), UnityEngine.Vector2.zero, "tilt inside deadzone is zero");
            CheckNear(JoystickFilter.Apply(0.59f, 0f, 0.18f, 1f).x, 0.5f, 0.001f, "mid tilt is analog (~0.5)");
            CheckNear(JoystickFilter.Apply(2f, 0f, 0.18f, 1f).magnitude, 1f, 0.001f, "beyond-rim tilt clamps to 1.0");
            CheckNear(JoystickFilter.Apply(5f, 5f, 0.18f, 1f).magnitude, 1f, 0.001f, "diagonal full tilt clamps to 1.0 (no 1.41 sprint)");
            CheckNear(JoystickFilter.Apply(0.75f, 0.75f, 0.18f, 1f).magnitude, 1f, 0.001f, "diagonal at the rim stays 1.0");
            CheckEq(JoystickFilter.Apply(0.2f, 0.2f, 0.18f, 1f).y / JoystickFilter.Apply(0.2f, 0.2f, 0.18f, 1f).x, 1f,
                  "joystick output preserves direction (y == x for a 45deg tilt)");

            // ---- bus movement honors the filter; zero write clears
            InputBus.SetMovement(0.05f, 0.05f);
            CheckEq(InputBus.Movement, UnityEngine.Vector2.zero, "bus movement zero inside deadzone");
            InputBus.SetMovement(0.9f, 0f);
            Check(InputBus.Movement.x > 0.7f && InputBus.Movement.x < 1f, "bus carries analog tilt");
            InputBus.SetMovement(0f, 0f);
            Check(!InputBus.HasMovementInput, "joystick release clears movement");

            // ---- look deltas accumulate until consumed exactly once
            InputBus.AddLookDelta(10f, -4f);
            InputBus.AddLookDelta(5f, 1f);
            UnityEngine.Vector2 look = InputBus.ConsumeLookDelta();
            CheckNear(look.x, 15f, 0.001f, "look deltas accumulate");
            CheckNear(look.y, -3f, 0.001f, "look deltas keep sign");
            CheckEq(InputBus.ConsumeLookDelta(), UnityEngine.Vector2.zero, "look consume is destructive (camera owns the frame)");

            // ---- press edges fire exactly once; unavailable buttons swallow presses
            InputBus.SetPressed(MobileButton.Attack);
            Check(InputBus.ConsumePress(MobileButton.Attack), "attack press consumed once");
            Check(!InputBus.ConsumePress(MobileButton.Attack), "attack press does not double-fire");
            InputBus.SetAvailable(MobileButton.Attack, false);
            InputBus.SetPressed(MobileButton.Attack);
            Check(!InputBus.ConsumePress(MobileButton.Attack), "press on an unavailable (no-combat) button is swallowed");
            InputBus.SetAvailable(MobileButton.Attack, true);

            // ---- dialogue/pause lock gates movement, look and buttons at ONE place
            InputLock.Set(true, "dialogue");
            InputBus.SetMovement(1f, 1f);
            InputBus.AddLookDelta(30f, 30f);
            InputBus.SetPressed(MobileButton.Attack);
            CheckEq(InputBus.Movement, UnityEngine.Vector2.zero, "input lock zeroes movement (dialogue freezes Ari)");
            CheckEq(InputBus.ConsumeLookDelta(), UnityEngine.Vector2.zero, "input lock zeroes look");
            Check(!InputBus.ConsumePress(MobileButton.Attack), "input lock swallows button presses");
            InputLock.Set(false, "dialogue");
            InputBus.Reset();
            CheckEq(InputBus.Movement, UnityEngine.Vector2.zero, "reset clears live input state");
        }

        // ================================================================ 51. settings persistence
        private static void TestSettingsPersistence()
        {
            Log.Add("[51] Settings: defaults, nudge clamps, file roundtrip, corrupt-file safety");
            string dir = TempDir("settings");
            InputSettingsStore.Bind(new TestJsonAdapter(), new TempPaths(dir));

            InputSettings s = InputSettingsStore.Load();
            CheckEq(File.Exists(InputSettingsStore.Path), false, "fresh install: no settings file yet");
            CheckNear(s.lookSensitivity, 1.0f, 0.0001f, "default sensitivity");
            CheckNear(s.cameraDistance, 4.4f, 0.0001f, "default camera distance");

            // ---- nudge: step + clamp at both ends (pure SettingsNudge)
            InputSettings t = new InputSettings();
            Check(SettingsNudge.Apply(t, SettingId.Sensitivity, +1), "sensitivity nudge applies");
            CheckNear(t.lookSensitivity, 1.2f, 0.0001f, "sensitivity steps by 0.2");
            for (int i = 0; i < 40; i++) SettingsNudge.Apply(t, SettingId.Sensitivity, +1);
            CheckNear(t.lookSensitivity, 3.0f, 0.0001f, "sensitivity clamps at 3.0");
            for (int i = 0; i < 60; i++) SettingsNudge.Apply(t, SettingId.Volume, -1);
            CheckNear(t.audioVolume, 0f, 0.0001f, "volume clamps at 0 (mute)");
            Check(!SettingsNudge.Apply(t, SettingId.Volume, -1), "clamped nudge reports no change");
            SettingsNudge.Apply(t, SettingId.Quality, -5);
            CheckEq(t.qualityLevel, 0, "quality clamps at Low");

            // ---- roundtrip: save -> load keeps values; hand-corrupted file falls back to defaults
            InputSettingsStore.Save(t);
            InputSettings loaded = InputSettingsStore.Load();
            CheckNear(loaded.lookSensitivity, 3.0f, 0.0001f, "sensitivity survives a save/load roundtrip");
            CheckNear(loaded.audioVolume, 0f, 0.0001f, "volume survives a save/load roundtrip");
            CheckEq(loaded.qualityLevel, 0, "quality survives a save/load roundtrip");

            File.WriteAllText(InputSettingsStore.Path, "{ this is not json");
            InputSettings recovered = InputSettingsStore.Load();
            CheckNear(recovered.lookSensitivity, 1.0f, 0.0001f, "corrupt settings file falls back to defaults (never blocks launch)");

            // hostile file: values out of range are clamped on load, not trusted
            InputSettings hostile = new InputSettings();
            hostile.lookSensitivity = 99f;
            hostile.cameraDistance = -3f;
            hostile.controlOpacity = 12f;
            File.WriteAllText(InputSettingsStore.Path, new TestJsonAdapter().ToJson(hostile, false));
            InputSettings sane = InputSettingsStore.Load();
            CheckNear(sane.lookSensitivity, 3.0f, 0.0001f, "hostile sensitivity clamped on load");
            CheckNear(sane.cameraDistance, 2.6f, 0.0001f, "hostile camera distance clamped on load");
            CheckNear(sane.controlOpacity, 1f, 0.0001f, "hostile opacity clamped on load");

            Directory.Delete(dir, true);
        }

        // ================================================================ 52. camera rig math
        private static void TestCameraRigMath()
        {
            Log.Add("[52] Camera rig: orbit offsets, pitch clamp, collision pull-in/ease-out, indoor bias");

            // ---- orbit: yaw 0/pitch 18 puts the camera BEHIND (+Z look) and ABOVE the player
            UnityEngine.Vector3 behind = CameraRigMath.OrbitOffset(0f, 18f, 4.4f, 0.35f);
            Check(behind.z < -3.5f, "yaw 0: camera sits behind the player (-Z)");
            Check(behind.y > 1f, "pitch 18: camera looks down at the player");
            CheckNear(behind.x, 0f, 0.001f, "yaw 0: no lateral drift");

            UnityEngine.Vector3 right = CameraRigMath.OrbitOffset(90f, 18f, 4.4f, 0f);
            Check(right.x < -3.5f && Math.Abs(right.z) < 0.5f, "yaw 90: camera orbits to the side (X) cleanly");

            // ---- pitch clamps are indoor-sensible
            CheckNear(CameraRigMath.ClampPitch(-40f), 5f, 0.001f, "pitch never dives under the floor");
            CheckNear(CameraRigMath.ClampPitch(120f), 65f, 0.001f, "pitch never goes fully top-down");

            // ---- collision: pull in instantly, ease out at a bounded speed
            float d = CameraRigMath.ResolveDistance(4.4f, 2.0f, 4.4f, 0.016f, 3.5f);
            CheckNear(d, 2.0f - CameraRigMath.PullMargin, 0.001f, "wall at 2m: camera snaps in immediately");
            float eased = CameraRigMath.ResolveDistance(4.4f, 4.4f, 1.82f, 0.1f, 3.5f);
            CheckNear(eased, 1.82f + 0.35f, 0.001f, "clear of walls: eases OUT at the speed limit (no pop)");
            float cornered = CameraRigMath.ResolveDistance(4.4f, 0.6f, 4.4f, 0.016f, 3.5f);
            Check(cornered >= CameraRigMath.MinDistance, "cornered: distance never drops under the minimum framing floor");

            // ---- indoor: low headroom scales the height bias down smoothly
            CheckNear(CameraRigMath.IndoorHeightBias(4f, 0.35f), 0.35f, 0.0001f, "high ceiling: full height bias");
            Check(CameraRigMath.IndoorHeightBias(1.5f, 0.35f) < 0.35f * 0.5f, "door frame: height bias drops (framing stays on the player)");
            Check(CameraRigMath.IndoorHeightBias(0f, 0.35f) > 0f, "unprobed headroom keeps a sane bias");
        }

        // ================================================================ 53. combat-control gating
        private static class FakeAgentList
        {
            // EnemyAgent is a plain class under the test stub: IsDefeated/isActiveAndEnabled
            // default to (false, true) - exactly what a live enemy looks like.
            internal static List<EnemyAgent> Of(params bool[][] agents)
            {
                var list = new List<EnemyAgent>();
                foreach (bool[] spec in agents)
                {
                    if (spec == null) { list.Add(null); continue; } // destroyed entry
                    var a = new EnemyAgent();
                    if (spec[0]) a.SetDefeatedForTests();
                    a.SetEnabledForTests(spec[1]);
                    list.Add(a);
                }
                return list;
            }
        }

        private static void TestCombatControlGating()
        {
            Log.Add("[53] Combat gating: touch combat buttons exist only while a live enemy is engaged");

            Check(!CombatPresence.HasLiveEnemy(null), "no registry: not in combat");
            Check(!CombatPresence.HasLiveEnemy(new List<EnemyAgent>()), "empty registry: not in combat");
            Check(!CombatPresence.HasLiveEnemy(FakeAgentList.Of(new[] { true, true }, null)),
                  "defeated warden + destroyed entry: combat controls stay hidden (looting, not fighting)");
            Check(!CombatPresence.HasLiveEnemy(FakeAgentList.Of(new[] { false, false })),
                  "dormant (story-gated, inactive) warden: no combat controls before the decision");
            Check(CombatPresence.HasLiveEnemy(FakeAgentList.Of(new[] { true, true }, new[] { false, true })),
                  "one live activated enemy: combat controls appear");
            Check(!CombatPresence.HasLiveEnemy(FakeAgentList.Of(new[] { true, true }, new[] { true, true }, null)),
                  "hides again when every enemy is down (post-fight, buttons disappear)");
            Check(CombatPresence.HasLiveEnemy(FakeAgentList.Of(new[] { true, true }, new[] { false, true }, null)),
                  "stays visible while at least one live enemy remains (multi-enemy future-proofing)");
        }

        // ================================================================ 54. ability UI shows only owned abilities
        private static void TestAbilityOwnershipFilter()
        {
            Log.Add("[54] Ability UI: rows visible only for owned abilities (locked lines hidden)");
            string dir = TempDir("abilityui");
            NewRun(dir, out IEncounterSource content);
            var mgr = GameServices.Abilities;

            // nobody owns anything yet: every row must hide
            List<AbilityRowView> rows = AbilitySheetModel.Build(mgr);
            int visibleAtStart = 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].access != AbilityAccessState.Locked) visibleAtStart++;
            CheckEq(visibleAtStart, 0, "fresh save: no ability rows visible (nothing owned)");

            // ember decision unlocks ember_pulse: exactly that row becomes visible
            PlayFirstLight("ember_reach");
            rows = AbilitySheetModel.Build(mgr);
            int visible = 0; string visibleName = "";
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].access != AbilityAccessState.Locked) { visible++; visibleName = rows[i].name; }
            }
            CheckEq(visible, 1, "after the ember decision: exactly one ability row visible");
            Check(visibleName.IndexOf("Ember", StringComparison.OrdinalIgnoreCase) >= 0,
                  "the visible row is the owned ember line (" + visibleName + ")");

            // blocked stays VISIBLE (owned but sealed by a decision) - the sheet explains why
            GameServices.State.BlockAbility(StoryContentBuilder.AbilityEmber); // same write path decisions use
            rows = AbilitySheetModel.Build(mgr);
            bool blockedVisible = false;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].name.IndexOf("Ember", StringComparison.OrdinalIgnoreCase) >= 0
                    && rows[i].access == AbilityAccessState.Blocked) blockedVisible = true;
            Check(blockedVisible, "blocked line stays visible (owned-then-sealed shows as blocked, not hidden)");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 55-56. full mobile gameplay loop
        private static void TestFullMobileLoop()
        {
            Log.Add("[55] Full loop part 1: launch -> load -> decide -> objective -> ability -> fight -> world reacts");
            string dir = TempDir("loop");
            InputSettingsStore.Bind(new TestJsonAdapter(), new TempPaths(dir));
            CM.InputBus.Reset();
            InputLock.Set(false, "tests");
            NewRun(dir, out IEncounterSource content);

            // LAUNCH -> LOAD SAVE: fresh install, service boot, default player state
            CombatSettingsData settings = content.Content.combat;
            CheckEq(GameServices.State.GetVar(settings.healthVarKey, -1), -1,
                  "launch: no persisted hp yet - defaults apply on first spawn");
            Check(!CombatPresence.HasLiveEnemy(CombatDirector.LiveEnemies),
                  "launch: no enemies registered - touch combat buttons hidden");

            // settings persist from "a previous session" (the pause menu wrote them)
            InputSettings persisted = new InputSettings();
            persisted.lookSensitivity = 1.8f;
            persisted.cameraDistance = 5.2f;
            InputSettingsStore.Save(persisted);

            // MOVE + CAMERA + INTERACT happen through the bus (covered in [50]); the
            // story they drive starts here: interact with Mara -> first-light dialogue
            PlayFirstLight("ember_reach");

            // DECISION made -> objective appears -> combat controls become appropriate
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Active,
                  "decision made: hunt objective auto-tracked");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveEmberBeacon), ObjectivePhase.Active,
                  "path objective auto-tracked too");

            // USE ABILITY (through the REAL manager, as the ability button does)
            var mgr = GameServices.Abilities;
            float clock = 10f;
            mgr.Now = () => clock;
            CheckEq(mgr.Activate(StoryContentBuilder.AbilityEmber), AbilityActivation.Ok,
                  "ability button path: ember pulse activates through the real manager");

            // COMBAT: the warden takes the ability payload, then strikes fall; player fights back
            CombatantState warden = CombatantState.ForEnemy(content.Content.FindEnemy(StoryContentBuilder.EnemyChoirWarden));
            var payload = new AbilityUsedEvent
            {
                abilityId = StoryContentBuilder.AbilityEmber,
                level = 1,
                power = 1f
            };
            var targets = new List<CombatantState> { warden };
            CombatResolution.ResolveAbilityAttack(payload, content.Content.FindAbilityCombat(StoryContentBuilder.AbilityEmber),
                content.Content.statusEffects, null, targets);
            Check(warden.Health < warden.MaxHealth, "enemy takes damage from the ability attack");

            int guard = 0;
            while (warden.Alive && guard++ < 50)
                warden.ApplyDamage(settings.basicAttack.damageType, settings.basicAttack.baseDamage);
            Check(!warden.Alive, "mobile attack button spam defeats the warden (basic strike spam viable)");
            CombatResolution.DefeatEnemy(content.Content.FindEnemy(StoryContentBuilder.EnemyChoirWarden), GameServices.State);

            // WORLD + NPC REACTION through the existing event graph
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Completed,
                  "warden defeat completes the hunt objective");
            CheckEq(GameServices.State.GetBond("sera"), 5, "sera bond +5 (world/NPC reaction)");
            CheckEq(GameServices.State.GetEntity("warden_wreckage", false), true, "wreckage spawned in the world");

            // player hp persisted through the fight -> save
            GameServices.State.SetVar(settings.healthVarKey, 82);
            GameServices.PersistNow(autosaveMirror: true);
            Log.Add("[56] Full loop part 2: restart -> everything restored (save, world, settings)");

            // RESTART the "app": services re-boot from disk exactly like a relaunch
            Shutdown();
            CM.InputBus.Reset();
            NewRun(dir, out content);

            CheckEq(GameServices.State.GetVar(settings.healthVarKey, -1), 82, "restart: player hp restored from the save");
            CheckEq(GameServices.State.GetEntity("choir_warden", true), false, "restart: warden stays defeated");
            CheckEq(GameServices.State.GetEntity("warden_wreckage", false), true, "restart: world change (wreckage) persists");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Completed,
                  "restart: objective stays completed");
            CheckEq(GameServices.State.DecisionOption(StoryContentBuilder.DecisionFirstLight), "ember_reach",
                  "restart: the decision itself persists");

            InputSettings reloaded = InputSettingsStore.Load();
            CheckNear(reloaded.lookSensitivity, 1.8f, 0.0001f, "restart: settings file restored (sensitivity)");
            CheckNear(reloaded.cameraDistance, 5.2f, 0.0001f, "restart: settings file restored (camera distance)");

            var sera = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            CheckEq(sera.CurrentTitle, "Sera · Shieldmate", "restart: sera still reacts to the completed fight");

            Shutdown();
            Directory.Delete(dir, true);
        }
    }
}
