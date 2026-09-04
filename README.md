# CROSSROADS (working title)

A decision-based 3D life/action game for mobile, built in Unity.

> Every decision — who you trust, who you save, who you betray — rewires your powers, rewrites the city, and determines which of 7 endings your life lands on. One playthrough is a tight 1.5–2 hour "life in four chapters".

## Project status

**Design phase — no gameplay code yet.** The design foundation and exact build order are complete:

| Document | Contents |
|----------|----------|
| [`GAME_DESIGN.md`](GAME_DESIGN.md) | Full game design: concept, decision system, powers (4 ability lines), branching & endings, NPCs, game loop, world structure, save system, required Unity scenes/systems, mobile performance budget |
| [`DEVELOPMENT_PLAN.md`](DEVELOPMENT_PLAN.md) | Exact build order from prototype to release: Phases 0–6, milestones M0–M5 with exit gates, playtest protocol, risk register |

Next step per the plan: **Phase 0 — Project Foundation** (create the Unity 6 LTS project in this repo root, folder structure, boot scenes, device build pipeline).

### Phase 0 status (partially scaffolded in-sandbox, 2026-09-03)
Already in repo: folder tree (`Assets/_Project/...` per GAME_DESIGN §13.3), 5 assembly definitions (`Crossroads.Core/Input/Gameplay/Narrative/UI`), `Packages/manifest.json` (URP 17, Input System, Cinemachine, UGUI + modules), `ProjectSettings/ProjectVersion.txt` (Unity 6000.0.23f1).
On a dev machine: open the repo root in Unity Hub → editor completes steps 0.5–0.9 (URP config, boot scenes, device builds) and generates `.meta` files — **commit metas on first open**.
Visual-analysis deliverables: `CHARACTER_REFERENCE.md`, `ASSET_PIPELINE.md`, `FIRST_ASSET_BRIEFS.md`, concept sheets in `reference/concept/`.
Prototype character delivered (2026-09-03): Ari v1 — rigged/animated/textured FBX set + Unity prefab tooling + test scene; see `CHARACTER_PROTOTYPE_REPORT.md`. Runtime Unity test pending a machine with the editor (report §5–6).
First environment delivered (2026-09-03): Fracture Hall — modular kit (13 pieces) + `Assets/Scenes/Prototype/FirstLocation.unity` + follow camera + interactables; see `ENVIRONMENT_PROTOTYPE_REPORT.md`.

First playable interaction + decision system delivered (2026-09-03): proximity interaction with mobile [INTERACT] prompt, the data-driven **"The First Light"** encounter in the Fracture Hall (Mara, 3 affinity choices → distinct persisted states + condition-gated aftermath dialogue/re-talk), `DecisionManager`/`EncounterFlow`/`GameState`/`SaveSystem` per GAME_DESIGN §4/§12, runtime uGUI (dialogue sheet, choice cards, state HUD, toast), JSON persistence that survives restart, 115/115 headless flow tests + full compile check; see `DECISION_SYSTEM_REPORT.md`. Runtime Editor verification pending (same hand-off pattern as the prototypes).

World State & Mission/Objective system delivered (2026-09-04): reusable `Gameplay/World/` assembly — data-driven `WorldStateSystem` (open/closed areas, changed objects, NPC locations, story flags, completed objectives, condition-gated interaction unlocks) + `ObjectiveManager` (offer/complete/fail conditions, counters, checklists, consequences + follow-ups — all through `EffectApplier`), event-driven on the `EventBus` (no per-frame polling), `WorldActionInteractable` (ability-gated world actions), `NpcRelocator` (persisted NPC placements), mobile `ObjectiveHUD`, save schema v4 (objective phases, NPC locations, interaction unlocks, re-sealed areas) with v3 migration, 3 decision-dependent path objectives + 3 follow-ups incl. a failable crisis + recovery, 566/566 headless tests + asset/scene regeneration + validation; see `WORLD_OBJECTIVE_REPORT.md`. Runtime Editor verification pending (same hand-off pattern).

## Tech stack (planned)

- **Unity 6 LTS** (6000.x), **URP** (mobile-optimized)
- Input System (touch + gamepad), Cinemachine, TextMeshPro/UGUI
- Target: Android / iOS, 30 fps floor on mid-range devices, 60 fps on high tier
- Stylized low-poly art; ScriptableObject-driven abilities/decisions/dialogue

## Repository layout

```
/
├── GAME_DESIGN.md        # Game design document (source of truth for design)
├── DEVELOPMENT_PLAN.md   # Build order & milestones
├── README.md
├── .gitignore            # Unity ignore rules
├── .gitattributes        # Line endings, Unity YAML merge, Git LFS patterns
├── scripts/
│   └── git-setup.sh      # One-time repo config helper (identity, LFS, remote)
└── (Unity project files — Assets/, ProjectSettings/, Packages/ — arrive in Phase 0)
```

## Getting started (for developers)

Prerequisites: **Git**, **Git LFS** (`git lfs install` once per machine), **Unity 6 LTS** with Android + iOS build modules.

```bash
git clone <repo-url>
cd <repo>
./scripts/git-setup.sh "Your Name" "you@example.com"   # sets commit identity + activates LFS
```

When the Unity project exists (Phase 0+): open the repo root in Unity Hub.

### Branching model

- `main` — always device-buildable; tagged at every milestone (`m0-foundation`, `m1-combat-proto`, …)
- `dev` — integration branch
- `feat/*`, `fix/*` — short-lived feature branches off `dev`

See `DEVELOPMENT_PLAN.md` → "Cross-cutting Workflows" for the Definition of Done and build rules.

## License

All rights reserved (private project). No license granted for reuse of code, art, or design documents without permission.
