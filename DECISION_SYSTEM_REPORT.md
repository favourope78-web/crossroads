# DECISION_SYSTEM_REPORT.md — Interaction, Decision, Consequence & Progression
### CROSSROADS Foundation (Phase 2-native systems, built on the Phase 0–1 prototypes)

| | |
|---|---|
| **Date** | 2026-09-03 |
| **Branch** | `feat/consequence-progression` (on top of `feat/decision-system`, merged to `dev` + `main`) |
| **Scene** | `Assets/Scenes/Prototype/FirstLocation.unity` (environment + Ari **unchanged**; story additions additive) |
| **Character** | Ari prototype v1 — **unchanged** |
| **Design refs** | `GAME_DESIGN.md` §2.1/§4 (decision system), §5.2 (world state), §8.3 (interaction), §9.1 (Mara), §12 (save), §13.4 (services) |
| **This phase** | 10/10 requirements of the **Consequence & Progression** task implemented; 204/204 headless checks; compile clean in both input defines |

---

## 1. Phase 2a — What was built (interaction + branching decisions)

| # | Task | Where |
|---|------|-------|
| 1 | Reusable interaction system | `PlayerInteraction` + `Interactable` base (proximity scan, nearest-wins, priority ties) — doors/holo-panels keep working |
| 2 | Clear interaction prompt when close | Mobile **[INTERACT]** button bottom-left (`InteractionHUD`), label per target ("TALK TO MARA") |
| 3 | First story encounter from GAME_DESIGN | **"The First Light"** — Mara in the Fracture Hall, the awakening beat (§11.2 P→C1L1); content in `CL_C1_StoryContent.asset` + `StoryContentBuilder` |
| 4 | Short dialogue/story event | `EncounterFlow` + `DialogueUI` (typewriter, speaker chip, tap-to-advance) |
| 5 | 2–3 meaningful choices | Exactly 3, one per affinity line (Ember / Tide / Stone) |
| 6 | Choice stored persistently | `ResolvedDecisionEntry` in `GameState.decisions`, autosaved at resolution |
| 7 | Choices produce different consequences | Flags + affinity + Mara bond + **world state** + **spawnable consequence objects** + different aftermath dialogue + different re-talk dialogue (all condition-gated) |
| 8 | `DecisionManager` | Register / Store / Check (`IsResolved`, `ResolvedOption`) / Expose (`AllDecisions`, `VisibleOptions`, `Get`) |
| 9 | Branching test A→A, B→B, C→C | Headless suite: 3 flows, each asserts its own state (see §4) |
| 10 | Data-driven, no code changes per encounter | Content = `StoryContentData` POCOs via ScriptableObject asset; adding an encounter = one asset entry (recipe in §6) |
| 11 | Decisions persist after restart | JSON at `persistentDataPath/crossroads/save_slot_0.json`; boot reloads and replays world state |
| 12 | Android/mobile UI | Runtime-built uGUI: SafeArea, ≥88dp targets, big choice cards, landscape layout per §8.1 |

---

## 2. Phase 2b — Consequence & Progression system (this phase, maps 1:1 to the task list)

