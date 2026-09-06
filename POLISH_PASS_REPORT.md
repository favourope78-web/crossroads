# CROSSROADS — Production Polish Pass Report

Scope: a full production-polish pass across the playable game **without redesigning core
mechanics or story**. Every change reuses the existing systems (EventBus, GameState,
SaveSystem, Campaign/Locations/Objectives runners, the JSON → generator → asset pipeline)
and every generated asset still passes `scripts/validate_assets.py`.

Commits (all on `main`, pushed):

| Commit | Pass | What |
|---|---|---|
| `5ea764c` | 1 | URP mobile render pipeline (3 tiers), canonical URP/Lit shader GUID on all 41 materials, static profiler + perf budget |
| `c9acc64` | 2 | Ari in device builds, static batching, shadow policy, cinematic camera, location transitions, controls |
| `34322bb` | 3 | Procedural audio set (25 clips) + event-driven audio director, pooled combat VFX, save recovery |
| `2fdf0d7` | 4 | Android build config parity (IL2CPP Master / stripping / Vulkan / ASTC), presentation-layer tests |
| `ad4c36c` | 5 | Canonical character silhouettes for every recurring character, dialogue/toast motion, speaker colour language |
| `9073687` | 6 | Procedural character reactions (hit squash / wind-up / alert / defeat crumple) on primitive rigs |
| *(this)* | 7 | Docs: `ANDROID_BUILD.md`, `DEVELOPMENT_PLAN.md` 0.5, this report |

---

## 1. Profiling first — what was actually wrong

`scripts/profile_scene.py` (new) parses the generated scene YAML, materials, textures and
project settings offline, so the game can be measured in CI and in this sandbox where Unity
is not available. Baseline at `a7bc47f` (end of the campaign content pass):

| Finding (baseline) | Impact on a mid-range phone |
|---|---|
| **41 / 41 materials referenced a stale URP/Lit GUID** (`9335e4…`) — unresolved in a fresh project import | Every surface renders magenta (error shader); no lighting at all |
| **No URP pipeline asset, no `GraphicsSettings.asset`** (`m_CustomRenderPipeline: 0`) | URP package installed but project would run the built-in pipeline → all URP materials invalid; no tiering |
| **0 of 818 renderers static-batchable** (`m_StaticEditorFlags: 0` everywhere) | 704 draw calls / frame worst case, all dynamic, ~700 SetPass/batches |
| **0 shadow casters, 1 unshadowed light, no ambient/fog tuning** | Flat look; every extra "atmosphere" hack would have cost overdraw |
| **Ari absent from device builds** — spawned by an editor-only bootstrap | APK launched into an empty hall (no player, no camera target) |
| Camera did 2 SphereCasts + `FindFirstObjectByType` per frame while target null | Constant scene scan on device until Ari existed (never, see above) |
| 49 `EnemyAgent`s ticked full-rate even when dormant / off-screen | Wasted CPU in the biggest-room worst case |
| 0 audio files, 0 VFX beyond ability pulse | No feedback layer at all |
| SaveSystem: atomic write but no recovery from a truncated main file | Corrupt save = lost campaign |
| IL2CPP configuration **Debug** (`il2cppCompilerConfiguration: 0`), no stripping policy | Slow native code, bigger APK, longer load |

## 2. Measurable results (static profile, `python3 scripts/profile_scene.py`)

