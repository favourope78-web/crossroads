# FIRST_ASSET_BRIEFS.md — CROSSROADS
### Executable production briefs for the first two assets (prototype prep)

| | |
|---|---|
| **Companions** | `CHARACTER_REFERENCE.md` (sheets/style), `ASSET_PIPELINE.md` (stages & gates), `DEVELOPMENT_PLAN.md` (phases) |
| **Scope of this doc** | The two P0 assets: **REF-01 Ari** (first character) and the **Power-Grab / Fracture Hall kit** (first environment). Everything here is ready to execute the moment a Meshy account (or equivalent) is available. |
| **Stage-2 status** | ✅ Canonical concept sheets generated & style-checked vs. reference frames (see Gate B notes per brief). Awaiting stakeholder sign-off. |

---

## Brief A — REF-01 "The Lead" → ARI (player character)

### A.1 Canonical inputs (Stage 2 complete)
| File | Use |
|------|-----|
| `reference/concept/ari_turnaround.png` | Canonical 4-view sheet + face inset — **the identity lock for every future asset** |
| `reference/concept/ari_front.png` | Meshy image-to-3D **primary input** (clean A-pose front) |
| `reference/concept/ari_side.png`, `ari_back.png`, `ari_threequarter.png` | Meshy multi-view inputs (if the tool offers multi-view mode) |
| `reference/concept/ari_face.png` | Texture/face QA reference |
| `reference/chars/t_60.jpg`, `t_330.jpg`, `t_230.jpg` | Ground-truth frames for Gate F side-by-side |

**Gate B note:** turnaround matches t:60/t:330 on face read, hair silhouette (curtain fringe + brow locks), outfit A (pale blue-grey open shirt, white inner, necklace), palette. *Signed off by: ______ (stakeholder).*

### A.2 Meshy generation (Stage 3)
- **Mode:** Image-to-3D. Primary: `ari_front.png`. Multi-view (optional, better symmetry): front+side+back.
- **Settings:** PBR texture mode ON; quads-preferred topology if offered; no base/pedestal; auto-UV.
- **Prompt (paste verbatim — includes the style-bible block from ASSET_PIPELINE §3):**
  > Semi-realistic stylized 3D character, donghua-grade game CG style, idealized smooth facial features, large expressive eyes, chunky sculpted hair clusters, realistic adult proportions ~7.5 heads, clean stylized PBR clothing, muted realistic palette, game-ready single mesh, no base, neutral A-pose. Young man, black medium shaggy hair with center-parted curtain fringe and two pointed locks between the eyes, amber-brown eyes, pale porcelain skin; pale blue-grey open-collar shirt-jacket over white crew-neck inner, thin silver necklace chain with small pendant, dark charcoal trousers, dark shoes.
- **Rolls:** 5. **Selection checklist (Gate C):** face matches `ari_face.png` at arm's length; fringe locks present; necklace present as geometry or texture (not floating junk); hands have 5 clean fingers; shell count ≤ 3; hair silhouette matches side view.
- **Export:** GLB (with textures).

### A.3 Blender (Stages 4–5)
- Scale to **1.78 m**; delete base shells; weld; recalculate normals.
- Retopo/decimate → **LOD0 ≤ 12 000 tris** (hair ≤ 35% of budget).
- Single UV set; face texel density 2×; bake normal + AO from the Meshy high-poly.
- Albedo palette-lock vs. sheet hexes (shirt `#B9C6CF`, inner `#F2F1EE`, trousers `#2E3138`, hair `#14161A`, skin `#F2E4DA`) within ±10 ΔE; 2048 BaseColor + Normal + ORM.
- **Gate D:** render under warm-key/cool-ambient vs. `t_60.jpg`.

### A.4 Rig & animation (Stages 6–7)
- AccuRIG → Unity Humanoid; save `.ht` muscle file as `Assets/_Project/Data/Art/ari_humanoid.ht` (shared by all humanoids later).
- Necklace + (future) earring skinned 100% to Head bone.
- Mixamo base set: idle, walk, run, dodge-dash, hurt, death, light1/2/3, heavy, parry-stance; animation events `HitboxOn/Off`, `Footstep`, `VFXCue` added in Unity.
- 4 expression blendshapes: neutral / angry / soft / determined.

