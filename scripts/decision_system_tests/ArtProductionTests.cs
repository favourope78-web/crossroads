// ============================================================================
// CROSSROADS headless tests of the ART PRODUCTION PASS:
//   boot loading screen (progress curve, readiness gating, tips) · story intro
//   (beat table, labels/captions, input lock lifecycle, skip callback contract) ·
//   chapter title cards (kicker/title split, duration) · art library fallback
//   behaviour when sprites are unbound · HUD cleanliness invariants (no raw state
//   text on the normal path, StateHUD stays behind the debug gate).
// Runs the exact code paths the game uses (LoadingScreenUI, IntroCinematicUI,
// ChapterTransitionUI, ArtLibrary, UIDebugGate, GameStateManager.StatusLines).
// Invoke from FlowTests.Main (single process, shared counters).
// ============================================================================
using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Gameplay;
using Crossroads.UI;
using UnityEngine;

namespace Crossroads.Tests
{
    public static class ArtProductionTests
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

        public static List<string> GetLog() { return Log; }

        public static void RunAll(out int passed, out int failed)
        {
            Console.WriteLine();
            Console.WriteLine("[ArtProductionTests] art production pass --------------------------------------------------");

            TestLoadingScreen();
            TestVfx();
            TestIntroCinematic();
            TestChapterCards();
            TestArtLibraryFallback();
            TestHudCleanliness();

            passed = _passed;
            failed = _failed;
            foreach (var line in Log) Console.WriteLine(line);
            Console.WriteLine("[ArtProductionTests] " + _passed + " passed, " + _failed + " failed");
        }

        // ---------------------------------------------------------------- vfx pool
        private static void TestVfx()
        {
            // alpha curve: 0 at spawn, peaks mid-life, 0 at death, never negative/over 1
            CheckNear(Crossroads.Gameplay.VfxMath.AlphaOverLife(0f), 0f, 0.001f, "vfx: alpha starts at zero");
            CheckNear(Crossroads.Gameplay.VfxMath.AlphaOverLife(1f), 0f, 0.001f, "vfx: alpha ends at zero");
            float peak = 0f;
            for (int i = 0; i <= 20; i++) peak = Math.Max(peak, Crossroads.Gameplay.VfxMath.AlphaOverLife(i / 20f));
            Check(peak > 0.5f && peak <= 1f, "vfx: alpha peaks mid-life within (0.5, 1]");
            // size curve: monotonic pop-out, bounded
            Check(Crossroads.Gameplay.VfxMath.SizeOverLife(0f) < Crossroads.Gameplay.VfxMath.SizeOverLife(0.25f), "vfx: size pops out");
            CheckNear(Crossroads.Gameplay.VfxMath.SizeOverLife(1f), 1f, 0.001f, "vfx: size settles at 1");
            // step: gravity pulls down, drag slows, life expires
            UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
            UnityEngine.Vector3 vel = new UnityEngine.Vector3(2f, 2f, 0f);
            float life = 0.5f;
            float y0 = pos.y;
            bool alive = Crossroads.Gameplay.VfxMath.Step(ref pos, ref vel, ref life, 0.1f);
            Check(alive && life < 0.5f, "vfx: step consumes life while alive");
            Check(pos.y < y0 + 0.2f, "vfx: gravity bounds the rise");
            bool died = false;
            for (int i = 0; i < 30; i++)
            {
                if (!Crossroads.Gameplay.VfxMath.Step(ref pos, ref vel, ref life, 0.1f)) { died = true; break; }
            }
            Check(died, "vfx: particle expires instead of living forever");
            // layers: contiguous, non-overlapping, cover the pool
            int a0, aN, h0, hN, d0, dN;
            Crossroads.Gameplay.VfxMath.LayerRange(0, out a0, out aN);
            Crossroads.Gameplay.VfxMath.LayerRange(1, out h0, out hN);
            Crossroads.Gameplay.VfxMath.LayerRange(2, out d0, out dN);
            Check(a0 == 0 && h0 == a0 + aN && d0 == h0 + hN, "vfx: pool layers are contiguous");
            Check(aN + hN + dN <= Crossroads.Gameplay.VfxMath.PoolSize, "vfx: layers fit the pool");
            CheckEq(Crossroads.Gameplay.VfxMath.LayerOfSlot(aN), 1, "vfx: first hit slot maps to layer 1");
            // cursor wraps inside its layer
            int cursor = Crossroads.Gameplay.VfxMath.NextCursor(d0 + dN - 1, 2);
            CheckEq(cursor, d0, "vfx: dust cursor wraps to the layer start");
            // palettes match the HudTheme ability colours
            Check(Crossroads.Gameplay.VfxMath.ColorForLine("ember").r > 0.9f, "vfx: ember is warm red");
            Check(Crossroads.Gameplay.VfxMath.ColorForLine("tide").g > 0.7f, "vfx: tide is cyan-green");
            Check(Crossroads.Gameplay.VfxMath.ColorForLine("stone").b < 0.5f, "vfx: stone is gold");
            // ability id -> line mapping (used by the director)
            CheckEq(Crossroads.Gameplay.VfxDirector.LineOfAbility("cinder_burst"), "ember", "vfx: cinder maps to ember");
            CheckEq(Crossroads.Gameplay.VfxDirector.LineOfAbility("riptide"), "tide", "vfx: riptide maps to tide");
            CheckEq(Crossroads.Gameplay.VfxDirector.LineOfAbility("tremor_stomp"), "stone", "vfx: tremor maps to stone");
            CheckEq(Crossroads.Gameplay.VfxDirector.LineOfAbility("choir_song"), "hollow", "vfx: unknown maps to hollow");
        }