| # | Task | Implementation |
|---|------|----------------|
| 1 | `GameStateManager` tracking current player state | `Assets/_Project/Scripts/Narrative/GameStateManager.cs` — façade over `GameState`: `Reputation(groupId)`, `Bond`, `BondTier`, `HasAbility`, `HasItem`, `Skill`, `CurrentArea`, `AreaUnlocked`, `StatusLines()` (player card), `Describe()` |
| 2 | Data-driven player attributes | `GameState`: `reputation` (groupId→−100..100), `bonds` (npcId→−100..100 + `BondTier` Hostile/Wary/New/Warm/Bonded/Kin), `abilities`, `items`, `skills` (skillId→level), `unlockAreas`, `currentArea` — alongside existing affinities/echoBank/flags/worldStates/entities/decisions/codex. Named via `ProgressionIndex` (content-driven display names, zero hardcoding) |
| 3 | Decisions → consequences | `EffectApplier` extended with 7 new effects (AddReputation, SetReputation, UnlockAbility, AddSkillLevel, AddItem, RemoveItem, UnlockArea) — all writes through `StateMutator` |
| 4 | Consequences change the world | • **NPC behavior**: `NpcFateDriver` (Sera, Mara) re-applies material + title per state — live on bond/flag/decision/save events<br>• **Available dialogue**: Sera's encounter options are condition-gated (lookout / shard_show / keep_low)<br>• **Available interactions**: `GatedDoor` (data-driven rule door), `StoryEventInteractable` (shard) appears/disappears per state<br>• **Accessible areas**: `AreaGate` (Energy Seal) opens only with the ability from your first choice; `AreaTrigger` tracks current area; unlocks persist across restart<br>• **Future choices**: options hidden/unlocked by `FlagIs`, `ItemHeld`, `SkillAtLeast`, `ReputationAtLeast`, `AbilityOwned`, `AreaUnlocked`<br>• **Player abilities**: `UnlockAbility` grants Ember Pulse / Tide Mend / Stone Ward |
| 5 | Complete branching example | "The First Light" → ember_reach / tide_clear / stone_still → **different ability, different gate scenario, different shard line, different Sera dialogue & options** (see §4.3) |
| 6 | Brief indication of what changed | `EffectNotices` builds a `ChangeNotice` list ("Ember +10", "Ability unlocked: Ember Pulse", "The Choir −10") → `ToastUI` + `StateHUD` both live-update |
| 7 | Future encounters check prior decisions + state | 6 new `ConditionType`s (ReputationAtLeast, ItemHeld, AbilityOwned, AreaUnlocked, SkillAtLeast + existing FlagIs/DecisionWas/BondAtLeast); `GateRuleEvaluator.FirstMatch(rules, state)` for gates |
| 8 | Story/choice data separate from code | `scripts/story_content.json` (canonical) → `gen_story_content.py` → `Assets/_Project/Data/Decisions/CL_C1_StoryContent.asset`; `StoryContentBuilder.cs` mirrors it for the runtime fallback; `validate_assets.py` cross-checks all three |
| 9 | Save/load complete game state locally | Save schema **v2**: all progression fields serialized; in-memory v1→v2 upgrade preserves legacy saves; autosave on every decision + area unlock + area change |
| 10 | Restart keeps consequences | Test group 13: full restart — 3 decisions, ability, open area, item, reputation, skill level, current area, entity state all restored and re-applied to the scene |

**New files this phase**

```
Assets/_Project/Scripts/
  Narrative/
    GameStateManager.cs        - player-current-state façade + StatusLines player card
    ProgressionIndex.cs        - content-driven display names (abilities/skills/items/rep groups/areas/NPCs)
    EffectNotices.cs           - "what changed" notices from applied effects
    GateRuleEvaluator.cs       - pure FirstMatch(rules, state) for gate/conditional logic
  Gameplay/
    Interaction/
      GatedDoor.cs             - data-driven rule-based door (rules → open/locked + flavor)
      AreaGate.cs              - energy-seal gate: per-path open/shut + persistence via UnlockArea
      StoryEventInteractable.cs- non-NPC story beat that runs an encounter graph (the shard)
    WorldState/
      NpcFateDriver.cs         - state → material/title variant driver (Sera, Mara)
      AreaTrigger.cs           - persists currentArea (hall / annex) via trigger volumes
  UI/
    StateHUD.cs                - live player card (affinities, standings, bonds, powers, skills, resources, area, save status) + DEV reset
    ToastUI.cs                 - post-choice "what changed" toast + world notices (NoticeRequestEvent)
```

> The existing interaction system, `DecisionManager`, `EncounterFlow`, FirstLocation environment and Ari were **not rebuilt** — they were extended (interfaces/events/condition–effect whitelists grew, runners gained the new hooks only).

---

## 3. Architecture (design §13.4 service architecture, kept headless-testable)

```
Assets/_Project/Scripts/
  Core/       Crossroads.Core        (no deps; pure C# except 2 tiny Unity adapters)
    GameState / StateMutator         - single authoritative state; ALL writes via mutator
      + progression fields: reputation, bonds, abilities, items, skills, unlockAreas, currentArea
    EventBus + StoryEvents           - typed pub/sub (+ change notices, progression events)
    SaveSystem, SaveData             - JSON slots, schema v2 + v1→v2 upgrade, atomic .tmp->replace
    InputLock                        - global input gate while dialogue/choices are up
    Point3, ProximitySelector        - engine-free proximity rules (unit-tested)
    AppServices / StoryLog           - service locator + injectable logging
  Narrative/  Crossroads.Narrative   (refs Core)
    Content/  StoryContentLibraryAsset (ScriptableObject, authorable content)
              StoryContentBuilder      (code-built fallback + test content; mirrors JSON)
              ContentData              (DecisionNodeData/EffectData/ConditionData/GateRuleData/ProgressionContentData)
    DecisionManager                   - register / present / resolve / expose
    EncounterFlow                     - dialogue+decision state machine (runner)
    ConditionEvaluator / EffectApplier- whitelists (+7 effects / +6 conditions)
    GameStateManager / ProgressionIndex / EffectNotices / GateRuleEvaluator
    GameServices                      - typed facade: State, Progress, Decisions, Encounters, Save
  Gameplay/   Crossroads.Gameplay   (refs Core, Narrative)
    Interaction/ Interactable, DoorInteractable (slide anim, SetOpen), GatedDoor, AreaGate,
                 PlayerInteraction, StoryEncounterNPC, StoryEventInteractable
    WorldState/ StoryWorldState (replays persisted entity variants), NpcFateDriver, AreaTrigger
    StoryModeBootstrap                - boots services, loads save, autosaves on pause
  UI/         Crossroads.UI         (refs Core, Narrative, Gameplay)
    GameUIBootstrap (scene), RuntimeMenuFactory, DialogueUI (+ speaker title chip),
    StateHUD, ToastUI, InteractionHUD, SafeAreaFitter
```

