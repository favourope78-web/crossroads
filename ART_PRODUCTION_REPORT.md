# ART PRODUCTION REPORT — full-pass presentation upgrade

Brief: replace every trace of prototype presentation with production-quality player-
facing content across the whole flow (launch → loading → menu → intro → gameplay →
dialogue/decisions → abilities/combat → chapters → map/menus → endings), keep all
systems/data, respect Android budgets, verify honestly.

---

## 1. Every player-facing scene upgraded

| # | Scene | What shipped |
|---|-------|--------------|
| 1 | Loading | `LoadingScreenUI`: keyart backdrop, CROSSROADS wordmark, honest eased progress (never completes before services ready), rotating authored tips, fade-out to menu |
| 2 | Main Menu | Keyart under deep-glass gradient + typographic wordmark (logo sprite), CONTINUE (save-aware, hero treatment) / NEW GAME / SETTINGS / CREDITS / QUIT, autosave status line, no developer info |
| 3 | Character introduction | `IntroCinematicUI`: 3 original story beats (PROLOGUE / BEFORE / NOW) with keyart stills, crossfades, typewriter captions, tap-advance, SKIP; plays after New Game confirm; input-locked until done |
| 4 | Tutorial | Last-summer pier room now dressed with the authored pier set; tutorial beats unchanged (systems preserved) |
| 5 | FirstLocation / Fracture Hall | Monument focal point, planters, crates, lanterns, rugs, banners, exterior city + plazas (visual pass) — plus real skybox, brighter authored lighting |
| 6 | NPC encounters | Canonical Mara/Sera avatars (one model each, consistent identity); themed dressing in every NPC room |
| 7 | Dialogue scenes | Bottom-sheet glass UI, speaker strips, relation chips, typewriter, rounded choice cards (visual pass, unchanged systems) |
| 8 | Decision scenes | Timed decisions + consequence-coloured cards; no IDs/flags shown |
| 9 | Combat areas | Annex + arena rooms dressed (barricades), CombatHUD plates, damage numbers, hit flash, camera feedback |
| 10 | Ability scenes | Ability hotbar with cooldown sweeps, line-coloured rings, VFX palette hooks (particles tracked in ART_GAPS) |
| 11 | Additional campaign locations | **All 13 campaign rooms dressed** with authored prop clusters (1 merged mesh each, room-corner placement clear of the camera orbit) |
| 12 | Map/location screen | `WorldMapUI` presentation previewed in `worldmap.png`: unlocked/locked cards with authored requirement hints over city art |
| 13 | Character/ability screen | Ability sheet + hotbar models (existing, verified by tests) |
| 14 | Cinematics | Intro sequence + chapter cards (below) |
| 15 | World-reaction scenes | Area chips, toasts, world-state-driven NPC reactions (existing systems, presentation verified) |
| 16 | Pause menu | 760×640 glass panel: PAUSED + autosave line + RESUME/SETTINGS/SAVE & CLOSE |
| 17 | Settings | 980×940 tabbed panel (GAMEPLAY/CONTROLS/GRAPHICS/AUDIO), rows with value pills |
| 18 | Save/load | CONTINUE save-aware; SAVE & CLOSE in pause; autosave pip in HUD; save status lines |
| 19 | Chapter transitions | `ChapterTransitionUI`: kicker/title/rule-line card on `CampaignChapterStartedEvent`, non-blocking, 3.0 s |
| 20 | Ending/finale scenes | Epilogue room dressed (planter — something growing again); ending flow via existing campaign system |

## 2. Character assets

- One canonical model per recurring character (13 humanoid FBX avatars, Mixamo-named
  rig, humanoid-mapped in Unity, per CHARACTER_REFERENCE.md); the SAME prefab serves
  gameplay, dialogue framing and cinematics — no per-scene look-alikes.
- Production textures: 1024–2048 albedo atlases per character (validated ≤2048, ETC2).
- 9 shared animation clips (idle/walk/run/turn/etc.) driving all avatars.
- Verified in renders: Ari present in every gameplay-perspective shot; Mara, Sera,
  civilians, warden shell in the encounter/combat frames.

## 3. Environment assets completed

- 8 AUTHORED prop-cluster meshes (Blender-built FBX, merged single-mesh, 2 material
  slots): LanternPair, CrateStack, Planter, BannerWall, PierSet, ShrineSet, Barricade,
  MarketStall — `scripts/blender_build_dressing.py`.