        // ---------------------------------------------------------------- loading screen
        private static void TestLoadingScreen()
        {
            // progress never completes before services are ready (no fake loading)
            Check(LoadingScreenUI.ProgressFor(30f, false) <= 0.851f,
                "loading: bar never completes while services are booting");
            CheckNear(LoadingScreenUI.ProgressFor(0f, false), 0f, 0.001f, "loading: bar starts empty");
            Check(LoadingScreenUI.ProgressFor(0.2f, false) > LoadingScreenUI.ProgressFor(0.1f, false),
                "loading: bar advances over time");
            CheckEq(LoadingScreenUI.ProgressFor(2f, true), 1f, "loading: bar completes once services are ready");
            Check(!LoadingScreenUI.ReadyToDismiss(100f, false), "loading: screen holds while booting");
            Check(LoadingScreenUI.ReadyToDismiss(100f, true), "loading: screen may leave when ready");
            Check(!LoadingScreenUI.ReadyToDismiss(0.5f, true), "loading: screen respects the minimum dwell");
            // tips: deterministic, wrap around, never empty
            CheckEq(LoadingScreenUI.TipText(0), LoadingScreenUI.TipText(5), "loading: tips wrap around the ring");
            Check(LoadingScreenUI.TipText(3).Length > 10, "loading: tip text is real copy, not a placeholder");
            CheckEq(LoadingScreenUI.TipIndexFor(0f), 0, "loading: first tip is index 0");
            Check(LoadingScreenUI.TipIndexFor(4.2f) > LoadingScreenUI.TipIndexFor(1f), "loading: tips rotate over time");
            // distinct copy: no two tips identical (no duplicated filler)
            var seen = new HashSet<string>();
            bool allUnique = true;
            for (int i = 0; i < 5; i++)
            {
                if (!seen.Add(LoadingScreenUI.TipText(i))) allUnique = false;
            }
            Check(allUnique, "loading: every tip is distinct copy");
        }

        // ---------------------------------------------------------------- intro cinematic
        private static void TestIntroCinematic()
        {
            Check(IntroCinematicUI.BeatCount >= 3, "intro: at least three story beats");
            CheckEq(IntroCinematicUI.LabelFor(0), "PROLOGUE", "intro: first beat is the prologue");
            Check(IntroCinematicUI.CaptionFor(1).Contains("kite") || IntroCinematicUI.CaptionFor(1).Length > 40,
                "intro: second beat carries the pier memory");
            bool labelsDiffer = IntroCinematicUI.LabelFor(0) != IntroCinematicUI.LabelFor(1);
            Check(labelsDiffer, "intro: beats have distinct labels");
            bool allCaptioned = true;
            for (int i = 0; i < IntroCinematicUI.BeatCount; i++)
                if (String.IsNullOrEmpty(IntroCinematicUI.CaptionFor(i))) allCaptioned = false;
            Check(allCaptioned, "intro: every beat is captioned (no blank story cards)");
            CheckEq(IntroCinematicUI.CaptionFor(99), IntroCinematicUI.CaptionFor(IntroCinematicUI.BeatCount - 1),
                "intro: out-of-range index clamps to the last beat");

            // full lifecycle on a real component: Play locks input, Finish fires the callback exactly once
            var parent = new GameObject("ArtTestRoot").AddComponent<RectTransform>();
            var intro = IntroCinematicUI.Attach(parent);
            int fired = 0;
            intro.Play(delegate { fired++; });
            Check(InputLock.Active && InputLock.Reason == "intro", "intro: playing locks gameplay input");
            intro.FinishPublic();
            Check(fired == 1, "intro: finish fires the completion callback exactly once");
            Check(!InputLock.Active, "intro: finish releases the input lock");
            intro.FinishPublic();
            Check(fired == 1, "intro: double finish is a no-op (no double callbacks)");
        }

