# CROSSROADS — Complete Playable Campaign Content Pass

**Verdict before this pass:** the "complete playable campaign" task had **not** been done. The repo
held a vertical slice — 3 locations of the 13 in GAME_DESIGN §11.2, chapter one plus a one-beat
teaser, two NPCs (Mara, Sera), one enemy, three single abilities, no endings.

**After this pass:** a beginning-to-end campaign (Prologue → Fracture → Becoming → Reckoning →
Epilogue, 7 endings) as **data** through the existing canonical pipeline
(`scripts/story_content.json` → `gen_story_content.py` → `CL_C1_StoryContent.asset`, mirrored by the
generated `StoryContentBuilder.Campaign.cs`), wired into the **existing** scene generator, validated by
the **existing** validator (extended), and proven by headless full playthroughs through the real
runtime services. No stable architecture was rewritten.

Commits (all on `main`, pushed):

| Commit | Content |
|---|---|
| `de3e60b` | (1/4) canonical JSON + generated builder mirror + test-count assertions made data-relative |
| `6a21495` | (2/4) headless new-game→THE END playthrough tests + fate/branch ordering fixes |
| `1621728` | (3/4) scene wiring for all 13 locations, palette materials, validator §4b/§5 contracts |
| *(final — see bottom)* | (4/4) combat animations + Animator triggers, Hollow VFX line, CI fix, this report |

---

## 1. Complete campaign flow (new game → ending)

Chapter one (existing "First Light" hall content) is untouched and still the game's opening; the
memory pier and the campaign stair hang off the same hub. Every location below is a `LocationDefinitionData`
with unlock rules, connections, a checkpoint anchor and first-arrival world-state changes.

| # | Location (`id`) | Kind | What happens | Gate |
|---|---|---|---|---|
| P1 | The Last Summer (`last_summer`) | Story | **Tutorial**: controls sign, kite pickup (move + INTERACT), pier bell. Young Mara: kite / pier / sunset decisions (D1) shape Mara's bond and starting affinities. No combat. | open from the hall |
| C1L1 | Night of the Fracture (`fracture_night`) | Combat | Cutscene (the light comes down) → **Mentor D3** (Kael/Odalys/Bran, +20 line affinity, line ability). Mentor lesson = **combat tutorial** + obey/defy. 3 arenas (grunts, chargers). Dax spar → **spare/press**. Side: family at the pharmacy door. | prologue done + `ch1_complete` |
| C1L2 | Under the Spire (`under_spire`) | Combat | Casters + bruiser cordon → boss **The First Echo** (dormant until 3 kills). Mentor-flavoured fallen cutscene. | street arenas cleared |
| I2 | Interlude: Becoming (`interlude_becoming`) | NPC | Archivist recap (mentor-variant) → **Ch.2 path D3** (Docks/Sanctuary/Long Wall). Mentor interlude (obeyed/defied variants). Mara at 20. | Fracture complete |
| C2A | Contested Docks (`docks`) | Combat | Assault: 4 arenas + Elite in the tower; **fuel shed** burn/breach decision. Capstone *Phoenix Reckoning*. | path = docks |
| C2B | The Sanctuary (`sanctuary`) | Combat | Defence crisis: 4 waves, sluices to close; **3 breaches = failure → recovery route** (carry them out). Capstone *Call Ally*. | path = sanctuary |
| C2C | The Long Wall (`long_wall`) | Combat | Hold-the-line crisis: braces, **3 breaches = failure → second wall recovery**. Capstone *Bulwark*. | path = long_wall |
| — | (all three) | | **Timed D2 (6 s): save Mara vs pursue the Choir ledger**; timeout = *hesitate* (Mara hurt). | Elite down |
| C2X | Dax Confrontation (`dax_arena`) | Combat | **Duel** (bond ≤ 0: Dax boss → finish / yield / *absorb* at Hollow ≥ 25) **or team-up** (bond > 0: Choir Hunter). | path done + crane answered |
| I3 | Interlude: Reckoning (`interlude_reckoning`) | NPC | Archivist reads the **world state** back (market/docks/Mara/Dax variants); reckoning shrine (upgrades); **dark plinth** (Hollow ≥ 25 → *Drain Touch*, Spire collapses). Mara at 26. Dominant line + mentor fate evaluated. | Becoming complete |
| C3L1 | The Old Market (`market`) | Combat | Variant by state (intact/contested/ruined/rebuilt dressing); husks → mid-boss **Choir Cantor**. | reckoning visited |
| C3L2 | Ascent of the Spire (`spire_ascent`) | Combat | Gravity anomalies to anchor (world actions), Spire Wardens, spire sealed/breached/collapsed dressing. | Cantor silenced |
| C3B | The Choirmaster (`choirmaster`) | Combat | **3 phases**. Phase 1 → *door in the song*: **press / mentor shields you (mentor falls) / refuse** (ending 6). Phase 2 inserts by fate flags (Dax Final Enemy, Mara Turned, or Mara/Dax/mentor as allies). Phase 3 narration by dominant line. → **Final decision (7 endings)**. | ascent done |
| EP | Epilogue: Vessa, After (`epilogue`) | Story | One scene, seven variants: Archivist reads the ending + Mara's fate + the mentor's fate. Memorial stone. `THE END`. | campaign ended |

