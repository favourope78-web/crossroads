# CROSSROADS — Final Release Report

Release-candidate pass on top of the polish pass (`a2343d9`). Nothing was restarted or
redesigned; every item below was produced through the existing pipelines
(`scripts/story_content.json` → generators → `Assets/`, checked by `scripts/validate_assets.py`).

**Verification rule used throughout this document:** an item is marked *verified* only if it was
executed in this pass, in this repository state. Anything that needs a Unity editor, an Android
SDK or a physical device is marked **NOT VERIFIED** with the exact reason. No device or APK
numbers are estimated or invented anywhere in this report.

---

## 1. Final commit

| | |
|---|---|
| Branch | `main` |
| Final release commit | **see the last line of `git log --oneline -1` — the commit that adds this file** (hash reported in the hand-over message; the hash cannot be written into the commit it identifies) |
| Release-pass commits (oldest → newest) | `865dc15` P1/P2 cast models + LOD · `c69098c` P3/P4/P6 tiers, audio, QA chain · `55c4149` deterministic/strict audio generator · `ae653b7` release-APK entry point, docs · *final* (this report) |
| Base | `a2343d9` (end of the polish pass) |
| Working tree at hand-over | clean (`git status --short` empty), `origin/main` == `HEAD` (verified by `git fetch` after push) |
| Tracked junk check | `git ls-files \| grep -E '\.(exe\|dll\|pyc\|log\|tmp\|bak)$'` → nothing; no `__pycache__`, no build outputs tracked (861 tracked files, 70 MB, largest = the 21 MB reference video) |

**Clean-checkout reproducibility (verified, `55c4149`/`ae653b7`):** a fresh `git clone` into
`/tmp/cleanbuild` followed by the whole generator chain (`gen_builder_campaign.py`,
`gen_story_content.py`, `gen_audio.py`, `gen_character_assets.py`, `gen_firstlocation_scene.py`,
`gen_render_settings.py`) produced **0 modified files** — the committed assets are exactly what the
canonical sources generate. Then, in that same clean clone: validator PASSED (0 warnings), both
compile configurations exit 0, 1994/1994 tests, perf budget PASSED.

While doing this the audio generator was found to be *non*-reproducible on a machine without
`ffmpeg` (it silently synthesized stand-ins for the 16 recorded clips) — fixed in `55c4149`:
missing recordings/decoders are now a hard error, and every clip is byte-identical across runs
(per-clip RNG seeds; verified with `md5sum` over two consecutive runs).

---

## 2. Test suite