### A.5 Unity (Stage 8)
- `Assets/_Project/Prefabs/Player/Ari_LOD0.prefab`; material `M_Ari` (URP + `XR_Stylized`); LOD1 50% / LOD2 20%.
- **Gate F:** device arena test vs. `t_330.jpg` on second screen; tris/DC within budget; 30 fps held.

---

## Brief B — Power-Grab / Fracture Hall kit (first environment)

### B.1 Canonical input
`reference/concept/fracture_hall_concept.png` (Stage-2 concept; matches `t_0.5` on light columns, gold orb, crowd, dusk-rose glazing).
**Gate B style note:** concept is slightly painterly; in-engine target remains semi-real per `CHARACTER_REFERENCE.md` §2 — treat concept as **composition + kit layout truth**, not material truth. *Signed off by: ______.*

### B.2 Kit breakdown (modular pieces to generate/build)
| Piece | Method | Budget |
|-------|--------|--------|
| Floor tile 4×4 m (panel-lined, emissive seam option) | text-to-3D | ≤ 300 tris |
| Light-column: stone/metal pillar shaft + inner emissive beam cylinder (fresnel shader, no particles) | text-to-3D + shader | ≤ 800 tris |
| Balcony/railing block (crowd stand) | text-to-3D | ≤ 600 tris |
| Wall + angled glazing panel (dusk-rose light) | text-to-3D | ≤ 500 tris |
| Roof truss cassette | text-to-3D | ≤ 400 tris |
| **Hero prop: golden power orb** (icosahedral core + double gyro ring, emissive gold `#E8B84B`) | image-to-3D from concept crop | ≤ 1 500 tris |
| Holo-panel (cyan glyph quad, shader-driven scroll) | shader quad | 2 tris |
| Crowd stand-ins | REF-07 kit (later) | LOD2 only |

- **Text-to-3D prompt block:** style-bible (ASSET_PIPELINE §3) + "monumental civic hall architecture, clean bevelled concrete and steel, muted grey palette with dusty-rose window light, stylized PBR, modular game kit piece, flat bottom, no props".
- 3 rolls per piece; Gates C/D applied per piece (silhouette + palette vs. concept).

### B.3 Assembly & budgets
- Arena floor kept **flat and clear 40×40 m** (GAME_DESIGN §11.3) — columns ring the arena, orb floats center-high.
- Scene budget: **≤ 90 draw calls, ≤ 250 k visible tris**, one baked lightmap set; light columns + orb are the only emissives (sanctioned accents).
- Instance everything (GPU instancing); glazing uses one shared material.
- World-state hook: column emissive intensity + orb presence driven by `WorldState` flags (hall appears intact/contested/ruined in later chapters via dressing swaps).

### B.4 Unity
- Greybox first in `99_Sandbox` (Phase 1), kit swap in `Scenes/Chapters/C1L1` (Phase 3).
- VFX children (orb ring spin, column shimmer) pooled via PoolService.

---

## Execution order & ownership
1. Stakeholder signs Gate B on both sheets (**blocking**).
2. Meshy: Brief A rolls → Gate C → Brief B piece rolls → Gate C. *(Needs Meshy account/API key — not available in sandbox.)*
3. Blender + Unity steps require the Unity editor on a dev machine (sandbox has none): open repo root in **Unity 6000.0.x** — the Phase-0 scaffold (folders, asmdefs, manifest, ProjectVersion) is already committed; editor will generate `.meta` files → commit them immediately.
4. Gates D–F per asset; approvals logged in `Assets/_Project/Data/Art/approvals/{asset}.json`.

**Next assets after these (per ASSET_PIPELINE §8):** Soldier-A grunt → Mara (combat outfit) → crowd kit.

---
*End of FIRST_ASSET_BRIEFS.md.*
