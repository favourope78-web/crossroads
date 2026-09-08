// ============================================================================
// CROSSROADS headless tests of the VISUAL TRANSFORMATION PASS:
//   debug-gate defaults · mini-map math · HUD text derivation · ability hotbar
//   model · loading tips · world-marker registry · settings additions ·
//   dialogue/camera/interaction seams that must stay pure.
// Runs the exact code paths the game uses (UIDebugGate, MiniMapMath,
// TrackerModel, HotbarModel, PlayerHUD.HpText, WorldMarkers,
// LocationTransitionFader.PickTip, SettingsNudge, DialogueUI.SpeakerColor).
// Invoke from FlowTests.Main (single process, shared counters).
// ============================================================================
using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Gameplay;
using Crossroads.Gameplay.Input;
using Crossroads.UI;

namespace Crossroads.Tests
{
    public static class VisualPassTests
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

        public static void RunAll(out int passed, out int failed)
        {
            Console.WriteLine();
            TestDebugGateDefaults();
            TestMiniMapMath();
            TestTrackerModel();
            TestHotbarModel();
            TestHudTextAndTheme();
            TestWorldMarkersRegistry();
            TestLoadingTips();
            TestSettingsAdditions();
            TestCameraImpulseSeam();
            passed = _passed;
            failed = _failed;
        }

        // ---------------------------------------------------------------- 72. dev gate
        private static void TestDebugGateDefaults()
        {
            Log.Add("[72] Debug gate: clean HUD by default, dev overlays compile-time stripped");
            UIDebugGate.ResetForTests();
            // headless compile == release defines: no UNITY_EDITOR / DEVELOPMENT_BUILD
            Check(!UIDebugGate.DevBuild, "gate: release builds report DevBuild = false");
            Check(!UIDebugGate.OverlaysVisible, "gate: overlays hidden by default (clean player HUD)");
            UIDebugGate.SetOverlays(true);
            Check(!UIDebugGate.OverlaysVisible, "gate: release can NEVER open the dev overlays");
            UIDebugGate.Toggle();
            Check(!UIDebugGate.OverlaysVisible, "gate: release toggle is a no-op");
            UIDebugGate.ResetForTests();

            // the devOverlays preference rides InputSettings without breaking its clamps
            InputSettings s = new InputSettings();
            CheckEq(s.devOverlays, -1, "gate: devOverlays default = follow build default");
            s.devOverlays = 5;
            s.ApplyClamps();
            CheckEq(s.devOverlays, 1, "gate: devOverlays clamps into -1..1");
        }