| Metric | Before `a7bc47f` | After `9073687` | Budget (`docs/PERF_BUDGET.json`) |
|---|---|---|---|
| Renderers total / active | 818 / 704 | 872 / 739 | ≤ 820 active |
| Static-batchable / dynamic | **0** / 704 | **425** / 314 | static fraction ≥ 0.55 (0.575) |
| Estimated draw calls (worst case, all rooms in frustum) | **~704** | **~335** | ≤ 420 |
| Shadow casters (structure only; floors/glazing/holo receive) | 0 | 633 | — |
| Realtime lights / shadowed | 1 / 0 | 1 / 1 (soft, tiered) | ≤ 2 / ≤ 1 |
| Ticking MonoBehaviours (Update) | 56 | 61 (GameAudio, CombatVFX, fader, camera, joystick) | ≤ 64 |
| Camera far / near / HDR / MSAA / occlusion | 120 / 0.1 / off / off / on | 120 / 0.1 / off / off (High: 2×) / on | far ≤ 120 |
| Materials resolvable | **0 / 41** (stale GUID) | 45 / 45 URP/Lit, instancing 42/45 | — |
| Quality tiers → URP assets | Low, Balanced → **none** | Low, Balanced, High → `URP_Low/Balanced/High` | 3 tiers required |
| Scene file | 2.2 MB / 82 032 lines | 2.4 MB / 89 999 lines | ≤ 3.2 MB |
| Character texture | Ari_Albedo 2048×1024 ASTC 6×6 (855 KB) | unchanged (already mobile-formatted) | — |
| Audio | 0 | 25 clips, mono 22.05 kHz, 2.8 MB total | — |
| IL2CPP config / stripping / graphics APIs | Debug / default / default | Master / Medium + engine stripping / Vulkan → GLES3, ASTC | — |

Notes on the numbers:
* The +54 renderers are the canonical character silhouettes (hair masses, hoods, glasses,
  plates, holo coils) — deliberately spent, still inside budget, and all correctly flagged
  static/dynamic.
* "Estimated draw calls" is a per-scene worst case with every room in the frustum; the
  far plane (120 m) and exp² fog (0.014) mean a typical hall frame sees ~1/3 of that.
* Nothing here replaces an on-device Unity Profiler session — see §6.

## 3. What changed, by the 13 requested areas

1. **Character models / materials** — all 41 (now 45) materials use the canonical URP/Lit
   GUID; new shared character materials `M_Char_Skin`, `M_Char_White_Top`,
   `M_Char_Trousers`, `M_Char_Archivist_Plate`. Ari is a real prefab instance in the scene
   (mesh, controller, albedo) instead of an editor-only spawn.
2. **Recurring-character consistency** — `gen_campaign_scene.py` now builds each recurring
   character from a single canonical recipe (`CANON`) reused by *every* appearance: Mara
   (prologue / C2 / C3 / ally / hall) = dark hair mass + high bun/tail, hood or dress, white
   top, trousers; Dax = side-swept chestnut hair, glasses + bridge, tee; Archivist =
   floor-length silver hair, plate bodysuit, gem line, holo coils; mentors (Kael/Odalys/Bran)
   = line token + coat. Speaker colours in dialogue follow the same canon.
3. **Environment / lighting / atmosphere** — sun light with soft shadows (tier-dependent
   distance/resolution), tuned ambient + exp² fog, static/shadow flags per kit piece,
   one global post volume (bloom 0.55, vignette 0.28, exposure/contrast/saturation,
   Neutral tonemap), URP renderer with SRP batcher.
4. **Camera + cinematics** — `ThirdPersonCameraController` rewritten: cached target,
   no per-frame scene scan, collision pull-in/ease-out, indoor bias, `Cinematic` mode for
   story beats, `CombatBlend` (wider/lower framing while engaged).
5. **VFX** — `CombatVFX`: zero-allocation pooled shards (bursts, streaks, rings, pillars,
   motes) driven by combat/ability/story events; palettes per line (ember/tide/stone/
   hollow/kinetic); procedural body reactions on rigs (hit squash, wind-up anticipation,
   alert hop, defeat crumple).
6. **UI/UX** — dialogue sheet slide/fade, speaker accent strip + coloured name, staggered
   decision-card reveal, timer shows tenths; toasts lift/fade; pause menu gains a live
   Quality stepper; safe-area layout kept.
7. **Mobile controls** — `VirtualJoystick` rewritten (deadzone, clamp, return spring,
   `Held`), look pad and `MobileControlsUI` cache references instead of per-frame lookups;
   combat buttons appear only while engaged.
8. **Audio** — `scripts/gen_audio.py` synthesises 25 placeholder clips (UI, footsteps,
   attacks per line, hits, dodge, defeat, 4 ambient beds, 3 music states) with import metas;
   `GameAudio` director maps abilities/locations/combat state to clips, ambient crossfades
   by location profile, music states Calm/Tension/Combat.
