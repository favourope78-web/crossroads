# DEVELOPMENT_PLAN.md — CROSSROADS (Working Title)
### Exact build order: prototype → playable game

| | |
|---|---|
| **Companion doc** | `GAME_DESIGN.md` (v0.1) — all design references below point to its sections |
| **Starting state** | Empty workspace (audited 2026-09-03). Greenfield. |
| **Team assumption** | 1 full-time Unity developer + part-time/outsourced 3D art & audio. Estimates assume this; scale by headcount. |
| **Total estimate** | ~7–9 months to release-candidate (Phase 0–7). Playable vertical slice at ~month 3–4. |
| **Rule #1** | No phase starts before the previous phase's **exit gate** passes. Gates are playtested **on a physical phone**, never only in the editor. |

---

## Milestone Map

```
M0 Foundation ──► M1 Combat Prototype ──► M2 Systems Core ──► M3 Vertical Slice
   (P0, ~1 wk)        (P1, ~3 wks)            (P2, ~4 wks)        (P3+P4, ~6 wks)
                                                                       │
   Release Candidate ◄── M5 Polish/Beta ◄──── M4 Alpha ◄───────────────┘
      (P7, ~2 wks)        (P6, ~5 wks)       (P5, ~10 wks)
```

| Milestone | Definition |
|-----------|------------|
| **M0** | Project opens on device, scenes route, services boot. |
| **M1** | 2 minutes of greybox combat on a phone that feels *good* (the do-or-die gate). |
| **M2** | Save/load, decisions, abilities, enemies all functional as frameworks (placeholder content). |
| **M3** | Prologue + Chapter 1 fully playable, menu→ending-of-Ch.1, with real decisions & saves. |
| **M4 (Alpha)** | All 13 playable scenes exist, all 7 endings reachable, feature-complete. |
| **M5 (Beta)** | Content-complete, polished, performance targets met on device matrix. |
| **RC** | Store-ready builds, compliance done. |

---

## Phase 0 — Project Foundation (Week 1) → M0

Exact order (each step depends on the previous):