        // ---------------------------------------------------------------- 73. minimap math
        private static void TestMiniMapMath()
        {
            Log.Add("[73] Mini-map: camera-forward-up rotation, rim pinning, north heading");
            // camera yaw 0 looks along +Z: a contact 5 m north (+Z) reads straight up
            UnityEngine.Vector2 up = MiniMapMath.MapDelta(new UnityEngine.Vector2(0f, 5f), 0f);
            CheckNear(up.y, 5f, 0.001f, "minimap: yaw 0 -> world +Z maps up");
            CheckNear(up.x, 0f, 0.001f, "minimap: yaw 0 -> world +Z has no x drift");

            // camera yaw 90 looks along +X: a contact to world +X now reads up
            UnityEngine.Vector2 right = MiniMapMath.MapDelta(new UnityEngine.Vector2(5f, 0f), 90f);
            CheckNear(right.y, 5f, 0.001f, "minimap: yaw 90 -> world +X maps up (camera-relative)");

            // a contact BEHIND the camera reads down
            UnityEngine.Vector2 behind = MiniMapMath.MapDelta(new UnityEngine.Vector2(0f, -4f), 0f);
            CheckNear(behind.y, -4f, 0.001f, "minimap: contact behind the camera maps down");

            // rim pinning: out-of-range contact lands exactly on the rim, flagged
            bool pinned;
            UnityEngine.Vector2 pos = MiniMapMath.ToMapPosition(new UnityEngine.Vector2(0f, 100f), 0f, 5f, 50f, out pinned);
            Check(pinned, "minimap: out-of-range contact is rim-pinned");
            CheckNear(pos.magnitude, 50f, 0.01f, "minimap: pinned contact sits on the rim");

            UnityEngine.Vector2 inside = MiniMapMath.ToMapPosition(new UnityEngine.Vector2(0f, 6f), 0f, 5f, 50f, out pinned);
            Check(!pinned, "minimap: in-range contact is not pinned");
            CheckNear(inside.y, 30f, 0.001f, "minimap: in-range contact scales by pixels-per-metre");

            // north heading: yaw 0 -> north up (0 deg); yaw 90 -> north reads left (-90)
            CheckNear(MiniMapMath.NorthHeading(0f), 0f, 0.01f, "minimap: north is up when camera faces +Z");
            CheckNear(Math.Abs(MiniMapMath.NorthHeading(90f)), 90f, 0.01f, "minimap: north rotates 90 deg when the camera turns 90");

            CheckNear(MiniMapMath.YawFromForward(new UnityEngine.Vector3(0f, 0f, 1f)), 0f, 0.01f, "minimap: yaw from +Z forward = 0");
            CheckNear(MiniMapMath.YawFromForward(new UnityEngine.Vector3(1f, 0.5f, 0f)), 90f, 0.01f, "minimap: yaw ignores pitch");

            CheckNear(MiniMapMath.PixelsPerMeterForRange(25f, 50f), 2f, 0.001f, "minimap: range 25 m over 50 px rim = 2 px/m");
        }

        // ---------------------------------------------------------------- 74. tracker text
        private static void TestTrackerModel()
        {
            Log.Add("[74] Objective tracker: current objective only, no ids/phase dumps");
            var active = new List<ObjectiveView> { new ObjectiveView { title = "Speak with the Archivist", counterText = "1/2" } };
            var offered = new List<ObjectiveView> { new ObjectiveView { title = "Light the braziers", counterText = "" } };
            string title, counter;
            TrackerModel.Compose(active, offered, out title, out counter);
            CheckEq(title, "\u25B6  Speak with the Archivist", "tracker: shows the first ACTIVE objective");
            CheckEq(counter, "1/2", "tracker: shows the counter");

            TrackerModel.Compose(null, offered, out title, out counter);
            Check(title.Contains("Light the braziers"), "tracker: falls back to the offered objective");
            Check(counter.Contains("available"), "tracker: offered objectives are marked available");

            TrackerModel.Compose(null, null, out title, out counter);
            CheckEq(title, "", "tracker: quiet when nothing is tracked");
            CheckEq(counter, "", "tracker: no counter when quiet");
        }

        // ---------------------------------------------------------------- 75. hotbar model
        private static void TestHotbarModel()
        {
            Log.Add("[75] Ability hotbar: owned+unlocked lines only, glyph from name, cooldown text");
            var rows = new List<AbilityRowView>
            {
                new AbilityRowView { abilityId = "ember_pulse", name = "Ember Pulse", line = "ember", access = AbilityAccessState.Unlocked },
                new AbilityRowView { abilityId = "tide_veil", name = "Tide Veil", line = "tide", access = AbilityAccessState.Unlocked },
                new AbilityRowView { abilityId = "stone_ward", name = "Stone Ward", line = "stone", access = AbilityAccessState.Locked },
                new AbilityRowView { abilityId = "hollow_call", name = "Hollow Call", line = "hollow", access = AbilityAccessState.Blocked },
            };
            var ids = new string[3];
            var colors = new UnityEngine.Color[3];
            var glyphs = new string[3];
            HotbarModel.Compose(rows, ids, colors, glyphs, 3);
            CheckEq(HotbarModel.LastComposedCount, 2, "hotbar: locked + sealed lines never appear");
            CheckEq(ids[0], "ember_pulse", "hotbar: first owned ability takes the first slot");
            CheckEq(ids[1], "tide_veil", "hotbar: second owned ability takes the second slot");
            CheckEq(ids[2], "", "hotbar: unused slots are empty");
            CheckEq(glyphs[0], "E", "hotbar: glyph is the ability initial");
            Check(colors[0].r > 0.9f, "hotbar: ember line gets the ember ring colour");
            Check(Math.Abs(colors[1].g - 0.80f) < 0.02f, "hotbar: tide line gets the tide ring colour");

            CheckEq(HotbarModel.CooldownText(0f), "", "hotbar: no cooldown label when ready");
            CheckEq(HotbarModel.CooldownText(0.4f), "1", "hotbar: sub-second cooldown rounds up to 1s");
            CheckEq(HotbarModel.CooldownText(7.2f), "8", "hotbar: cooldown text is whole seconds, ceiling");

            // empty manager state: nothing owned -> no buttons
            HotbarModel.Compose(new List<AbilityRowView>(), ids, colors, glyphs, 3);
            CheckEq(HotbarModel.LastComposedCount, 0, "hotbar: no abilities owned -> no buttons");
        }

