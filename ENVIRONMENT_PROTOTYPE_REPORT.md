# ENVIRONMENT_PROTOTYPE_REPORT.md — FirstLocation: The Fracture Hall
### First playable environment prototype build & verification report

| | |
|---|---|
| **Date** | 2026-09-03 |
| **Location** | Power-Grab / Fracture Hall — first player-facing location per `GAME_DESIGN.md` §11.2 (scene `P→C1L1` awakening) and `FIRST_ASSET_BRIEFS.md` Brief B; reference frames t:0.5 / t:20 + approved concept `reference/concept/fracture_hall_concept.png` |
| **Scene** | `Assets/Scenes/Prototype/FirstLocation.unity` (user-specified path) |
| **Assets** | `Assets/Game/Environment/Kit/` (13 modular FBX), `Assets/Game/Environment/Materials/` (6 URP materials) |
| **Player** | Ari prototype v1 (`CHARACTER_PROTOTYPE_REPORT.md`) spawned at (0, 0, −16) facing the hall |

---

## 1. What was built

**Modular kit (13 pieces, single-mesh FBX, 12–288 tris each):**
SM_FloorTile (10 m), SM_Column, SM_LightBeam (emissive sleeve), SM_WallPanel (10 m), SM_GlazingPanel (dusk-rose windows), SM_BalconyBlock, SM_Railing, SM_Truss, SM_DoorFrame, SM_Door, SM_OrbCore, SM_OrbRing, SM_HoloPanel.

**Scene composition (103 placed pieces from `scripts/hall_layout.json`):**
- 40×40 m flat arena floor (16 tiles) per GAME_DESIGN §11.3 arena rule
- 2 rows × 5 light columns (pillar + glowing cyan beam) flanking the arena
- Perimeter walls + tilted dusk-rose glazing band; roof truss cassettes
- Side balconies with railings (crowd stands for the power-grab beat)
- 3 door sets (main south double-door, north exit, east side) — sliding interactables
- Golden power orb + 2 gyro rings floating at arena center (y = 6 m)
- 4 holo panels (sanctioned cyan accent) as inspect interactables

**Systems in scene:** directional warm key light (no shadows — mobile), trilight dusk ambient (rose sky / cool ground), exponential fog (haze + distance cull), solid dusk background (no skybox cost), 61 box/sphere colliders, static flags on architecture, GPU-instancing-enabled materials.

**Player & camera:** FirstLocationBootstrap spawns Ari (tagged `Player`, + InteractInput); `ThirdPersonCameraController` = smooth-damped follow (offset 0/2.1/−4.4, look-damp) — Cinemachine upgrade slated for Phase 1. Interaction: nearest-`Interactable` proximity + **E key / tap**, IMGUI prompt; doors slide open/closed; orb & holo panels log inspect events. **No combat / powers / missions / decisions added**, per scope.

## 2. Visual verification vs reference — ✅ PASS (look-dev)

Blender look-dev renders (`reference/prototype_renders/hall_verify_entry/arena/player.png`) reproduce the concept's composition and palette: blue light columns ringing a clear central arena, floating gold orb with gyro rings, dusk-rose glazing over grey concrete, balcony crowd lines, trussed ceiling; Ari reads correctly at the spawn door. Two defects found & fixed during look-dev: light beam hidden inside pillar shaft (radius 0.22→0.50), preview camera placements.

## 3. Static verification — ✅ PASS

- Scene YAML parses clean: **499 serialized objects / 106 GameObjects**.
- **237 GUID cross-references, 0 unresolved** (89 metas; URP shader GUID resolves from package).
- Colliders: 61 (floor, columns, walls, balconies, railings, door frames + leaves, orb sphere).
- Draw calls: **103 renderers** pre-batching — inside the ≤150 arena budget; static flags set so Android build static-batches architecture down substantially. Tris: kit total ≈ 1.1 k tris/piece-max; whole hall < 15 k tris.
- Android-oriented choices: no realtime shadows, no realtime GI, no skybox texture, fog-culled far plane 120 m, instancing on, MSAA off in camera, dynamic-resolution allowed.

## 4. Runtime verification — ⏳ pending Unity Editor (same hand-off as character)

Sandbox has no Unity Editor/license; the following 5 checks are one Play-button run on your machine (menu *CROSSROADS ▸ Prototype ▸ Build Ari Prefab & Test Scene* once first, then open `FirstLocation` and Play):
1. **Spawn:** log `[CROSSROADS] Ari spawned in FirstLocation at (0,0,-16)`; character visible at main door.
2. **Movement:** WASD/arrows walk the arena; idle↔walk animator transitions.
3. **Camera:** smooth third-person follow, no jitter at columns (damping 0.18/0.12).
4. **Collision:** walls/columns/railings/doors block the CharacterController; orb sphere blocks center.
5. **Load:** console free of errors (warnings about editor-only bootstrap in builds are expected).
Expected console noise: none beyond the documented editor-only warnings.

## 5. Assets still to create (next environment work, in order)

1. **Kit texture pass** — concrete/metal surface detail (currently flat URP colors): 1 shared 1 K atlas, baked AO in corners.
2. **Beam/orb VFX** — particle-less shader shimmer on light columns; orb ring spin + glow pulse (PoolService).
3. **Crowd kit** (REF-07, 4 meshes + recolors, LOD2) to dress balconies for the power-grab beat.
4. **Dressing props** — crates, benches, signage, floor decals (≤ 8 pieces, same kit discipline).
5. **Exterior backdrop** — cheap silhouette card / gradient beyond glazing (dusk city skyline).
6. **World-state variants** — contested / ruined / rebuilt dressings (GAME_DESIGN §5.2) when Ch.2–3 production starts.
7. **NavMesh bake** — deferred until enemy/ally AI exists (Phase 1+); walkable area = arena + door thresholds.
8. **Ari fidelity pass** (Meshy brief) and **door hinge animation** polish — quality upgrades, not blockers.
9. **Audio pass** — hall ambience + door/interact stings (AudioService, Phase 2).

**Conclusion:** FirstLocation is a playable, reference-faithful, Android-frugal environment shell around the completed player prototype; all five runtime checks are scripted/logged for a one-click verification run in the editor.

---
*Look-dev: `reference/prototype_renders/hall_verify_*.png`. Generators: `scripts/blender_build_hall_kit.py`, `scripts/blender_preview_hall.py`, `scripts/gen_firstlocation_scene.py`, layout `scripts/hall_layout.json`.*