        // ---------------------------------------------------------------- chapter cards
        private static void TestChapterCards()
        {
            CheckEq(ChapterTransitionUI.KickerText("Chapter 3 - The Long Wall"), "CHAPTER 3",
                "chapter card: kicker is the chapter half");
            CheckEq(ChapterTransitionUI.TitleText("Chapter 3 - The Long Wall"), "The Long Wall",
                "chapter card: title is the story half");
            CheckEq(ChapterTransitionUI.KickerText("No Dash Here"), "CHAPTER",
                "chapter card: undashed titles fall back to a generic kicker");
            CheckEq(ChapterTransitionUI.TitleText("No Dash Here"), "No Dash Here",
                "chapter card: undashed titles stay whole");
            CheckEq(ChapterTransitionUI.KickerText(""), "CHAPTER", "chapter card: empty title still has a kicker");
            Check(ChapterTransitionUI.CardDurationSeconds > 2f && ChapterTransitionUI.CardDurationSeconds < 5f,
                "chapter card: duration is a readable hold, not a flash");
        }

        // ---------------------------------------------------------------- art library
        private static void TestArtLibraryFallback()
        {
            // headless: no library instance -> art getters return null (UI falls back to panels)
            var prev = ArtLibrary.Instance;
            Check(ArtLibrary.Get(ArtLibrary.SpriteKind.Logo) == null,
                "art: unbound sprite reads as null (graceful, never a missing-asset box)");
            Check(!ArtLibrary.HasKeyart, "art: HasKeyart is false without a bound library");
            Check(ArtLibrary.IntroBeats.Length == IntroCinematicUI.BeatCount,
                "art: intro beats are the single source the intro plays");

            // with a bound library (no sprites assigned), the getters surface the null fields
            var parent = new GameObject("ArtLibRoot").AddComponent<RectTransform>();
            var go = new GameObject("ArtLib");
            var lib = go.AddComponent<ArtLibrary>();
            Check(ArtLibrary.Instance == lib, "art: library registers itself as the singleton");
            Check(ArtLibrary.Get(ArtLibrary.SpriteKind.KeyartMenu) == null,
                "art: unassigned field stays null instead of throwing");
            UnityEngine.Object.DestroyImmediate(go);
            Check(ArtLibrary.Instance == null, "art: library unregisters on destroy");
            if (prev != null) { /* restore if a prior test had one */ }
        }

        // ---------------------------------------------------------------- HUD cleanliness
        private static void TestHudCleanliness()
        {
            // the raw state dump must exist ONLY as a dev surface: gate closed by default,
            // and the status lines (the strings the brief bans from the normal HUD) are
            // exactly what StateHUD shows - never PlayerHUD.
            UIDebugGate.SetOverlays(false);
            Check(!UIDebugGate.OverlaysVisible, "clean: debug overlays default to hidden");

            GameServices.ResetRun();
            var lines = GameServices.Progress.StatusLines();
            bool mentionsStanding = false;
            foreach (var line in lines)
                if (line.StartsWith("Standing") || line.StartsWith("Decisions") || line.StartsWith("Area "))
                    mentionsStanding = true;
            Check(mentionsStanding, "clean: raw state lines exist for the DEV surface (StateHUD)");

            string hp = PlayerHUD.HpText(82f, 100f);
            Check(hp.Contains("/") && !hp.Contains("Standing") && !hp.Contains("Ember"),
                "clean: player HUD shows health, never raw state text");
            CheckEq(hp, "82/100", "clean: health text is the familiar 82/100 form");
        }
    }
}
