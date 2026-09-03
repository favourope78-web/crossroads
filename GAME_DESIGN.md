# GAME_DESIGN.md — CROSSROADS (Working Title)
### A decision-based 3D life/action game for mobile

| | |
|---|---|
| **Version** | 0.1 (Design Foundation) |
| **Date** | 2026-09-03 |
| **Status** | Approved-for-planning — no gameplay code exists yet |
| **Engine** | Unity 6 LTS (6000.x), URP |
| **Platforms** | Android / iOS (primary), portrait-optional but designed **landscape** |
| **Session length** | 10–15 min per level; **1.5–2 h** full playthrough |
| **Genre** | 3D action (hack-and-slash) + narrative decision game ("life-journey" structure) |

---

## 0. Project Audit (What Actually Exists)

An inspection of the workspace was performed before writing this document.

**Findings:**
- The workspace root (`/home/user`) was **completely empty**.
- No `Assets/`, `ProjectSettings/`, `Packages/` folders — no Unity project exists.
- No `.unity` scenes, no C# scripts, no art/audio assets, no settings files.
- A filesystem-wide search (`*.unity`, `ProjectSettings` directories) confirmed no project exists elsewhere.
- **Nothing was deleted or modified.** Only the two new documents (`GAME_DESIGN.md`, `DEVELOPMENT_PLAN.md`) were added.

**Consequence:** This is a **greenfield** project. Section 13 defines the required Unity scenes/systems from scratch, and Section 14 lists *all* missing systems (everything is missing — so it is prioritized into tiers that gate development). The exact build order lives in `DEVELOPMENT_PLAN.md`.

**Confirmed product decisions** (from stakeholder input, 2026-09-03):
1. Greenfield build — documents first, no feature code yet.
2. Target platform: **mobile (Android/iOS)** — touch controls, strict performance budgets.
3. Scope: **short focused experience, ~1–2 hours per playthrough**, high replayability (not a 365-day real-time sim; the "life journey" is compressed and dramatized).
4. Action intensity: **heavy action** — full real-time combat with enemies, combos, and power-based builds as a core loop.

---

## 1. Core Game Concept

### 1.1 Elevator pitch
> **CROSSROADS** is a mobile 3D action game about one ordinary person whose life is split open by a supernatural event. Every decision — who you trust, who you save, who you betray — literally rewires your powers, rewrites the city around you, and determines which of 7 endings your life lands on. One playthrough is a tight 1.5–2 hour "life in four chapters"; the build you carry into the final fight is the sum of the person you chose to become.

### 1.2 Design pillars
1. **Choices become powers.** Decisions don't just change story text — they grant, upgrade, or lock away concrete combat abilities. The player's build *is* their biography.
2. **The city remembers.** The same districts are revisited across life stages, visibly altered by the player's earlier decisions (ruined vs. rebuilt, hostile vs. friendly population).
3. **Short, dense, replayable.** A full life in ~2 hours. Multiple endings (7), a hidden power line, and a discovered-choices codex drive replays.
4. **Mobile-first action.** Combat designed for touch from day one: virtual stick + 6 buttons, auto-lock targeting, arena-based encounters, 30 fps floor / 60 fps target, 10–15 min levels that fit real play sessions.

### 1.3 Tone & fantasy
- **Tone:** grounded contemporary city + supernatural fracture; personal and warm in dialogue, kinetic and weighty in combat. Stylized low-poly art with strong silhouettes and bold color language (each power line owns a color: Ember = orange/red, Tide = teal/blue, Stone = ochre/grey, Hollow = violet/black).
- **Player fantasy:** "My life is my build." You are not a chosen one at the start — you *become* extraordinary through decisions, and the kind of extraordinary is yours to choose.

### 1.4 Unique selling points
- Decision system with **systemic consequences** (affinity meters, NPC fate states, world-state variants) rather than cosmetic dialogue branches.
- **Playstyle resonance:** the game silently tracks how you fight (aggression, defense, exploration) and biases which powers upgrade faster — even "not choosing" is a choice.
- **One city, four lives:** heavy asset reuse through world-state set-dressing variants — production-efficient *and* thematically resonant.
- **Foldback branching** (diamond structure): real branch variety without exponential content cost.

---

## 2. Story Overview — The Life Journey

### 2.1 Premise
The player character (**"Ari"**, default name, player-renamable) grows up in **Vessa**, a mid-size coastal city. At age 16, an event called **the Fracture** tears open above the city's Spire district, saturating a handful of people — including Ari — with energy called **Echo**. Which Echo "line" resonates with Ari is not random: it is shaped by the decisions Ari keeps making. A faction called **the Choir** wants to harvest the Fracture; Ari's life becomes the battleground.

### 2.2 Chapter map (life stages → action chapters)

| # | Chapter | Age | Life stage | Content | Est. time |
|---|---------|-----|-----------|---------|-----------|
| P | **Prologue — "The Last Summer"** | 10 | Childhood | No-combat tutorial level; movement/interaction teaching; first friendship choices (Mara) | 8–10 min |
| 1 | **"Fracture"** | 16 | Youth | Fracture night (combat tutorial + awakening); **Mentor choice** sets starting affinity; first line abilities; Boss: *The First Echo* | 25–30 min |
| 2 | **"Becoming"** | 20 | Early adulthood | **3 path variants** (Ember/Tide/Stone) of one chapter; build deepens; Dax rivalry peaks (duel OR team-up) | 30 min |
| 3 | **"Reckoning"** | 26 | Adulthood | Convergence; city shows accumulated world state; final decisions; final boss with state-dependent phases | 30 min |
| E | **Epilogue** | 30+ | Resolution | Ending scene rendered from state matrix (7 endings) | 5–10 min |

**First playthrough ≈ 100–120 min.** Replays are faster (skip dialogue, known branches) ≈ 60–80 min.