9. **Animation transitions / reactions** — Ari controller triggers (Attack/Dodge/Hit/Defeat)
   fire from `PlayerCombatController`; enemies react via `CombatVFX.React`; footstep events
   from the controller.
10. **Loading / transitions** — `LocationTransitionFader` rewritten (unscaled-time fade,
    `Transitioning` guard, arrival applied at black, no double-trigger).
11. **Save/load reliability** — `SaveSystem` writes a `.bak` mirror and recovers from a
    truncated/corrupt main file (`LastLoadSource` reports which copy loaded); v1→v5
    migrations kept; autosave mirror test extended.
12. **Error handling** — null-safe audio/VFX (tests instantiate them without a scene),
    `FirstLocationBootstrap` no-op when a Player exists, fader/camera tolerate missing UI or
    target, settings file corruption tolerated.
13. **Android performance** — see §2; plus `EnemyAgent` dormant early-out and ¼-rate idle
    LOD beyond 28 m, IL2CPP Master + stripping, Vulkan→GLES3, ASTC, Low tier caps at 30 fps
    with render scale 0.8.

## 4. Verification run (all green at `9073687`)

| Gate | Command | Result |
|---|---|---|
| Automated tests (headless, Mono) | `bash scripts/run_tests.sh` | **1909 passed, 0 failed** (was 1899 at `2fdf0d7`, 1862 before the pass) |
| Compile check (two define sets, Unity stub) | `bash scripts/compile_check.sh` | exit 0 / exit 0 |
| Asset validation (7 sections) | `python3 scripts/validate_assets.py` | **PASSED, 0 warnings** |
| Scene validation + perf budget | `python3 scripts/profile_scene.py --check` | **BUDGET PASSED** |
| New-game test | suites [5] [13] [55] [57] | pass |
| Save/load test | suites [7] [14] [15] [27] [37] [39] [56] [62] [63] | pass |
| Branch A playthrough (Ember → ASHEN CROWN) | suite [81] | pass |
| Branch B playthrough (Tide → TIDE'S EMBRACE) | suite [82] (+ [83]/[84] Stone, Hollow, Balance, Martyr) | pass |
| Ability test | suites [22]–[27] [33] [45] [54] | pass |
| Combat test | suites [40]–[49] [53] | pass |
| Presentation layer (audio director, VFX pool + reactions, camera, fader, speaker palette) | suite [56] "Polish pass" | pass |
| **Android build test** | `.github/workflows/android-apk.yml` | **not run** — CI still needs the repo secret `UNITY_LICENSE`; no Unity/Android SDK in this environment |

## 5. Files that matter

* `scripts/profile_scene.py`, `docs/PERF_BUDGET.json` — profiler + budget gate.
* `scripts/gen_render_settings.py` → `Assets/Settings/*`, `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset`.
* `scripts/gen_audio.py` → `Assets/_Project/Audio/**` (25 WAV + metas).
* `scripts/gen_campaign_scene.py` (`CANON` rigs), `scripts/gen_firstlocation_scene.py` (static flags, light, camera data, post volume, Ari prefab instance, hall rigs).
* `Assets/_Project/Scripts/Gameplay/World/GameAudio.cs`, `Gameplay/Combat/CombatVFX.cs`, `UI/DialogueUI.cs`, `UI/ToastUI.cs`, `UI/LocationTransitionFader.cs`, `UI/VirtualJoystick.cs`, `Core/SaveSystem.cs`, `Assets/Game/Scripts/ThirdPersonCameraController.cs`, `Assets/Editor/AndroidDevBuild.cs`.
* `scripts/unity-stub/UnityStub.cs` — extended (CanvasGroup, audio, particles) so the UI/VFX code compiles headless.

## 6. Remaining issues (honest list) — status after the release pass

> Updated by the release pass (`865dc15` → `c69098c` → final). Each item keeps its original
> number; **RESOLVED** items say which commit closed them and what to look at.
> The authoritative release status is `FINAL_RELEASE_REPORT.md`.

1. **No on-device numbers.** — **STILL OPEN (non-blocking, cannot be closed here).** Unity and
   the Android SDK/adb are unavailable in this environment, and CI cannot build because the
   repository has no `UNITY_LICENSE` secret. Every performance figure in this report and in
   `FINAL_RELEASE_REPORT.md` §7 is a static estimate from `scripts/profile_scene.py`. First
   thing to do with a device: add the licence secret, build the release APK, attach the
   Profiler, and check Batches/SetPass, GPU frame time (Vulkan vs GLES3) and the
   `EnemyAgent`/`NpcAgent` Update cost against `docs/PERF_BUDGET.json`.
2. **URP YAML unverified in the editor.** — **STILL OPEN (non-blocking).** `Assets/Settings/*.asset`,
   `GraphicsSettings.asset`, `QualitySettings.asset` and `DynamicsManager.asset` are generated
   for URP 17 from the public serialisation format and cross-checked by `validate_assets.py`
   (§7 render checks), but no Unity 6000.0.23f1 editor was available to load them. If the
   editor re-serialises them on first open, commit that diff — expected and harmless.
3. **Audio is synthesised placeholder content.** — **RESOLVED for all SFX (`c69098c`)**, 16 of 27
   clips are now recorded CC0 sources (`reference/audio_source/`, provenance in
   `SOURCES.json`, licence in `LICENSE.txt`), decoded/normalised by `gen_audio.py`. **The 11
   remaining procedural clips are the only production placeholders left**: the four Fracture
   ability palettes, four ambient loops and three music beds — listed by
   `reference/audio_source/AUDIO_STATUS.json` and enforced by the validator/test [86].
4. **Character rigs are primitives.** — **RESOLVED (`865dc15`).** 13 Humanoid character models
   (`Assets/_Project/Art/Characters/<Name>/<Name>.fbx`, 1 376–2 344 tris, 22 Mixamo-named bones,
   1024² atlases) built by `scripts/blender_build_characters.py` from the canonical sheets, one
   prefab per character (`Assets/_Project/Prefabs/Characters/<Name>.prefab`) used by every
   scene binding; identities match `CHARACTER_REFERENCE.md`.
5. **Ari's avatar is Generic.** — **RESOLVED (`865dc15`).** All 13 FBX metas import as Humanoid;
   the 9 shared clips (Idle, Walk, Run, Talk, Alert, Attack, Hit, Dodge, Defeat) retarget through
   the single `Character_Controller`.
6. **`NpcAgent` has no distance LOD.** — **RESOLVED (`865dc15`).** `CharacterAvatar` tiers:
   < 14 m full rate; 14–32 m animator + brain at ½ rate, no shadow casting; ≥ 32 m animator
   frozen + brain at ¼ rate; a talking NPC is always tier 0. Covered by test [86].
7. **Post-process volume cost on Low tier.** — **RESOLVED (`c69098c`).** `PostProcess_Low.asset`
   (no bloom, vignette 0.2, same grade) is swapped in by `QualityTierApplier` on the Low tier;
   Balanced/High keep `PostProcess_Global`. Quality tiers now live in a proper
   `ProjectSettings/QualitySettings.asset` (+ `DynamicsManager.asset`), default Balanced.
8. **Build test outstanding.** — **STILL OPEN (non-blocking, cannot be closed here).** No Android
   APK was produced: no Unity editor/Android SDK locally, and every CI run fails at
   `game-ci/unity-builder@v4` with "Missing Unity License File and no Serial" (0 repository
   secrets). What *was* verified from a clean clone is listed in `FINAL_RELEASE_REPORT.md` §9.

Found and fixed during the release QA (P6, `c69098c`): after the prologue Mara's bond is ≥ 8,
so her `confide` interaction (authored before `talk`, gated only on bond) shadowed the First
Light conversation — a real progression blocker on a fresh save. `confide` now also requires
`dec_c1_hall_first_light` to be resolved (`scripts/story_content.json` + `StoryContentBuilder.cs`),
caught by the new end-to-end test [87] `TestReleaseQaSequence`.
