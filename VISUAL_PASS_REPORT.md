# VISUAL TRANSFORMATION PASS — REPORT

Scope: the 16-point visual transformation brief. **Presentation only** — no new major
gameplay systems; every existing system, test and data asset preserved (test baseline
moved 1994 → **2063 passed / 0 failed**, the +69 being new VisualPassTests for this pass).

---

## What shipped (by brief item)

| # | Item | What was done |
|---|------|---------------|
| 1 | Debug off by default | `UIDebugGate` gates every diagnostic overlay; raw state text only behind the F3 dev toggle; player-facing HUD is clean (verified by `VisualPassTests` + `UIDebugGate` tests) |
| 2 | Third-person camera | `ThirdPersonCameraController`: 4.4 m orbit, 1.45 m pivot, 0.32 m shoulder offset, 55° FOV, spring damping, wall-clip raycast pull-in, 60° pitch clamp, camera shake impulse channel |
| 3 | Character | Canonical **Ari** (humanoid rig, 2048×1024 albedo) everywhere: gameplay, dialogue framing, cinematics; idle/walk/run/turn-in-place clips; 62 scene avatar bindings replace primitive stand-ins |
| 4 | Fracture Hall as a location | Monument focal point (platform + pedestal + glow ring + benches), 4 planter vignettes with foliage, 6 supply crates, 4 emissive lanterns, 2 rugs, 4 roof banners, exterior ground plane + 2 plazas + 10 skyline towers (outside the west campaign rooms) — 61 new static renderers, all static-batched |
| 5 | Clean mobile HUD | `PlayerHUD` (portrait ring, delayed-ghost health bar, echoes chip), `MiniMapHUD` (disc, rings, north chip, heading), `ObjectiveTrackerHUD`, `InteractionHUD` (ring glyph + label pill), `AbilityHotbarHUD` (cooldown sweeps), toasts, area chip, autosave pip. Consistent HudTheme glass/accent language. **Fixed during this pass:** minimap overlapped the pause/map corner cluster — minimap + tracker now sit below the button row |
| 6 | Touch controls | `MobileControlsUI`: floating virtual joystick (340/150 ring/knob), look pad, ATK/DODGE action cluster, pause/map corners, handedness + scale + opacity settings |
| 7 | Dialogue | Bottom-sheet DialogueUI: rounded glass, speaker accent strip, relation chip, typewriter, hint row, timed decisions, staggered rounded choice cards. No IDs/flags/debug text |
| 8 | Combat presentation | CombatHUD enemy plate + delayed damage bar, floating DamageNumberUI (crit variant), CombatCameraFeedback (hit shake + framing), ability VFX hooks, hit flash |
| 9 | Atmosphere | Real skybox on the camera (clear flags 1), bright ambient (0.60/0.66/0.76), readable fog (density 0.006), warm sun 1.3 with softened shadows; **16 authored per-location environment profiles** (story_content.json + C# mirror parity) driving ambient/fog/sun through the existing `LocationArrivedEvent` pipeline |
| 10 | Menus | MainMenuUI (title block, CONTINUE/NEW/SETTINGS/CREDITS/QUIT, save status, version), SettingsPanelUI (tabs incl. controls/graphics), SaveLoad presentation, ability sheet |
| 11 | Transitions | Location-to-location arrival flow with area chip + authored environment blend |
| 12 | Performance budgets | `profile_scene.py --check` → **BUDGET PASSED**: 796/820 active renderers, ~162 batched draw calls (≤420), 482 static-batchable, 61/64 ticking scripts, 1 shadowed light, far plane 120, 3 URP quality tiers, all texture sizes within cap |
| 13 | The screen reads as a game | See `reference/visual_slice/` — minimap top-right below the pause cluster, objective tracker under it, health top-left, joystick bottom-left, ability arc + ATK/DODGE bottom-right, interact prompt contextual. No debug stats, no dev text |
| 14 | VISUAL_TARGET.md | Exists (camera/character/environment/HUD/dialogue/combat/menu/lighting/mobile targets) |
| 15 | One deep slice | Fracture Hall: final-style player, 2 presented NPCs (Mara, Sera), clean HUD, touch controls, exploration, dialogue, decisions, objectives, abilities, combat (annex Warden), world reactions, save/load — all live in the FirstLocation slice |
| 16 | Visual verification | See below — offline renders of the **real generated scene** + objective metrics; device capture steps listed as follow-up |

## How the visual slice was verified (honestly)

This sandbox has **no Unity editor/runtime**, so the game could not be captured from a
device. What was done instead — `scripts/render_visual_slice.py` + `compose_visual_slice.py`:

1. Parsed the **actual generated** `FirstLocation.unity` (every active renderer's transform
   chain, scale, material GUID → base/emission colours from the real .mat files).
2. Rebuilt it in Blender headless (kit FBX pieces + primitive props + canonical
   Ari/Mara/Sera/Warden FBX avatars at their authored positions, scene-authored sun
   rotation/colour/intensity and ambient).
3. Rendered the camera exactly as `ThirdPersonCameraController` frames it (4.4 m orbit,
   1.45 m pivot, 0.32 m shoulder, 55° vFOV) at 1920×1080 — gameplay / dialogue / combat.
4. Composited the HUD with the **same constants the C# HUD code uses** (sizes, colours,
   anchors transcribed from PlayerHUD/MiniMapHUD/TrackerHUD/HotbarHUD/MobileControlsUI/
   InteractionHUD/DialogueUI/CombatHUD/MainMenuUI, reference resolution 1920×1080).