### 2.3 Structural rule: foldback branching
Chapters branch **within** themselves (2–3 path variants in Ch. 2; world-state variants in Ch. 3) but always converge at the chapter boss. State (affinities, flags, relationships, abilities) carries forward and changes *how* the convergence plays out (dialogue, enemy composition, boss phases, available final choices). This keeps content cost linear-ish while making choices feel structural.

---

## 3. Player Character & Progression

### 3.1 Character
- **Ari** — one playable character, cosmetic variants unlocked via meta-progression (Section 10.4). Single rigged model; ability VFX/color trim change with dominant affinity (cheap "build identity" visual).
- Third-person action kit: light combo, heavy attack, dodge, interact + 3 ability slots + 1 ultimate (Section 8).

### 3.2 Affinity meters (the progression backbone)
Three visible meters + one hidden:

| Meter | Gained from | Color | Gameplay meaning |
|-------|------------|-------|------------------|
| **Ember** | Bold, aggressive, confrontational choices; aggressive playstyle | Red/orange | Offensive abilities; damage resonances |
| **Tide** | Empathetic, connective, merciful choices; ally-protecting playstyle | Teal/blue | Sustain/support abilities; NPC assist calls |
| **Stone** | Disciplined, patient, principled choices; defensive playstyle | Ochre/grey | Defense/control abilities; stagger immunity |
| **Hollow** *(hidden)* | Specific selfish/betrayal choices only (5 exist in the whole game) | Violet/black | Secret 4th ability line; locks out 2 endings |

- Range 0–100 each. Choices grant +5…+20. Combat behavior grants +1 per qualifying action (capped per level) — the **resonance tracker** (Section 4.4).
- **Resonance thresholds:** at 30 / 60 / 90 in a line, passive bonuses activate (e.g., Ember 60: "+10% ability damage"; Tide 60: "healing pulses also cleanse"; Stone 90: "dodge gains a shockwave").
- **Dominant line** (highest meter at a milestone) gates the Chapter 2 path variant and major ability-tree access. Ties → player picks explicitly at the interlude.

### 3.3 Ability acquisition & upgrading
- **Unlocks:** milestone decisions, affinity thresholds, hidden choices, exploration secrets (Hollow shrines), boss defeats.
- **Currency: Echoes** — earned from combat (per enemy), first-time exploration, and decision outcomes (some choices grant lump sums).
- **Resonance Shrines** (one per interlude, run by the Archivist NPC): spend Echoes to unlock ability-tree nodes and upgrade owned abilities (2 ranks each: +damage/effect, +cooldown reduction or added effect).
- **Respec is free** at any shrine — encourages experimentation, zero grind anxiety on a 2-hour game.

### 3.4 Progression curve

| Point | Actives owned | Ult | Passives | Echoes spent (approx) | Power feel |
|-------|--------------|-----|----------|----------------------|------------|
| End Prologue | 0 | – | – | 0 | Ordinary kid |
| Mid Ch.1 | 2 | – | 1 | 200 | Awakened |
| End Ch.1 | 3 | 1 | 1 | 600 | Competent |
| End Ch.2 | 4–5 (hybrid possible) | 1 (+rank) | 2–3 | 1,500 | Formidable |
| Mid Ch.3 | 6 | 2 | 3–4 | 2,800 | Peak build |
| Final fight | 6 + situational ally assists | 2 | 4 | – | Legend / Tragedy (depends on state) |

Deliberate design: player ends a run owning ~60–70% of their main line (or a hybrid) — the rest is replay bait, discoverable in the codex.

---

## 4. Decision System

### 4.1 Decision taxonomy

| Type | Where | Timing | Example | Typical effects |
|------|-------|--------|---------|-----------------|
| **D1 — Dialogue choices** | Interludes, NPC conversations | Untimed (mobile-friendly default) | "Ask Kael about the war" vs. "Ask about the Fracture" | Affinity, flags, codex, small Echo rewards |
| **D2 — Pressure choices** | During/around combat | **Timed (5–10 s ring timer)** | "Chase the fleeing caster" vs. "Shield the trapped civilians" | Spawns, immediate world state, affinity, ability unlocks |
| **D3 — Milestone choices** | Chapter interludes (fixed nodes) | Untimed, saved-before-commit | Choose mentor; choose Ch.2 path; final Ch.3 choice | Structural: next level variant, ability line access, ending eligibility |
| **D4 — Silent decisions (resonance)** | Everywhere | Passive telemetry | Play aggressively, protect allies, explore secrets | Slow affinity drift; upgrade discounts |

### 4.2 Data model (ScriptableObject-driven)
All decision content is data, not code:

```
DecisionNode (ScriptableObject)
├── id: string                      // "c2_docks_save_civilians"
├── promptText / speaker            // localized keys
├── timeLimitSeconds: float         // 0 = untimed
├── options: DecisionOption[]
│   ├── textKey
│   ├── conditions: Condition[]     // gates visibility/selectability
│   └── effects: Effect[]           // applied on selection
├── onTimeoutOptionIndex: int       // for D2 (default = "hesitate" outcome)
└── codexEntryId                    // logged to journal on resolution
```

- **Condition types** (whitelist, expandable): `FlagIs`, `AffinityAtLeast(line, n)`, `BondTier(npc, tier)`, `AbilityOwned(id)`, `ItemHeld`, `ChapterIs`, `Chance(p)`.
- **Effect types** (whitelist): `SetFlag`, `AddAffinity(line, n)`, `AddBond(npc, n)`, `UnlockAbility(id)`, `GrantEchoes(n)`, `SpawnSceneEntity(key, active)`, `SetWorldState(district, variant)`, `RouteScene(sceneKey)`, `PlayCinematic(id)`, `AddCodex(id)`, `KillNPC(id)/ReviveNPC(id)` (fate states).
- **Dialogue graphs:** `DialogueGraph` SO = ordered list of `DialogueNode` (speaker, text, optional embedded `DecisionNode`, next). Runner = coroutine-based `DialogueRunner`; UI = typewriter text + choice list (touch-friendly, ≥88 px targets).

