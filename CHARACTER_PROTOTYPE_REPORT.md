# CHARACTER_PROTOTYPE_REPORT.md — Ari (REF-01) Prototype v1
### Build & test report — first playable character prototype

| | |
|---|---|
| **Date** | 2026-09-03 |
| **Character** | REF-01 "The Lead" → Ari (player character), per `CHARACTER_REFERENCE.md` |
| **Sources** | Approved sheet `reference/concept/ari_turnaround.png` + measured proportions `reference/concept/ari_proportions.json` |
| **Toolchain used** | Headless Blender 4.2.1 LTS (mesh/UV/rig/anim/export/QA renders); system Python+PIL (atlas); hand-authored Unity 6 project assets |
| **Toolchain NOT available in sandbox** | Meshy/other neural 3D (sandbox RAM 1.9 GB < 4–8 GB required); Unity Editor (no GUI/license). Both flagged below with exact hand-off steps. |

---

## 1. Model creation result — ✅ PASS (prototype grade)

- **Method:** silhouette-measured procedural build (proportions sampled row-by-row from the approved turnaround: wrist span 1.059 m, shoulder ±0.204 m, knee z 0.49 m, head width 0.227 m…), A-pose matched to sheet arm angle (~59° below horizontal), 1.78 m canonical height (1.80 m incl. hair shell).
- **Output:** `Assets/_Project/Art/Characters/Ari/Ari.fbx` — single skinned mesh, **2 020 tris** (budget ≤ 12 000 — 6× headroom), 2 material slots (body atlas + hair).
- **Visual check:** `reference/prototype_renders/verify_front_idle.png` reads as REF-01 at gameplay distance: black curtain-fringe hair, pale blue-grey open shirt + white inner + necklace, charcoal trousers. Face close-up (`verify_face.png`) keeps the reference face via texture.
- **Known limitations (documented, not blocking):** blocky hands/feet (box primitives); side-facing surfaces show ortho-projection stretch at extreme close-up; hair is a chunky shell (style-bible compliant) rather than sculpted locks. **Fidelity upgrade path:** Meshy image-to-3D from `reference/concept/ari_front.png` + multi-view crops (brief in `FIRST_ASSET_BRIEFS.md` §A) — requires Meshy account or a machine with ≥8 GB RAM.

## 2. Rigging result — ✅ PASS

- 20-bone **Mixamo-compatible humanoid skeleton** (Hips→Spine→Spine1→Spine2→Neck→Head, Shoulder/Arm/ForeArm/Hand, UpLeg/Leg/Foot ×2), auto-weighted (15 deforming groups carrying weights).
- Verified by FBX re-import: armature + skin present, deform groups intact.
- Unity mapping: **Generic rig** in metas (`animationType: 2`, `avatarSetup: 0`) — deliberate: hand-authored Humanoid Avatars are not reliably authorable outside the editor. **One-click Humanoid conversion:** Rig tab → Animation Type = Humanoid → Apply (standard bone names auto-map). Editor setup script leaves this untouched.

## 3. Texture / material result — ✅ PASS (with documented artifacts)

- **Atlas:** `Ari_Albedo.png` 2048×1024 (front ortho left / mirrored back ortho right), generated from the approved views with background-dilation fill so off-axis faces sample plausible colors.
- **UVs:** orthographic front/back projection aligned to the measured silhouette — front view lands pixel-accurate (face, necklace, shirt, trousers all verified in renders).
- **Materials:** `M_Ari` (URP Lit, atlas, smoothness 0.35) + `M_Ari_Hair` (URP Lit, flat #14161A-range chunky hair per style bible) + `M_Ground` (test scene).
- **Artifacts:** projection stretch on ±X-facing surfaces at close range; fixed-color hair loses strand detail (acceptable stylization).

## 4. Unity import result — ✅ READY (static verification; runtime import pending editor)

Hand-authored, GUID-consistent Unity assets (all cross-references machine-verified):
- FBX metas: base mesh (no anim) + `Ari_Idle/Walk/Turn.fbx` with clip ranges matched to real takes (2–61 / 2–37 / 2–31), loopTime 1/1/0.
- `Ari_Controller.controller`: params `Speed` (float), `Turning` (bool); states Idle/Walk/Turn; transitions Idle⇄Walk on Speed, →Turn on Turning, Turn→Idle/Walk on exit+conditions. Parses clean; clip refs use fileID 7400002 per FBX.
- `CharacterTest.unity`: camera (FOV 50, 10° down), warm directional light, 20 m ground plane (M_Ground), `PrototypeBootstrap` object.
- `CrossroadsPrototypeSetup.cs` (menu **CROSSROADS ▸ Prototype ▸ Build Ari Prefab & Test Scene**): builds `Prefabs/Player/Ari.prefab` with Animator+controller, **CharacterController** (h 1.78, center 0.89, radius 0.22 — per GAME_DESIGN §8; no Rigidbody, by design), prototype locomotion component, material slot reassignment; places an instance in the test scene and saves it.
- Validation run: 57 meta GUIDs; 11 asset refs resolved (only "unresolved" GUID is the URP Lit shader living in the URP package — expected); scene/controller/materials YAML parse clean.

## 5. Movement test result — ✅ PASS in Blender / ⏳ pending Unity runtime

- **Blender-verified:** Idle (60 f breathing/sway loop), Walk (36 f cycle: opposing limb swing, knee flex, hip bob — stride visible in `verify_side_walk.png` / `verify_front_walk.png`), Turn (30 f 90° pivot with spine counter-rotation). Clips re-import with exact frame counts.
- **Unity runtime:** cannot execute in this sandbox (no Unity Editor/license). Repro steps on a dev machine: open repo root in Unity 6000.0.x → let it import → run the setup menu → open `Scenes/Dev/CharacterTest` → Play → **WASD/arrows**: idle→walk transitions, smooth turn while moving, pivot-Turn clip on sharp reversals. Expected animator behavior per §4.

## 6. Problems & blockers

| # | Issue | Severity | Resolution / path |
|---|-------|----------|-------------------|
| 1 | No neural 3D generator runnable in sandbox (RAM) | Medium (fidelity) | Prototype built from measured silhouette instead; Meshy brief ready for high-fidelity pass on capable machine/account |
| 2 | No Unity Editor in sandbox → runtime test unexecuted | Medium (verification) | Static verification done (re-import, YAML/GUID audits, Blender anim renders); one-click runtime test steps above |
| 3 | Unity license activation needed for any headless Unity run | Medium | Manual activation file flow on stakeholder machine, or run test in GUI editor |
| 4 | Ortho-projection stretch on side faces | Low (prototype-only) | Accept for prototype; replaced by proper unwrap in fidelity pass |
| 5 | Generic rig (not Humanoid avatar) | Low | 1-click Humanoid conversion in editor; bone names already standard |
| 6 | `.meta` files for FBX clips assume take name `Ari_Rig\|Scene` | Low | Verified against actual exports; if Unity renames takes on import, re-set loopTime in Anim tab (2 clicks) |

**Conclusion:** Ari v1 is a valid, rigged, textured, animated, Unity-ready prototype foundation consistent with the reference at gameplay distance. Nothing in §6 blocks Phase 1 combat prototyping; items 1–3 are environment hand-offs, not design defects.

---
*Renders: `reference/prototype_renders/`. Build script: `scripts/blender_build_ari.py` (re-runnable, deterministic).*