        // ---------------------------------------------------------------- 76. HUD text + theme
        private static void TestHudTextAndTheme()
        {
            Log.Add("[76] Player HUD: health label formatting + semantic colours");
            CheckEq(PlayerHUD.HpText(97.4f, 100f), "98/100", "hud: hp label ceilings the current value");
            CheckEq(PlayerHUD.HpText(0f, 100f), "0/100", "hud: dead player reads 0/100");
            CheckEq(PlayerHUD.HpText(-3f, 100f), "0/100", "hud: negative hp clamps to 0");

            UnityEngine.Color good = HudTheme.HealthFill(0.9f);
            UnityEngine.Color warn = HudTheme.HealthFill(0.4f);
            UnityEngine.Color bad = HudTheme.HealthFill(0.1f);
            Check(good.g > 0.7f && good.r < 0.5f, "hud: healthy fraction is green");
            Check(warn.r > 0.7f && warn.g > 0.5f, "hud: half fraction is amber");
            Check(bad.r > 0.7f && bad.g < 0.5f, "hud: low fraction is red");

            Check(HudTheme.LineColor("ember").r > 0.9f, "hud: ember line colour resolves");
            Check(Math.Abs(HudTheme.LineColor("TIDE").g - 0.80f) < 0.02f, "hud: line colour matching is case-insensitive");
            Check(HudTheme.LineColor("").b > 0.9f, "hud: unknown line falls back to the accent");

            // speaker colour parity for the dialogue nameplates (was in MobileExperienceTests, stays green)
            Check(DialogueUI.SpeakerColor("Mara").g == RuntimeMenuFactory.Tide.g, "dialogue: Mara keeps the tide colour");
        }

        // ---------------------------------------------------------------- 77. world markers
        private static void TestWorldMarkersRegistry()
        {
            Log.Add("[77] World markers: register/unregister drives the minimap dot sources");
            WorldMarkers.ClearForTests();
            CheckEq(WorldMarkers.Count, 0, "markers: registry starts empty");
            WorldMarkers.Register(null, MarkerKind.Npc);
            CheckEq(WorldMarkers.Count, 0, "markers: null owner ignored");

            // Component stubs: GameObject.AddComponent is the way to make one
            var go = new UnityEngine.GameObject("marker_probe");
            var a = go.AddComponent<DoorInteractableStub>();
            var b = go.AddComponent<DoorInteractableStub>();
            WorldMarkers.Register(a, MarkerKind.PointOfInterest);
            WorldMarkers.Register(a, MarkerKind.PointOfInterest);
            CheckEq(WorldMarkers.Count, 1, "markers: double registration is idempotent");
            WorldMarkers.Register(b, MarkerKind.Enemy);
            CheckEq(WorldMarkers.Count, 2, "markers: distinct components both tracked");
            CheckEq(WorldMarkers.Get(1).kind, MarkerKind.Enemy, "markers: kinds survive registration order");

            WorldMarkers.Unregister(a);
            CheckEq(WorldMarkers.Count, 1, "markers: unregister removes exactly the owner");
            WorldMarkers.Unregister(a);
            CheckEq(WorldMarkers.Count, 1, "markers: unregistering a missing owner is safe");
            WorldMarkers.ClearForTests();
        }