**Event flow — the complete consequence loop**

```
tap INTERACT → StoryEncounterNPC.OnInteract → EncounterFlow.Run("c1_hall_first_light")
  → 3 dialogue lines → DecisionPromptEvent (3 cards) → SelectChoice("ember_reach"…)
      → EffectApplier → StateMutator writes: flag, affinity, bond, world state,
        entity markers, codex, echoes + NEW: reputation, ability, skill, item, area unlock
      → EffectNotices → NoticeRequestEvent → ToastUI ("what changed" toast)
      → ResolvedDecisionEntry recorded → autosave (atomic JSON)
  → aftermath line (condition-gated) → DialogueEndedEvent → input unlock

Same loop for the shard (StoryEventInteractable) and Sera (StoryEncounterNPC) —
every beat is content, every gate is a rule.

After the choice the world itself re-evaluates:
  AreaGate.Refresh():  rules(AbilityOwned ember_pulse|tide_mend|stone_ward) → seal cracks open + per-path text
  NpcFateDriver.Refresh(Sera):  dialogue, title and look change per stored drive flag
  NpcFateDriver.Refresh(Mara):  bond tier → title variant
  AreaTrigger.OnTriggerEnter:  currentArea = annex/hall → autosave
  StoryWorldState:  shard entity stays off once taken (persisted)

RESTART: StoryModeBootstrap loads save → StoryWorldState replays entity/area variants,
  AreaGate re-opens (unlock persisted), NpcFateDriver re-applies titles, StateHUD shows the
  restored card, re-talk shows the stored-drive opener and never re-presents resolved decisions.
```

---

## 4. Content & the branching proof

### 4.1 Player attributes (all data-driven, all saved)

| Attribute | Shape | Example |
|---|---|---|
| Reputation | `reputation[groupId] −100..100` | choir −10 · folk +8 · wards +8 |
| Bonds | `bonds[npcId] −100..100` + tier | Mara +5 → **Warm** (New → Warm at 5) |
| Abilities | `abilities` list | `ember_pulse` (Ember Pulse) |
| Items | `items` list | `echo_shard` (Echo Shard) |
| Skills | `skills[skillId] → level` | `echo_attunement` 1 |
| Areas | `unlockAreas` + `currentArea` | `annex` unlocked · current `annex` |
| Affinities/echoes | existing | ember 10 · echoBank 15 |
| Story flags | existing | `c1_hall_drive = ember` |

### 4.2 Condition / effect whitelist (extended)

**Conditions** — new: `ReputationAtLeast` (9), `ItemHeld` (10), `AbilityOwned` (11), `AreaUnlocked` (12), `SkillAtLeast` (13) — plus existing `FlagIs`, `DecisionWas`, `AffinityAtLeast`, `BondAtLeast`, `EchoesAtLeast`, `WorldStateIs`, `Always`.

**Effects** — new: `AddReputation` (11), `SetReputation` (12), `UnlockAbility` (13), `AddSkillLevel` (14), `AddItem` (15), `RemoveItem` (16), `UnlockArea` (17) — plus existing flag/affinity/bond/echoes/codex/world-state/entity/spawn.

Adding a *new type* later = one enum entry + one switch case (by design §4.2); everything else stays data.

### 4.3 The short playable proof sequence

