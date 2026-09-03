# ASSET_PIPELINE.md — CROSSROADS
### Reference → AI 3D generation (Meshy) → Unity: the production pipeline

| | |
|---|---|
| **Companions** | `CHARACTER_REFERENCE.md` (visual source of truth), `GAME_DESIGN.md` §13.1 (perf budget), `DEVELOPMENT_PLAN.md` (phases) |
| **Primary AI tool** | **Meshy** (image-to-3D + text-to-3D + PBR texturing). Fallbacks if a roll fails style checks: Tripo / Rodin (geometry), manual Blender sculpt (last resort). |
| **DCC** | Blender 4.x (cleanup/retopo/UV/bake), Mixamo + AccuRIG (rig/anim) |
| **Engine** | Unity 6 LTS, URP, Humanoid avatars |

```
[1 REFERENCE]  video → frames → canonical character sheets (APPROVAL GATE A)
      │
[2 CONCEPT]    sheets → clean turnarounds / outfit flats (APPROVAL GATE B)
      │
[3 GEOMETRY]   Meshy image-to-3D → rolls → selection (GATE C) → Blender cleanup/retopo
      │
[4 TEXTURES]   Meshy PBR / bake → palette-lock → stylized pass (GATE D)
      │
[5 RIG]        AccuRIG/Mixamo → Unity Humanoid mapping (GATE E)
      │
[6 ANIMATION]  Mixamo base set + custom keys → animation events
      │
[7 UNITY]      import → URP materials → LODs → scene integration
      │
[8 OPTIMIZE+QA] budgets, atlas, device test, side-by-side vs reference (GATE F)
```

---

## Stage 1 — Reference capture → canonical character sheets *(done)*

- Source: repo-root mp4 (5:48, 9:16). Extraction: `scripts/extract_reference_frames.sh` → `reference/frames/` (1/10 s), `reference/chars/` (curated), `reference/contact_sheet.jpg`.
- Per character, the **canonical sheet** = 3–5 curated frames covering: face close-up, full body, action pose, alternate outfit (see CHARACTER_REFERENCE §5 index).
- **GATE A:** stakeholder signs off that sheets match the video. *(Complete for REF-01…REF-07; mentors are derived, gate applies on first render.)*

## Stage 2 — Concept cleanup & turnarounds

AI video frames are motion-blurred/watermarked — never feed raw frames to Meshy.

1. Pick the sharpest face+body frame per character.
2. Clean pass (image editor or image-model edit): remove watermark/subtitles, fix blur, neutral pose where possible. **Identity lock:** edits may clean, never redesign (face/hair silhouette must survive pixel-compare).
3. Generate a **4-view turnaround** (front/side/back/3-4) with an image model using the cleaned frame as identity reference + the style-bible prompt block (§9). Roll 3×, pick the set whose faces still match the frame.
4. Outfit flats: each outfit (e.g. REF-01 A/B/C) gets its own cleaned reference image.
- **GATE B:** turnaround vs. `reference/chars/` side-by-side — same face read at arm's length? Same hair silhouette? Same palette (eyedropper within ±10 ΔE)?

## Stage 3 — Geometry generation (Meshy)

**Mode:** **Image-to-3D** from the approved front view (identity fidelity > text control). Text-to-3D only for props/environments without a clean frame.

- Settings: topology = quads-preferred if offered; texture mode = **PBR**; auto-UV on; scale reference noted for later (Meshy units unreliable — rescale in Blender).
- **Prompt block (style bible — reuse verbatim for every character):**
  > "Semi-realistic stylized 3D character, donghua-grade game CG style, idealized smooth facial features, large expressive eyes, chunky sculpted hair clusters, realistic adult proportions ~7.5 heads, clean stylized PBR clothing, muted realistic palette, game-ready single mesh, no base, neutral A-pose."
  + character-specific lines copied from CHARACTER_REFERENCE sheets (hair color/cut, outfit, accessories).
- **Rolls:** 4–6 per character. Selection checklist (GATE C): face matches sheet; hair silhouette matches (front + side); outfit layers correct (open collar, necklace present!); no melted hands; manifold-ish shell count ≤ 3; UVs non-overlapping.
- Export: `.glb` (with texture) + `.fbx` if offered.
- **Hard rule:** one canonical body mesh per character. Outfits = separate Meshy runs generated *from the same body prompt + outfit flat*, cleaned in Blender onto the same body topology family.