        private class DoorInteractableStub : UnityEngine.MonoBehaviour { }

        // ---------------------------------------------------------------- 78. loading tips
        private static void TestLoadingTips()
        {
            Log.Add("[78] Loading card: deterministic, authored tips (no ids/paths)");
            string t0 = LocationTransitionFader.PickTip(0);
            string t1 = LocationTransitionFader.PickTip(1);
            Check(!string.IsNullOrEmpty(t0), "loading: tips exist");
            Check(t0 != t1, "loading: consecutive arrivals vary the tip");
            CheckEq(LocationTransitionFader.PickTip(0), t0, "loading: tip pick is deterministic");
            CheckEq(LocationTransitionFader.PickTip(-1), LocationTransitionFader.PickTip(LocationTransitionFader.Tips.Length - 1), "loading: negative index wraps to the last tip");
            for (int i = 0; i < LocationTransitionFader.Tips.Length; i++)
            {
                if (LocationTransitionFader.Tips[i].Contains("/") || LocationTransitionFader.Tips[i].Contains("save_slot"))
                {
                    Check(false, "loading: tip leaks a path-like string");
                    break;
                }
            }
            Check(true, "loading: no tip leaks paths or file names");
        }

        // ---------------------------------------------------------------- 79. settings additions
        private static void TestSettingsAdditions()
        {
            Log.Add("[79] Settings: opacity/handedness/touch-visibility steppers + clamps");
            InputSettings s = new InputSettings();
            float before = s.controlOpacity;
            bool changed = SettingsNudge.Apply(s, SettingId.ControlOpacity, -1);
            Check(changed, "settings: opacity nudge changes the value");
            Check(s.controlOpacity < before, "settings: opacity steps down");
            for (int i = 0; i < 20; i++) SettingsNudge.Apply(s, SettingId.ControlOpacity, -1);
            CheckNear(s.controlOpacity, 0.35f, 0.001f, "settings: opacity clamps at the 0.35 floor");

            bool left0 = s.leftHanded;
            SettingsNudge.Apply(s, SettingId.LeftHanded, +1);
            Check(s.leftHanded != left0, "settings: handedness toggles");

            int vis0 = s.showTouchControls;
            SettingsNudge.Apply(s, SettingId.TouchVisibility, +1);
            Check(s.showTouchControls != vis0, "settings: touch visibility cycles");
            for (int i = 0; i < 5; i++) SettingsNudge.Apply(s, SettingId.TouchVisibility, +1);
            Check(s.showTouchControls >= 0 && s.showTouchControls <= 2, "settings: touch visibility stays in band (cycles)");
        }

        // ---------------------------------------------------------------- 80. camera impulse
        private static void TestCameraImpulseSeam()
        {
            Log.Add("[80] Combat camera: impulse clamps + starts at zero");
            var cam = new Crossroads.Prototype.ThirdPersonCameraController();
            CheckNear(cam.Impulse, 0f, 0.0001f, "camera: no impulse while calm");
            cam.AddImpulse(0.16f);
            CheckNear(cam.Impulse, 0.16f, 0.0001f, "camera: hit impulse is accepted");
            cam.AddImpulse(5f);
            Check(cam.Impulse <= 0.4f, "camera: impulse is comfort-clamped (0.4 m max)");
            cam.AddImpulse(-2f);
            Check(cam.Impulse >= 0f, "camera: negative impulses are ignored");
            CheckNear(cam.Cinematic, 0f, 0.0001f, "camera: gameplay framing untouched by impulses");
        }

        public static IEnumerable<string> GetLog() { return Log; }
    }
}
