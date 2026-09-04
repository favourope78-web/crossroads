# Core Branching Campaign System — Report

Phase goal: a **reusable, data-driven campaign framework** on top of every existing system
(GameStateManager, DecisionManager, NPC bonds, EncounterFlow, AbilityManager, WorldStateManager,
objectives, combat, player/mobile, save/load, content→asset→builder→validation pipeline), plus
**one polished vertical slice** proving the architecture — not the full story. Two players who
choose differently must reach meaningfully different conversations, NPC reactions, objectives,
abilities, world states and future events. No working system was rewritten; everything below
extends the existing managers additively.

**Result: 853/853 checks · asset validation clean (0 warnings) · both compile configs green ·
save v5 with v4→v5 migration · route fully survives restart.**

---

## 1. Architecture

```
content (story_content.json)
   │  gen_story_content.py            (chapters_out → CL_C1_StoryContent.asset, 4-space indent)
   ▼
StoryContentBuilder / ContentData      (campaign constants + mirrored content — compile-time parity)
   ▼
RuntimeContentSource → EncounterFlow (existing graphs/decisions)
                    → GameStateManager (existing flags/vars/bonds/abilities/objectives)
                    → CampaignManager  (NEW — pure derivation, owns NO state)
   ▼
GameState.campaign* fields (v5)  ←── StateMutator writes / LoadFrom restores
   ▼
CampaignServices (boot order + autosave) · CampaignHUD (presentation-only) · CampaignEvents
```

**`Gameplay/Campaign/CampaignManager.cs` (301 lines)** — the whole campaign is *derived*, never
stored as control state: chapters activate from `entryConditions`, beats cascade from
prerequisites → offer conditions → `TriggerSatisfied` (the existing, battle-tested trigger
language: flags, vars, decisions, bonds, abilities, objectives — incl. `ObjectiveFailed`),
branches route from conditions, chapters complete from `completionConditions`. The only persisted
things are the *outcomes*: `campaignBeats` / `campaignBranches` / `campaignChapters` /
`campaignJournal` (GameState v5 fields) — everything else re-derives from live state, so a save
can never contradict the systems that produced it.

`Refresh()` runs an outer loop (≤4 passes): **activate chapters → resolve beats/branches →
complete chapters**, repeating until stable. That single cascade means a chapter whose entry flag
just appeared (e.g. `ch_whispers` ← `ch1_complete`) activates, journals its start (guarded by the
persistent `chap_started_<id>` flag so it never re-journals), resolves its beats and completes —
all in the *same* refresh, no external ordering assumptions.

**`Gameplay/Campaign/CampaignServices.cs` (126 lines)** — boot shim (`StoryModeBootstrap`:
GameServices.Init → WorldServices.Init → CampaignServices.Init), subscribes *before* setting
`IsInitialized`, and autosaves (`PersistNow(autosaveMirror:true)`) on every campaign-changing
event (chapter started/completed, beat resolved, branch taken). Non-linear by construction:
availability is computed from decisions, NPC bonds, owned abilities, world state, completed/failed
objectives and flags — there is no fixed sequence anywhere in the code.

**`Core/CampaignEvents.cs`** — `CampaignChangedEvent`, `CampaignChapterStartedEvent`,
`CampaignBeatResolvedEvent`, `CampaignBranchTakenEvent`, `CampaignChapterCompletedEvent`
(payloads carry ids/labels). **`UI/CampaignHUD.cs`** — top-center presentation-only HUD: current
chapter + objective, latest journal lines, unlocked story paths (branch labels). It owns no logic;
all `[63]`-style assertions run without UI instantiation.

## 2. Data model & data files

| File | Role |
|---|---|
| `scripts/story_content.json` | source of truth: 2 chapters, 12 beats, 11 branches, 8 encounter graphs, 6 decisions |
| `scripts/gen_story_content.py` | GUID registry (through 0xbb) + FOLDER_META_PATHS incl. Gameplay/Campaign; emits the asset |
| `Assets/_Project/Data/Decisions/CL_C1_StoryContent.asset` | regenerated, YAML-verified |
| `Assets/_Project/Scripts/Narrative/Content/StoryContentBuilder.cs` | mirrored constants (content↔code parity) |
| `scripts/validate_assets.py` | chapters parity incl. **deep completionEffects field parity** (type/key/value/amount per effect) + branch-ref integrity |
| `scripts/add_campaign_content.py` | the one-shot seeder that created the chapter scaffolding (kept for history; content edits now go straight to JSON) |

A chapter = `id`, `title`, `entryConditions`, `completionConditions`, `beats[]` (prerequisites,
offerConditions, trigger, journalText, branches) and `branches[]` (label, conditions,
completionEffects: flags/vars/bonds/unlockAreas). Designers add chapters by editing the JSON,
regenerating, and validating — **no CampaignManager rewrite** (proven by `ch_whispers`, which was
added purely as data and chains off chapter one with zero code changes).

## 3. The branches (≥3 meaningful, all consequential)

The slice's trunk decision (`dec_c1_hall_first_light`, "The Trode asks what you reached for")
offers three routes; failure of an objective creates a fourth:

| Branch | Trigger | Changes downstream |
|---|---|---|
| **br_trode_ember** (Path of Ember) | decision `ember_reach` | `beat_ember_mastery` → grants **ember_pulse** ability; `obj_ember_beacon` (silence the beacon, follow-up cache objective); sera's echo line *"Ember still hums under your nails…"*; second-door branch `br_second_door` (requires the owned ability) |
| **br_trode_tide** (Path of Tide) | decision `tide_clear` | `beat_tide_path` → `obj_tide_keepsake` (return the keepsake → follow-up `obj_tide_report`); sera's *"You went into the water for the twins…"* |
| **br_trode_stone** (Path of Stone) | decision `stone_still` | `beat_stone_path` → `obj_stone_barricade` (brace twice); sera's *"Stone remembers hands that stay."* |
| **br_line_fell** (The Line Fell) — *failure as a route* | objective **failed** (echo sealed before braced) | `beat_stone_fell` → **does not** complete the trunk — opens `beat_recovery` (`obj_stone_rebuild`: clear 2 rubble) → `br_line_reheld` sets `path_resolved` → chapter completes through the recovery route |
| Side branches | `camp_sera_echo` decision: `tell_her` (+2 sera bond) / `deflect` | `br_told_sera` vs `br_deflected`; bond ≥7 unlocks `beat_sera_confide` (waystation key + echoes) |

Same-objective divergence: the three path beats each fire **different world interactions** (beacon
/ keepsake / barricade), spawn different world objects (rubble only on the fell route), and the
warden encounter (`beat_warden`, `obj_warden_hunt`) reacts to route state with sera bond +5 on
defeat. The slice ends at `beat_council` → `ch1_complete` → **`ch_whispers`** (Chapter Two:
"Whispers Under the Hall") activates, journals and completes in the same cascade — proving
chapter chaining via pure data.

## 4. Vertical slice flow (played start-to-finish by the tests)

exploration (world state/interactions) → Trode NPC → dialogue (`g_c1_hall_first_light`) →
**decision** → branch taken + journaled → route-specific **objective** (+ ability on ember) →
world interaction completes it → settle branch → `beat_council` → chapter completes → Chapter Two
cascades → **save** → **restart** → decision, branches, beats, chapters and journal restored
1:1 — including the chapter-start journal lines and the exact branch route. On the stone route the
objective can *fail* mid-slice and the run continues through `beat_recovery` to the same chapter
completion via a different branch — no game over.

## 5. Save system (v5) & migration

GameState gained four `List<string>` fields (`campaignBeats/Branches/Chapters/Journal`,
journal capped, oldest-first). `SaveSystem.Normalize` null-guards them, and — the bug this phase
ended on — **`StateMutator.LoadFrom` now restores all four** (it previously copied only legacy
fields, silently discarding the loaded route; the refresh re-derived beats/branches/chapters so it
*looked* restored, but flag-guarded chapter-start journal lines were unrecoverable — exactly what
test 62 caught). Schema bumped to v5; a pre-campaign **v4 save loads, upgrades in-memory and the
route re-derives live** from the restored decision (verified by the migration checks). Campaign
autosave mirrors to the autosave slot.

## 6. Tests (`scripts/decision_system_tests/CampaignTests.cs`, tests 57–63, 57 checks)

Content contracts (asset↔builder↔manager parity, branch-ref integrity) · three-way trunk
(trode → ember/tide/stone each yield different beats, objectives, sera reactions) · chapter
completion chain (ch1 → ch2 in one cascade) · **failure-as-route** (barricade falls → failure
beat → recovery → chapter still completes; `ObjectiveFailed` as a first-class trigger) · dependent
branches (bond-gated confide, ability-gated second door) · save/load restart (whole route incl.
journal survives) · v4→v5 migration. Wired into `FlowTests` Main/GetLog → **853 passed, 0 failed**
(full suite: story/world/combat/mobile/campaign).

## 7. Validation & compile

- `python3 scripts/validate_assets.py` → **VALIDATION PASSED** — 145 scene roots, 0 warnings
  (chapters parity incl. deep `completionEffects` field parity, branch-reference integrity).
- `bash scripts/compile_check.sh` → **both** `ENABLE_LEGACY_INPUT_MANAGER` and
  `ENABLE_INPUT_SYSTEM` compile (14 pre-existing warnings each), now including
  `Gameplay/Campaign/*.cs`, `Core/CampaignEvents.cs`, `UI/CampaignHUD.cs`.
- Scene binding: CampaignHUD wired via `GameUIBootstrap` (top-center; StateHUD top-left,
  ObjectiveHUD top-right); boot order via `StoryModeBootstrap`.

## 8. What's next (not in this phase)

The framework is ready for content scale-up: more chapters per the design doc, branch-gated
areas beyond the hall, and (post-slice) objective-failure variants on the ember/tide routes.
Per scope: no full story, no large enemy roster, no multiplayer, no advanced AI.

## 9. Commit

Feature commit: **`<HASH>`** — *"Core branching campaign system: data-driven chapters/beats/branches (CampaignManager + CampaignServices, CampaignEvents, CampaignHUD), v5 save fields + LoadFrom restore + Normalize guards, stone-route failure-as-recovery, sera side branches (bond-gated confide, ability-gated second door), ch_whispers chained by data, content+builder+asset regen, deep completionEffects validation, CampaignTests 57–63; 853/853 checks + validation + both compile configs"*.