**Chapters** (`CampaignChapterData`, 7 total incl. the two existing): `ch_prologue` (3 beats),
`ch_fracture` (4 beats, 5 branches), `ch_becoming` (8 beats, 11 branches: path, Mara fate, Dax fate),
`ch_reckoning` (13 beats incl. 7 phase-two inserts, 12 branches: dominant line, mentor fate, pressed/refused),
`ch_epilogue` (1 beat, 7 ending branches). Chapters chain purely through flags (`ch1_complete` →
prologue-visited → `c2_open` → `c2_complete` → `ep_open`).

## 2. Characters (canonical references, consistent every appearance)

| NPC id(s) | Sheet | Scene materials (CHARACTER_REFERENCE palette) |
|---|---|---|
| `mara_young`, `mara_c2`, `mara_c3`, `Mara_Turned`, `MaraAlly_CM` | **REF-02** in every chapter | hair `#1D1A1C` (`M_Char_Mara`), white dress `#F4F2EF` (P1), grey hoodie `#8F939C` (C2/C3), kite-string accent |
| `dax`, `dax_rival`, `dax_final`, `DaxAlly_CM` | **REF-03** | chestnut `#4A3527`, white tee, navy blazer `#2A3140`, glasses |
| `archivist` | **REF-05** | cyan-white bodysuit `#CFE6F2`, silver-white hair `#E8F1F6`, holo `#7FD8E8` halo; never fights |
| `kael` / `odalys` / `bran` (one per run) | derived per §4 grammar | Ember-trim coat / Tide-teal robe + silver bun / Stone-grey ward coat |
| Choir grunt/charger/sentinel/lancer · bruiser/spire warden · caster/elite/hunter/cantor | **REF-06 A / B / C** | tan `#8A6F52` · olive `#4C5548` · white `#C7CCD2`, light `#66D9E8`, shared hit-flash |

Fate states drive titles/behaviour through the existing `NpcBrain` state tables: Mara *Civilian / Ally /
Lost / Turned*; Dax *Wary / Rival / Truce / Redeemed / Final Enemy / dead*; mentor *Alive / Fallen / Estranged*
(bond < 20 by C3); Archivist *Uneasy* at Hollow ≥ 25, *Witness* after the ending.

## 3. Branches and decisions (22 total; 16 new)

D3 milestones: `dec_mentor`, `dec_c2_path`, `dec_ending`. D2 timed: `dec_save_mara` (6 s, timeout →
hesitate). D1: prologue trio, `dec_mentor_advice`, `dec_dax_spar`, `dec_docks_fire`, `dec_dax_confront`,
`dec_dax_duel_end` (finish/yield/absorb), `dec_i3_shrine`, `dec_hollow_shrine`, `dec_cm_transition`
(press / mentor shield / **refuse**). D4 silent: `dec_epilogue_close`.

**Ending matrix** (`dec_ending`, conditions exactly as GAME_DESIGN §5.3): 1 Ashen Crown (Ember ≥ 60, Dax
dead) · 2 Tide's Embrace (Tide ≥ 60, Mara bond ≥ 50, alive) · 3 The Unmoved (Stone ≥ 60, ≥ 2 districts saved) ·
4 Hollow Throne (Hollow ≥ 25) · 5 Balance (no line ≥ 60, Mara AND Dax alive) · 6 The Long Way Home (always;
also reached by refusing at the phase transition) · 7 Martyr's Dawn (any dominant line; mentor-alive text variant).

