# CHARACTER_REFERENCE.md — CROSSROADS
### Visual character bible extracted from the reference video

| | |
|---|---|
| **Source video** | `2d7a9744e7a9eb3cce978c7f45cbdcdb_1788379399516.mp4` (repo root) — 5:48, 576×1024 (9:16), 30 fps, TikTok `@godstimeszz1` |
| **Extraction** | `scripts/extract_reference_frames.sh` → `reference/frames/` (1 frame/10 s), `reference/chars/` (hand-picked full-res frames), `reference/contact_sheet.jpg` (timestamped overview) |
| **Status** | Authoritative visual source of truth for all characters. Supersedes the art bullet-points in `GAME_DESIGN.md` §15 (see §15 note). |
| **Companion** | `ASSET_PIPELINE.md` — how these designs become Unity-ready 3D assets (Meshy → Blender → Unity). |

> **Mandate:** every character in the game must look the same as — or as close as reasonably possible to — the corresponding reference character below: overall visual style, proportions, clothing style, facial style, and cross-scene consistency.

---

## 1. The Reference, in One Paragraph

The video is a vertical-format 3D CG animated short in a **semi-realistic stylized "donghua-grade" render style** (Chinese animated-drama / premium mobile-game CG look): idealized human faces with smooth porcelain skin and large expressive eyes, realistic adult proportions, chunky stylized hair with soft anisotropic sheen, clean stylized-PBR clothing (satin, cotton, tactical weave), cinematic soft lighting over a muted realistic palette, punctuated by **emissive sci-fi accents** — cyan holographic UI, columns of blue light, golden floating power orbs, and a giant luminous AI avatar. The story beats visible in the reference (a "power grab" hall, players dumped into an apocalypse game, island base-building, mechs, a System AI announcing "Apocalypse Mode") map directly onto CROSSROADS' Fracture / power-choice / System-narrator fiction.

---

## 2. One Consistent 3D Art Style for the Whole Game

**Style name: "Semi-Real Stylized CG (Donghua Grade), mobile-optimized."**

Every asset — characters, environments, props, VFX — follows these rules:

| Aspect | Rule |
|--------|------|
| **Proportions** | Realistic adult proportions, slightly idealized: heroes ≈ 7.5–8 heads tall; slim-athletic builds; no chibi, no exaggeration. |
| **Faces** | Idealized semi-real: smooth poreless skin with soft subsurface-like gradient, large almond eyes with detailed iris + upper-lash shadow line, narrow nose, small mouth, defined but soft jaw. Expression range subtle/cinematic (no cartoon squash). |
| **Hair** | Chunky stylized strand clusters (not strand-sim), sculpted silhouette with 2–3 hero locks over the forehead; baked anisotropic-style highlight (gradient/specular ramp in texture), no flyaways. |
| **Materials** | Stylized PBR: clean albedo with painted fabric detail, low-roughness variation; satin pieces get a broad soft highlight; metals clean and slightly desaturated. No photogrammetry noise, no grunge unless scene state demands it (ruined variants). |
| **Palette** | Muted realistic base (greys, ivories, dusty blues, tans, olives) + **three sanctioned emissive accents only**: hologram cyan `#66D9E8`, power gold `#E8B84B`, alert amber-red `#D96A4A`. Affinity line colors (Ember/Tide/Stone/Hollow) appear in VFX and trim, never as flat costume colors. |
| **Lighting look** | Soft cinematic: one warm key + cool ambient; dusk-rose exterior skies `#B98A83`; interiors cool-white panels; emissives carry the sci-fi read. Baked/probe lighting per GAME_DESIGN §13.1. |
| **VFX language** | Thin luminous rings/coils, vertical light columns, floating icon orbs, translucent holo-panels with glyph rows (reference t:30, t:340, t:345). |
| **Consistency law** | One character = one canonical face mesh + one canonical hair mesh reused in every scene; outfits swap as separate mesh sets on the same body; faces are NEVER regenerated per scene. |

---

## 3. Character Sheets

IDs: `REF-xx` = reference character; **cast mapping** = role in CROSSROADS per `GAME_DESIGN.md` §9.

---

### REF-01 — "The Lead" → **Ari (player character / protagonist)**
*Reference frames: t:60, t:70(bg), t:230, t:240, t:330 (see `reference/chars/`)*