```
Mara — "The First Light"  (3 choices)
  ├─ ember_reach   → flag drive=ember  · Ember +10 · bond Mara +5 · choir −10
  │                   · ABILITY: Ember Pulse · echoes +15 · hall burns red
  ├─ tide_clear    → flag drive=tide   · Tide +10  · bond Mara +10 · folk +8
  │                   · ABILITY: Tide Mend · echoes +20 · twins clear the east hall
  └─ stone_still   → flag drive=stone  · Stone +10 · bond Mara +3 · wards +8
                      · ABILITY: Stone Ward · echoes +15 · hall turns to mirror-still stone

North Energy Seal (AreaGate) — same room, now openable
  · Ember Pulse   → "The seal sings, cooling to red glass…"   → annex unlocked
  · Tide Mend     → "The seal flows apart like a curtain…"     → annex unlocked
  · Stone Ward    → "The seal stills… you walk through light." → annex unlocked
  · no ability    → "A seal of the hall's own light. It does not move for you."

Annex — Echo Shard (StoryEventInteractable)
  · per-path line (drive flag) · take → item echo_shard, echoes +25, skill +1, shard stays off forever

Sera at the lookout — same room, different person per your first choice
  · drive=tide   → she hails you as kin (bond +4 option) + "lookout with me" option
  · drive=ember  → wary: shard_show only if you *hold* the shard, otherwise keep_low
  · drive=stone  → only the fallback: quiet, she keeps her distance

RESTART: everything above is restored from save — seal already open, shard gone,
  Sera's dialogue still matches the drive flag, HUD card matches.
```

### 4.4 Save format (schema v2)

```json
{
  "schemaVersion": 2,
  "meta": { "slotName": "Ari - FirstLocation", "timestamp": "…", "playtimeSec": 0 },
  "scene": { "sceneKey": "FirstLocation", "checkpointId": "hall_spawn" },
  "gameState": {
    "flags": [{ "key": "c1_hall_drive", "value": "ember" }],
    "worldStates": [{ "key": "hall", "value": "ember" }],
    "entities": [{ "key": "echo_shard", "value": false }],
    "bonds": [{ "key": "mara", "value": 5 }],
    "decisions": [{ "decisionId": "…", "optionId": "…", "summary": "…", "resolvedAt": "…" }],
    "codex": ["c1_echo_ember", "c1_echo_first_light"],
    "reputation": [{ "key": "choir", "value": -10 }],
    "abilities": ["ember_pulse"],
    "items": ["echo_shard"],
    "skills": [{ "key": "echo_attunement", "value": 1 }],
    "unlockAreas": ["annex"],
    "currentArea": "annex",
    "ember": 10, "tide": 0, "stone": 0, "hollow": 0, "echoBank": 40
  }
}
```

- **v1 → v2 upgrade**: in-memory migration keeps every legacy field, adds empty progression collections — old saves load and continue; new fields then accumulate normally.
- **Atomic writes**: `.tmp` → `File.Replace`; corrupt/old-schema files refused gracefully.
- **Autosave triggers**: every decision resolution, area unlock, area change; `OnApplicationPause(true)` / `OnApplicationFocus(false)`.
- **DEV helpers**: `StoryModeBootstrap.devClearSaveOnStart` checkbox + ✕ RESET STATE button in `StateHUD` (editor/dev builds only).

---

## 5. Verification

### ✅ Static (run: `python3 scripts/validate_assets.py`)
- GUID cross-references: **0 unresolved**; no duplicate GUIDs in the registry.
- `CL_C1_StoryContent.asset` parses as YAML and matches `scripts/story_content.json` field-for-field (list-based emitter; decisions/options/conditions/effects/graphs/progression indentation verified).
- Every content string in the JSON also exists in the C# fallback builder (no drift — the validator catches drift).
- Scene sanity: 153 GameObjects / 134 SceneRoots; seal gate + shard + Sera + Mara + 2 area triggers + `StoryWorldState` bindings all present; `AreaGate`/`NpcFateDriver`/`StoryEventInteractable` MonoBehaviour GUIDs match their `.meta` files.

### ✅ Headless flow tests — **204 passed / 0 failed** (`scripts/decision_system_tests/`, mcs+mono)
```
1  walk → prompt appears / priority ties / disappears            [proximity rules]
2  First Light A/B/C: dialogue → 3 choices → state asserted
3  autosave on resolution → disk round-trip
4  variant consequences (markers, aftermath, re-talk)
5  re-talk: no re-prompt, variant opener, no double record
6  D2 gating + timeout (reserved; content intact)
7  save resilience: atomic write, corrupt tolerance, delete
8  content contracts: unknown encounter = clean no-op
9  GameStateManager: attributes + StatusLines player card
10 shard flow + re-talk variants (take / leave, entity stays off)
11 Sera per-drive dialogue + future-choice gating
   (lookout hidden off-tide · shard_show only with item · stone→fallback only)
12 gate rules per ability (opens/falls back per path text)
13 FULL RESTART: 3 decisions, ability, open area, item, rep, skill=2,
   currentArea, entity stays off — all restored
14 change notices: "Ember +10" / "Ability: Ember Pulse" / "The Choir −10"
15 v1 save upgrade: schema v2, legacy preserved, new fields default
```
Re-run: `cd scripts/decision_system_tests && mcs -langversion:latest -define:ENABLE_LEGACY_INPUT_MANAGER -out:FlowTests.exe TestJson.cs FlowTests.cs ../unity-stub/UnityStub.cs ../../Assets/_Project/Scripts/Core/*.cs ../../Assets/_Project/Scripts/Narrative/*.cs ../../Assets/_Project/Scripts/Narrative/Content/*.cs ../../Assets/_Project/Scripts/Gameplay/*.cs ../../Assets/_Project/Scripts/Gameplay/Interaction/*.cs ../../Assets/_Project/Scripts/Gameplay/WorldState/*.cs ../../Assets/_Project/Scripts/UI/*.cs ../../Assets/Game/Scripts/FirstLocationBootstrap.cs ../../Assets/Game/Scripts/ThirdPersonCameraController.cs && mono FlowTests.exe`