## 4. Abilities / power paths (11 total, 4 lines)

| Line | Root (decision) | Capstone (path objective) |
|---|---|---|
| Ember | `ember_pulse` (ch1) · **Cinder Burst** (mentor Kael) | **Phoenix Reckoning** (Docks taken) |
| Tide | `tide_mend` (ch1) · **Riptide** (mentor Odalys) | **Call Ally** (Sanctuary held or recovered) |
| Stone | `stone_ward` (ch1) · **Tremor Stomp** (mentor Bran) | **Bulwark** (Long Wall held or recovered) |
| Hollow (secret) | **Drain Touch** (dark plinth, Hollow ≥ 25) | **Hollow Throne** (absorb Dax's Echo) |

Each has 3 upgrade levels, an `abilityCombat` payload (damage type, statuses: `burn_deep`, `riptide_pull`,
`tremor_daze`, `bulwark_guard`, `hollow_drain`, `choir_song`) and the pulse VFX now colours by line (Hollow violet).

## 5. Missions / objectives (24 total; 17 new)

Tutorial `obj_tut_move` → `obj_tut_talk` (3 steps). Main: `obj_fn_arenas`, `obj_us_descent`, `obj_i2_consult`,
`obj_docks_assault`, `obj_dax`, `obj_i3_consult`, `obj_market`, `obj_ascent`, `obj_choirmaster`, `obj_epilogue`.
Crisis (failable): `obj_sanctuary_hold`, `obj_wall_hold` → Recovery `obj_sanctuary_recover`, `obj_wall_recover`.
Side: `obj_fn_civilians`. Counters/steps read the world-action vars (`fn_kills`, `c2_kills`, `anomaly_count`,
`sanctuary_breaches`, `wall_breaches` …) written by enemy defeats and `WorldActionInteractable`s.

## 6. Combat encounters (19 enemy definitions, 49 scene agents)

Archetypes per GAME_DESIGN §7.1 with per-chapter skins — grunt (`choir_grunt`, `choir_sentinel`), charger
(`choir_charger`, `choir_lancer`), caster (`choir_caster`), bruiser (`choir_bruiser`, `spire_warden`, `hollow_husk`),
elite (`choir_elite`, `choir_hunter`) — and bosses **First Echo**, **Dax** (duel), **Choir Cantor**,
**Choirmaster ×3 phases**, plus fate inserts **Dax Final Enemy** / **Mara Turned**. All dormant until their
data `activationConditions` pass (visited flags, kill counters, fate flags), all `onDefeatEffects` feed the
objective/campaign cascade through `CombatResolution.DefeatEnemy`.

## 7. World-state changes, objects, transitions, cinematics, animations

* **World state** (`StoryWorldState.areaVariants`, 18 dressing sets): Old Market intact → contested → ruined /
  rebuilt; Docks contested → working / flooded / fortified; Spire sealed → breached → collapsed (Hollow ≥ 25);
  Vessa 7 epilogue skies. 84 `SpawnEntity` keys bound (mentor presence, crane moment, boss phases, fate inserts…).
* **Environmental interactions / important objects** (19 `WorldActionInteractable`s): kite, pier bell, street
  barricades, pharmacy door, Spire lift, fuel shed, sluices + breach counter, gate braces + breach counter,
  gravity anomalies, memorial stone, shrines (via encounters).
* **Scene transitions**: every location has `LocationAnchor_<id>` + `AreaTrigger_<id>`; the map (`MapHUD`) →
  `LocationServices.Travel` → `LocationTransitionFader` teleport path is unchanged; the connection graph is
  fully reachable from the hub (asserted by test 80). `NpcRelocator` moves the mentors to the interlude camp
  and the Archivist to the reckoning shrine / pier (`MoveNpc` effects).
* **Cinematics**: 26 `StoryEventInteractable` cutscene/dialogue triggers (opening light-fall, First Echo
  intro/fallen, Docks/Sanctuary/Wall openings, crane, Dax down/Hunter fallen, market/ascent openings,
  Choirmaster open / door in the song / chorus / finale, final decision, epilogue count).
* **Animations** (`scripts/gen_ari_combat_anims.py`): four new clips on the canonical Ari rig —
  `Ari_Attack` (0.45 s), `Ari_Dodge` (0.35 s), `Ari_Hit` (0.28 s), `Ari_Defeat` (0.9 s) — plus Trigger
  parameters and Any-State transitions in `Ari_Controller.controller`; `PlayerCombatController` fires them
  (attack/dodge/health-drop/defeat), null-safe.

## 8. Placeholder content (genuinely not producible here)

* NPC/enemy **bodies are primitive rigs in the canonical palettes** (as the existing Mara/Sera/Warden were);
  no Blender is available in this sandbox, so no new character meshes — the `NpcAgent.avatarPrefab` slot and
  `CHARACTER_REFERENCE.md` sheets are the hand-off. Mentor sheets remain "derive per §4".
* **Cutscenes are text/dialogue set pieces**, not camera animations (no Timeline authoring without Unity).
* Combat animations are procedural key poses on the generic rig, not mocap; enemies keep flash/sink feedback.
* Ally combat AI (§9.3) is represented by *Call Ally* strikes and phase-two ally presence, not a follower FSM.
* Ability count per line is 2–3 (root + capstone [+ ch1 pulse]), not the full 6+2+3 trees of §6.2.

## 9. Verification

| Check | Result |
|---|---|
| `bash scripts/run_tests.sh` (Mono 6.12, all suites incl. new `CampaignContentTests`) | **1862 passed, 0 failed** (baseline 962) |
| `python3 scripts/validate_assets.py` (GUIDs, asset↔JSON, JSON↔builder, scene fileID classes, needles, **§4b campaign contracts**, §5 kits, **§6 combat anims**) | **PASSED, 0 warnings** — 540 scene roots |
| `bash scripts/compile_check.sh` (both input defines) | exit 0, 16 pre-existing warnings |
| Headless playthroughs (tests 81–85) | 7 endings reached: Ashen Crown, Tide's Embrace, The Unmoved, Long Way Home (refusal), Hollow Throne, Balance, Martyr's Dawn; crisis-failure recovery; D2 timeout; mid-campaign save/restart |

The validator's new contract pass also caught one pre-existing defect (the `sera_lamp` SpawnEntity key had no
scene binding) — fixed in the generator.