| Attribute | Specification |
|-----------|---------------|
| **Role** | Playable protagonist. In-fiction: the person whose decisions shape the run. |
| **Body / proportions** | Young adult male, ~20s; slim-athletic; ≈7.7 heads; narrow shoulders vs. hips ratio moderate; long limbs; elegant hands. |
| **Hair** | Black (`#14161A`, cool sheen highlight `#3A4148`), medium-length shag: curtain/center-parted fringe with 2–3 pointed locks falling between the eyes, layered sides covering ear tops, tapered nape. Messy-but-sculpted. |
| **Clothing (canonical outfit A — "Awakening")** | Pale blue-grey open-collar shirt/jacket `#B9C6CF` (soft cotton, rolled feel) over white crew-neck inner `#F2F1EE`; **thin silver necklace chain** `#C9CCD2` with small pendant. |
| **Clothing (outfit B — "Base/interior")** | Ivory-white satin shirt `#EEE9E2`, open lapels, deep V over bare chest/inner; same necklace. |
| **Clothing (outfit C — "Field")** | Plain white shirt `#F4F3F0`, sleeves loose; dark trousers `#2E3138`. |
| **Colors** | Skin pale porcelain `#F2E4DA` (shadow `#D9BFAE`); eyes amber-brown `#6B4A35`; hair black-blue; wardrobe = pale desaturated blues/ivories. |
| **Facial style** | The style-defining face: sharp thin eyebrows angled slightly down at inner ends; large amber eyes with dark limbal ring; straight narrow nose; small neutral mouth; pointed chin; calm, unreadable default expression. |
| **Distinctive features** | Small **silver stud/hoop earring, left ear** (t:330); silver necklace (always); fringe locks over brow (always). |
| **Game notes** | 3 outfit mesh-sets on one body (A default, B interludes, C field/combat). Dominant-affinity trim appears ONLY as subtle emissive piping on outfit C (line color), never changing face/hair. |

---

### REF-02 — "The Fighter Woman" → **Mara (childhood friend / potential ally)**
*Reference frames: t:10 (combat), t:70 (team scene), t:60 (crowd bg)*

| Attribute | Specification |
|-----------|---------------|
| **Role** | Deuteragonist; bond-driven ally (GAME_DESIGN §9.1 fate states). |
| **Body / proportions** | Young adult female, slim-athletic, ≈7.3 heads; runner's build. |
| **Hair** | Dark brown-black `#1D1A1C`; **high ponytail or high bun** in action, loose face-framing strands at temples; long straight when down (crowd scene). |
| **Clothing (combat)** | Flowing **white dress/light tunic** `#F4F2EF` with soft skirt panels that read in motion; bare arms; simple sandals/soft shoes. |
| **Clothing (civilian)** | Grey hooded jacket `#8F939C` over white top `#F1F0ED`; dark trousers. |
| **Colors** | Porcelain skin `#F0E2D8`; dark eyes `#3A2E28`; wardrobe white/grey. |
| **Facial style** | Same family grammar as REF-01: large dark eyes, soft small mouth, pointed chin; slightly rounder cheeks than REF-01. |
| **Distinctive features** | High ponytail silhouette in every action scene (recognizability rule); white dress = her combat identity. |
| **Game notes** | Ally-assist VFX = white/cyan ribbon trails matching t:10 motion streaks. |

---

### REF-03 — "Glasses" → **Dax (rival / foil)**
*Reference frames: t:30(bg), t:40 (close-up)*

| Attribute | Specification |
|-----------|---------------|
| **Role** | Intellectual rival; duel-or-teamup boss (GAME_DESIGN §9.1). |
| **Body / proportions** | Young adult male, slim, ≈7.5 heads; upright posture. |
| **Hair** | Chestnut-brown `#4A3527`, wavy, side-swept volume over right brow, short tapered sides. |
| **Clothing** | White crew tee `#F1F0ED` under **dark navy unstructured blazer** `#2A3140`. |
| **Colors** | Fair skin `#EFDfd4`-range `#EFDFD4`; eyes warm brown `#5A4232`. |
| **Facial style** | Semi-real family; thinner face than REF-01, slightly higher brows; expressive mouth (talks a lot). |
| **Distinctive features** | **Thin silver-metal round-square glasses** `#B9BEC6` (signature — never removed on camera). |
| **Game notes** | Glasses = separate small mesh (2 draw-call tris trivial); keep in all outfits incl. combat (his identity). |

---

### REF-04 — "Hoodie Teammate" → **Civilian-team archetype / minor NPC "Teammate"**
*Reference frames: t:70*

| Attribute | Specification |
|-----------|---------------|
| **Role** | Named-minor/civilian-team archetype; also seeds the civilian crowd kit. |
| **Body / proportions** | Young adult male, average build, ≈7.3 heads. |
| **Hair** | Black `#17181A`, short neat bowl-fringe. |
| **Clothing** | **Grey/white color-block hooded parka** `#9AA0A8` / `#E8E8E6` with drawstrings and chest strap detail. |
| **Facial style** | Family grammar, softer/younger read; rounder jaw. |
| **Distinctive features** | Color-block hoodie silhouette. |
| **Game notes** | This mesh + REF-02 civilian outfit + 2 recolors = civilian crowd kit (LOD2). |

---

### REF-05 — "The System" → **The Archivist (System presence / shrine keeper / narrator)**
*Reference frames: t:345, t:347*