### ✅ Full C# compile check (both input modes)
`scripts/compile_check.sh` compiles **every** project source (Core, Narrative, Narrative/Content, Gameplay, Interaction, WorldState, UI, both Game scripts) against the dev-only Unity stub with `ENABLE_LEGACY_INPUT_MANAGER` **and** `ENABLE_INPUT_SYSTEM` — both `Compilation succeeded`.

### ⏳ Runtime verification — pending Unity Editor (no editor in sandbox)
Run one Play-mode pass (menu *CROSSROADS ▸ Prototype ▸ Build Ari Prefab & Test Scene* first if needed):
1. Log `[CROSSROADS] Game UI ready` + `[CROSSROADS] PlayerInteraction ready on Ari`; no console errors. **Also confirm `ToastUI` is attached** (`ToastUI.Attach` wiring in `GameUIBootstrap` is a known pre-existing item to verify).
2. Reach Mara → `TALK TO MARA` → tap → 3 lines → choose **tide_clear**.
3. Toast reads `locked in ◆ Tide +10 … saved ✓`; HUD card updates; twins appear; **check `ToastUI` visible** with the change notices.
4. Walk north to the Energy Seal → it stays shut without an ability; tap it → flavor line. *(Optional: verify the open state with an ability by making a fresh save on each path.)*
5. Enter the annex → `currentArea` flips to annex in HUD; take the shard → shard disappears.
6. Sera: her title/line differs from the tide path; lookout option visible only on tide.
7. **Stop Play → Play again** → seal already open, shard gone, Sera still tide-aware, HUD card restored — the persistence proof.
8. Use the RESET button (dev) → first encounter re-presents cleanly.

---

## 6. Adding a future encounter (no core changes)

1. Edit `scripts/story_content.json` (canonical) → run `python3 scripts/gen_story_content.py` (regenerates the asset) → `python3 scripts/validate_assets.py` (cross-checks asset ↔ JSON ↔ C# builder).
2. Or author directly in the `StoryContentLibraryAsset` inspector (decisions / graphs / encounters / progression).
3. Drop a `StoryEncounterNPC` (or `StoryEventInteractable`) on a GameObject, set `encounterId`.
4. Gate anything with the condition whitelist; add gate rules to `GatedDoor`/`AreaGate`; bind consequence objects in `StoryWorldState.entities`.
5. Conditions/effects beyond the whitelist = one enum entry + one switch case; nothing else changes.

## 7. Notes & fixes found while "checking well"

- **Phase 2a notes** (kept): fixed latent v1 scene-emitter rotation bug (`m_LocalRotation` identity → now `qy⊗qx⊗qz`, verified unit quaternions); JsonUtility can't serialize dictionaries → entry-lists with dictionary helpers; one id-keyed `StoryContentLibrary` asset instead of one asset per node; D2 timed choices implemented and reserved; `dev` branch was stale and was brought up to date.
- **This phase**: found and fixed a real runner bug — `end:true` dialogue nodes with a final line called `EndRun()` before publishing, so the last line never rendered; now the final line is published and waits for `Advance`.
- Two patches (ContentData enum extension, SaveSystem v1→v2 migration) were lost mid-session and re-applied after grep verification; `validate_assets.py` now also catches builder/JSON drift.
- `GatedDoor` is the data-driven rule door (base + rules); `DoorInteractable` was rewritten as its slide-animation base (same GUID, scene doors unchanged).
- Energy Seal moved to z=20.5 in the doorway so it doesn't z-fight the door panels.
- `GameStateManager.AreaUnlocked()` is the canonical API (one accessor — `IsAreaUnlocked` alias removed during the compile check).