## 10. Android build

The sandbox has no Unity editor / Android SDK, so no APK could be built or played on-device here. The CI path
(`.github/workflows/android-apk.yml`, game-ci) was diagnosed from the run logs: all three previous runs failed
**before Unity started** — game-ci's semantic-versioning step refused a "dirty" checkout because the LFS
rules in `.gitattributes` re-normalised the committed binaries. This pass fixes the workflow (`lfs: false`,
`versioning: None`, `allowDirtyBuild: true`, Unity-6 email/password secrets supported) — but **the repository
still has no `UNITY_LICENSE` (or `UNITY_EMAIL`/`UNITY_PASSWORD`) secret**, so the next run will still stop at
activation until one is added (Settings → Secrets → Actions). After that, Actions → *android-dev-apk* → *Run
workflow* produces the `CrossroadsDev-APK` artifact; the local path remains `scripts/build_android_apk.sh` /
`Assets/Editor/AndroidDevBuild.cs`. Device checklist: `ANDROID_BUILD.md`.

**Playthrough status:** complete new-game→ending playthroughs were executed **headlessly** (every system the
scene uses, minus rendering/input) for all seven endings; an on-device playthrough is pending the APK.

## 11. Pipeline additions (for the next content pass)

* `scripts/add_campaign_pass_content.py` — the one-shot merge (guarded; do not re-run).
* `scripts/campaign_pass_manifest.json` + `scripts/gen_builder_campaign.py` — regenerate
  `StoryContentBuilder.Campaign.cs` after **any** edit to those records: `python3 scripts/gen_builder_campaign.py`.
* `scripts/gen_campaign_scene.py` — scene extension (imported by `gen_firstlocation_scene.py`); rooms from
  `scripts/campaign_layout.json`.
* `scripts/gen_ari_combat_anims.py`, `scripts/run_tests.sh`.
* Regeneration order: `gen_story_content.py` → `gen_firstlocation_scene.py` → `validate_assets.py` → `run_tests.sh` → `compile_check.sh`.