| Attribute | Specification |
|-----------|---------------|
| **Role** | The game's System voice: shrine keeper, codex narrator, ending announcer (GAME_DESIGN §9.1 Archivist). |
| **Body / proportions** | Female figure, tall and statuesque, ≈8 heads; appears **giant-scale** in sky projections and human-scale as shrine hologram. |
| **Hair** | Floor-length flowing **silver-white** `#E8F1F6`, center-parted, weightless drift (animated sine sway, not sim). |
| **Clothing / body** | Luminous **pale cyan-white armored bodysuit**: sculpted breastplate with center gem line, shoulder caps, segmented forearm guards, thigh plates; skin surfaces glow `#CFE6F2` (emissive), plates semi-matte `#A8C4D4`. |
| **Colors** | Emissive cyan-white core; holo rings behind her `#7FD8E8`. |
| **Facial style** | Same family grammar, elevated: serene, downcast-capable lids, no pupils-glow (soft white iris). |
| **Distinctive features** | Concentric **holographic light coils/rings** behind torso (signature VFX); full-body translucency + fresnel rim. |
| **Game notes** | Implemented as emissive shader + fresnel + scroll-ring VFX; ONE mesh, two scales (sky projection / shrine); cheapest "character" in the game (no combat anims). |

---

### REF-06 — "Tactical Trio" → **Choir soldiers / enemy archetypes**
*Reference frames: t:320 (backs to burning wreck), t:0.5 crowd-context*

| Variant | Look | Game mapping |
|---------|------|--------------|
| **Soldier-A (tan)** | Tan-brown field jacket `#8A6F52`, short black hair, light gear | Grunt skin #1 |
| **Soldier-B (green armor)** | Bulky olive-green tactical vest/armor `#4C5548` over dark suit, high collar, shaved/short hair | **Bruiser** (shield-bearer skin) |
| **Soldier-C (white armor)** | Sleek white-grey body armor `#C7CCD2` with black under-suit and chest accents, short hair | **Elite / Caster** skin |

Shared rules: helmets OFF (faces use family grammar, coarser features), armor = hard-surface stylized plates with clean bevels, one small cyan `#66D9E8` status light per suit (Choir signature).

---

### REF-07 — "The Crowd" → **Civilian kit**
*Reference frames: t:0.5, t:20, t:60 bg*

Muted modern casuals: hoodies (`#9AA0A8`), white/cream tees (`#F1F0ED`), beige knits (`#C9BBA8`), dusty-blue shirts (`#7C8894`), dark trousers. 4 body meshes × recolors × 2 hair cards-LOD = crowd kit (≤2 k tris each, LOD2-only).

---

## 4. Cast Mapping Summary & No-Reference Characters

| REF | CROSSROADS cast | In reference? |
|-----|-----------------|---------------|
| REF-01 | Ari (PC) | ✅ primary |
| REF-02 | Mara | ✅ |
| REF-03 | Dax | ✅ |
| REF-04 | Teammate / civilian seed | ✅ |
| REF-05 | The Archivist (System) | ✅ |
| REF-06 A/B/C | Grunt / Bruiser / Elite-Caster | ✅ |
| REF-07 | Civilians | ✅ |
| — | **Mentors Kael / Odalys / Bran** | ❌ derive per §2 grammar: Kael = 40s male, scar over brow, cropped grey-flecked hair, Ember-trim military coat; Odalys = 50s female, Tide-teal healer robe, silver bun; Bran = 50s male, bulky, Stone-grey high-collar ward coat, shaved head. Faces approved against REF-01/02 close-ups for family likeness. |

**Consistency protocol:** canonical face/hair meshes are created ONCE per character (ASSET_PIPELINE stage gates); every scene, outfit, and LOD reuses them; any new character must pass a side-by-side sheet vs. `reference/chars/` before merge.

---

## 5. Frame Index (evidence)

| File | t (s) | Shows |
|------|-------|-------|
| `t_0.5` | 0.5 | Power-grab hall: crowd, blue light columns, gold power orb |
| `t_10` | 10 | REF-02 combat (white dress) + male fighter, motion streaks |
| `t_20` | 20 | Crowd surge, holo panels, dusk sky |
| `t_30` | 30 | Holo power-select UI (Chinese glyph rows, gold coin icon) |
| `t_40` | 40 | REF-03 close-up (glasses, blazer) |
| `t_60` | 60 | REF-01 close-up (outfit A, necklace) + crowd |
| `t_70` | 70 | REF-04 + REF-02 (civilian outfits) |
| `t_230` | 230 | REF-01 profile, outfit B (satin, wine glass) |
| `t_240` | 240 | REF-01 in weapons warehouse w/ mechs |
| `t_270` | 270 | Environment: coastal satellite dish |
| `t_320` | 320 | REF-06 trio backs, burning wreck |
| `t_330` | 330 | REF-01 extreme close-up (earring, amber eyes) |
| `t_340` | 340 | Public holo screen (names/numbers) |
| `t_345/347` | 345/347 | REF-05 System avatar full body |

*Regenerate anytime: `./scripts/extract_reference_frames.sh`.*

---

## 6. Rights & Similarity Note

The reference is a third-party TikTok video (handle on-frame). It is used **internally** as the visual target per project mandate. Because the footage itself appears AI-generated from an existing story genre, shippable assets should be produced as **"as-close-as-reasonable original interpretations"** of these sheets (same style/proportions/silhouettes/palette) rather than vertex-level clones, which both satisfies the mandate and keeps the shipped art defensible. Flag to stakeholder before store submission.

---
*End of CHARACTER_REFERENCE.md — pipeline: `ASSET_PIPELINE.md`.*