| Item | Result |
|---|---|
| Command | `bash scripts/run_tests.sh` (mono `mcs` 6.x, C# sources compiled against `scripts/unity-stub/UnityStub.cs`) |
| Result | **1994 passed, 0 failed** — identical on the working tree and on the clean clone |
| Numbered scenarios | 77 (`[1]`–`[87]`, numbering has gaps) in 7 suites: Flow, World, Campaign, Combat, Location, MobileExperience, CampaignContent (5 362 lines) |
| Compile check | `bash scripts/compile_check.sh` → `ENABLE_LEGACY_INPUT_MANAGER` exit 0, `ENABLE_INPUT_SYSTEM` exit 0 (16 warnings, all CS0414 "private field assigned but never used" in UI bootstraps — cosmetic) |
| Editor-only scripts | `Assets/Editor/AndroidDevBuild.cs`, `Scripts/Editor/CrossroadsPrototypeSetup.cs`, `CharacterTestBootstrap.cs` are outside the stub build (they need `UnityEditor`); they were compiled against a throw-away `UnityEditor` API stub after this pass's edits → 0 errors |
| Asset validation | `python3 scripts/validate_assets.py` → **VALIDATION PASSED (0 warnings)**: 557 scene roots, 16 locations / 10 NPCs / 19 enemies / 84 entity keys / 18 variants under contract, 13 humanoid models + 9 clips + 62 avatar bindings, tiers wired, 27 clips bound to 27 `GameAudio` fields, camera/volume wiring |
| Perf budget | `python3 scripts/profile_scene.py --check` → **BUDGET PASSED** against `docs/PERF_BUDGET.json` |

New in this pass: test **[87] `TestReleaseQaSequence`** (end-to-end clean-save chain, §8) and the
tier/LOD/audio checks in **[86]** + MobileExperience additions (13 new assertions).

---

## 3. Characters & animation

**Status: DONE — all 13 characters are real skinned Humanoid models; verified by generator +
validator + test [86]. Visual sign-off in a Unity viewport: NOT VERIFIED (no editor).**

| Character | Tris | Height | Model / prefab |
|---|---|---|---|
| Ari (player) | 1 900 | 1.78 | `Art/Characters/Ari/Ari.fbx` → `Prefabs/Characters/Ari.prefab` |
| Mara / Mara_Dress (prologue) | 2 104 / 2 344 | 1.68 | one body, two outfits, same face atlas |
| Dax, Archivist, Kael, Odalys, Bran, Sera | 1 376 – 2 200 | per `CHARACTER_REFERENCE.md` | one prefab each |
| Civilian, Soldier_A/B/C | 1 376 – 1 900 | | shared armour palette, distinct silhouettes |

* Built from the canonical sheets in `reference/concept/` by
  `scripts/build_character_atlases.py` (1024² atlases, `reference/concept/build_data/*_atlas.json`)
  → `scripts/blender_build_characters.py` (Blender 4.3.2; 22 Mixamo-named bones, deterministic
  skinning) → `scripts/gen_character_assets.py` (Humanoid `.fbx.meta`, URP/Lit materials, prefabs).
* **One canonical prefab per recurring character**, referenced by all 62 scene bindings
  (12 NPC + 50 enemy; 53 active at boot). `CrossroadsPrototypeSetup` / `CharacterTestBootstrap`
  now consume that same prefab instead of building a second `Prefabs/Player/Ari.prefab` (`ae653b7`).
* Ari's avatar is **Humanoid** (was Generic); all metas `animationType: 3`.
* Animation library — 9 shared clips, retargeted through the single `Character_Controller`
  (params `Speed/Talking/Turning`, triggers `Attack/Dodge/Hit/Defeat/Alert`):
  `Idle, Walk, Run, Talk (interaction), Alert, Attack, Hit (reaction), Dodge, Defeat`.
* FBX rebuild reproducibility: re-running the Blender build on the clean clone yields the same
  node/property layout but not byte-identical files (Blender writes a creation timestamp; 16–32 B
  header difference). The committed FBX are therefore the canonical binaries; the scripts
  regenerate equivalents.

Not done: facial animation, cloth, per-character bespoke clips (design didn't require them, and
no motion source is available here).

---

## 4. NPC performance / LOD

**Status: DONE — implemented in `CharacterAvatar.cs`, exercised by test [86]; runtime cost on a
phone NOT MEASURED (see §7).**

| Tier | Distance to player | Animator | `NpcBrain` / AI | Shadows |
|---|---|---|---|---|
| 0 | < 14 m | full rate | full rate | cast |
| 1 | 14 – 32 m | every 2nd frame | every 2nd tick | off |
| 2 | ≥ 32 m | frozen (pose held) | every 4th tick | off |

* A talking NPC (active dialogue partner) is pinned to tier 0 regardless of distance.
* Applies to every `NpcAgent` and `EnemyAgent` avatar; dormant enemies stay at tier 2 until alerted.
* Population: 53 active avatars at boot = 108 skinned draw calls / 100 316 triangles, replacing the
  previous 287 primitive renderers. Within `docs/PERF_BUDGET.json` (active renderers 739/820,
  estimated draw calls ~156/420 with static batching, ticking behaviours 61/64).

---

## 5. Audio

**Status: architecture unchanged (`GameAudio` event → clip fields, 27/27 bound); 16 of 27 clips are
now real recordings; the remaining 11 are the ONLY production placeholders left in the project.**

| Status | Count | Clips |
|---|---|---|
| **Recorded — CC0 (Kenney.nl)**, decoded/trimmed/normalised by `gen_audio.py`; sources + provenance in `reference/audio_source/` (`*.ogg`, `SOURCES.json`, `LICENSE.txt`) | 16 | `sfx_attack_swing`, `sfx_attack_hit`, `sfx_dodge`, `sfx_player_hurt`, `sfx_enemy_defeat`, `sfx_enemy_alert`, `sfx_enemy_windup`, `sfx_footstep`, `sfx_ui_tap`, `sfx_ui_confirm`, `sfx_decision_lock`, `sfx_save`, `sfx_transition`, `sfx_dialogue_open`, `sfx_objective`, `sfx_ability_unlock` |
| **Procedural placeholder** (marked in `reference/audio_source/AUDIO_STATUS.json`, generator header, `ASSET_PIPELINE.md` §7.1; checked by validator + test [86]) | 11 | `sfx_ability_{ember,tide,stone,hollow}`, `amb_{hall,dusk_wind,water,hollow}`, `mus_{calm,tension,combat}` |

* Why 11 remain procedural: no CC0/licensed recording matches the four Fracture ability palettes
  (Ember crackle, Tide shimmer, Stone impact, Hollow drone), and there is no composed score or
  location ambience source. They are deterministic, loudness-matched and loop-safe, and swap in
  place when composed audio exists (drop the file under `reference/audio_source/`, add it to
  `SOURCES.json`, re-run `gen_audio.py`).
* Event mapping verified: every gameplay/dialogue/combat/UI event in `GameAudio` has a clip
  (validator "27 GameAudio fields bound"); test [86] asserts the recorded/placeholder census.
* Total 27 clips, 2.8 MB, 22.05 kHz mono; Vorbis + DecompressOnLoad (SFX) / CompressedInMemory
  (ambient) / Streaming (music) import settings.

---

## 6. URP / quality tiers

**Status: DONE in YAML + runtime code; NOT VERIFIED in a Unity editor (none available — see §9).**

| Tier | URP asset | Shadows | Render scale / LOD bias | Post volume | Target |
|---|---|---|---|---|---|
| Low | `Assets/Settings/URP_Low.asset` | off | 0.8 / 0.7 | `PostProcess_Low` — **no bloom**, vignette 0.2, dusk grade | 30 fps |
| Balanced (default) | `URP_Balanced.asset` | hard, 20 m, 1024 | 1.0 / 1.0 | `PostProcess_Global` — bloom 0.55 / thr 1.05, vignette 0.28 | 60 fps |
| High | `URP_High.asset` | soft, 35 m, 2048, MSAA 2× | 1.0 / 1.0 | `PostProcess_Global` | 60 fps |

* Bloom removed from Low (was on all tiers): `QualityTierApplier` swaps the scene volume profile
  when the tier changes (Pause menu → tier), so the Fracture emissive look is kept on
  Balanced/High and the Low tier saves the bloom pyramid.
* Quality levels now live in a proper `ProjectSettings/QualitySettings.asset` (`!u!47`), physics
  defaults in `DynamicsManager.asset` (`!u!55`) — previously both were mis-bundled inside
  `ProjectSettings.asset`. `GraphicsSettings.asset` → Balanced, SRP batcher on.
* Fog (exp², 0.014), one shadowed directional light, far clip 120 m, HDR off, dynamic resolution
  on — all within `docs/PERF_BUDGET.json`.
* **What is NOT verified:** that Unity 6000.0.23f1 / URP 17.0.3 accepts the hand-authored YAML
  without re-serialising it, and how each tier *looks*. The YAML follows the public serialisation
  format and is cross-checked structurally by the validator; if the editor rewrites these files on
  first open, commit the diff.

---

## 7. Performance measurements

**On-device: NOT MEASURED.** No physical Android device, no Unity editor, no Android SDK/adb exist
in this environment, and no APK could be built (§9). The numbers below are **static analysis of
the committed scene** by `scripts/profile_scene.py` — they bound draw-call/renderer/tick counts,
they do not measure FPS, GPU time, CPU time, memory or load time.

| Metric (static, `profile_scene.py`) | Value | Budget (`docs/PERF_BUDGET.json`) | |
|---|---|---|---|
| Scene file | 2.4 MB, 1 070 GameObjects, 557 roots | ≤ 3.2 MB | ✔ |
| Renderers | 872 total / **739 active** | ≤ 820 active | ✔ |
| Static-batchable fraction | 0.575 (425 static / 314 dynamic) | ≥ 0.55 | ✔ |
| Estimated draw calls | 560 unbatched → **~156** with static batching | ≤ 420 | ✔ |
| Skinned characters | 53 avatars, 108 skinned draws, 100 316 tris | — | |
| Ticking `MonoBehaviour`s | 61 (of 167) | ≤ 64 | ✔ |
| Realtime lights / shadowed | 1 / 1 | ≤ 2 / ≤ 1 | ✔ |
| Camera far clip | 120 m, HDR off, MSAA per tier, dynamic res on, occlusion on | ≤ 120 | ✔ |
| Materials / shaders | 40 in scene, all URP/Lit, GPU instancing on 67/70 | — | |
| Textures | 13 character atlases 1024² (Ari 2048×1024), ASTC (fmt 50), 512–855 KB each | ≤ 2048, ASTC/ETC2 | ✔ |
| Audio | 27 clips, 2.8 MB | — | |

Fields that a device profile must fill in before shipping: FPS per tier, GPU frame time (Vulkan vs
GLES3), CPU main-thread time (esp. `NpcAgent`/`EnemyAgent`/`CharacterAvatar` update),
memory (PSS), Batches/SetPass from the Frame Debugger, cold-start and scene-load times.

---

## 8. Campaign / branch QA (from a clean save)

Executed as automated end-to-end scenarios against the real gameplay code (`GameServices`,
`StoryContentBuilder`, `NpcBrain`, `EnemyBrain`, `SaveSystem`) compiled against the Unity stub.
**All PASS.** Not executed: a human play-through in the editor/on device (no editor).

| Scenario | Test | Result |
|---|---|---|
| New Game → tutorial → exploration → NPC interaction → dialogue → decision → branching consequence → objective → ability unlock → ability use → **live** `EnemyBrain` combat → world-state change → NPC reaction → location transition → save → restart → restored state | **[87] `TestReleaseQaSequence`** (plus [5][13][55][56] new game / full loop) | PASS |
| Ember line → Kael → Dax pressed → Docks burned → **Ashen Crown** | [81] | PASS |
| Tide line → Odalys → Dax spared + truce → Sanctuary held → Mara saved → **Tide's Embrace** | [82] | PASS |
| Stone line → Bran → Long Wall → Dax yielded → **The Unmoved**; refusal → **The Long Way Home** | [83] | PASS |
| **Hollow Throne** (absorb Dax), **Balance** (hybrid affinities), **Martyr's Dawn**, mid-campaign restart | [84] | PASS |
| Crisis failure → recovery route; hesitation timeout; Dax as final enemy; NPC state titles | [85] | PASS |
| Save/restore integrity (world flags, objectives, bonds, abilities, settings, location) | [7][14][15][27][37][39][56][62][63][84] | PASS |
| NPC reactions to world state | [17]–[21] | PASS |

**Progression bug found and fixed by this QA (`c69098c`):** on a fresh save the prologue leaves
Mara's bond at ≥ 8, so her `confide` interaction (authored before `talk`, gated only on bond)
became her default interaction and **shadowed the First Light conversation** — the player could
not reach the first decision through the normal prompt. `confide` now additionally requires
`dec_c1_hall_first_light` to be resolved (`scripts/story_content.json` + `StoryContentBuilder.cs`,
regenerated assets). No other progression corruption was found across the six route tests.

---

## 9. Android build

**Result: APK NOT BUILT. Nothing about an APK, its launch, or device behaviour is claimed.**

What was tried and exactly why it could not be completed:

| Path | Outcome |
|---|---|
| Local Unity editor (`unity-editor`, Unity Hub) | not installed; not installable here (no licence, 2 GB RAM sandbox) |
| Android SDK / NDK / Gradle / `adb` | not installed |
| GitHub Actions `.github/workflows/android-apk.yml` (`game-ci/unity-builder@v4`) | every run fails at the builder step **before Unity starts**: `##[error] Missing Unity License File and no Serial was found` — the repository has **0 Actions secrets** (`UNITY_LICENSE` / `UNITY_EMAIL`+`UNITY_PASSWORD` absent). Latest runs: `34028721026` (`ae653b7`, both jobs), `34028373721` (`55c4149`), `34028316670` (`c69098c`) — all `failure` at step 4 with that message |

What *was* prepared and verified to compile (against an editor API stub):

* `Assets/Editor/AndroidDevBuild.cs` — `Configure()` (API 24+, ARM64+ARMv7, IL2CPP **Master**,
  managed stripping Medium, engine stripping, Vulkan → GLES3, ASTC, landscape, Balanced tier
  default) plus a new **`BuildReleaseApk()`** (`BuildOptions.None` → `Builds/Crossroads.apk`)
  next to the existing `BuildDevApk()` (development + profiler → `Builds/CrossroadsDev.apk`).
* `scripts/build_android_apk.sh [UnityPath] [dev|release]` — batch-mode wrapper.
* Workflow now has two jobs, `build` (dev APK) and `build-release` (`buildMethod:
  Crossroads.EditorTools.AndroidDevBuild.BuildReleaseApk`, artifact `Crossroads-Release-APK`);
  the unsupported `androidBuildType` input was removed.

**To produce the APK:** add `UNITY_LICENSE` (a `.ulf` from game-ci's activation flow) — or
`UNITY_EMAIL` + `UNITY_PASSWORD` for a Unity 6 personal licence — as repository secrets, then push
or *Actions → android-apk → Run workflow*. Both artifacts appear on the run. Then
`adb install -r Crossroads.apk`, launch, and fill §7.

---

## 10. Remaining non-blocking limitations

1. **No on-device performance data** (§7) and **no APK** (§9) — both blocked on tooling/licence
   that does not exist in this environment. The project is release-*candidate* quality from the
   code/content/asset side; it is not device-certified.
2. **URP YAML never opened in the target editor** (§6). Expected worst case: Unity re-serialises
   `Assets/Settings/*.asset` / `ProjectSettings/{Quality,Graphics}Settings.asset` on first import
   → commit the diff. Structural checks pass.
3. **11 procedural audio placeholders** (§5): four ability palettes, four ambient loops, three
   music beds — clearly marked, swap-in path documented.
4. **Character quality ceiling**: 1.4–2.3 k-tri stylised models with 1024² atlases, 22-bone rigs,
   9 shared clips; no facial rig, no cloth, no bespoke per-character animation. Identities and
   consistency across scenes are canonical; fidelity is prototype-plus, not AAA.
5. **FBX rebuilds are semantically but not byte-identical** (Blender timestamp) — commit the
   generated FBX, don't rely on re-running Blender in CI to reproduce them bit-for-bit.
6. **Static perf estimates only** for draw calls/renderers; real batching depends on the SRP
   batcher and the device driver.
7. **16 CS0414 compiler warnings** (unused private fields in UI bootstraps) — cosmetic.
8. Dev-only helpers (`CharacterTest.unity`, `CrossroadsPrototypeSetup`) remain in the repo as
   reference tooling; they are editor-only and not in the build scene list
   (`EditorBuildSettings` pins `FirstLocation.unity` only).

---

### How to re-verify everything this report claims (≈ 1 minute, no Unity needed)

```bash
git clone <repo> crossroads && cd crossroads
sudo apt-get install -y mono-mcs mono-runtime ffmpeg          # ffmpeg only needed to regenerate audio
python3 scripts/gen_builder_campaign.py && python3 scripts/gen_story_content.py && python3 scripts/gen_audio.py \
  && python3 scripts/gen_character_assets.py && (cd scripts && python3 gen_firstlocation_scene.py) \
  && python3 scripts/gen_render_settings.py && git status --short      # → empty: assets == generated
python3 scripts/validate_assets.py        # VALIDATION PASSED (0 warnings)
bash scripts/compile_check.sh             # exit=0 twice
bash scripts/run_tests.sh | tail -1       # RESULT: 1994 passed, 0 failed
python3 scripts/profile_scene.py --check  # BUDGET PASSED
```