## Stage 4 — Blender cleanup & retopo

1. Import GLB; **apply scale → meters** (REF-01 height 1.78 m, REF-02 1.70 m, REF-05 1.85 m; crowd 1.65–1.85 m).
2. Delete base/junk shells; weld mirrors; fix normals; collapse hair-card interiors.
3. **Retopo/decimate to budget** (GAME_DESIGN §13.1, adjusted for semi-real style — see table §7): hero LOD0 ≤ 12 k tris, major NPC ≤ 8 k, enemy ≤ 6 k, crowd ≤ 2 k; props per kit table.
4. UV: repack to single 0–1 set per character (body+outfit A); keep face texel density 2× body.
5. Bake high-poly (Meshy raw) → normal map onto retopo mesh; bake AO.
6. Silhouette check: orthographic front/side renders vs. turnaround overlay (GATE C.2).

## Stage 5 — Textures

1. Start from Meshy PBR set; repaint albedo to **palette-locked hexes** from CHARACTER_REFERENCE (±10 ΔE) — AI textures drift saturation.
2. Set: **BaseColor + Normal + ORM (packed AO/Rough/Metal)**. No emissive on characters except REF-05 (emissive mask) and affinity piping mask on REF-01 outfit C.
3. Resolution: hero 2048, major NPC/enemy 1024, crowd 512 (atlas-shared). Faces get dedicated albedo region; eyes = painted iris (no geometry).
4. Stylized pass: soften albedo noise, add painted fabric weave on satin pieces, rim-friendly roughness ramp on hair (fake anisotropy).
5. **GATE D:** material ball + character render under reference lighting (warm key / cool ambient) side-by-side with `reference/chars/` frame — palette and sheen must read identical at phone distance.

## Stage 6 — Rigging

1. **AccuRIG** (Reallusion, free) primary; Mixamo auto-rig fallback. Target: **Unity Humanoid-compatible** skeleton (Hips→Spine→Chest→Neck→Head, standard limbs, fingers optional for heroes only).
2. Checks (GATE E): no shoulder candy; elbow/knee pole directions correct; skirt/dress panels (REF-02) get 2-bone chains per panel; REF-05 hair = 3-chain ribbon bones; REF-03 glasses + REF-01 necklace/earring = **skinned to Head bone, zero deform**.
3. Export FBX with skin; no animation baked in (anim separate, Stage 6).
4. Facial: **no face rig at MVP** (design: subtle cinematic expressions via 4 baked expression blendshapes per hero — neutral/angry/soft/determined — swapped in dialogue; crowd/enemies none).

## Stage 7 — Animation

1. **Mixamo base set** (retarget to Humanoid in Unity): idle, walk, run, dodge-dash, hurt, death + combat per GAME_DESIGN §8: light1/2/3, heavy windup/strike, dodge, parry (Stone), launcher.
2. **Custom keys (Blender, style-matched to reference motion language — fast, weighty, streak-friendly):** ability casts per line (Ember dash-strike, Tide wave gesture, Stone slam), REF-02 ribbon-flourish assist, REF-05 hover/drift loop.
3. Every attack clip carries **animation events**: `HitboxOn/HitboxOff`, `Footstep`, `VFXCue` — combat reads depend on them (DEVELOPMENT_PLAN 1.4).
4. Clips: 30 fps, trimmed, loop-flagged; naming `@{char}_{action}_{variant}`.

## Stage 8 — Unity import, materials, integration

1. FBX settings: scale 1.0, import normals + tangents, avatar = Humanoid (configure once per character, **muscle settings saved as .ht file and shared**).
2. Materials: **URP Lit** + project stylized extension shader `XR_Stylized`: rim-light term, hair specular ramp from ORM, optional fresnel-emissive slot (REF-05), affinity-piping emissive mask slot (REF-01-C). One material per character (+1 for emissive variants) to hold draw calls.
3. Textures: import at max size per Stage 5, **ASTC 6x6 (Android) / ASTC 8x8 (crowd)**, sRGB for BaseColor/emissive only.
4. LODs: Unity LODGroup — LOD0 as built, LOD1 ≈ 50%, LOD2 ≈ 20% (crowd ships LOD1/2 only).
5. Prefabs: `Assets/_Project/Prefabs/Characters/{CHAR}/{CHAR}_LOD0.prefab` + outfit-variant prefabs; VFX children pooled (PoolService).
6. **GATE F (final):** on mid device — character in test arena vs. reference frame on second screen: silhouette ✓ palette ✓ face read ✓; tris/draw calls within §7 table; 30 fps held with 3 enemies + 6 crowd.