5. Objective image metrics (no dark-void / blank-frame failures):

   | frame | lum mean | p05 | dark% | edges% |
   |---|---|---|---|---|
   | gameplay | 120 | 100 | 4.0* | 11.9 |
   | dialogue | — | 9 | 60* (dark glass sheet + letterbox by design; top half mean 86) | 7.4 |
   | combat | 96 | 5 | 5.7* | 7.9 |

   *dark% comes from the HUD's own dark glass panels, letterbox and vignette — the 3D
   scene behind them is lit (raw renders: p05 75–100, 0.0–0.3% dark pixels).

Results: `reference/visual_slice/{contact_sheet,gameplay,dialogue,combat,menu}.png`
(raws in `raw/`). These are offline renders of the real scene data with approximated
materials/sky — a faithful preview, **not** device captures.

**Remaining for a full item-16 sign-off (needs Unity editor/device):**
- Open `Assets/Scenes/Prototype/FirstLocation.unity` in Unity 6 + URP, build Android,
  capture the same four states from a device/emulator.
- Confirm the URP skybox renders as expected in-player (the offline render approximates
  it with the ambient colour) and that per-location environment blends look right in motion.

## Gates (all green at commit time)

- `scripts/run_tests.sh` → **2063 passed / 0 failed** (FlowTests + CampaignContentTests + VisualPassTests 69)
- `scripts/compile_check.sh` → 0 errors (both define sets)
- `scripts/validate_assets.py` → VALIDATION PASSED (0 warnings), incl. JSON↔C# content parity for all 16 environment profiles
- `scripts/profile_scene.py --check` → BUDGET PASSED
- every new .cs/.mat carries a deterministic .meta GUID (generator-registered)

## Files of note for this pass

- `scripts/add_visual_pass_content.py` — Fracture Hall dressing module (wired into `gen_firstlocation_scene.py`)
- `scripts/render_visual_slice.py`, `scripts/compose_visual_slice.py` — the verification renders
- `scripts/decision_system_tests/VisualPassTests.cs` — 69 presentation tests
- `Assets/_Project/Scripts/UI/*` — PlayerHUD, MiniMapHUD, ObjectiveTrackerHUD, AbilityHotbarHUD, DamageNumberUI, InteractionHUD, MobileControlsUI, VirtualJoystick, TouchLookPad, MainMenuUI, SettingsPanelUI, WorldMapUI, UiShapes, UIDebugGate, CombatCameraFeedback, MiniMapMath
- `scripts/story_content.json` + `StoryContentBuilder(.Campaign).cs` — 16 environment profiles
- `docs/PERF_BUDGET.json` — unchanged budgets, still passing