- 13 campaign rooms themed: pier (last_summer), lanterns (fracture_night, spire_ascent),
  shrines (under_spire, sanctuary, choirmaster), camp cargo (interlude_becoming, docks),
  barricades (long_wall, dax_arena, interlude_reckoning), market stall (market),
  planter (epilogue).
- Hall dressing (visual pass): monument, benches, planters, crates, lanterns, rugs,
  banners, exterior ground + plazas + skyline.
- 6 dressing materials + 16 authored per-location environment profiles (ambient/fog/sun).

## 4. Animation status
9 shared clips; LOD tiers throttle animator rate (tier1 ½, tier2 frozen). Idle posing
verified in renders. Gaps: per-NPC idle variants, facial animation (see ART_GAPS).

## 5. UI status
Runtime-built production UI: loading, menu (keyart+logo), intro, dialogue, decisions,
HUD (health/echoes/minimap/tracker/hotbar/interact), touch rig (joystick/look/ATK/
DODGE/pause/map), pause, settings tabs, world map, toasts, chapter cards. Raw state
text (Ember/Tide/Standing/Decisions/Area) lives ONLY behind the F3 dev gate
(StateHUD/ObjectiveHUD/MapHUD) — enforced by tests.

## 6. VFX status
Emissive material language (lanterns, glow rings, beacons, warden shell), camera
impulse feedback, hit flash, damage numbers (crit variant), cooldown sweeps. Particle
systems tracked in ART_GAPS.

## 7. Audio status
27 clips (16 recorded CC0 + 11 procedural, mixed low) wired through GameAudio event
director: per-line ability SFX, combat stingers, UI ticks, ambient beds per location
profile, calm/tension/combat music crossfades.

## 8. Art pipeline
New original art under `Assets/Game/UI/Art/` (keyart, 4 story stills, wordmark) —
generated for this project, not copied from any game. Deterministic metas + GUID
registry; scene-bound `ArtLibrary` component (GameAudio pattern) with graceful
fallbacks. Campaign dressing generated by `scripts/add_campaign_dressing.py` through
the existing JSON→builder→validation pipeline (no pipeline changes).

## 9. Performance measurements (unchanged budgets, still passing)
- Active renderers **809 / 820** · static-batchable 482+ · estimated draw calls
  **~168 / 420** with static batching · ticking behaviours 61 / 64 · 1 shadowed light ·
  far clip 120 · 3 quality tiers · all textures ≤2048, ETC2/ASTC-family formats.
- Dressing costs +13 renderers total (one merged mesh per room) with collider, static-
  batched; clusters use the existing material set (no new shaders).

## 10. Screenshots (offline renders of real scene data + real art + real layout constants)
`reference/visual_slice/`: `launch_flow.png` (loading→menu→intro→gameplay),
`art_pass_sheet.png` (12 frames), plus individual: loading, menu_artpass, intro_beat,
gameplay, dialogue, combat, chapter_card, pause, settings, worldmap, room_market,
room_sanctuary. These are honest offline previews: scene geometry/transforms/materials
are parsed from the generated FirstLocation.unity and rendered in Cycles; HUD/menus
are composited with the exact constants the C# UI code uses. NOT device captures —
device capture remains the final sign-off step (see ART_GAPS → Verification).

## 11. Final verification (this sandbox)
1. Clean-save flow — logic exercised headlessly (ResetRun → intro → menu close path);
   device walk-through pending editor access.
2. `scripts/run_tests.sh` → **2101 passed / 0 failed** (2063 prior + 38 new
   ArtProductionTests: loading progress/gating/tips, intro beats/lock/callback-once,
   chapter card formats, art fallbacks, HUD-cleanliness invariants).
3. `scripts/compile_check.sh` → 0 errors (both define sets).
4. `scripts/validate_assets.py` → VALIDATION PASSED (0 warnings), JSON↔C# parity incl.
   16 environment profiles.
5. `scripts/profile_scene.py --check` → BUDGET PASSED (numbers above).
6. Every new asset (.cs/.png/.fbx/.mat) carries a deterministic .meta GUID; no
   unresolved GUIDs; no duplicates.
7. Android APK build — not possible here (no Unity editor/SDK); UNITY_CLOUD_BUILD
   setup doc remains the build path.

Acceptance: CODE ✓ (all gates green). VISUAL — the player-facing screens no longer
present as prototype (see screenshots); final device sign-off listed in ART_GAPS.