---

## Environment & prop pipeline (same 8 stages, text-to-3D first)

- Environments start from **text-to-3D kit pieces** (walls, stalls, crates, pillars) or Blender blockout + Meshy detail pass; hero props (power orb, holo-panel, light column emitter) from cleaned reference frames (t:0.5, t:30, t:340).
- Kit discipline: one modular kit per district (GAME_DESIGN §11.1); world-state variants = dressing swaps, not new geometry.
- Environment budget: arena ≤ 90 draw calls, ≤ 250 k tris visible, 1 baked lightmap set per district variant.

---

## 7. Budgets (style-adjusted, supersedes §15 numbers in GAME_DESIGN)

| Asset | LOD0 tris | Texture | Notes |
|-------|-----------|---------|-------|
| REF-01 Ari (hero) | ≤ 12 000 | 2048 | +3 outfit sets (share body tex) |
| REF-02 Mara / REF-03 Dax | ≤ 8 000 | 1024–2048 | |
| REF-05 Archivist | ≤ 6 000 | 1024 + emissive | shader-driven, cheapest |
| REF-06 enemies | ≤ 6 000 | 1024 | shared armor atlas |
| REF-07 crowd | ≤ 2 000 | 512 atlas | LOD1/2 only |
| Mech (t:240 bg) | ≤ 15 000 | 2048 | Ch.3 set-piece only |
| District kit piece | ≤ 3 000 | atlas 2048 shared | instanced |

Rationale: semi-real style costs ~+50% tris vs. original low-poly plan; paid for by cutting crowd counts (≤6 on-screen, already designed) and shipping LOD-only crowd. **GAME_DESIGN §13.1/§15 updated accordingly.**

---

## 8. Priorities — what to build FIRST

**For the playable prototype (DEVELOPMENT_PLAN Phase 1 greybox → Phase 3 vertical slice):**

| Priority | Asset | Why first |
|----------|-------|-----------|
| **P0-1** | **REF-01 Ari** (body + outfit A + rig + base anim set) | The player. Everything downstream (camera, combat feel, lock-on framing) is tuned against him. |
| **P0-2** | **Power-Grab / Fracture Hall environment kit** — hall architecture, blue light columns, golden power-orb prop, holo-panel shader | The reference's signature location AND our Ch.1 awakening scene; its light-column + orb VFX become the game's emissive language; doubles as prototype arena. |
| P1-1 | REF-06 Soldier-A (grunt) | First enemy for combat read. |
| P1-2 | REF-02 Mara (combat outfit) | Ally/assist + second face for style-consistency proof. |
| P1-3 | Crowd kit (4 meshes, recolors) | Power-grab hall needs the crowd to read like the reference. |
| P2 | REF-03 Dax, REF-05 Archivist, Soldier-B/C, mentors (derived) | Vertical-slice content. |

**Recommended first character: REF-01 "The Lead" → Ari.**
**Recommended first environment asset: the Power-Grab / Fracture Hall kit (hall + light columns + gold power orb).**

---

## 9. Consistency & prompt hygiene

- One `style-bible.txt` in `Assets/_Project/Data/Art/` holds the §3 prompt block + palette hexes; every Meshy/image prompt must `include: style-bible` verbatim.
- Never generate a character "from scratch" twice — always image-to-3D from the approved sheet.
- Every merged asset links its GATE A–F approvals in the asset's `.meta`-sidecar JSON (`Assets/_Project/Data/Art/approvals/{asset}.json`).
- Weekly style review: new renders lined up against `reference/contact_sheet.jpg`; drift > ±10 ΔE or silhouette mismatch = reject.

---
*End of ASSET_PIPELINE.md.*