### 4.3 Game state container
A single authoritative `GameState` (plain C# class, serializable) holds everything decisions read/write:

```csharp
GameState {
  int chapterId, levelId;                 // progress
  string lastCheckpointId;
  Dictionary<string,bool> flags;
  Dictionary<string,int> vars;            // generic ints
  int ember, tide, stone, hollow;         // affinities 0..100 (hollow hidden)
  Dictionary<string,int> bonds;           // npcId -> -100..100
  List<string> abilitiesUnlocked, abilityRanks, traits;
  int echoes;
  List<string> codexDiscovered;
  string dominantLine;                    // recomputed at milestones
}
```

All systems read state via `GameServices.State`; all writes go through `StateMutator` API (so saves, telemetry, and the codex see every change in one place).

### 4.4 Resonance tracker (silent decisions)
Lightweight playstyle telemetry → affinity drift:
- Aggressive acts (kills within 3 s of engage, no-damage kills, heavy-attack finishes) → Ember +1 (cap +5/level).
- Protective acts (heals landed, allies kept alive, shields used to block for NPC) → Tide +1.
- Defensive/exploration acts (successful parries/dodge-cancels, secrets found, no civilian casualties) → Stone +1.
- Rule of thumb: telemetry drift can never exceed ~20% of choice-driven affinity — choices stay king.

### 4.5 Decision UX rules (mobile)
- Choice UI pauses gameplay camera but **not** the world for D2 (pressure must feel real; timer ring around the prompt).
- Every choice displays a subtle affinity glyph hint *after* selection (not before) — teaches the system without spoilers.
- **Autosave fires immediately before every D3 milestone** and a "Decision made — this cannot be undone" confirmation appears on irreversible flags.
- No choice is a dead end: every option routes to valid content (foldback guarantee).

---

## 5. Consequences & Branching Paths

### 5.1 Consequence categories

| Horizon | Mechanism | Example |
|---------|-----------|---------|
| **Immediate (seconds)** | Spawn toggles, level routing, NPC reaction lines | Chasing the caster → elite mini-fight now, civilians rescued by Mara (+bond) |
| **Level (minutes)** | Encounter composition, arena layout variant, secret availability | Saved the dockworkers → they barricade a flank for you in the next arena |
| **Chapter (10s of min)** | Path variant selection, boss variant/phases, ally availability | Ember-dominant → Ch.2 plays "Contested Docks" assault; Tide → "Sanctuary" defense |
| **Ending (run)** | Ending matrix evaluation | See 5.3 |
| **Meta (across runs)** | Codex entries, discovered endings, cosmetic unlocks | Secret Hollow shrine location shown on replay map after first discovery |

### 5.2 World-state variants ("the city remembers")
Districts exist once as geometry; each has 2–3 **set-dressing + lighting + population variants** toggled by `WorldState` flags:

| District | Variant A | Variant B | Variant C |
|----------|-----------|-----------|-----------|
| Old Market | Intact (P, C1) | Contested — Choir graffiti, patrols (C2 Ember path) | Ruined (C2 failure) / Rebuilt (C2 success, Tide/Stone paths) |
| Docks | Working docks (C2 Ember) | Flooded sanctum (C2 Tide) | Fortified wall (C2 Stone) |
| Spire Uptown | Sealed (C1–C2) | Breached, gravity anomalies (C3) | Collapsed (C3, if Hollow ≥ 25) |

Implementation: `WorldStateVariant` prefabs (child sets) swapped at scene load from `GameState.flags`; baked lighting per variant is avoided — use one bake + colored light probes/volumes per variant to stay in mobile budget.

### 5.3 Ending matrix (7 endings)
Evaluated once, in Ch.3 final decision node, from state:

| # | Ending | Primary requirements |
|---|--------|---------------------|
| 1 | **Ashen Crown** | Ember dominant (≥60); Dax dead/enemy; Choir destroyed by force |
| 2 | **Tide's Embrace** | Tide dominant (≥60); Mara bond ≥ Tier 3 (alive & bonded) |
| 3 | **The Unmoved** | Stone dominant (≥60); city districts ≥2 in "saved/rebuilt" state |
| 4 | **Hollow Throne** *(secret)* | Hollow ≥ 25; take the final betrayal option |
| 5 | **Balance** | No line ≥ 60 (deliberate hybrid); Mara AND Dax alive (hardest) |
| 6 | **The Long Way Home** | Choose "refuse the call" in C3 (available always); consequences flavor by state |
| 7 | **Martyr's Dawn** | Any dominant + final self-sacrifice option (mentor alive modifies epilogue text) |

Endings are short cinematic scenes (same epilogue location, variant VO/text/particles) — cheap to produce, high replay value. Ending gallery on main menu shows discovered/undiscovered silhouettes.

### 5.4 NPC fate branching
See Section 9 — each major NPC has 3–4 fate states resolved by flags; states determine spawn tables in Ch.3 and epilogue appearances.

---

## 6. Powers / Abilities System

### 6.1 Base combat kit (everyone, always)
| Action | Spec |
|--------|------|
| Light combo | 3-hit chain (10/10/18 dmg), 0.5 s combo window |
| Heavy attack | 0.6 s windup, 30 dmg, staggers non-elite |
| Dodge | 0.25 s dash @ 12 m/s, i-frames 0–0.18 s, 0.6 s cooldown |
| Interact / Vault | Context button; auto-vault obstacles ≤1.2 m |
| Lock-on | Auto soft-lock nearest; swipe right side to switch targets |

### 6.2 Ability lines
**Loadout: 3 active slots + 1 ultimate + 2 passive traits.** Ultimates charge from combat (meter), not cooldown.

Each line: 6 actives, 2 ultimates, 3 passives, arranged in a small tree (root → 2 branches → capstone). Ranks: each active upgradable ×2 at shrines.

**EMBER (offense / mobility)**
| Ability | Type | Effect (rank 1) | CD |
|---|---|---|---|
| Flamestep | Active | Dash-strike, 22 dmg, applies Burn (5 dps, 3 s) | 4 s |
| Cinder Burst | Active | 3 m AoE, 35 dmg, knockback | 8 s |
| Rising Phoenix | Active | Launcher (self+target airborne), 18 dmg, air-combo enabler | 7 s |
| Meteor Slam | Active (branch) | Leaping AoE 50 dmg, leaves fire patch | 12 s |
| Ignite | Passive-line active | Next 3 hits apply stacking Burn | 10 s |
| **Phoenix Reckoning** | Ultimate | 8 s: attacks explode, +30% speed | Meter |
| **Cinderheart** | Ultimate 2 | Revive-once-per-level w/ 50% HP + nova | Meter |

**TIDE (sustain / support / flow)**
| Ability | Type | Effect (rank 1) | CD |
|---|---|---|---|
| Mending Wave | Active | Heal self 25 HP + cleanse | 10 s |
| Riptide | Active | Pull enemies in 6 m cone, 12 dmg | 7 s |
| Mirror Current | Active | 1.5 s projectile reflect stance | 9 s |
| Call Ally | Active | NPC assist strike (Mara/Dax if bonded; else water spirit) 40 dmg | 15 s |
| Geyser Lance | Active (branch) | Line pierce 45 dmg, launch | 11 s |
| **Deep Tsunami** | Ultimate | Full-arena wave: 60 dmg, stagger all | Meter |
| **Undertow Aegis** | Ultimate 2 | Team-wide (self+allies) shield 80, 6 s | Meter |

**STONE (defense / control)**
| Ability | Type | Effect (rank 1) | CD |
|---|---|---|---|
| Bulwark | Active | 1.2 s parry stance; perfect parry = stagger + 30% dmg next hit | 6 s |
| Tremor Stomp | Active | 4 m AoE stun 1.5 s, 15 dmg | 9 s |
| Boulder Skin | Active | +50 armor 5 s, hyperarmor | 12 s |
| Fault Line | Active (branch) | Ground fissure line, 40 dmg + slow | 10 s |
| Sentinel Slam | Active | Shield-charge 35 dmg, knocks down | 8 s |
| **Mountain's Verdict** | Ultimate | Petrify all non-bosses 3 s + 70 dmg on shatter | Meter |
| **Earthen Bastion** | Ultimate 2 | Arena-wall + regen zone 8 s | Meter |

**HOLLOW (secret line — unlocked at Hollow ≥ 25 via shrine in C2/C3)**
| Ability | Type | Effect (rank 1) |
|---|---|---|
| Drain Touch | Active | Lifesteal strike 25 dmg / heal 50% |
| Phase Step | Active | Short teleport through enemies, 20 dmg exit |
| Dread Nova | Active | Fear (flee) non-elites 2 s |
| Shadow Bind | Active | Root 1 target 2.5 s |
| Eclipse Veil | Active | Invis 3 s, next attack +100% dmg |
| **Hollow Throne** | Ultimate | Execute all enemies <15% HP; boss: 120 dmg + 2 s vulnerability |

### 6.3 Cross-line synergies (cheap depth)
Owning specific pairs activates a named synergy (lookup table, no extra UI beyond a codex entry):
- Ignite + Tremor Stomp → **Magma Prison** (stunned enemies take double burn).
- Mirror Current + Bulwark → **Perfect Reply** (any parry/reflect refunds 10% ultimate meter).
- Call Ally + Rising Phoenix → **Tandem Launch** (ally follow-up juggle).
- Drain Touch + Mending Wave → **Leech Bloom** (heals also pulse to allies).

### 6.4 Balance & economy rules
- Ability power budget: an unlocked line ≈ +40% effective DPS/EHP vs. base kit; two-line hybrid ≈ +55% (reward breadth slightly less than depth).
- Echo economy per chapter: combat ~60%, decisions ~25%, exploration ~15%. Player should always afford their next intended node by chapter end (no grinding gates).
- Bosses are ability-agnostic but have stagger/parry windows tuned for base kit — abilities accelerate, never hard-require (except Hollow-ending content).

---

## 7. Combat & Enemies

### 7.1 Enemy archetypes (shared skeleton of behaviors, per-chapter skins)
| Archetype | Behavior (FSM) | Counter-play | Count target |
|-----------|---------------|--------------|--------------|
| Grunt | Approach → 2-hit combo | Combo punish | 3 base variants |
| Charger | Wind-up rush (telegraph 0.8 s) | Dodge/side-step | 2 |
| Caster | Keep distance, projectiles | Reflect/close gap | 2 |
| Bruiser | Shield front (immune frontal) | Heavy/parry/flank | 2 |
| Elite | Kit mix + 1 ability | Full kit check | 1 per chapter (variants) |
| Boss | Phased, arena mechanics | See 7.2 | 4 |

All telegraphs use color + shape language readable on a 6" screen; all enemies ≤ 3 simultaneous on-screen actors per arena wave (mobile perf + readability).

### 7.2 Bosses
1. **The First Echo** (C1) — Fracture construct; teaches dodge-timing + ability use; single phase + enrage.
2. **Dax** (C2) — *Duel* (bond low: rival fight) OR *Team-up* (bond high: fight alongside him vs. Choir elite) — same arena, flag-driven variant.
3. **Choir Cantor** (C3 mid) — world-state flavored (arena = ruined or rebuilt market).
4. **The Choirmaster + final decision phase** (C3) — 3 phases; phase 2 inserts ally/enemy NPCs per fate flags; phase 3 mechanics depend on dominant line (Ember: DPS race; Tide: protect objective; Stone: survival attrition; Hollow: execution window). Refusal choice (ending 6) is offered at phase transition.

### 7.3 Difficulty & mobile fairness
- One difficulty at launch, tuned "medium-fair": death → restart at level checkpoint with arena wave reset; no resource loss (Echoes kept). Optional "Story" toggle (–30% enemy dmg) in settings.
- No frame-perfect requirements; parry window 0.25 s; i-frames generous (0.18 s); all boss telegraphs ≥ 0.6 s.

---

## 8. Character Movement & Interaction (Mobile Controls)

### 8.1 Control scheme (landscape, two-thumb)
```
┌──────────────────────────────────────────────────────────┐
│  HP bar / affinity glyphs          [PAUSE]  objective    │
│                                                          │
│                        (camera drag anywhere on right)   │
│   ┌─────┐                                    ┌───┐       │
│   │  ◉  │  virtual stick                     │ATK│ light │
│   └─────┘  (left 35% of screen)        ┌───┐  └───┘       │
│                                        │DDG│  ┌───┐ ┌───┐ │
│   [INTERACT] appears near targets      └───┘  │A1 │ │A2 │ │
│                                          ┌───┐└───┘ └───┘ │
│                                          │A3 │  ┌─────┐   │
│                                          └───┘  │ULT  │   │
│                                                 └─────┘   │
└──────────────────────────────────────────────────────────┘
```
- **Left thumb:** floating virtual stick (appears at touch-down within left zone); full tilt = run (6 m/s), no sprint button.
- **Right thumb:** camera drag + button cluster: ATK (tap = light, hold 0.4 s = heavy), DDG (dodge/dash), A1–A3 (equipped actives), ULT (glows when charged), INTERACT (contextual, left-bottom).
- **Lock-on:** automatic soft-lock in combat; flick right stick zone horizontally to switch target; locked camera frames target (FOV 55→50).
- Also supported: gamepad (Xbox/PS via Input System) + keyboard/mouse in editor. All through one `InputActionAsset`.
- Button sizes ≥ 88 dp; full input remap + left/right handed flip in settings; haptics on hit/dodge/parry (light/medium/heavy).

### 8.2 Movement spec
| Parameter | Value |
|-----------|-------|
| Walk / Run | 3.5 / 6.0 m/s |
| Dodge dash | 12 m/s × 0.25 s, CD 0.6 s |
| Acceleration / decel | 40 / 60 m/s² (snappy, arcade) |
| Auto-vault | obstacles ≤ 1.2 m, no button |
| Jump | **None** (design decision: keeps combat readable on touch; verticality via launchers/vaults/prefab ramps) |
| Gravity / fall | Standard; fall > 6 m = stagger on land (no fall damage) |

CharacterController-based (not Rigidbody) for deterministic mobile perf; rotation snaps to move/aim direction with 0.1 s smoothing.

### 8.3 Interaction system
- Proximity volumes (`IInteractable` interface): doors, shrines, codex memories (glowing echo motes), NPCs, levers.
- Nearest valid target gets the INTERACT prompt; priorities: quest-critical > NPC > shrine > collectible.
- NPCs: interact → dialogue graph runs (camera moves to conversational framing).
- Combat hit registration: overlap-sphere hitboxes spawned from animation events (no per-frame physics queries); hurtboxes = simple capsule colliders on layer matrix `Player/Enemy/PlayerAbility/EnemyAbility`.

---

## 9. NPCs — Cast, Differences & Choices

### 9.1 Major cast (deeply state-driven)

| NPC | Role | Bond range | Fate states (flag-driven) | Player choices that matter |
|-----|------|-----------|---------------------------|---------------------------|
| **Mara** | Childhood friend → possible ally fighter | −100…100 | `Ally` (fights with you C3; powers Call Ally), `Civilian` (epilogue only), `Lost` (dies C2 if timer choice failed), `Turned` (Hollow path: hostile elite in C3) | Prologue friendship choices; C2 pressure choice "save Mara vs. pursue Choir"; Hollow betrayal option |
| **The Mentor** (3 variants — only one exists per run) | Ch.1 mentor choice: **Kael** (Ember, disgraced soldier) / **Sister Odalys** (Tide, Fracture healer) / **Warden Bran** (Stone, Spire protector) | 0…100 | `Alive` (assists C3 phase 2), `Fallen` (dies C3 — memorial scene), `Estranged` (absent, if bond < 20 by C3) | Mentor pick (+20 starting affinity to their line); mentor-specific dialogue choices; obeying/defying their advice flags |
| **Dax** | Rival Echo-awakened | −100…100 | `Rival` (C2 duel boss), `Truce` (C2 team-up), `Redeemed` (C3 ally), `Final Enemy` (C3 phase 2 mini-boss) | Ch.1 "spare/press him after sparring"; C2 duel conduct (finish him vs. yield); Hollow option "absorb his Echo" |
| **The Archivist** | Shrine keeper, codex narrator, meta-unlock vendor | – | Always present | Echo spending; codex questions (lore + hints) |

**Bond tiers:** ≤−50 Hostile · −49…0 Wary · 1…49 Warm · 50…79 Bonded · ≥80 Kin. Tiers gate dialogue options (conditions) and assists.

### 9.2 NPC implementation rule (scope control)
One character = one prefab + a `FateStateDriver` that reads flags at scene load and selects: spawn (yes/no), behavior profile (ally AI / civilian idle / hostile AI), dialogue graph variant, and model trim color. **No NPC needs more than 2 behavior profiles.** Ambient crowd in rebuilt/intact districts = 3 shared low-detail prefabs on simple wander splines (max 6 on-screen).

### 9.3 Ally combat (Tide line + C3)
Allies use a stripped enemy AI FSM with `Follow → Engage → Retreat-at-30%HP → Revive-if-player-near` states. Allies cannot die permanently in combat (downed 10 s, recover) — permanent death only happens via decisions (protects the fate-state system from combat RNG).

---

## 10. Game Loop

### 10.1 Micro loop (10–30 s)
```
Enter arena → wave spawns (telegraphed) → fight (combo/dodge/abilities)
→ hit feedback (haptics/VFX/slow-mo on kill) → Echoes drop → next wave or exit
```

### 10.2 Meso loop (10–15 min = one level)
```
Level intro card (age/place) → 2–3 encounter arenas + traversal
→ secrets (codex mote / Hollow shrine) → pressure decision (D2)
→ level boss or exit → interlude: dialogue (D1) + shrine (spend/respec)
→ milestone decision (D3, autosave before) → world state updates
```

### 10.3 Macro loop (one 1.5–2 h life)
```
Prologue (learn) → Ch.1 (awaken + line seed) → Ch.2 (commit: path variant,
build deepens, rival climax) → Ch.3 (converge: city reflects you, final
choices, state-dependent final boss) → Epilogue (1 of 7 endings)
```

### 10.4 Meta loop (across lives)
- **Persistent meta file** survives save deletion: endings discovered, codex %, cosmetic unlocks (Ari outfits, ability VFX tints), one equippable **Memory boon** per completed run (e.g., "start with Flamestep" — small, non-trivial head starts).
- New Game+ = replay with boons; no level scaling (scope control) — replay motivation is endings/builds/codex, not difficulty tiers.

---

## 11. Level / World Structure

### 11.1 The city of Vessa (one asset base, many states)
Districts: **Old Market** (dense stalls, close arenas), **Docks/Reaches** (open cranes, verticality via vaults), **Spire Uptown** (clean geometry → anomaly-warped in C3), **Outskirts/Fracture Zone** (the tear, crystallized Echo terrain).

### 11.2 Scene list (playable)
| Scene ID | Name | Type | Encounters | Notes |
|----------|------|------|-----------|-------|
| P1 | Last Summer | Exploration | 0 | Movement/interaction tutorial; Mara friendship D1s |
| C1L1 | Night of the Fracture | Action | 3 arenas | Combat tutorial → awakening set piece |
| C1L2 | Under the Spire | Action | 3 arenas + boss | Mentor-variant flavor trims; Boss: First Echo |
| I2 | Interlude: Becoming | Narrative | – | Mentor D3 recap, shrine, Ch.2 path D3 |
| C2A | Contested Docks | Action (Ember path) | 4 arenas + D2 | Assault framing |
| C2B | The Sanctuary | Action (Tide path) | 4 arenas + D2 | Defense framing (protect objective waves) |
| C2C | The Long Wall | Action (Stone path) | 4 arenas + D2 | Hold-the-line framing |
| C2X | Dax Confrontation | Boss arena | 1 | Duel OR team-up variant |
| I3 | Interlude: Reckoning | Narrative | – | World-state reveal, shrine, Hollow shrine if unlocked |
| C3L1 | Market (Ruined/Rebuilt variant) | Action | 3 arenas | Variant by C2 outcome |
| C3L2 | Ascent of the Spire | Action | 3 arenas | Gravity-anomaly set pieces |
| C3B | The Choirmaster | Final boss | 3 phases | State-dependent; refusal choice offered |
| EP | Epilogue | Cinematic | – | 7 ending variants in one scene |

**Total playable scenes: 13** (+ boot/menu/test = 17 scenes). Interludes reuse one scene shell with swapped data/backdrops.

### 11.3 Arena design rules (mobile)
- Arena footprint ≤ 40×40 m; camera never clips (bounded volumes); flat-ish floors with ≤1.2 m vaultables.
- One wave active at a time; wave telegraph = ground rune glow + audio sting 1.5 s before spawn.
- Every arena has exactly one decision hook or secret (rewards attention); traversal between arenas ≤ 45 s.
- Checkpoint = arena entry (Section 12.3).

---

## 12. Save / Progression System

### 12.1 Architecture
- **Format:** JSON (readable, diffable, easy migration) via Unity `JsonUtility` + wrapper, written to `Application.persistentDataPath`.
- **Files:**
  - `save_slot_{0,1,2}.json` — 3 manual slots + rolling `autosave.json` (slot-independent).
  - `meta.json` — endings/codex/cosmetics/boons (Section 10.4), never deleted by "new game".
  - `settings.json` — graphics/haptics/controls/sensitivity.
- **Versioning:** every file has `schemaVersion`; `SaveMigrator` upgrades old saves on load (table of version→migration functions).
- **Atomic writes:** write to `.tmp` → `File.Replace`; checksum (CRC32) field to detect corruption → fallback to autosave with user prompt.

### 12.2 Save data (payload)
```json
{
  "schemaVersion": 1,
  "meta": { "slotName": "Ari — Ch.2", "timestamp": "...", "playtimeSec": 5123 },
  "scene": { "sceneKey": "C2B_Sanctuary", "checkpointId": "arena_03_entry" },
  "gameState": { /* full GameState from 4.3 */ },
  "abilityLoadout": { "actives": ["Flamestep","Bulwark","Riptide"], "ultimate": "PhoenixReckoning", "traits": ["CinderSkin","SureFooting"] }
}
```

### 12.3 Checkpoint & autosave rules
- Autosave triggers: level entry, after every decision node resolution, after every boss phase, on shrine exit.
- Mobile lifecycle: save on `OnApplicationPause(true)` / `OnApplicationFocus(false)` (games get killed in background — treat every pause as a potential quit).
- Death: respawn at checkpoint with wave state reset; `GameState` unchanged except a `deaths` counter (codex flavor stat).
- D3 milestone decisions: forced autosave *before* commit + "point of no return" confirm dialog.

### 12.4 Cloud save
Out of MVP scope; interface-isolated (`ISaveBackend`) so Play Games / Game Center backend can be added post-launch without touching game code.

---

## 13. Required Unity Scenes & Systems

### 13.1 Project & tech stack
| Item | Choice | Why |
|------|--------|-----|
| Unity | **6 LTS (6000.0.x or newer 6000.x stable)** | Current LTS, mature mobile support |
| Render pipeline | **URP** (mobile-optimized settings) | Best perf/tooling for target devices |
| Input | **Input System** package + custom touch layer | Gamepad/editor parity |
| Camera | **Cinemachine** | Third-person follow + combat framing + shake |
| UI | **UGUI + TextMeshPro** | Dialogue/HUD/menus (no UI Toolkit for runtime perf predictability on low-end) |
| Animation | Mecanim + **animation events → hitboxes** | Standard, artist-friendly |
| Tweens | DOTween (or hand-rolled tween service) | UI/game feel |
| Scene loading | `SceneManager` async + loading screen scene | Addressables deferred (not needed at 17 scenes; keep option open) |
| Art | Stylized low-poly, single dir. light + baked/probe lighting, GPU instancing | Mobile budget |
| Audio | Unity Audio + pooled `AudioSource` players, one mixer (Music/SFX/UI/Voice buses) | Standard |
| VFX | Shuriken (pooled), no VFX Graph on low tier | Mobile compatibility |

**Performance budget (mid-range target device, e.g. Snapdragon 6-series / iPhone 11):**
| Metric | Target |
|--------|--------|
| FPS | 30 stable floor / 60 on high tier (quality tiers: Low/Med/High auto-detect + manual) |
| Draw calls | < 150 per arena view |
| Tris/frame | < 350 k |
| On-screen actors | ≤ 3 enemies + ≤ 2 allies + player |
| Particles | ≤ 200 active; no realtime shadows except player+boss on High tier |
| Build size | < 1.2 GB install (asset bundles/texture compression ASTC) |
| Memory | < 1.6 GB peak |

### 13.2 Scene list (non-playable)
| Scene | Purpose |
|-------|---------|
| `00_Boot` | Service init, save load, quality detect → routes to Menu or Resume |
| `01_MainMenu` | Menu, save slots, ending gallery, settings |
| `02_Loading` | Async loading screen (persistent/additive) |
| `99_Sandbox` | Dev-only combat/systems test bed (never shipped) |

### 13.3 Folder structure
```
Assets/
  _Project/
    Art/            Characters/ Environment/ Props/ VFX/ UI/ (Source + Imported)
    Audio/          Music/ SFX/ Voice/
    Data/           Abilities/ Decisions/ Dialogue/ Enemies/ WorldState/ Codex/ Balancing/
    Prefabs/        Player/ Enemies/ NPCs/ Props/ VFX/ UI/
    Scenes/         Boot/ Main/ Chapters/ Interludes/ Dev/
    Scripts/
      Core/         (AppServices, EventBus, SaveSystem, SceneFlow, Pooling, Utils)
      Gameplay/     (Player/, Combat/, Abilities/, Enemies/, Interaction/, Camera/)
      Narrative/    (GameState, Decisions, Dialogue, WorldState, Codex, Resonance)
      UI/           (HUD, Menus, Dialogue, Shrine, Touch controls)
      Input/
    Settings/       (URP assets, InputActionAsset, Quality tiers)
  Plugins/          (DOTween etc.)
  Resources/        (minimal — bootstrap only)
```

### 13.4 System architecture (runtime services)
Lightweight **service locator** (`AppServices`) + **typed EventBus**; no DI framework (scope). All services headless-testable (pure C# logic separated from MonoBehaviours).

| System | Responsibility | Key classes |
|--------|---------------|-------------|
| **AppServices** | Bootstrap, service registry, lifecycle | `AppServices`, `IGameService` |
| **EventBus** | Decoupled comms (`EnemyDied`, `DecisionResolved`, `AbilityUsed`, `CheckpointReached`…) | `EventBus`, event structs |
| **SceneFlow** | Async scene routing, loading screen, level intro cards | `SceneFlowService`, `SceneKey` table |
| **GameState / StateMutator** | Authoritative run state; all writes logged | `GameState`, `StateMutator` |
| **SaveSystem** | Slots, autosave, migration, checksums, `ISaveBackend` | `SaveSystem`, `SaveData`, `SaveMigrator` |
| **DecisionSystem** | Loads `DecisionNode`/`DialogueGraph` SOs, evaluates conditions, applies effects, routes | `DecisionRunner`, `DialogueRunner`, `Condition`, `Effect` |
| **AbilitySystem** | Ability SO definitions, cooldowns, loadout, activation → spawns hitbox/VFX | `AbilityDefinition`, `AbilityController`, `LoadoutService` |
| **CombatSystem** | Hitbox/hurtbox resolution, damage/stagger/status (Burn/Stun/Root/Fear), i-frames, ultimate meter | `DamageResolver`, `HealthComponent`, `StatusEffect` |
| **PlayerController** | Movement, dodge, combo state machine, lock-on, interact | `PlayerMotor`, `PlayerCombatFSM`, `LockOnTargeter` |
| **EnemyAI** | FSM per archetype, wave director, telegraphs | `EnemyFSM`, `WaveDirector`, `Telegraph` |
| **WorldStateSystem** | District variant swapping from flags | `WorldStateVariant`, `DistrictDresser` |
| **CameraRig** | Cinemachine wrappers, combat framing, shake service | `CameraDirector`, `ShakeService` |
| **InputService** | Touch controls (stick/buttons), gamepad/keyboard maps, remap, haptics | `TouchControlsUI`, `InputService` |
| **UIRoot** | HUD, dialogue UI, shrine UI, pause/menus, prompts | `HUDController`, `DialogueUI`, `ShrineUI` |
| **AudioService** | Pooled playback, ducking, per-bus mixers | `AudioService` |
| **PoolService** | Object pooling for VFX/hitboxes/enemies/projectiles | `Pool<T>` |
| **Codex/Journal** | Discovered choices, endings gallery, lore motes | `CodexService` |
| **ResonanceTracker** | Playstyle telemetry → affinity drift (4.4) | `ResonanceTracker` |
| **QualityManager** | Device detect, quality tiers, resolution scaling | `QualityManager` |
| **AnalyticsHooks** *(optional)* | Funnel events (decision picks, deaths, session length) | `AnalyticsService` |

### 13.5 Key data assets (ScriptableObjects)
`AbilityDefinition`, `TraitDefinition`, `DecisionNode`, `DialogueGraph`, `EnemyDefinition`, `BossPhaseData`, `WorldStateVariantSet`, `CodexEntry`, `EndingDefinition`, `LevelDefinition` (encounter/wave tables), `BalanceTable` (central numbers).

---

## 14. Missing Systems (Everything Needed Before Building)

Since the audit found an **empty workspace**, every system is missing. Prioritized into gates — each tier must exist before the next tier of development starts (mirrors `DEVELOPMENT_PLAN.md` phases):

| Tier | Missing items | Needed before |
|------|--------------|---------------|
| **P0 — Project foundation** | Unity 6 LTS project + URP mobile config; Input System + touch control layer; folder structure; git repo + `.gitignore` + LFS rules; `AppServices` locator; `EventBus`; `PoolService`; `SceneFlowService`; `QualityManager` skeleton; device build pipeline (one Android + one iOS test build) | Any gameplay code |
| **P1 — Core gameplay framework** | `PlayerController` (motor + combat FSM); `CombatSystem` (hitbox/hurtbox, damage, status); `HealthComponent`; `CameraRig` (Cinemachine); `InputService` full mapping + haptics; `EnemyAI` FSM + `WaveDirector`; 1 grunt + 1 bruiser; `AudioService` skeleton; HUD skeleton (HP/Echoes/abilities) | Combat prototype ("feels good on a phone") |
| **P2 — Narrative & progression framework** | `GameState`/`StateMutator`; `SaveSystem` (+mobile lifecycle saves); `DecisionSystem` + `DialogueRunner` + Dialogue UI; affinity/bond model; `AbilitySystem` + `LoadoutService` + Shrine UI; `ResonanceTracker`; checkpoint system; interlude scene shell | Vertical slice (Ch.1 playable start→finish) |
| **P3 — Content systems** | `WorldStateSystem` (district variants); `FateStateDriver` (NPC variants); boss framework (`BossPhaseData`); ally AI; codex/journal; ending evaluator + epilogue variants; VFX system pass; localization-ready text pipeline (string keys from day one) | Alpha (all chapters playable) |
| **P4 — Meta & release** | Meta-progression file + boons + ending gallery; settings/options full; performance tiers validated on device matrix; crash reporting; store SDKs (Play Games/Game Center optional), icons/splash, compliance (data safety form) | Beta → Release |

**Explicitly deferred (not missing, intentionally out of scope):** cloud saves, New Game+ scaling, multiplayer, IAP, additional difficulties beyond Story toggle, voice-over (text + stings only at MVP).

---

## 15. Art & Audio Direction (summary)

- **Art:** stylized low-poly ("readable at arm's length"), flat shading + rim light, bold per-line color language; UI = bold sans, high contrast, card-based dialogue; VFX = shape-driven (cones/rings/runes) over particle soup.
- **Character budget:** player/NPCs ≤ 8 k tris, enemies ≤ 5 k, crowd ≤ 1.5 k; single texture atlas per district.
- **Audio:** adaptive music = 3 layers (explore/combat/boss) cross-faded by `AudioService`; one leitmotif per affinity line woven into the main theme (the soundtrack itself reflects your build); diegetic stings for decisions (low chime = flag set) to teach consequence without UI spam.

---

## 16. Risks & Mitigations (design-level)

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Touch combat feels bad | Fatal for "heavy action" pillar | P1 exit gate = on-device feel test before ANY content work (see dev plan) |
| Branching content cost explodes | Schedule | Foldback structure (2.3); NPC fate variants via flags not new characters; district variants via set-dressing |
| Scope creep past 2 h | Mobile retention | Fixed chapter map (Section 2.2) as contract; cut list pre-agreed: crowd systems first, synergies second |
| Thermal throttling on long sessions | Frame drops | 10–15 min levels; 30 fps floor tier; aggressive pooling; playtest protocol includes 30-min sustained session |
| Decision system too opaque | Players don't perceive consequences | Post-choice affinity glyphs; interlude "your life so far" recap screen; codex |

---

## 17. Glossary
**Echo** — the supernatural energy / currency. **Fracture** — the inciting event. **Line** — an affinity family (Ember/Tide/Stone/Hollow). **Affinity** — 0–100 meter per line. **Bond** — −100…100 relationship meter per NPC. **D1–D4** — decision types (Section 4.1). **Foldback** — branching that reconverges. **Resonance** — threshold passives + playstyle telemetry system. **Shrine** — upgrade/respec station. **Fate state** — an NPC's flag-driven existence/behavior variant. **World-state variant** — a district's flag-driven dressing/layout variant.

---
*End of GAME_DESIGN.md — companion document: `DEVELOPMENT_PLAN.md` (exact build order).*
