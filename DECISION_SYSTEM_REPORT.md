# DECISION_SYSTEM_REPORT.md — Interaction, Decision, Consequence, Progression, NPC & Power/Ability Systems
### CROSSROADS Foundation (Phase 2-native systems, built on the Phase 0–1 prototypes)

| | |
|---|---|
| **Date** | 2026-09-03 |
| **Branch** | `feat/npc-system` (on top of `feat/consequence-progression`, merged to `dev` + `main`) |
| **Scene** | `Assets/Scenes/Prototype/FirstLocation.unity` (environment + Ari **unchanged**; story additions additive) |
| **Character** | Ari prototype v1 — **unchanged** |
| **Design refs** | `GAME_DESIGN.md` §5.4/§9.1/§9.2 (NPC cast, fate states, one prefab + state driver), §4.2 (data-driven), §12 (save), §13.4 (services); `CHARACTER_REFERENCE.md` (visual bible: REF-02 Mara, REF-04 civilian) |
| **This phase** | 10/10 requirements of the **Core NPC System** task implemented; **277/277** headless checks; compile clean in both input defines; static validation 0 warnings |

---

## 1. Phase 2c — Core NPC System (this phase, maps 1:1 to the task list)

| # | Task | Implementation |
|---|------|----------------|
| 1 | Reusable NPC framework | `Assets/_Project/Scripts/Gameplay/NPC/`: `NpcBrain` (pure-C# state resolver), `NpcLogic` (pure-C# behaviour FSM), `NpcAgent` (Unity host + movement), `NpcInteractable` (interaction bridge). One drop-in component set on any GameObject |
| 2 | Every NPC has | **Unique ID + Name** (`NpcDefinitionData.id/displayName`) · **Character model/prefab** (`sheetRef` → CHARACTER_REFERENCE sheet + `avatarPrefab` slot for the canonical prefab; `baseMaterial`/trim variants today) · **Personality/state data** (`NpcPersonality` + `NpcStateData` fate states) · **Relationship value** (persisted bond −100..100 + tier) · **Dialogue data** (`NpcInteractionData.encounterId` → existing graphs) · **Available interactions** (condition-gated list) · **Behavior conditions** (`states[].conditions` → title/mood/behaviour overrides) |
| 3 | Reference-video visual style | `sheetRef` (Mara **REF-02**, Sera **REF-04**) ties each NPC to its CHARACTER_REFERENCE sheet; palette-conform materials (muted bases `M_Npc_Mara`/`M_Npc_Civilian`; affinity colours only as trim via per-state variants) — the exact "line colors in trim, never flat costume colors" rule; one canonical prefab per character will slot into `avatarPrefab` (placeholder primitives hide automatically), faces/hair never regenerated per scene (§ consistency law) |
| 4 | Basic behaviours | `NpcLogic` FSM: **Idle** · **Walking** (routine loop) · **Talking** (freezes + faces player during dialogue) · **Routine** (waypoints + dwell) · **Reacting to the player** (approach / avoid / face). Personality presets: *Friendly* (Mara walks to you), *Wary* (Sera steps back), *Curious* (approaches politely), *Reserved* |
| 5 | Connected to GameStateManager + DecisionManager | `NpcBrain` reads the LIVE `GameStateManager` (bond, bond tier, decisions, flags, items, abilities, reputation, areas) via the same `ConditionEvaluator` whitelist the decision system uses; `NpcAgent` re-resolves on every state event (bond/flag/decision/item/rep/skill/area/load/reset) and `NpcInteractable` runs dialogues through the existing `EncounterFlow`/`DecisionManager` |
| 6 | Dialogue & behaviour change per previous decisions | Data-driven `states` (first match wins): Sera has one state per drive flag (**Grateful** approaches / **Watchful** backs off farther / **Intrigued** approaches slowly); Mara's state is bond-gated. Dialogue graphs already branch on decisions; new payoff conversations (below) are per-decision too; title feeds the speaker chip |
| 7 | Two NPCs, different personalities | **Mara** — Friendly, bond-driven, approaches, patrols near the columns, confides when warm. **Sera** — Wary, distance-keeping, paces the lookout; her whole bearing flips with your first decision |
| 8 | Relationship change from an earlier decision | **Decision A (tide) → Mara bond +10 → Warm tier → "Mara · Warm" title → she stands closer → the INTERACT button becomes "Comfort Mara" → a new per-decision conversation.** The ember/stone paths keep her at "Talk to Mara" (bond 5/3 < 8). Tests prove path A vs path B produce different later reactions |
| 9 | Modular NPC data | NPCs are rows in `story_content.json` → `CL_C1_StoryContent.asset` (`npcs:`) — id, name, sheet ref, personality, behaviour numbers, states (conditions+overrides), interactions, routine. Adding a character = one row (mirrored in `StoryContentBuilder.cs` per repo convention), zero framework changes |
| 10 | Mobile-performance friendly | Pure-logic tick = a couple of distance comparisons + one move per NPC/frame; no allocations per frame (event-driven re-resolve), no raycasts, no pathfinding; ≤6 on-screen NPC cap per §9.2; avatar/animator hooks for the real meshes |

**New files**

```
Assets/_Project/Scripts/Gameplay/NPC/
  NpcBrain.cs        - pure resolver: definition x live GameStateManager -> active fate state,
                       title, mood line, behaviour profile, available interactions, bond/tier
  NpcLogic.cs        - pure behaviour FSM: Idle/Walk/Dwell/Approach/Avoid/ReactFace/Talk
                       (INpcWorld sink injected - fully headless-testable)
  NpcAgent.cs        - Unity host: identity, avatar/prefab + trim materials, routine,
                       event wiring (state x 9 + dialogue), NpcStatusChangedEvent publish,
                       movement/animator sink (INpcWorld impl)
  NpcInteractable.cs - Interactable bridge: label = first AVAILABLE interaction; runs it
```

Existing systems were **not rebuilt**: `PlayerInteraction`, `Interactable`, `DecisionManager`, `EncounterFlow`, `ConditionEvaluator`/`EffectApplier`, `GameStateManager`, SaveSystem all unchanged in behavior — the NPC framework is a new consumer of them. (`StoryEncounterNPC`/`NpcFateDriver` remain in the codebase and still work; the prototype scene now uses the framework.)

### 1.1 The playable proof (task test sequence)

```
Decision A (tide)  ──►  Mara bond 10 (Warm), title "Mara · Warm", approaches closer
                          INTERACT now reads "Comfort Mara" -> confide conversation
                          ("You got the twins out. Everyone saw it…")
                   ──►  Sera state = Grateful: guard drops, she WALKS TO you,
                          greets you as her sisters' saviour, shard story in a warm tone
Decision B (ember) ──►  Mara bond 5 -> stays "Talk to Mara"; confide locked
                   ──►  Sera state = Watchful: backs off FARTHER (avoid 2.6 -> 3.4),
                          warns you about the Choir instead ("The Choir has your scent now")
Decision C (stone) ──►  Sera = Intrigued: approaches slowly, studies you sidelong
Shard (any path)   ──►  Sera's interaction LIST grows ("show_shard", item-gated)
RESTART            ──►  everything above re-applies from the save: bond, titles,
                          behaviours, prompts, later conversations
```

Two independent save paths (tests group 20) confirm: same NPC, same room, **different reaction because of the earlier decision** — the required acceptance sequence.

---

## 2. Phase 2b — Consequence & Progression (prior phase; still authoritative)

| # | Task | Implementation |
|---|------|----------------|
| 1 | `GameStateManager` | `Narrative/GameStateManager.cs` — façade: reputation, bond/tier, abilities, items, skills, areas, flags, decisions, current area, `StatusLines()` |
| 2 | Data-driven attributes | `GameState` progression fields (all persisted, schema v2) + `ProgressionIndex` display names |
| 3 | Decisions → consequences | `EffectApplier` +7 effects (AddReputation/SetReputation/UnlockAbility/AddSkillLevel/AddItem/RemoveItem/UnlockArea), all writes via `StateMutator` |
| 4 | Consequences change the world | Gated doors/areas (`GatedDoor`, `AreaGate`), story objects (`StoryEventInteractable`), NPC drivers, area triggers, entity replay (`StoryWorldState`), condition-gated dialogue/choices (6 new `ConditionType`s) |
| 5 | Complete branching example | First Light → ember_reach/tide_clear/stone_still → different ability, gate text, shard line, Sera dialogue (see §4 of the phase-2b section below) |
| 6 | Brief indication of change | `EffectNotices` → change notices → `ToastUI` + `StateHUD` live update |
| 7 | Future encounters check prior state | Conditions + `GateRuleEvaluator.FirstMatch` |
| 8 | Story/choice data separate from code | `scripts/story_content.json` → asset generator → `StoryContentBuilder.cs` mirror → `validate_assets.py` cross-checks |
| 9 | Save/load complete state | schema v2 + v1→v2 in-memory upgrade; autosave on decision/area events |
| 10 | Restart keeps consequences | test group 13 + 21 prove full restore incl. NPC reactions |

Save shape (schema v2): all progression collections (`reputation`, `abilities`, `items`, `skills`, `unlockAreas`, `currentArea`) beside the legacy flags/world-states/entities/bonds/decisions/codex/affinities/echoes.

---

## 3. Architecture (design §13.4; everything headless-testable)

```
Assets/_Project/Scripts/
  Core/       Crossroads.Core
    GameState / StateMutator   - authoritative state + all writes; progression fields
    EventBus + StoryEvents     - typed pub/sub (+ NpcStatusChangedEvent)
    SaveSystem, SaveData       - JSON slots, schema v2 + v1->v2 upgrade, atomic writes
    Point3, ProximitySelector  - engine-free math/proximity (tested)
    InputLock, AppServices, StoryLog
  Narrative/  Crossroads.Narrative
    Content/ ContentData (StoryContentData + npcs/decisions/graphs/encounters/progression)
             StoryContentBuilder (code mirror), ScriptableObjectAssets, IEncounterSource
    DecisionManager / EncounterFlow / ConditionEvaluator / EffectApplier / EffectNotices
    GameStateManager / ProgressionIndex / GateRuleEvaluator
    GameServices (State, Progress, Decisions, Encounters, Save, Content)
  Gameplay/   Crossroads.Gameplay
    Interaction/ Interactable, PlayerInteraction, DoorInteractable, GatedDoor, AreaGate,
                 StoryEncounterNPC (kept), StoryEventInteractable
    NPC/        NpcBrain (pure) -> NpcLogic (pure) -> NpcAgent -> NpcInteractable
    WorldState/ StoryWorldState, NpcFateDriver (kept), AreaTrigger
  UI/         Crossroads.UI
    GameUIBootstrap, DialogueUI (+ speaker title chip), InteractionHUD, StateHUD,
    ToastUI, RuntimeMenuFactory, SafeAreaFitter
```

**NPC runtime wiring**

```
NpcAgent.Start ─► resolve definition (content.npcs[id]) ─► NpcBrain + NpcLogic + world
                ─► subscribe: bond/affinity/flag/decision/item/rep/skill/ability/area/
                              load/reset/dialogue events
state change   ─► Apply(): brain.Reapply() -> title + mood + profile (+ material trim)
                ─► if changed: NpcStatusChangedEvent {npcId,title,bond,tier,moodLine}
                               + NoticeRequestEvent(moodLine) when live (the "reacts" beat)
Update         ─► logic.Tick(dt, playerPos, playerNear, profile, talking) -> world moves:
                Friendly: approach -> stop at talkDistance -> face | Wary: avoid ->
                back to routine (waypoints + dwell) | Talk: freeze + face
Interact (button) ─► first AVAILABLE interaction (conditions) -> EncounterFlow.Run(encounter,
                CurrentTitle) -> DialogueStarted/Ended drive the Talk state
```

---

## 4. NPC data model (modular: one JSON row per character)

```jsonc
// scripts/story_content.json -> "npcs": [ ... ]
{
  "id": "sera", "displayName": "Sera", "sheetRef": "REF-04",
  "description": "A refugee from the lower halls. Wary of the Echo; warms only to proof of kindness.",
  "behaviour": { "personality": 2 /*Wary*/, "facesPlayer": 1, "reactRadius": 4.5,
                 "approachDistance": 0, "avoidDistance": 2.6, "talkDistance": 2.2,
                 "moveSpeed": 0.9, "turnSpeed": 4, "usesRoutine": 1 },
  "states": [   // first matching conditions win; -1 = inherit the base behaviour
    { "conditions": [ {type:0, "key":"c1_hall_drive", "value":"tide"} ],
      "title": "Sera · Grateful", "moodLine": "Sera's guard drops. She steps closer, unafraid.",
      "approachDistance": 1.5, "avoidDistance": 0, "moveSpeed": 1.0, "reactRadius": -1 },
    { "conditions": [ {type:0, "key":"c1_hall_drive", "value":"ember"} ],
      "title": "Sera · Watchful", "moodLine": "Sera keeps her distance. Your echo burns too bright.",
      "approachDistance": 0, "avoidDistance": 3.4, "moveSpeed": -1, "reactRadius": -1 },
    { "conditions": [ {type:0, "key":"c1_hall_drive", "value":"stone"} ],
      "title": "Sera · Intrigued", "moodLine": "Sera studies you sidelong, curious despite herself.",
      "approachDistance": 1.2, "avoidDistance": 0, "moveSpeed": 0.8, "reactRadius": -1 }
  ],
  "interactions": [   // ordered: the first AVAILABLE one is the INTERACT button
    { "id": "talk", "label": "Talk to Sera", "encounterId": "c1_hall_sera", "conditions": [] },
    { "id": "show_shard", "label": "Show the shard", "encounterId": "c1_hall_sera_shard",
      "conditions": [ {type:10, "key":"echo_shard"} ] }   // ItemHeld
  ],
  "routine": [ { "position": {"x":16.5,"y":0,"z":3.2}, "dwellSeconds": 2.0 },
               { "position": {"x":18.5,"y":0,"z":2.2}, "dwellSeconds": 2.0 } ]
}
```

Mara (REF-02, `personality: 1` Friendly, approach 1.6 → 1.3 when Warm) has a bond-gated state
(`BondAtLeast mara 8`) and two interactions: `confide` ("Comfort Mara", bond-gated, put FIRST so
the button changes when she warms) and `talk` ("Talk to Mara" → first-light encounter).

---

## 5. Verification

### ✅ Static (`python3 scripts/validate_assets.py`)
- GUID cross-references 0 unresolved; registry no duplicates; **NPC/Mara/Sera scene bindings** (NpcAgent ×2, NpcInteractable ×2, npcId fields, component refs) verified; scene 153 GameObjects / 134 SceneRoots.
- `CL_C1_StoryContent.asset` re-parses and matches `story_content.json` **field-for-field including the new `npcs` block** (states/conditions/interactions/routine/behaviour), graphs 4–5 and encounters 4–5.
- Every content string exists in the C# builder mirror (no drift; non-ASCII-aware).

### ✅ Headless flow tests — **277 passed / 0 failed**
Groups 1–15 (prior phases: proximity, flows, persistence, gating, gates, notices, schema upgrade)
plus the NPC groups:

```
16 NPC framework data: definitions (id/name/sheet/personality/states/interactions),
   every interaction resolves to a registered encounter, index names from content
17 Mara: Decision A(tide) -> bond 10 Warm -> title/approach/prompt/confide change;
   stone path -> bond 3 -> stays locked (TWO PATHS, different NPC state)
18 Sera: baseline Wary (avoid 2.6) -> tide Grateful (FLIPS to approach 1.5) /
   ember Watchful (avoid 3.4) / stone Intrigued (approach 1.2); item-gated
   interaction unlocks only after the shard, prompt keeps talk-first
19 NpcLogic FSM: friendly approach->face, wary retreat (moves AWAY), polite -> face only,
   routine walk->dwell->loop, talking freezes movement, out-of-radius -> idle
20 THE SEQUENCE, path A vs path B (independent saves): A -> Mara confide opens +
   tide lines + Sera thanks you; B -> confide locked + Sera warns you (different
   later encounters for the same NPC); ItemHeld shard story in her ember tone
21 RESTART: bond/title/behaviour/interactions/prompt re-applied from disk and the
   later conversation still matches the restored decision
```
Re-run: `cd scripts/decision_system_tests && mcs -langversion:latest -define:ENABLE_LEGACY_INPUT_MANAGER -out:FlowTests.exe TestJson.cs FlowTests.cs ../unity-stub/UnityStub.cs ../../Assets/_Project/Scripts/Core/*.cs ../../Assets/_Project/Scripts/Narrative/*.cs ../../Assets/_Project/Scripts/Narrative/Content/*.cs ../../Assets/_Project/Scripts/Gameplay/*.cs ../../Assets/_Project/Scripts/Gameplay/Interaction/*.cs ../../Assets/_Project/Scripts/Gameplay/WorldState/*.cs ../../Assets/_Project/Scripts/Gameplay/NPC/*.cs ../../Assets/_Project/Scripts/UI/*.cs ../../Assets/Game/Scripts/FirstLocationBootstrap.cs ../../Assets/Game/Scripts/ThirdPersonCameraController.cs && mono FlowTests.exe`

### ✅ Full C# compile check (both input modes)
`bash scripts/compile_check.sh` — every project source compiled against the Unity stub with
`ENABLE_LEGACY_INPUT_MANAGER` and `ENABLE_INPUT_SYSTEM` — both `Compilation succeeded`.

### ⏳ Runtime verification — pending Unity Editor (no editor in sandbox)
1. Play → no console errors; `TALK TO MARA` appears near Mara.
2. **Watch Mara**: idle near her without interacting — she should turn toward Ari and take a
   couple of steps closer (Friendly), then wander her short patrol loop when Ari leaves.
3. **Watch Sera** (fresh save): approach her — she should **step back** and keep ~2.6 m
   (Wary) instead of turning toward you.
4. First Light → choose **tide_clear** → toast + HUD update; **Mara's prompt changes to
   COMFORT MARA** and she stands closer (Warm, "Mara · Warm" chip on interact); the confide
   conversation plays the tide line.
5. Reset (RESET button) → **ember_reach** → Sera's title chip is different, she backs off
   farther, her greeting is the warning line; Mara's prompt stays TALK TO MARA.
6. North seal → annex → shard → Sera: talk graph gains the shard option (item-gated).
7. **Stop Play → Play again**: Mara still Comfort Mara + Warm; Sera still grateful/approaching;
   confide still plays the tide line — the persistence proof.
8. (Optional, when meshes arrive) assign `avatarPrefab` on NpcAgent once per character →
   placeholders hide automatically; keep `sheetRef` = the sheet the prefab was built from.

---

## 6. Adding an NPC (no core changes)

1. Add one row to `scripts/story_content.json` `npcs` (id/name/sheet/personality/behaviour/
   states/interactions/routine) + any new dialogue graphs/encounters it needs.
2. `python3 scripts/gen_story_content.py && python3 scripts/gen_firstlocation_scene.py && python3 scripts/validate_assets.py`.
3. Mirror the row in `StoryContentBuilder.cs` (convention; the validator enforces parity).
4. Scene: one GameObject with collider + body/head visuals + `NpcInteractable` (→ agent ref)
   + `NpcAgent` (npcId, base material, optional trim variants, optional avatar prefab).
5. Conditions/effects beyond the whitelist = one enum entry + one switch case; everything else is data.

## 7. Notes & fixes found while "checking well"

- **Behavior FSM is pure C#** (`NpcLogic` + `INpcWorld`): the movement/physics layer is 30 lines in `NpcAgent`; the FSM is unit-tested with a fake world (moves, directions, arrivals, freezes).
- **`NpcBrain` is event-agnostic by design**: `Reapply()` is called by the agent on relevant state events; tests call it explicitly after `Resolve()` — the pure/core split keeps headless tests honest (found by the failing title assertions).
- **`Point3` gained `+`, scalar `*`, `normalized`, `MoveTowards`, `Dot`, `magnitude`** (pure C#; used by the FSM; no Unity types leaked into logic).
- **Sera's graph embeds a decision** — sequence tests must `SelectChoice` before running the next encounter (runner rule: an embedded decision waits for the player).
- Unity stub grew `Vector3.MoveTowards`, `Quaternion.Slerp`, 2-arg `LookRotation`, `Mathf.Clamp01` (compile-only; the FSM uses Point3).
- `StoryContentBuilder.cs` needed `using Crossroads.Core;` (routines use `Point3`); NpcLogic needed the Narrative using (NpcStopData).
- The asset YAML for `npcs` required a dedicated emitter (nested conditions under list items, inline `position: {x,y,z}`, strict 4/6/8/10 indentation) — first run produced a stray `behaviour` list and a glued `npcs:` key; both caught by re-parsing and fixed.
- **Conventions kept**: data in JSON → asset → C# mirror, validated; GUIDs deterministic (NpcBrain 0x91 … NpcInteractable 0x94); scene generator is the only scene writer; `main` stays device-buildable, `dev` is integration.


---

## 8. Phase 2d — Core Power & Ability System (this phase)

Scope per the task: a reusable, data-driven ability foundation with **three initial decision→power paths**
(the complete catalogue is out of scope). Decision A/B/C in `dec_c1_hall_first_light` already granted one
of three powers; this phase promotes those placeholders into a full system: definitions, manager, state
machine, upgrades, blocking, NPC reactions, mobile UI, one in-world playable effect, and tests.

### 8.1 Data model (pure data — everything is `AbilityDefinitionData`)
`id, name, line (ember/tide/stone), description, category (Active), unlockHint, unlockConditions[],
vfxRef, sfxRef, echoCostPerLevel, levels[]` — one `AbilityLevelData` row per rank carrying
`level, cooldown, power, radius, duration, energyCost, description`. **Power→story fit:** the three
powers are the three answers to the Fracture light already established in Ch.1 (`ember_pulse` heat pulse,
`tide_mend` soothing swell, `stone_ward` stillness ring) — no out-of-genre supers.

### 8.2 Runtime — `Narrative/Abilities/AbilityManager.cs` (pure C#, no Unity types)
- Reads `GameServices.Progress` (persisted unlocks), ignores Unity; injected clock (`Now`) drives cooldowns
  (Unity binds `Time.time` in `StoryModeBootstrap`; tests drive a manual clock).
- State machine per power: **Locked → (Unlocked Lv1 → Lv2 → Lv3) | Blocked**. `Blocked` (sealed at the
  Echo Shrine) is persisted and beats `Unlocked`; a later `UnlockAbility` (e.g. re-claiming the light)
  clears the seal — "future decisions can unlock, block, upgrade or change behaviour" with zero core edits.
- `Activate(id)` → validates access → cost → cooldown → publishes `AbilityUsedEvent` carrying the
  **current level row** (upgrades genuinely change behaviour: 12/9/6 s cooldown, 3.5/4.5/6 m radius,
  ×1/×1.5/×2.25 power) → starts cooldown. No per-frame processing anywhere; cooldown state is session-only
  (never persisted — reloading a save leaves powers ready, by design).

### 8.3 Core extensions (single write path preserved)
- `StateMutator`: `BlockAbility`, `UpgradeAbility`, `SetAbilityLevel` (+ `LoadFrom` copies the new lists).
- `GameState`: `blockedAbilities`, `abilityLevels` (+ helpers `HasBlockedAbility`, `GetAbilityLevel`).
- `StoryEvents`: `AbilityLevelChangedEvent`, `AbilityBlockedEvent`, `AbilityUsedEvent` (payload = level row).
- `ContentData`: `AbilityCategory`, `AbilityLevelData`, extended `AbilityDefinitionData`,
  `ConditionType.EchoesAtLeast` / `AbilityLevelBelow`, `EffectType.UpgradeAbility` / `BlockAbility`
  (evaluator + applier + notices wired).
- `SaveData` **schema v3**: in-memory migration (v1→v2→v3) + `SaveSystem.Normalize` guarantees no null
  collections; old files load and upgrade, never rewritten in place.

### 8.4 Authored content (three initial paths + consequences)
- **Unlock** — `dec_c1_hall_first_light`: `ember_reach`/`tide_clear`/`stone_still` → exactly one power each.
- **Upgrade + Block** — new **Echo Shrine** encounter `c1_east_shrine` (East Annex, new interactable):
  `deep_*` options (gated `AbilityOwned` + `EchoesAtLeast 10` + `AbilityLevelBelow 3`) raise a rank
  (+1 attunement skill each); `seal_*` options block the power, pay 20 Echoes, set `c1_echo_sealed`;
  `leave` is always available. Options are condition-filtered, so the same data works repeatably.
- **NPC reactions** — Sera gained two data-driven states: `Sera · Attuned` (skill/level ≥ 2 → she notices
  the deepened bind) and `Sera · Warded` (sealed → she notices the silence). `NpcAgent` now re-applies on
  `AbilityLevelChangedEvent`/`AbilityBlockedEvent`; visual variants stay per the state pipeline.
- **Playable effect** — `Gameplay/Abilities/AbilityPulseVFX.cs`: on `AbilityUsedEvent` a radial pulse ring
  (one pooled cylinder, line-coloured, fade+expand) bursts at Ari — attached by `StoryModeBootstrap` to
  whichever player object is active. One object, no particles, idle = no work.

### 8.5 UI — mobile power sheet (`UI/AbilityHUD.cs` + `UI/AbilitySheetModel.cs`)
- `[POWERS]` toggle bottom-right (≥88 dp) opens a sheet listing every known power with live state:
  **LOCKED + unlock hint / SEALED / Lv N · READY (MAX) / recharging countdown**; tap a row to activate.
  Cooldown labels tick only while the sheet is open (0.25 s cadence, nothing runs while closed).
- `AbilitySheetModel` is a pure snapshot builder (display rows from manager + data) — headless-tested.

### 8.6 Verification
- **Headless flow tests: 432 passed / 0 failed** (was 277). New groups:
  [22] ability content contracts (defs, 3 ranks, cooldown/radius/power deltas, path↔ability map),
  [23] three decision paths → three different powers (owner unlocked, others locked, sheet rows match),
  [24] activation event payload, cooldown state machine (12 s), unknown/locked/cost gates,
  [25] shrine upgrades (level 2→3, echo cost, gates on echoes + max level, behaviour rows change),
  [26] seal → Blocked + refused activation + persisted block + NPC states (Attuned/Warded) + re-unlock wins,
  [27] restart persistence (unlock + level + seal restored; cooldown session-only) + v2→v3 migration.
  Two pre-existing expectations updated: Sera now has 5 states (3 drive + 2 ability-reaction) and the
  restart test asserts the ability-derived title (the tide+shard flow reaches attunement 2 by design).
- **Static validation** (`validate_assets.py`): 0 warnings — progression abilities now compare field-for-field
  including `levels`/`unlockConditions` (numeric-tolerant), scene GUIDs incl. the new shrine interactable.
- **Compile check**: clean under both `ENABLE_LEGACY_INPUT_MANAGER` and `ENABLE_INPUT_SYSTEM`
  (new folders `Narrative/Abilities`, `Gameplay/Abilities` in the harness).
- **Generators idempotent**: content + scene regenerate to identical shapes (117 scene objects / 135 roots).

### 8.7 Adding an ability (no core changes)
1. Add one `AbilityDefinitionData` entry (with `levels` rows) to `scripts/story_content.json`,
   mirror it in `StoryContentBuilder.cs`, regenerate (`gen_story_content.py` → `validate_assets.py`).
2. Grant it from any decision via `UnlockAbility`/`UpgradeAbility` effects (option conditions gate when).
3. If it needs a world effect, subscribe a new listener to `AbilityUsedEvent` (or extend `AbilityPulseVFX.abilityIds`).
4. NPC reactions: add a state row gated on `AbilityOwned`/`SkillAtLeast`/`AbilityLevelBelow`/flag — no code.