1. **0.1 Create Unity project** — Unity 6 LTS (6000.x), 3D URP template, name `Crossroads`, at workspace root. Verify Unity Hub/Editor + Android & iOS modules installed.
2. **0.2 Git init** — repo + Unity `.gitignore` + Git LFS (track `*.png *.psd *.fbx *.wav *.ogg *.mp4 *.ttf`). First commit before any edits. Remote backup configured.
3. **0.3 Folder structure** — create exactly the tree in DESIGN §13.3 (`Assets/_Project/...`). Add assembly definitions: `Core`, `Gameplay`, `Narrative`, `UI` (compile times + dependency discipline from day 1).
4. **0.4 Packages** — Input System, Cinemachine, TextMeshPro (essentials), DOTween (or commit to hand-rolled tweens). Remove unused template packages.
5. **0.5 URP mobile config** — URP asset with: no realtime shadows on Low tier, HDR off (mobile), MSAA 2×, render scale 0.8–1.0 hook; 3 quality tier assets (Low/Med/High) wired into Quality Settings.
6. **0.6 Core services skeleton** (pure C#, no content):
   - `AppServices` (service locator) + `IGameService` lifecycle (`Init/Pause/Resume/Shutdown`).
   - `EventBus` (typed pub/sub structs).
   - `PoolService`.
   - `SceneFlowService` (async load + `02_Loading` scene).
   - `QualityManager` (device detect: RAM/GPU whitelist → tier; resolution scaling).
7. **0.7 Boot flow scenes** — `00_Boot` (init services → route), `01_MainMenu` (placeholder buttons: Play/Continue/Settings), `02_Loading`, `99_Sandbox`. Play button loads Sandbox through SceneFlow.
8. **0.8 Device build pipeline** — Android: keystore, min/target API per current store policy, IL2CPP/ARM64, build & install on one physical mid-range Android. iOS: bundle id, dev cert, build to one physical iPhone. **Both must boot to Main Menu → Sandbox.**
9. **0.9 Perf baseline scene** — Sandbox stuffed with 200 k tris + 30 enemies-worth of particle load; record fps/draw calls/memory per tier on both devices. This is the reference budget (DESIGN §13.1).

**Exit gate M0:** Boot → Menu → Sandbox → back, on both physical devices, no crashes; services log clean init/shutdown; repo pushed; baseline perf recorded.

---

## Phase 1 — Combat Prototype, Greybox (Weeks 2–4) → M1
> Goal: answer the only question that matters — *does heavy-action combat feel good on a phone?* Everything is greybox cubes/capsules. No story, no saves, no abilities beyond 3 test ones.

1. **1.1 Input layer** — `InputService` + `TouchControlsUI`: floating virtual stick (left zone), camera-drag (right zone), buttons ATK/DDG/A1–A3/ULT/INTERACT per DESIGN §8.1 layout. Keyboard/mouse + gamepad maps in the same `InputActionAsset`. Button sizing ≥88 dp; handedness flip stub.
2. **1.2 Camera rig** — Cinemachine 3rd-person follow (orbit on drag, collision solve, bounded pitch), placeholder framing values from DESIGN §8.
3. **1.3 Player motor** — CharacterController-based `PlayerMotor`: walk/run per spec (3.5/6.0 m/s, accel 40/60), dodge dash (12 m/s × 0.25 s, i-frames, 0.6 s CD), auto-vault ≤1.2 m. **No jump** (design decision).
4. **1.4 Combat FSM + hitboxes** — `PlayerCombatFSM` (Idle/Move/Light1-3/Heavy/Dodge/Hurt/Dead), animation-event → overlap-sphere hitbox spawning, layer matrix, `HealthComponent`, `DamageResolver` (damage numbers off — use hit-flash + knockback for greybox).
5. **1.5 Enemy framework + 2 archetypes** — `EnemyFSM` base; Grunt (approach → 2-hit combo) and Bruiser (frontal shield → forces flank/heavy). Telegraphs = colored ground rings + windup poses.
6. **1.6 WaveDirector + test arena** — one 40×40 m greybox arena, 3 scripted waves (≤3 simultaneous enemies), rune-glow spawn telegraph.
7. **1.7 Game feel pass #1** — hit-stop (60 ms), screen shake service, haptics (light hit / heavy hit / dodge), kill slow-mo (100 ms), camera punch on heavy.
8. **1.8 Lock-on** — auto soft-lock nearest in combat; horizontal flick to switch; lock framing (FOV 55→50).
9. **1.9 Three test abilities** — one per line, hardcoded-ish via new `AbilityDefinition` SO skeleton: Flamestep (dash-strike), Bulwark (parry), Mending Wave (heal). Cooldown UI on buttons. (Full ability system comes in Phase 2 — here only validate the *feel* of ability buttons in combat.)
10. **1.10 Device tuning week** — iterate 1.1–1.9 values **on phones only**. Log every tuning change in `Data/Balancing/`.

**Exit gate M1 (formal playtest, see §Cross-cutting):** 3 external testers × 2 physical devices play the arena for 10 min. Pass criteria: "fighting is fun" ≥4/5 average; no control complaints that block play; sustained 30+ fps on mid device; combat readable without tutorials. **If fail → iterate Phase 1 until pass. Do NOT proceed. Nothing downstream saves bad-feeling mobile combat.**

---

## Phase 2 — Core Systems Framework (Weeks 5–8) → M2
> Goal: turn prototype hacks into the real frameworks (DESIGN §13.4) with placeholder content. Order matters — state before save, save before decisions.

1. **2.1 GameState + StateMutator** — full state container per DESIGN §4.3; all writes through mutator, emitting `StateChanged` events. Unit tests (EditMode) for every mutator method.
2. **2.2 SaveSystem** — JSON slots ×3 + autosave, atomic write + CRC, `schemaVersion` + `SaveMigrator`, `ISaveBackend` abstraction; mobile lifecycle hooks (`OnApplicationPause/Focus` → save). Unit tests: save/load roundtrip of a fully-populated state.
3. **2.3 Checkpoint system** — checkpoint volumes → `lastCheckpointId`; death → respawn at checkpoint, wave reset; `CheckpointReached` event → autosave.
4. **2.4 AbilitySystem (real)** — `AbilityDefinition` SO (cost, cooldown, rank, targeting, hitbox/VFX/sfx refs, effects), `AbilityController` (slots, cooldowns, activation), `LoadoutService` (3 actives + ult + 2 traits), ultimate meter (charge from combat). Migrate the 3 test abilities onto it.
5. **2.5 Status effects** — Burn/Stun/Root/Fear/Slow/Shield as composable `StatusEffect` data; stack rules; VFX hooks.
6. **2.6 DecisionSystem + DialogueRunner** — `DecisionNode` / `DialogueGraph` SOs, Condition/Effect whitelists per DESIGN §4.2, coroutine runner, timeout handling for D2, `DecisionResolved` event → autosave. EditMode tests: condition evaluation + effect application matrix.
7. **2.7 Dialogue UI + Decision HUD** — typewriter text, choice cards (≥88 dp), timer ring for D2, post-choice affinity glyph feedback, pause framing.
8. **2.8 Affinity/Bond model** — meters, thresholds (30/60/90 resonance passives), dominant-line computation, bond tiers; debug overlay to force values (dev builds only).
9. **2.9 ResonanceTracker** — telemetry counters per DESIGN §4.4 with per-level caps.
10. **2.10 EnemyAI expansion** — Charger + Caster archetypes; elite skeleton; enemy `EnemyDefinition` SOs (stats from `BalanceTable`).
11. **2.11 Shrine UI + economy** — Resonance Shrine screen: unlock nodes, upgrade ranks, free respec; Echo currency wired to drops/decisions.
12. **2.12 HUD complete** — HP, ability cooldowns, ult meter, affinity glyphs, objective, pause menu (resume/settings/quit-to-menu with save).
13. **2.13 AudioService** — pooled sources, mixer (Music/SFX/UI), ducking; placeholder SFX set for all combat verbs.
14. **2.14 Integration test scene** — extend `99_Sandbox`: fight wave → D2 timed decision → ability unlock → shrine spend → checkpoint death/respawn → save → kill app → relaunch → load. **The full systems circle.**

**Exit gate M2:** 2.14 circle passes on both physical devices including app-kill/reload; all unit tests green; save file human-inspectable and matches schema.

---

## Phase 3 — Vertical Slice: Prologue + Chapter 1 (Weeks 9–12) → M3
> Goal: the first *real* game — content pipeline proven end to end. This phase establishes the template every later chapter copies.

1. **3.1 Level pipeline** — `LevelDefinition` SO (arena/wave tables, decision hooks, secrets, checkpoints); greybox all slice scenes with correct arena sizing (DESIGN §11.3 rules).
2. **3.2 Interlude scene shell** — one reusable narrative scene: backdrop slot, dialogue graph runner, shrine, milestone (D3) node, "your life so far" recap panel.
3. **3.3 Build P1 "Last Summer"** — no-combat exploration: movement/interaction tutorial beats, first Mara D1 choices (bond seeding), codex mote #1.
4. **3.4 Build C1L1 "Night of the Fracture"** — combat tutorial arenas (reuse Grunt/Bruiser), awakening set piece (scripted), first ability unlock via decision.
5. **3.5 Build I1 mentor choice** — the first D3: Kael/Odalys/Bran; mentor prefabs (placeholder art ok, silhouettes final); +20 affinity effects; mentor-specific follow-up dialogue.
6. **3.6 Build C1L2 "Under the Spire" + Boss 1** — line-flavored arena trims (3 variant dressings); `BossPhaseData` framework + **The First Echo** (2 phases: teaches dodge-timing, then ability check).
7. **3.7 WorldStateSystem v1** — district variant swapping (intact/contested flags), proven on Old Market dressing.
8. **3.8 FateStateDriver v1** — Mara spawn/behavior variants; mentor variant selection at load.
9. **3.9 Art pass 1 (outsourced starts here)** — final Ari model + rig, 3 mentor models, Grunt/Bruiser final, Old Market kit. Animation set: locomotion, 3-hit combo, heavy, dodge, hurt, ability casts (per line).
10. **3.10 Menu/pause/save-slot UI real** — slot cards with chapter/age/affinity summary, autosave indicator.
11. **3.11 First localization pass prep** — all slice text through string keys/table (no hardcoded strings audit).
12. **3.12 Slice integration + polish** — music layer 1, decision stings, loading cards ("Age 16 — Vessa"), full playthrough debugging.

**Exit gate M3:** A stranger picks up the phone and plays Menu → Prologue → Ch.1 → Boss → Ch.2 teaser interlude **unassisted**, in 35–45 min, with saves surviving app-kill. Playtest score ≥4/5 on "choices felt meaningful" and "combat fun". Performance within budget. **This slice is the production template — review and document lessons in `docs/POSTMORTEM_VS.md` before mass content.**

---

## Phase 4 — Content Production: Chapters 2–3 + Endings (Weeks 13–22) → M4 Alpha
> Goal: feature & content complete. Work chapter-by-chapter using the vertical-slice template; each chapter is a self-contained mini-milestone with its own device playtest.

**4.A Chapter 2 (weeks 13–16):**
1. 4.A.1 Path-variant routing from dominant affinity (tie → explicit choice in I2).
2. 4.A.2 C2A Contested Docks (assault framing, 4 arenas + D2).
3. 4.A.3 C2B Sanctuary (defense/protect-objective waves — new wave type: escort objective).
4. 4.A.4 C2C Long Wall (hold-the-line; new wave type: timed hold).
5. 4.A.5 Full ability trees per line (6 actives + 2 ults + 3 passives × Ember/Tide/Stone) + shrine economy balancing per DESIGN §3.4 curve.
6. 4.A.6 Dax arc: sparring memory (C1 flag check), C2X duel-vs-teamup boss variant, bond effects.
7. 4.A.7 Hollow line: 5 hidden betrayal choices placed across P–C2, Hollow shrine in I3, full Hollow kit.
8. 4.A.8 Ally AI (Mara/Dax assist states per DESIGN §9.3).
9. 4.A.9 Cross-line synergies table (DESIGN §6.3).
10. 4.A.10 Chapter 2 device playtest + balance pass. **Gate: all 3 path variants completable; both Dax outcomes work; Hollow reachable.**

**4.B Chapter 3 + endings (weeks 17–20):**
1. 4.B.1 I3 world-state reveal (district variants driven by C2 outcomes — ruined/rebuilt).
2. 4.B.2 C3L1 Market variant level (2 dressings, shared arena layout).
3. 4.B.3 C3L2 Ascent of the Spire (gravity-anomaly set pieces: one new mechanic — low-grav arenas).
4. 4.B.4 Final boss: 3 phases, state-dependent phase 2 (ally/enemy NPC inserts by fate flags), phase 3 dominant-line mechanics, **refusal choice** at phase transition.
5. 4.B.5 Ending evaluator (state matrix → `EndingDefinition`) + EP scene with 7 variants (VO text/particles/camera presets).
6. 4.B.6 Ending gallery + codex completion tracking.
7. 4.B.7 Full-run integration: every ending reachable from a fresh save (QA matrix §Cross-cutting).

**4.C Meta & systems completion (weeks 21–22):**
1. 4.C.1 `meta.json`: endings discovered, cosmetics, Memory boon (1 per completed run) + boon equip UI.
2. 4.C.2 Story-difficulty toggle; settings complete (haptics, handedness, remap, subtitles speed).
3. 4.C.3 All remaining art: Dax, Archivist, Charger/Caster/Elites final, Docks/Sanctuary/Wall/Spire kits, boss models.
4. 4.C.4 Audio complete: adaptive 3-layer music per chapter + per-line leitmotif, full SFX set, decision stings.
5. 4.C.5 VFX pass: per-line ability VFX, boss telegraphs, world-state ambiance.
6. 4.C.6 Cut all `TODO`/placeholder flags; string-table audit; dev-only overlays stripped from release config.

**Exit gate M4 (Alpha):** Complete game playable start→any of 7 endings on device; ending-reachability QA matrix 100%; feature freeze declared.

---

## Phase 5 — Polish, Performance, Beta (Weeks 23–27) → M5

1. **5.1 Device matrix perf campaign** — test devices: low Android (Snapdragon 6-series class), mid Android, iPhone 11-class, current flagship. 30-min sustained sessions (thermal). Fix per DESIGN §13.1 budget: LODs, occlusion, pooling, texture compression (ASTC), shader stripping.
2. **5.2 Combat & economy balance pass** — full-run data: deaths/level, echo income vs. curve (DESIGN §3.4), decision pick-rates (no option <10% or >60% without reason), boss durations 60–150 s.
3. **5.3 Onboarding & FTUE** — tutorial beats re-timed from playtests; first-session length check (target: first decision within 90 s of pressing Play).
4. **5.4 Game-feel polish** — haptics tuning per device, hit-stop/shake consistency, UI motion (tweens), loading masks.
5. **5.5 Accessibility** — colorblind-safe telegraph palette check, text sizes, one-hand mode verify, photosensitivity (flash) settings.
6. **5.6 Crash reporting + analytics** (opt-in, privacy-compliant): crash SDK, funnel events (session start/decision picks/deaths/ending reached).
7. **5.7 Localization execution** — ship languages (min: EN + 2, e.g. ES/PT or per market plan) via string tables; UI overflow checks.
8. **5.8 Beta playtest** — 10+ external players, 2 full runs each (different choices), survey: pillars check (DESIGN §1.2). Fix-list triage daily.

**Exit gate M5 (Beta):** content freeze; crash-free sessions ≥99%; 30 fps floor held on low device for 30-min session; beta survey ≥4/5 on all four pillars.

---

## Phase 6 — Release Prep (Weeks 28–29) → RC

1. **6.1 Store assets** — icons (adaptive), feature graphic, screenshots per device class, trailer (recorded from device builds), store copy.
2. **6.2 Compliance** — Play Console data safety form, App Store privacy nutrition labels, content ratings (IARC), target API level check.
3. **6.3 Release builds** — signed AAB (Play) + archive (App Store/TestFlight), versioning scheme `major.minor.patch`, smoke test on matrix devices.
4. **6.4 Closed testing** — Play internal testing track / TestFlight; 1-week soak; crash dashboards watched.
5. **6.5 Launch checklist** — rollback plan, hotfix branch, day-1 patch staging, review-response plan.
6. **6.6 Post-launch plan (documented, not built)** — cloud saves via `ISaveBackend`, additional endings/lines as content updates, seasonal cosmetics — scoped only after launch metrics.

---

## Cross-cutting Workflows (apply during every phase)

### Playtest protocol
- **Who:** dev daily (15 min arena), 3 external at every phase gate, 10+ at beta.
- **Where:** physical devices only for gates (editor runs don't count).
- **What:** scripted tasks ("reach the shrine", "die once on purpose", "replay a decision") + free play; think-aloud encouraged; session recorded (screen capture).
- **Metrics:** fun score (1–5), control complaints, deaths/arena, time-to-first-decision, fps/thermal notes.

### QA ending-reachability matrix (Phase 4+)
Enumerate state combinations: dominant line (4) × Mara fate (4) × Dax fate (4) × refusal flag (2) → reduced via equivalence classes to ~20 targeted saves; each ending verified reachable from at least one legal playthrough path. Automated save-injection tool (dev menu) loads crafted `GameState` JSONs.

### Definition of Done (per task)
Code committed + compiles on both platforms; no new warnings; runs on device without crash; placeholder art explicitly tagged `ART_TODO`; text via string keys; balances in `BalanceTable` SO, not magic numbers.

### Branching & builds
`main` (always device-buildable) ← `dev` ← feature branches (`feat/ability-system`, short-lived). Nightly dev build to test devices. Tag every milestone (`m1-combat-proto`…).

---

## Risk Register (top 8)

| # | Risk | Phase most exposed | Likelihood | Impact | Mitigation |
|---|------|--------------------|-----------|--------|-----------|
| 1 | Touch combat doesn't feel good | P1 | Med | **Fatal** | Hard M1 gate; 1.10 tuning week; copy-proven schemes (Brawl Stars/Auto Chess-class sticks, Genshin button cluster); kill/pivot decision made at gate, not later |
| 2 | Branch content cost overrun | P4 | Med-High | High | Foldback rule (DESIGN §2.3); variant = dressing not geometry; weekly scope review; pre-agreed cut list (crowd → synergies → 3rd C2 variant) |
| 3 | Thermal throttling long sessions | P5 | Med | High | Arena-based 10–15 min levels; 30 fps floor tier; aggressive pooling; 30-min soak tests from P2 onward |
| 4 | Save corruption on mobile lifecycle | P2 | Low-Med | High | Atomic writes + CRC + autosave fallback (DESIGN §12.1); app-kill tests in every gate |
| 5 | Art outsourcing latency stalls slice | P3–P4 | Med | Med | Slice proven with placeholder art; art orders batched per chapter; silhouette-first approval |
| 6 | Decision system opaque to players | P3+ | Med | Med | Glyph feedback, recap panel, codex; playtest question "what did your last choice change?" must get real answers |
| 7 | iOS/Android build-pipeline surprises | P0 | Low-Med | Med | Both platforms built in week 1 (step 0.8), never deferred |
| 8 | Scope creep past 2 h target | All | Med | Med | Chapter map is a contract (DESIGN §2.2); any addition requires an equal cut |

---

## Estimate Summary

| Phase | Weeks | Cumulative | Milestone |
|-------|-------|-----------|-----------|
| 0 Foundation | 1 | 1 | M0 |
| 1 Combat prototype | 3 | 4 | **M1 (go/no-go)** |
| 2 Core systems | 4 | 8 | M2 |
| 3 Vertical slice | 4 | 12 | **M3 (playable game exists)** |
| 4 Content production | 10 | 22 | M4 Alpha |
| 5 Polish/beta | 5 | 27 | M5 Beta |
| 6 Release prep | 2 | 29 | RC |

*Buffer: +15% recommended (≈ 4 weeks) → realistic RC at ~month 8. First playable (M1) in ~1 month; first real content playthrough (M3) in ~3 months.*

---

## Immediate Next Actions (when green-lit to build)
1. Execute Phase 0 steps 0.1–0.9 in order (they are the only approved "building" until M0 passes).
2. Draft the M1 playtest script and recruit 3 testers now (needed by week 4).
3. Commission art-direction probes (Ari + Old Market kit, stylized low-poly) so 3.9 has a pipeline ready.

---
*End of DEVELOPMENT_PLAN.md — see `GAME_DESIGN.md` for all referenced design details.*
