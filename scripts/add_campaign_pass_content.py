#!/usr/bin/env python3
"""ONE-SHOT merge: the complete playable campaign content pass (GAME_DESIGN.md §2/§5/§6/§7/§9/§11).

Adds to scripts/story_content.json (the canonical content) - nothing existing is rewritten:
  * 13 locations (P1 .. EP) wired into the existing hall hub, with unlock rules, connections,
    first-arrival world-state changes and checkpoints (LocationAnchor_<id> in the scene)
  * chapters ch_prologue / ch_fracture / ch_becoming / ch_reckoning / ch_epilogue
    (beats, branches, completion) chained after the existing ch_first_light / ch_whispers
  * NPC roster: young Mara, Dax, the three mentors (one exists per run), the Archivist,
    Mara at C2 / C3 (fate-driven) - CHARACTER_REFERENCE sheets REF-02/03/05 + derived mentors
  * abilities per line (Ember / Tide / Stone) + the secret Hollow line, each granted by a decision
  * enemies: grunt / charger / caster / bruiser / elite skins per chapter + bosses
    (First Echo, Dax duel, Cantor, Choirmaster x3 phases, fate-driven phase-2 inserts)
  * objectives per location (main / crisis / recovery / tutorial), timed D2 choice,
    world-state variants (market / docks / spire), 7 endings + epilogue narration.

Every record id added here is written to scripts/campaign_pass_manifest.json so that
scripts/gen_builder_campaign.py can generate the C# mirror (StoryContentBuilder.Campaign.cs)
from the SAME json - the builder never drifts from the data.

Guard: raises if the pass is already merged (chapter ch_prologue present).
"""
import json, os, collections

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, "story_content.json")
MANIFEST = os.path.join(HERE, "campaign_pass_manifest.json")
C = json.load(open(PATH))
if any(ch["id"] == "ch_prologue" for ch in C["chapters"]):
    raise SystemExit("campaign content pass already merged (ch_prologue exists) - nothing to do")

# ---------------------------------------------------------------- enum ints (ContentData.cs)
FlagIs, FlagIsNot, FlagMissing, VarAtLeast, AffinityAtLeast, BondAtLeast, DecisionWas, DecisionNotMade, \
    CodexOwned, ReputationAtLeast, ItemHeld, AbilityOwned, AreaUnlocked, SkillAtLeast, EchoesAtLeast, \
    AbilityLevelBelow, ObjectiveActive, ObjectiveCompleted, ObjectiveFailed, WorldStateIs = range(20)
SetFlag, ClearFlag, AddAffinity, SetAffinity, AddBond, SetVar, AddVar, SetWorldState, SpawnEntity, AddCodex, \
    GrantEchoes, AddReputation, SetReputation, UnlockAbility, AddSkillLevel, AddItem, RemoveItem, UnlockArea, \
    UpgradeAbility, BlockAbility, MoveNpc, CloseArea, ReopenArea, UnlockInteraction = range(24)
HUB, STORY, NPCLOC, COMBAT = 0, 1, 2, 3
MAIN, SIDE, CRISIS, RECOVERY = 0, 1, 2, 3
KINETIC, EMBER, TIDE, STONE, HOLLOW = 0, 1, 2, 3, 4
MELEE, PULSE = 0, 1
T_COND, T_DEC, T_OBJ_DONE, T_OBJ_FAIL = 0, 1, 2, 3

added = collections.defaultdict(list)

def cond(t, key, value="", amount=0): return {"type": t, "key": key, "value": value, "amount": amount}
def eff(t, key, value="", amount=0): return {"type": t, "key": key, "value": value, "amount": amount}
def flag(k, v="1"): return cond(FlagIs, k, v)
def setf(k, v="1"): return eff(SetFlag, k, v)

def node(id, speaker="", text="", nextId="", branchPrefix="", decisionId="", conditions=None, end=False):
    return {"id": id, "speaker": speaker, "text": text, "nextId": nextId, "branchPrefix": branchPrefix,
            "decisionId": decisionId, "conditions": conditions or [], "end": end}

def option(id, text, afterText="", conditions=None, effects=None):
    return {"id": id, "text": text, "afterText": afterText, "conditions": conditions or [], "effects": effects or []}

def decision(id, prompt, options, timeLimit=0, timeoutIdx=0, codex=""):
    d = {"id": id, "promptText": prompt, "timeLimitSeconds": timeLimit, "timeoutOptionIndex": timeoutIdx,
         "codexEntryId": codex, "options": options}
    C["decisions"].append(d); added["decisions"].append(id); return d

def graph(id, nodes):
    C["graphs"].append({"id": id, "nodes": nodes}); added["graphs"].append(id)

def encounter(id, npcName, graphId, start="start"):
    C["encounters"].append({"id": id, "npcName": npcName, "graphId": graphId, "startNodeId": start}); added["encounters"].append(id)

def scene(enc_id, npcName, lines, decisionId="", after=None, tail=None, variants=None):
    """Cutscene/dialogue helper. lines = [(speaker, text), ...] played in order; an optional
    embedded decision follows, then per-option aftermath lines (after = {optionId: (speaker, text)}),
    then optional tail lines. variants = {"prefix": [(conds, speaker, text), ...]} inserts a
    condition-branched line block at the position where a line equals ("@", prefix)."""
    gid = "g_" + enc_id
    nodes = []
    seq = list(lines)
    ids = []
    for i, ln in enumerate(seq):
        ids.append("n%d" % i)
    for i, ln in enumerate(seq):
        nid = ids[i]
        nxt = ids[i + 1] if i + 1 < len(seq) else ("decide" if decisionId else ("tail0" if tail else "end"))
        if ln[0] == "@":
            prefix = ln[1]
            nodes.append(node(nid, branchPrefix=prefix))
            for k, (conds, spk, txt) in enumerate(variants[prefix]):
                nodes.append(node("%s_v%d" % (prefix, k), spk, txt, nextId=nxt, conditions=conds))
            # unconditional fallback carries the bare prefix id
            nodes.append(node(prefix, "", "", nextId=nxt))
        else:
            nodes.append(node(nid, ln[0], ln[1], nextId=nxt))
    if decisionId:
        nodes.append(node("decide", branchPrefix="after", decisionId=decisionId))
        for opt, (spk, txt) in (after or {}).items():
            nodes.append(node("after_" + opt, spk, txt, nextId=("tail0" if tail else "end"),
                              conditions=[cond(DecisionWas, decisionId, opt)]))
        nodes.append(node("after", "", "", nextId=("tail0" if tail else "end")))
    for i, ln in enumerate(tail or []):
        nxt = "tail%d" % (i + 1) if i + 1 < len(tail) else "end"
        nodes.append(node("tail%d" % i, ln[0], ln[1], nextId=nxt))
    nodes.append(node("end", end=True))
    graph(gid, nodes)
    encounter(enc_id, npcName, gid, ids[0] if ids else "decide")

def npc(id, displayName, sheetRef, description, personality=1, faces=True, react=4.5, approach=1.6, avoid=0,
        talk=2.0, speed=1.1, turn=6, routine=None, states=None, interactions=None):
    C["npcs"].append({"id": id, "displayName": displayName, "sheetRef": sheetRef, "description": description,
        "behaviour": {"personality": personality, "facesPlayer": faces, "reactRadius": react,
                      "approachDistance": approach, "avoidDistance": avoid, "talkDistance": talk,
                      "moveSpeed": speed, "turnSpeed": turn, "usesRoutine": bool(routine)},
        "states": states or [], "interactions": interactions or [],
        "routine": [{"position": {"x": x, "y": 0, "z": z}, "dwellSeconds": d} for (x, z, d) in (routine or [])]})
    added["npcs"].append(id)

def state(conds, title, mood, approach=-1, avoid=-1, speed=-1, react=-1):
    return {"conditions": conds, "title": title, "moodLine": mood, "approachDistance": approach,
            "avoidDistance": avoid, "moveSpeed": speed, "reactRadius": react}

def talk(id, label, enc, conds=None): return {"id": id, "label": label, "encounterId": enc, "conditions": conds or []}

def objective(id, title, desc, type, area, offer, complete, giver="", auto=True, fail=None, counterVar="",
              counterTarget=0, counterText="", steps=None, consequences=None, failureConsequences=None,
              followUps=None, done="", failed=""):
    C["objectives"].append({"id": id, "title": title, "description": desc, "type": type, "areaId": area,
        "giverNpcId": giver, "offerConditions": offer, "autoActivate": auto, "completeConditions": complete,
        "failConditions": fail or [], "counterVar": counterVar, "counterTarget": counterTarget,
        "counterText": counterText, "steps": [{"text": t, "conditions": c} for (t, c) in (steps or [])],
        "consequences": consequences or [], "failureConsequences": failureConsequences or [],
        "followUps": followUps or [], "completionNotice": done, "failureNotice": failed})
    added["objectives"].append(id)

def attack(id, name, dmgType, delivery, dmg, rng, arc=70, radius=0, windup=0.7, cd=2.2, statuses=None):
    return {"id": id, "name": name, "damageType": dmgType, "delivery": delivery, "baseDamage": dmg, "range": rng,
            "arcDegrees": arc, "radius": radius, "windupSeconds": windup, "cooldownSeconds": cd,
            "applyStatusIds": statuses or []}

def enemy(id, name, desc, sheet, hp, defense, resist, speed, detect, leash, rng, stagger, atk, activation, onDefeat):
    C["enemies"].append({"id": id, "displayName": name, "description": desc, "sheetRef": sheet, "maxHealth": hp,
        "defense": defense, "resistances": [{"type": t, "multiplier": m} for t, m in
                                             zip((KINETIC, EMBER, TIDE, STONE, HOLLOW), resist)],
        "moveSpeed": speed, "turnSpeed": 5.0, "detectionRadius": detect, "leashRadius": leash, "attackRange": rng,
        "staggerSeconds": stagger, "attack": atk, "activationConditions": activation, "onDefeatEffects": onDefeat})
    added["enemies"].append(id)

def ability(id, name, line, desc, hint, unlock, vfx, rows):
    C["progression"]["abilities"].append({"id": id, "name": name, "line": line, "description": desc, "category": 0,
        "unlockHint": hint, "unlockConditions": unlock, "vfxRef": "fx/" + vfx, "sfxRef": "sfx/" + vfx,
        "echoCostPerLevel": 10, "levels": [
            {"level": i + 1, "cooldown": cd, "power": pw, "radius": rad, "duration": dur, "energyCost": 0, "description": d}
            for i, (cd, pw, rad, dur, d) in enumerate(rows)]})
    added["abilities"].append(id)

def ability_combat(abilityId, dmgType, dmgPer, healPer=0.0, toTargets=None, toPlayer=None):
    C["abilityCombat"].append({"abilityId": abilityId, "damageType": dmgType, "damagePerPower": dmgPer,
        "healPlayerPerPower": healPer, "applyStatusToTargets": toTargets or [], "applyStatusToPlayer": toPlayer or []})
    added["abilityCombat"].append(abilityId)

def status(id, name, desc, dur, tick=1.0, hpTick=0, moveMul=1.0, atkMul=1.0, immune=False):
    C["statusEffects"].append({"id": id, "name": name, "description": desc, "durationSeconds": dur,
        "tickIntervalSeconds": tick, "healthPerTick": hpTick, "moveSpeedMultiplier": moveMul,
        "attackRateMultiplier": atkMul, "grantsImmunity": immune})
    added["statusEffects"].append(id)

def item(id, name, desc):
    C["progression"]["items"].append({"id": id, "name": name, "description": desc}); added["items"].append(id)

def area(id, name):
    C["progression"]["areas"].append({"id": id, "name": name}); added["areas"].append(id)

def location(id, name, kind, checkpoint, desc, unlock, hint, connections, npcs, encounters, objectives, wsc, env):
    C["locations"].append({"id": id, "name": name, "kind": kind, "sceneKey": "FirstLocation", "checkpointId": checkpoint,
        "description": desc, "unlockRules": unlock, "lockedHint": hint, "entryConditions": [],
        "connections": connections, "npcs": npcs, "encounters": encounters, "objectives": objectives,
        "worldStateChanges": wsc,
        "environment": {"profile": env[0], "ambient": env[1], "fog": env[2], "fogDensity": env[3], "sun": env[4], "sunIntensity": env[5]}})
    added["locations"].append(id)

def rule(conds, text, opens=True): return {"conditions": conds, "opens": opens, "text": text}

def beat(id, title, journal, trigger=T_COND, key="", offer=None, req=None, resolveConds=None, effects=None, priority=0):
    return {"id": id, "title": title, "journalText": journal, "offerConditions": offer or [], "resolveTrigger": trigger,
            "resolveKey": key, "resolveConditions": resolveConds or [], "requiredBeatIds": req or [],
            "onResolveEffects": effects or [], "priority": priority}

def branch(id, frm, to, label, req=None, effects=None):
    return {"id": id, "fromBeatId": frm, "toBeatId": to, "label": label, "requiredConditions": req or [], "effects": effects or []}

def chapter(id, title, subtitle, desc, entry, beats, branches, completion, completionEffects, journal):
    C["chapters"].append({"id": id, "title": title, "subtitle": subtitle, "description": desc, "entryConditions": entry,
        "beats": beats, "branches": branches, "completionConditions": completion,
        "completionEffects": completionEffects, "completionJournal": journal})
    added["chapters"].append(id)

def world_interaction(key, label, conds):
    C["worldInteractions"].append({"key": key, "label": label, "conditions": conds}); added["worldInteractions"].append(key)

# ================================================================ progression: areas / items / statuses
for aid, aname in [("last_summer", "The Last Summer"), ("fracture_night", "Night of the Fracture"),
                   ("under_spire", "Under the Spire"), ("interlude_becoming", "Interlude: Becoming"),
                   ("docks", "Contested Docks"), ("sanctuary", "The Sanctuary"), ("long_wall", "The Long Wall"),
                   ("dax_arena", "Dax Confrontation"), ("interlude_reckoning", "Interlude: Reckoning"),
                   ("market", "The Old Market"), ("spire_ascent", "Ascent of the Spire"),
                   ("choirmaster", "The Choirmaster"), ("epilogue", "Epilogue: Vessa, After")]:
    area(aid, aname)
item("paper_kite", "Mara's Paper Kite", "A kite the colour of the last summer. The string is knotted where it broke and was mended.")
item("mentor_token", "Mentor's Token", "A small pressed sigil in your mentor's line colour. It is warm when they are near.")
item("dax_echo", "Dax's Echo", "What was left of a rival's light, folded into yours. It does not sit quietly.")
item("cantor_voice", "The Cantor's Voice", "A shard of the Choir's song, cut loose. It remembers every name it took.")
status("burn_deep", "Deep Burn", "Cinder heat that keeps eating after the burst.", 5.0, 1.0, -5)
status("riptide_pull", "Riptide", "The water drags at the legs; every step costs.", 3.0, 1.0, 0, 0.55)
status("tremor_daze", "Tremor Daze", "The ground's shout still ringing in the bones.", 2.0, 1.0, 0, 0.35, 0.4)
status("bulwark_guard", "Bulwark", "A parry stance of stone; the next blow slides off.", 1.5, 1.0, 0, 0.8, 1.0, True)
status("hollow_drain", "Hollow Drain", "Something is being taken. Slowly.", 4.0, 1.0, -3)
status("choir_song", "Choir Song", "The Choir's harmony in the ears - hands feel far away.", 3.0, 1.0, 0, 0.75, 0.75)

# ================================================================ abilities (per line; each granted by a decision)
ROWS = lambda a, b, c: [(12.0, 1.0, 3.5, 1.0, a), (9.0, 1.5, 4.5, 1.4, b), (6.0, 2.25, 6.0, 1.8, c)]
ability("cinder_burst", "Cinder Burst", "ember", "Kael's lesson: heat thrown outward in a ring that knocks the Choir off their song.",
        "Choose Kael as your mentor on the night of the Fracture.", [cond(DecisionWas, "dec_mentor", "kael")], "burst/cinder",
        ROWS("A tight ring of cinders. Close things burn.", "The ring widens; the burn lingers.", "Full bind. The air itself catches."))
ability("phoenix_reckoning", "Phoenix Reckoning", "ember", "The Ember capstone: every strike detonates for a breath of borrowed fire.",
        "Take the Contested Docks by force.", [cond(ObjectiveCompleted, "obj_docks_assault")], "ult/phoenix",
        ROWS("Eight seconds of burning strikes.", "The fire returns sooner and reaches further.", "Full bind. You are the beacon now."))
ability("riptide", "Riptide", "tide", "Odalys's lesson: a cone of water that drags what would flee into reach.",
        "Choose Sister Odalys as your mentor on the night of the Fracture.", [cond(DecisionWas, "dec_mentor", "odalys")], "pull/riptide",
        ROWS("A short pull. Feet slide toward you.", "The pull reaches further and holds longer.", "Full bind. The whole room leans in."))
ability("call_ally", "Call Ally", "tide", "The Tide capstone: whoever stands with you strikes when you call - Mara, Dax, or the water itself.",
        "Hold the Sanctuary.", [cond(ObjectiveCompleted, "obj_sanctuary_hold")], "ally/call",
        ROWS("One answer to one call.", "The answer comes faster and hits harder.", "Full bind. You are never alone in the arena."))
ability("tremor_stomp", "Tremor Stomp", "stone", "Bran's lesson: the ground shouts once, and everything near it forgets its footing.",
        "Choose Warden Bran as your mentor on the night of the Fracture.", [cond(DecisionWas, "dec_mentor", "bran")], "stomp/tremor",
        ROWS("A short shout from the floor.", "The tremor spreads and dazes longer.", "Full bind. Nothing stands that you did not permit."))
ability("bulwark", "Bulwark", "stone", "The Stone capstone: a parry stance; the perfect read staggers and answers.",
        "Hold the Long Wall.", [cond(ObjectiveCompleted, "obj_wall_hold")], "stance/bulwark",
        ROWS("A moment of stone. Blows slide off.", "The stance holds longer and answers harder.", "Full bind. You are the wall."))
ability("drain_touch", "Drain Touch", "hollow", "The Hollow line's first gift: a strike that takes what it breaks and gives it to you.",
        "Let the Hollow reach 25 and drink at the dark shrine.", [cond(DecisionWas, "dec_hollow_shrine", "drink")], "hollow/drain",
        ROWS("A cold hand. Some of what leaves them arrives in you.", "The drain runs deeper.", "Full bind. Nothing is lost - to you."))
ability("hollow_throne", "Hollow Throne", "hollow", "The Hollow ultimate: everything already breaking, breaks. Dax's echo taught you this.",
        "Absorb Dax's Echo after the duel.", [cond(DecisionWas, "dec_dax_duel_end", "absorb")], "hollow/throne",
        ROWS("The weak fall at once.", "The threshold rises; more of them are weak.", "Full bind. The throne is wherever you stand."))
ability_combat("cinder_burst", EMBER, 12.0, 0.0, ["burn_deep"])
ability_combat("phoenix_reckoning", EMBER, 18.0, 0.0, ["echo_burn", "burn_deep"])
ability_combat("riptide", TIDE, 6.0, 4.0, ["riptide_pull"])
ability_combat("call_ally", TIDE, 16.0, 6.0, [])
ability_combat("tremor_stomp", STONE, 7.0, 0.0, ["tremor_daze"])
ability_combat("bulwark", STONE, 9.0, 0.0, ["suppression"], ["bulwark_guard"])
ability_combat("drain_touch", HOLLOW, 11.0, 6.0, ["hollow_drain"])
ability_combat("hollow_throne", HOLLOW, 24.0, 8.0, ["hollow_drain", "suppression"])

# ================================================================ enemies (archetype skins per chapter + bosses)
def kills(var, echoes=5, extra=None):
    return [eff(AddVar, var, "", 1), eff(GrantEchoes, "", "", echoes)] + (extra or [])
visited = lambda loc: [flag("loc_visited_" + loc)]
R_GRUNT = (1.0, 1.15, 0.9, 1.0, 0.6)
R_BRUISER = (0.7, 1.0, 1.0, 0.6, 0.6)
R_CASTER = (1.1, 0.8, 1.3, 1.0, 0.5)
R_BOSS = (0.9, 1.0, 1.0, 1.0, 0.4)
# C1L1 Night of the Fracture: 3 arenas (2 grunts / grunt + charger / charger + grunt)
enemy("choir_grunt", "Choir Grunt", "Tan-coated foot soldier of the Choir. Approaches, swings twice, sings the whole time.", "REF-06A",
      34, 1.5, R_GRUNT, 1.7, 8.0, 14.0, 2.0, 0.3, attack("grunt_combo", "Two-Beat Combo", KINETIC, MELEE, 8, 2.0, 70, 0, 0.55, 1.6),
      visited("fracture_night"), kills("fn_kills"))
enemy("choir_charger", "Choir Charger", "A Choir runner that winds up for eight tenths of a second - then is simply where you were.", "REF-06A",
      40, 1.0, R_GRUNT, 3.2, 10.0, 18.0, 2.4, 0.25, attack("charger_rush", "Wind-up Rush", KINETIC, MELEE, 14, 2.4, 50, 0, 0.8, 2.6),
      visited("fracture_night"), kills("fn_kills", 6))
# C1L2 Under the Spire: casters + bruiser, then the First Echo boss
enemy("choir_caster", "Choir Caster", "White-plated chorister. Keeps its distance and throws the song in bright pulses.", "REF-06C",
      30, 0.5, R_CASTER, 1.3, 11.0, 16.0, 5.5, 0.35, attack("caster_pulse", "Bright Pulse", HOLLOW, PULSE, 9, 5.5, 0, 3.0, 0.9, 2.8, ["choir_song"]),
      visited("under_spire"), kills("us_kills", 6))
enemy("choir_bruiser", "Choir Bruiser", "Olive-plated shield-bearer. Frontal blows slide off - flank it, or break its stance with a heavy hit.", "REF-06B",
      70, 4.0, R_BRUISER, 1.2, 8.0, 14.0, 2.3, 0.5, attack("bruiser_slam", "Shield Slam", KINETIC, MELEE, 16, 2.3, 60, 0, 0.9, 2.8, ["suppression"]),
      visited("under_spire"), kills("us_kills", 8))
enemy("first_echo", "The First Echo", "A construct of Fracture light shaped like a person who never finished forming. It teaches the dodge - the hard way.", "BOSS-01",
      160, 3.0, R_BOSS, 1.6, 12.0, 20.0, 2.6, 0.4, attack("echo_lash", "Echo Lash", HOLLOW, MELEE, 15, 2.6, 90, 0, 0.75, 2.0, ["choir_song"]),
      [cond(VarAtLeast, "us_kills", "", 3)],
      [setf("first_echo_defeated"), eff(SpawnEntity, "first_echo", "0"), eff(SpawnEntity, "first_echo_husk", "1"),
       eff(SpawnEntity, "first_echo_sign", "1"), eff(GrantEchoes, "", "", 25), eff(AddCodex, "c1_first_echo_felled"), eff(AddReputation, "choir", "", -10), eff(AddReputation, "wards", "", 10)])
# C2 path skins (Contested Docks / Sanctuary / Long Wall) - 4 arenas each, counted per path
enemy("choir_sentinel", "Choir Sentinel", "Second-cohort grunt in dock-grey. Faster combo, same song.", "REF-06A",
      44, 2.0, R_GRUNT, 1.9, 9.0, 15.0, 2.0, 0.3, attack("sentinel_combo", "Three-Beat Combo", KINETIC, MELEE, 10, 2.0, 70, 0, 0.5, 1.4),
      [flag("c2_path_open")], kills("c2_kills", 6))
enemy("choir_lancer", "Choir Lancer", "A charger carrying a wave-lance. The telegraph is long; the reach is longer.", "REF-06A",
      52, 1.5, R_GRUNT, 3.4, 11.0, 18.0, 3.0, 0.25, attack("lancer_rush", "Lance Rush", KINETIC, MELEE, 18, 3.0, 45, 0, 0.85, 2.6),
      [flag("c2_path_open")], kills("c2_kills", 7))
enemy("choir_elite", "Choir Elite", "White-plated, full kit: a combo, a pulse and the song. One per chapter - this is the one.", "REF-06C",
      120, 3.5, R_BOSS, 1.8, 11.0, 18.0, 2.5, 0.35, attack("elite_cadence", "Cadence", HOLLOW, MELEE, 14, 2.5, 80, 0, 0.6, 1.6, ["choir_song"]),
      [cond(VarAtLeast, "c2_kills", "", 6)], kills("c2_kills", 15, [setf("c2_elite_down"), eff(SpawnEntity, "mara_crane", "1")]))
# C2X Dax Confrontation: duel (bond low) OR team-up against a Choir hunter (bond high)
enemy("dax_rival", "Dax", "Dax, Echo-awakened, in the navy blazer he still wears like a uniform. He fights like he argues: fast, and to win.", "REF-03",
      140, 2.5, (1.0, 0.9, 0.9, 1.1, 0.7), 2.0, 10.0, 16.0, 2.4, 0.3, attack("dax_cut", "Rival's Cut", KINETIC, MELEE, 13, 2.4, 80, 0, 0.5, 1.5),
      [flag("dax_duel")], [setf("dax_beaten"), eff(SpawnEntity, "dax_rival", "0"), eff(SpawnEntity, "dax_down", "1"), eff(GrantEchoes, "", "", 20)])
enemy("choir_hunter", "Choir Hunter", "The elite sent for Dax. Two of you against it - which is exactly what it did not plan for.", "REF-06C",
      150, 3.5, R_BOSS, 1.9, 12.0, 18.0, 2.5, 0.35, attack("hunter_cadence", "Hunter's Cadence", HOLLOW, MELEE, 15, 2.5, 80, 0, 0.6, 1.5, ["choir_song"]),
      [flag("dax_fate", "truce")],
      [setf("dax_resolved"), setf("dax_alive"), eff(SpawnEntity, "choir_hunter", "0"), eff(GrantEchoes, "", "", 25),
       eff(AddBond, "dax", "", 15), eff(SpawnEntity, "hunter_fallen", "1"), eff(AddCodex, "c2_hunter_felled")])
# C3: husks (Hollow skins), the Cantor mid-boss, the Choirmaster in three phases + fate-driven inserts
enemy("hollow_husk", "Hollow Husk", "What the Choir leaves of a person. It does not sing any more; it only reaches.", "REF-06B",
      50, 2.0, R_BRUISER, 1.5, 9.0, 15.0, 2.1, 0.3, attack("husk_reach", "Reach", HOLLOW, MELEE, 11, 2.1, 70, 0, 0.6, 1.8, ["hollow_drain"]),
      visited("market"), kills("c3_kills", 7))
enemy("choir_cantor", "Choir Cantor", "The Choir's voice in the Old Market. The arena is whatever the city became - the Cantor sings to it.", "BOSS-03",
      220, 3.5, R_BOSS, 1.6, 12.0, 20.0, 2.6, 0.4, attack("cantor_verse", "Verse", HOLLOW, PULSE, 13, 4.5, 0, 3.2, 0.9, 2.4, ["choir_song"]),
      [cond(VarAtLeast, "c3_kills", "", 3)],
      [setf("cantor_defeated"), eff(SpawnEntity, "choir_cantor", "0"), eff(AddItem, "cantor_voice"), eff(GrantEchoes, "", "", 30),
       eff(AddCodex, "c3_cantor_felled"), eff(AddReputation, "choir", "", -15)])
enemy("spire_warden", "Spire Warden", "A Choir warden built for the anomaly: it walks the tilted floors as if they were flat.", "REF-06B",
      80, 4.0, R_BRUISER, 1.4, 9.0, 15.0, 2.3, 0.5, attack("warden_arc", "Tilted Arc", KINETIC, MELEE, 17, 2.3, 60, 0, 0.85, 2.6, ["suppression"]),
      visited("spire_ascent"), kills("ascent_kills", 9))
enemy("choirmaster_p1", "The Choirmaster", "The Choir's conductor at the Fracture's heart. Phase one: the song alone.", "BOSS-04",
      240, 3.0, R_BOSS, 1.5, 14.0, 24.0, 2.8, 0.4, attack("cm_overture", "Overture", HOLLOW, PULSE, 14, 5.0, 0, 3.4, 1.0, 2.6, ["choir_song"]),
      visited("choirmaster"), [setf("cm_p1"), eff(SpawnEntity, "choirmaster_p1", "0"), eff(SpawnEntity, "choirmaster_p2", "1"), eff(SpawnEntity, "cm_door", "1"), eff(GrantEchoes, "", "", 20)])
enemy("choirmaster_p2", "The Choirmaster, Unmasked", "Phase two: the Choirmaster calls whoever your life left standing against you - or beside you.", "BOSS-04",
      260, 3.5, R_BOSS, 1.8, 14.0, 24.0, 2.8, 0.35, attack("cm_chorus", "Chorus", HOLLOW, MELEE, 17, 2.8, 90, 0, 0.7, 1.8, ["choir_song", "hollow_drain"]),
      [flag("cm_press")], [setf("cm_p2"), eff(SpawnEntity, "choirmaster_p2", "0"), eff(SpawnEntity, "choirmaster_p3", "1"), eff(SpawnEntity, "cm_finale_sign", "1"), eff(GrantEchoes, "", "", 20)])
enemy("choirmaster_p3", "The Choirmaster, Ascendant", "Phase three: the mechanics belong to your dominant line - a race, a ward, an attrition, or an execution window.", "BOSS-04",
      300, 4.0, R_BOSS, 2.0, 14.0, 24.0, 3.0, 0.3, attack("cm_finale", "Finale", HOLLOW, MELEE, 20, 3.0, 100, 0, 0.65, 1.5, ["choir_song", "hollow_drain", "suppression"]),
      [flag("cm_p2")], [setf("choirmaster_defeated"), eff(SpawnEntity, "choirmaster_p3", "0"), eff(SpawnEntity, "fracture_heart", "1"),
                        eff(GrantEchoes, "", "", 50), eff(AddCodex, "c3_choirmaster_felled"), eff(AddReputation, "choir", "", -30), eff(AddReputation, "folk", "", 20)])
enemy("dax_final", "Dax, Final Enemy", "The rival you never made peace with, singing the Choirmaster's part.", "REF-03",
      120, 2.5, (1.0, 0.9, 0.9, 1.1, 0.7), 2.0, 12.0, 20.0, 2.4, 0.3, attack("dax_final_cut", "Final Cut", HOLLOW, MELEE, 15, 2.4, 80, 0, 0.5, 1.5),
      [flag("cm_press"), flag("dax_fate", "final_enemy")], [eff(SpawnEntity, "dax_final", "0"), setf("dax_alive", "0"), eff(GrantEchoes, "", "", 15)])
enemy("mara_turned", "Mara, Turned", "Mara with the Hollow behind her eyes. She still moves the way she did on the pier.", "REF-02",
      110, 2.0, (1.0, 1.0, 0.8, 1.0, 0.6), 1.9, 12.0, 20.0, 2.3, 0.3, attack("mara_turned_cut", "Turned Cut", HOLLOW, MELEE, 13, 2.3, 80, 0, 0.55, 1.6, ["hollow_drain"]),
      [flag("cm_press"), flag("mara_fate", "turned")], [eff(SpawnEntity, "mara_turned", "0"), setf("mara_alive", "0"), eff(GrantEchoes, "", "", 15)])

# ================================================================ NPC roster (CHARACTER_REFERENCE sheets)
MENTOR = lambda m: [cond(DecisionWas, "dec_mentor", m)]
npc("mara_young", "Mara (age 10)", "REF-02", "Mara at ten: same dark hair, a paper kite, a plan for every afternoon. The last summer before the light.",
    personality=1, approach=1.4, speed=1.3, routine=[(-38, -12, 2.0), (-42, -8, 2.5)],
    states=[state([cond(DecisionWas, "dec_kite", "give")], "Mara \u00b7 Best Friend", "Mara grins with the kite string round her wrist. She is not letting go of it, or of you.", 1.1),
            state([cond(DecisionWas, "dec_kite")], "Mara \u00b7 Summer Friend", "Mara squints at the sky, planning tomorrow already.")],
    interactions=[talk("kite_talk", "Talk to Mara", "p1_kite", [cond(DecisionNotMade, "dec_kite")]),
                  talk("pier_talk", "Race Mara to the pier", "p1_pier", [cond(DecisionWas, "dec_kite"), cond(DecisionNotMade, "dec_pier")]),
                  talk("summer_end", "Sit with Mara", "p1_summer_end", [cond(DecisionWas, "dec_pier")])])
npc("dax", "Dax", "REF-03", "Dax: chestnut hair, glasses, white tee under a navy blazer. Awakened the same night you were - and never forgave you for being first.",
    personality=2, approach=2.0, avoid=1.2, speed=1.4, routine=[(-38, 22, 3.0), (-42, 26, 2.0)],
    states=[state([flag("dax_fate", "redeemed")], "Dax \u00b7 Redeemed", "Dax stands easier now. The blazer is buttoned wrong and he does not care.", 1.4),
            state([flag("dax_fate", "truce")], "Dax \u00b7 Truce", "Dax nods once. It costs him, and he pays it.", 1.6),
            state([cond(BondAtLeast, "dax", "", 1)], "Dax \u00b7 Wary", "Dax keeps his distance and his sharpness."),
            state([cond(DecisionWas, "dec_dax_spar", "press")], "Dax \u00b7 Rival", "Dax will not look at you. The bruise you gave him is a promise.", 2.6, 2.0)],
    interactions=[talk("spar", "Spar with Dax", "c1_dax_spar", [cond(DecisionNotMade, "dec_dax_spar")]),
                  talk("after_spar", "Talk to Dax", "c1_dax_after", [cond(DecisionWas, "dec_dax_spar")])])
npc("kael", "Kael", "MENTOR-EMBER", "Kael: forties, a scar over the brow, grey-flecked crop, an Ember-trimmed military coat he was told to stop wearing. Disgraced soldier; still teaches like it matters.",
    personality=0, approach=1.8, speed=1.0, routine=[(-66, -14, 3.0)],
    states=[state([flag("mentor_fate", "estranged")], "Kael \u00b7 Estranged", "Kael watches you like a report he has already filed.", 3.0),
            state([cond(BondAtLeast, "kael", "", 20)], "Kael \u00b7 Sworn", "Kael's coat is buttoned to the throat. He is ready when you are.", 1.4)],
    interactions=[talk("kael_lesson", "Train with Kael", "c1_mentor_lesson", MENTOR("kael") + [cond(DecisionNotMade, "dec_mentor_advice")]),
                  talk("kael_talk", "Talk to Kael", "c2_mentor_interlude", MENTOR("kael") + [cond(DecisionWas, "dec_mentor_advice")])])
npc("odalys", "Sister Odalys", "MENTOR-TIDE", "Sister Odalys: fifties, silver bun, a Tide-teal healer's robe with the hem always wet. She was mending Fracture-burns before anyone had a name for them.",
    personality=1, approach=1.5, speed=0.9, routine=[(-66, -14, 3.0)],
    states=[state([flag("mentor_fate", "estranged")], "Odalys \u00b7 Estranged", "Odalys folds her hands. Whatever she wanted to say, she keeps.", 3.0),
            state([cond(BondAtLeast, "odalys", "", 20)], "Odalys \u00b7 Sworn", "Odalys's robe hem drips. She has been in the water for someone again.", 1.2)],
    interactions=[talk("odalys_lesson", "Train with Odalys", "c1_mentor_lesson", MENTOR("odalys") + [cond(DecisionNotMade, "dec_mentor_advice")]),
                  talk("odalys_talk", "Talk to Odalys", "c2_mentor_interlude", MENTOR("odalys") + [cond(DecisionWas, "dec_mentor_advice")])])
npc("bran", "Warden Bran", "MENTOR-STONE", "Warden Bran: fifties, bulky, shaved head, a Stone-grey high-collar ward coat. Spire protector; he held a door for six hours the night the light came.",
    personality=0, approach=2.2, speed=0.8, routine=[(-66, -14, 3.0)],
    states=[state([flag("mentor_fate", "estranged")], "Bran \u00b7 Estranged", "Bran stands where he always stands. He does not turn.", 3.2),
            state([cond(BondAtLeast, "bran", "", 20)], "Bran \u00b7 Sworn", "Bran's collar is up, his feet are planted. He is a wall with an opinion.", 1.8)],
    interactions=[talk("bran_lesson", "Train with Bran", "c1_mentor_lesson", MENTOR("bran") + [cond(DecisionNotMade, "dec_mentor_advice")]),
                  talk("bran_talk", "Talk to Bran", "c2_mentor_interlude", MENTOR("bran") + [cond(DecisionWas, "dec_mentor_advice")])])
npc("archivist", "The Archivist", "REF-05", "The Archivist: a hologram in a cyan-white bodysuit, silver-white hair that moves against no wind. Keeper of shrines, codex and the count of your echoes. It does not fight. It remembers.",
    personality=3, faces=True, approach=0, speed=0, routine=[],
    states=[state([flag("ending")], "Archivist \u00b7 Witness", "The Archivist's light steadies. It has seen how this life ends."),
            state([cond(AffinityAtLeast, "hollow", "", 25)], "Archivist \u00b7 Uneasy", "The Archivist flickers when you come near. It keeps its distance in a way holograms should not need to.")],
    interactions=[talk("archive_shrine", "Consult the Archivist", "i2_archivist", [cond(FlagMissing, "c2_complete")]),
                  talk("archive_reckoning", "Consult the Archivist", "i3_archivist", [flag("c2_complete")])])
npc("mara_c2", "Mara", "REF-02", "Mara at twenty, grey hoodie, hair tied back with the kite string. She joined the relief lines the week the Choir came.",
    personality=1, approach=1.5, speed=1.2, routine=[(-74, -6, 2.5), (-72, -10, 2.5)],
    states=[state([flag("mara_fate", "lost")], "Mara \u00b7 Lost", "", 9.0, 9.0, 0, 0),
            state([flag("mara_fate", "ally")], "Mara \u00b7 Ally", "Mara has stopped asking if you're alright. She fights beside you instead.", 1.2),
            state([cond(BondAtLeast, "mara", "", 50)], "Mara \u00b7 Bonded", "Mara stands close enough to touch your sleeve. She does, once.", 1.2),
            state([cond(BondAtLeast, "mara", "", 1)], "Mara \u00b7 Warm", "Mara watches the water, then you.")],
    interactions=[talk("c2_mara_after", "Talk to Mara", "c2_mara_after", [cond(DecisionWas, "dec_save_mara")]),
                  talk("c2_mara_talk", "Talk to Mara", "c2_mara_talk", [])])
npc("mara_c3", "Mara", "REF-02", "Mara at twenty-six. Whatever your life made of her, she came to the Spire.",
    personality=1, approach=1.5, speed=1.2, routine=[(-96, -8, 3.0)],
    states=[state([flag("mara_fate", "ally")], "Mara \u00b7 Ally", "Mara checks her wraps and nods at the Spire. She's coming.", 1.1),
            state([flag("mara_fate", "civilian")], "Mara \u00b7 Civilian", "Mara stands at the barricade line. She is not coming, and she is not leaving either.", 2.0),
            state([cond(BondAtLeast, "mara", "", 50)], "Mara \u00b7 Bonded", "Mara stands close. She has always stood close.", 1.1)],
    interactions=[talk("c3_mara_talk", "Talk to Mara", "c3_mara_reckoning", [])])

# ================================================================ PROLOGUE - The Last Summer (tutorial, no combat)
decision("dec_kite", "Mara's paper kite snags on the pier rail and the string breaks in your hand. She is looking at you.", [
    option("give", "Knot the string and hand it back. It was always hers.", "Mara ties the string twice round her wrist so it can't happen again.",
           effects=[eff(AddBond, "mara", "", 12), eff(AddAffinity, "tide", "", 5), eff(AddCodex, "p1_kite_given"), eff(AddItem, "paper_kite"), setf("tut_kite")]),
    option("keep", "Keep it. You'll fix it properly at home.", "Mara shrugs like it doesn't matter. It does.",
           effects=[eff(AddBond, "mara", "", 2), eff(AddAffinity, "stone", "", 5), eff(AddItem, "paper_kite"), setf("tut_kite")]),
    option("fly", "Throw it back into the wind, broken string and all.", "It flies - badly, gloriously - and Mara laughs so hard she sits down.",
           effects=[eff(AddBond, "mara", "", 7), eff(AddAffinity, "ember", "", 5), eff(AddItem, "paper_kite"), setf("tut_kite")])],
    codex="p1_last_summer")
scene("p1_kite", "Mara", [("Mara", "Ari! You came. Look - I got the string long enough this time. Hold it, hold it - no, RUN with it."),
                          ("", "The kite climbs over the pier. For one long second the whole summer is holding its breath."),
                          ("Mara", "Told you. Told you it'd fly.")], "dec_kite",
      {"give": ("Mara", "You're the only one who doesn't tell me to grow up."),
       "keep": ("Mara", "Fine. But you owe me a kite, Ari. A good one."),
       "fly": ("Mara", "You're insane. Do it again.")})
decision("dec_pier", "Dax and the older kids are daring each other off the end of the pier. Mara wants to go home. Dax is watching you.", [
    option("stay", "Stay with Mara. Walk her home.", "Dax shouts something after you. Mara pretends not to hear it, and so do you.",
           effects=[eff(AddBond, "mara", "", 10), eff(AddBond, "dax", "", -4), eff(AddAffinity, "tide", "", 5), setf("tut_pier")]),
    option("jump", "Jump. Then walk her home.", "The water is colder than the sun promised. Dax whoops. Mara is already halfway up the road.",
           effects=[eff(AddBond, "dax", "", 6), eff(AddBond, "mara", "", -3), eff(AddAffinity, "ember", "", 5), setf("tut_pier")]),
    option("refuse", "Tell Dax no one is jumping. Mean it.", "Nobody jumps. Dax looks at you like he's just learned something he will not forget.",
           effects=[eff(AddBond, "dax", "", -2), eff(AddBond, "mara", "", 6), eff(AddAffinity, "stone", "", 5), setf("tut_pier")])],
    codex="p1_pier")
scene("p1_pier", "Mara", [("Dax", "Oi, Ari! Scared? Your girlfriend can hold your shoes."),
                          ("Mara", "Ignore him. Come on - it's getting dark and my mum will kill me.")], "dec_pier",
      {"stay": ("Mara", "Thanks. You didn't have to. I know you wanted to."),
       "jump": ("Mara", "You're soaked. You're an idiot. Come on."),
       "refuse": ("Mara", "That was... you were really scary just then. In a good way. I think.")})
decision("dec_summer_end", "The last light of the last summer. Mara asks what you want to be.", [
    option("hero", "Someone people run toward when it's bad.", "Mara nods slowly, like she's filing it away to hold you to.",
           effects=[eff(AddAffinity, "ember", "", 5), eff(AddBond, "mara", "", 4), setf("prologue_complete"), setf("mara_alive"), setf("dax_alive"), eff(GrantEchoes, "", "", 10)]),
    option("healer", "Someone who can fix what breaks.", "Mara holds up the kite string. 'You already are.'",
           effects=[eff(AddAffinity, "tide", "", 5), eff(AddBond, "mara", "", 6), setf("prologue_complete"), setf("mara_alive"), setf("dax_alive"), eff(GrantEchoes, "", "", 10)]),
    option("wall", "Someone who doesn't move when they should.", "Mara leans against your shoulder to test it. 'Yeah. Okay.'",
           effects=[eff(AddAffinity, "stone", "", 5), eff(AddBond, "mara", "", 5), setf("prologue_complete"), setf("mara_alive"), setf("dax_alive"), eff(GrantEchoes, "", "", 10)])],
    codex="p1_summer_end")
scene("p1_summer_end", "Mara", [("", "The pier boards are still warm. Somewhere uptown the Spire catches the last of the sun and holds it."),
                                ("Mara", "Six years from now. What are you?")], "dec_summer_end",
      {}, tail=[("Mara", "Whatever it is - you'll still come to the pier. Promise."), ("", "You promise. The summer ends.")])
world_interaction("kite_pickup", "Pick up the paper kite", [cond(FlagMissing, "tut_moved")])
world_interaction("pier_bell", "Ring the pier bell", [flag("tut_moved")])
objective("obj_tut_move", "First steps", "Walk to the end of the pier and pick up the fallen kite. (Left stick moves; INTERACT when the prompt appears.)",
          MAIN, "last_summer", visited("last_summer"), [flag("tut_moved")],
          consequences=[eff(GrantEchoes, "", "", 5)], followUps=["obj_tut_talk"], done="You found the kite. Now find Mara.")
objective("obj_tut_talk", "The last summer", "Talk to Mara. Your first choices with her shape who she becomes.",
          MAIN, "last_summer", [cond(ObjectiveCompleted, "obj_tut_move")], [cond(DecisionWas, "dec_summer_end")],
          steps=[("Hand Mara her kite - or don't", [cond(DecisionWas, "dec_kite")]),
                 ("Answer Dax at the pier", [cond(DecisionWas, "dec_pier")]),
                 ("Sit with Mara at sunset", [cond(DecisionWas, "dec_summer_end")])],
          consequences=[eff(AddCodex, "p1_complete"), eff(SetWorldState, "market", "intact")],
          done="The last summer ends. The hall is waiting.")
chapter("ch_prologue", "Prologue", "The Last Summer",
        "Ari at ten. A kite, a pier, a friend. The choices that decide who Mara becomes - remembered from the hall's threshold.",
        visited("last_summer"),
        [beat("beat_p1_kite", "The kite", "A paper kite, a broken string, and a choice about whose it was.", T_DEC, "dec_kite"),
         beat("beat_p1_pier", "The pier", "Dax dared the pier. You answered.", T_DEC, "dec_pier", req=["beat_p1_kite"]),
         beat("beat_p1_end", "The last light", "The last summer ended on the pier boards, with a promise.", T_DEC, "dec_summer_end", req=["beat_p1_pier"])],
        [branch("br_p1_bonded", "beat_p1_kite", "beat_p1_pier", "Mara's kite, Mara's friend", [cond(DecisionWas, "dec_kite", "give")], [setf("mara_fate", "civilian")]),
         branch("br_p1_kept", "beat_p1_kite", "beat_p1_pier", "A kite kept", [cond(DecisionWas, "dec_kite")], [setf("mara_fate", "civilian")])],
        [flag("prologue_complete")], [eff(AddCodex, "p1_ch_complete")], "Prologue complete: the last summer is remembered. Mara's fate begins as Civilian.")

# ================================================================ CHAPTER: FRACTURE (C1L1 Night of the Fracture, C1L2 Under the Spire)
decision("dec_mentor", "Three people came running toward the light tonight, not away. Each of them offers to teach you. Only one can.", [
    option("kael", "Kael - the soldier. Ember. 'Hit it first.'", "Kael pulls you up by the wrist. His coat smells of smoke that isn't tonight's.",
           effects=[setf("mentor", "kael"), eff(AddAffinity, "ember", "", 20), eff(AddBond, "kael", "", 10), eff(AddItem, "mentor_token"),
                    eff(AddCodex, "c1_mentor_kael"), eff(UnlockAbility, "cinder_burst"), eff(SpawnEntity, "kael", "1"), eff(SpawnEntity, "odalys", "0"), eff(SpawnEntity, "bran", "0")]),
    option("odalys", "Sister Odalys - the healer. Tide. 'Breathe first.'", "Odalys presses a wet palm to your burned shoulder. The pain goes somewhere else.",
           effects=[setf("mentor", "odalys"), eff(AddAffinity, "tide", "", 20), eff(AddBond, "odalys", "", 10), eff(AddItem, "mentor_token"),
                    eff(AddCodex, "c1_mentor_odalys"), eff(UnlockAbility, "riptide"), eff(SpawnEntity, "odalys", "1"), eff(SpawnEntity, "kael", "0"), eff(SpawnEntity, "bran", "0")]),
    option("bran", "Warden Bran - the protector. Stone. 'Stand first.'", "Bran plants himself between you and the street. Nothing gets past.",
           effects=[setf("mentor", "bran"), eff(AddAffinity, "stone", "", 20), eff(AddBond, "bran", "", 10), eff(AddItem, "mentor_token"),
                    eff(AddCodex, "c1_mentor_bran"), eff(UnlockAbility, "tremor_stomp"), eff(SpawnEntity, "bran", "1"), eff(SpawnEntity, "kael", "0"), eff(SpawnEntity, "odalys", "0")])],
    codex="c1_mentor_choice")
scene("c1_fracture_open", "The Night", [("", "Vessa, six years later. The Spire splits open above the uptown and the light pours DOWN."),
                                        ("", "The street is running. Three people are running the wrong way - toward it, toward you."),
                                        ("Kael", "You're lit up like a beacon, kid. They'll come for you first. Hit them before they do."),
                                        ("Sister Odalys", "You're burning. Let me - breathe. Breathe with me."),
                                        ("Warden Bran", "Behind me. Nothing reaches you while I'm standing.")], "dec_mentor",
      {"kael": ("Kael", "Good. Now: the Choir's grunts swing twice. Dodge the second, hit the gap. Go."),
       "odalys": ("Sister Odalys", "Good. Now: they can't hurt what they can't reach. Move, mend, move again."),
       "bran": ("Warden Bran", "Good. Now: hold your ground and let them break on it. Then push.")})
scene("c1_mentor_lesson", "Mentor", [("@", "lesson")], "dec_mentor_advice",
      {"obey": ("", "Your mentor nods. The lesson sits right."), "defy": ("", "Your mentor says nothing. The silence is a lesson too.")},
      variants={"lesson": [
          (MENTOR("kael"), "Kael", "Combat tutorial, soldier style: ATTACK is a two-hit; the third one you earn. DODGE gives you a breath nobody can hit you in. Use your power when they bunch."),
          (MENTOR("odalys"), "Sister Odalys", "The lesson: DODGE is not retreat, it is timing. Strike, step, mend. Your power closes wounds and slows what chases."),
          (MENTOR("bran"), "Warden Bran", "The lesson: plant, strike, hold. DODGE through the swing, not away from it. Your power makes the ground remember you.")]})
decision("dec_mentor_advice", "Your mentor tells you how the Spire fight should go. Do you take the advice?", [
    option("obey", "Take it. They've done this before.", "", effects=[eff(AddBond, "kael", "", 8), eff(AddBond, "odalys", "", 8), eff(AddBond, "bran", "", 8), setf("mentor_obeyed"), eff(AddCodex, "c1_advice_taken")]),
    option("defy", "You'll do it your way.", "", effects=[eff(AddBond, "kael", "", -5), eff(AddBond, "odalys", "", -5), eff(AddBond, "bran", "", -5), setf("mentor_defied"), eff(AddAffinity, "hollow", "", 5)])])
decision("dec_dax_spar", "Dax is on one knee, Echo-light guttering off his hands. He came at you first. The street is watching.", [
    option("spare", "Offer him a hand up.", "Dax takes it - after a second that costs him. His grip is a challenge.",
           effects=[setf("dax_fate", "wary"), eff(AddBond, "dax", "", 12), eff(AddAffinity, "tide", "", 5), eff(AddCodex, "c1_dax_spared")]),
    option("press", "Put him down properly. He needs to learn.", "Dax doesn't get up for a while. When he does he doesn't look at you.",
           effects=[setf("dax_fate", "rival"), eff(AddBond, "dax", "", -25), eff(AddAffinity, "ember", "", 5), eff(AddAffinity, "hollow", "", 10), eff(AddCodex, "c1_dax_pressed")])],
    codex="c1_dax_spar")
scene("c1_dax_spar", "Dax", [("Dax", "Ari. Of course it's you. Of course the light picked YOU."),
                             ("Dax", "Let's see it, then. Let's see what it gave you."),
                             ("", "Dax comes in fast and wrong. It's over in nine seconds.")], "dec_dax_spar",
      {"spare": ("Dax", "...Next time I won't be tired."), "press": ("Dax", "...")})
scene("c1_dax_after", "Dax", [("@", "daxline")], variants={"daxline": [
    ([cond(DecisionWas, "dec_dax_spar", "spare")], "Dax", "Don't make this a thing. I'd have done the same. Probably."),
    ([cond(DecisionWas, "dec_dax_spar", "press")], "Dax", "Go under the Spire, hero. I hope it's hungry.")]})
world_interaction("street_barricade", "Drag the barricade across the street", visited("fracture_night"))
world_interaction("civilian_door", "Get the family through the door", visited("fracture_night"))
world_interaction("spire_lift", "Wrench the Spire lift open", visited("under_spire"))
objective("obj_fn_arenas", "Night of the Fracture", "The Choir is sweeping the street for the newly lit. Clear the three arenas between the pier road and the Spire gate.",
          MAIN, "fracture_night", visited("fracture_night"), [cond(VarAtLeast, "fn_kills", "", 4)],
          counterVar="fn_kills", counterTarget=4, counterText="Choir driven off",
          consequences=[setf("fn_arenas_cleared"), eff(GrantEchoes, "", "", 15), eff(SetWorldState, "market", "contested")],
          followUps=["obj_fn_civilians"], done="The street is clear. The Spire gate hangs open ahead.")
objective("obj_fn_civilians", "The family at the door", "A family is trapped behind the pharmacy shutter. Get them through before the next sweep.",
          SIDE, "fracture_night", [cond(ObjectiveActive, "obj_fn_arenas")], [flag("fn_family_saved")],
          consequences=[eff(AddReputation, "folk", "", 10), eff(AddAffinity, "tide", "", 5), eff(AddVar, "districts_saved", "", 1)],
          done="The family is through. Vessa will remember.")
objective("obj_us_descent", "Under the Spire", "Fight down through the Choir's cordon to the Fracture's root. Casters keep their distance; bruisers hold the stairs.",
          MAIN, "under_spire", visited("under_spire"), [flag("first_echo_defeated")],
          steps=[("Break the cordon (three Choir)", [cond(VarAtLeast, "us_kills", "", 3)]),
                 ("Defeat the First Echo", [flag("first_echo_defeated")])],
          consequences=[setf("c1_fracture_complete"), eff(GrantEchoes, "", "", 20), eff(AddCodex, "c1_under_spire"), eff(AddBond, "kael", "", 6), eff(AddBond, "odalys", "", 6), eff(AddBond, "bran", "", 6),
                        eff(MoveNpc, "kael", "interlude_camp"), eff(MoveNpc, "odalys", "interlude_camp"), eff(MoveNpc, "bran", "interlude_camp")],
          done="The First Echo comes apart into light. Something under the Spire has learned your name.")
scene("c1_first_echo_intro", "The First Echo", [("", "At the Spire's root the light has pooled into a shape - almost a person, never finished."),
                                                ("The First Echo", "...first... you were... first..."),
                                                ("", "It teaches the dodge the hard way. Watch the lash; step through it.")])
scene("c1_first_echo_fallen", "The First Echo", [("", "The First Echo comes apart in slow ribbons. Where it stood, the floor is warm."),
                                                 ("@", "mentorword"), ("", "Above you, very far up, something in the Spire begins - quietly - to sing.")],
      variants={"mentorword": [(MENTOR("kael"), "Kael", "That's how it's done. Hit it first. Every time."),
                               (MENTOR("odalys"), "Sister Odalys", "You're shaking. Good - it means you were there for all of it. Breathe."),
                               (MENTOR("bran"), "Warden Bran", "You held. Whatever comes next - you held here first.")]})
chapter("ch_fracture", "Fracture", "The Night the Light Came",
        "Sixteen. The Fracture opens over Vessa; the Choir comes for the newly lit; a mentor chooses you back. Boss: the First Echo.",
        [flag("ch1_complete")],
        [beat("beat_fr_mentor", "Choose who teaches you", "Three ran toward the light. One of them is yours now.", T_DEC, "dec_mentor"),
         beat("beat_fr_street", "Clear the street", "The Choir swept the pier road for the newly lit. They found you.", T_OBJ_DONE, "obj_fn_arenas", req=["beat_fr_mentor"]),
         beat("beat_fr_dax", "Dax", "Dax came at you first. The street saw what you did after.", T_DEC, "dec_dax_spar", req=["beat_fr_mentor"]),
         beat("beat_fr_echo", "The First Echo", "Under the Spire the light had made a shape. You unmade it.", T_OBJ_DONE, "obj_us_descent", req=["beat_fr_street"],
              effects=[eff(GrantEchoes, "", "", 10)], priority=5)],
        [branch("br_fr_kael", "beat_fr_mentor", "beat_fr_street", "Mentor: Kael (Ember)", MENTOR("kael"), [setf("mentor_fate", "alive")]),
         branch("br_fr_odalys", "beat_fr_mentor", "beat_fr_street", "Mentor: Sister Odalys (Tide)", MENTOR("odalys"), [setf("mentor_fate", "alive")]),
         branch("br_fr_bran", "beat_fr_mentor", "beat_fr_street", "Mentor: Warden Bran (Stone)", MENTOR("bran"), [setf("mentor_fate", "alive")]),
         branch("br_fr_dax_spared", "beat_fr_dax", "beat_fr_echo", "Dax spared", [cond(DecisionWas, "dec_dax_spar", "spare")], [setf("dax_alive")]),
         branch("br_fr_dax_pressed", "beat_fr_dax", "beat_fr_echo", "Dax pressed", [cond(DecisionWas, "dec_dax_spar", "press")], [setf("dax_alive")])],
        [flag("c1_fracture_complete")], [setf("c2_open"), eff(AddCodex, "c1_fracture_ch_complete")],
        "Fracture complete: the First Echo is unmade, a mentor sworn, Dax remembered. Four years pass.")

# ================================================================ CHAPTER: BECOMING (I2, C2A/B/C, C2X)
scene("i2_archivist", "The Archivist", [("The Archivist", "Ari. Twenty years old, four since the Fracture. I have kept the count: your echoes, your codex, your debts."),
                                        ("@", "recap"),
                                        ("The Archivist", "The Choir holds three places in Vessa. You may take back one. Choose the shape of your becoming.")],
      "dec_c2_path",
      {"docks": ("The Archivist", "The Docks. An assault. The city will learn what your fire costs."),
       "sanctuary": ("The Archivist", "The Sanctuary. A defence. Hold what the Choir wants to drown."),
       "long_wall": ("The Archivist", "The Long Wall. A line. Stand where the city ends and the Choir begins.")},
      variants={"recap": [(MENTOR("kael"), "The Archivist", "Kael taught you to hit first. Your Ember runs hot; the Choir marks you as a threat, not a harvest."),
                          (MENTOR("odalys"), "The Archivist", "Odalys taught you to breathe first. Your Tide runs deep; the relief lines say your name like a prayer."),
                          (MENTOR("bran"), "The Archivist", "Bran taught you to stand first. Your Stone runs steady; the Wardens count you as one of their own.")]})
decision("dec_c2_path", "Three places. One becoming. Which do you take back from the Choir?", [
    option("docks", "The Contested Docks - assault. (Ember framing)", "",
           effects=[setf("c2_path", "docks"), setf("c2_path_open"), eff(AddAffinity, "ember", "", 10), eff(AddCodex, "c2_path_docks")]),
    option("sanctuary", "The Sanctuary - defend the flooded shrine. (Tide framing)", "",
           effects=[setf("c2_path", "sanctuary"), setf("c2_path_open"), eff(AddAffinity, "tide", "", 10), eff(AddCodex, "c2_path_sanctuary")]),
    option("long_wall", "The Long Wall - hold the line. (Stone framing)", "",
           effects=[setf("c2_path", "long_wall"), setf("c2_path_open"), eff(AddAffinity, "stone", "", 10), eff(AddCodex, "c2_path_wall")])],
    codex="c2_becoming")
scene("c2_mentor_interlude", "Mentor", [("@", "c2m")], variants={"c2m": [
    ([flag("mentor_obeyed")] + MENTOR("kael"), "Kael", "You listened under the Spire. Keep listening: the Docks fall to whoever is willing to burn the pier. Are you?"),
    (MENTOR("kael"), "Kael", "You didn't listen under the Spire. It worked. It won't always. The Docks are yours if you want them - my advice, or not."),
    ([flag("mentor_obeyed")] + MENTOR("odalys"), "Sister Odalys", "You listened under the Spire. The Sanctuary holds the last clean water in the Reaches. If it falls, so do the relief lines."),
    (MENTOR("odalys"), "Sister Odalys", "You went your own way under the Spire - and you're alive. Good. The Sanctuary needs someone who knows when not to listen."),
    ([flag("mentor_obeyed")] + MENTOR("bran"), "Warden Bran", "You held under the Spire like I told you. The Long Wall is longer than any of us. Hold it anyway."),
    (MENTOR("bran"), "Warden Bran", "You didn't hold the way I said. You held your way. The Wall doesn't care whose way - only that it's held.")]})
# --- C2A Contested Docks (assault; Ember framing) ---
scene("c2_docks_open", "The Docks", [("", "The Contested Docks: cranes stopped mid-lift, Choir graffiti on every hull, the song coming off the water in waves."),
                                     ("", "Four arenas between the gate and the harbour master's tower. The Elite is in the tower.")])
decision("dec_docks_fire", "The last Choir squad has barricaded inside the fuel shed. Burning it clears the docks in a breath - and the docks with it.", [
    option("burn", "Burn it. The docks were theirs the moment they took them.", "The shed goes up. So does half the pier. The song stops.",
           effects=[eff(AddAffinity, "ember", "", 15), eff(AddAffinity, "hollow", "", 5), eff(AddReputation, "folk", "", -10), eff(AddReputation, "choir", "", -15),
                    setf("docks_burned"), eff(SetWorldState, "docks", "working"), eff(SpawnEntity, "docks_fire", "1")]),
    option("breach", "Breach it. Fight them in the doorway.", "It takes longer and it costs more. The docks are still standing when it's done.",
           effects=[eff(AddAffinity, "ember", "", 5), eff(AddAffinity, "stone", "", 5), eff(AddReputation, "folk", "", 10), eff(AddVar, "districts_saved", "", 1),
                    setf("docks_held"), eff(SetWorldState, "docks", "working")])],
    codex="c2_docks_fire")
scene("c2_docks_shed", "The Docks", [("", "The fuel shed door is shut from the inside. You can hear them singing behind it.")], "dec_docks_fire",
      {"burn": ("", "Kael would be proud. You are not sure you are."), "breach": ("", "The doorway is narrow enough for one at a time. It is enough.")})
objective("obj_docks_assault", "Take the Docks", "Fight through four Choir arenas to the harbour tower and put down the Elite.",
          MAIN, "docks", [flag("c2_path", "docks")] + visited("docks"), [flag("c2_elite_down"), cond(DecisionWas, "dec_docks_fire")],
          counterVar="c2_kills", counterTarget=8, counterText="Choir driven off",
          steps=[("Clear the four arenas", [cond(VarAtLeast, "c2_kills", "", 8)]), ("Defeat the Choir Elite", [flag("c2_elite_down")]),
                 ("Decide the fuel shed", [cond(DecisionWas, "dec_docks_fire")])],
          consequences=[setf("c2_path_done"), eff(UnlockAbility, "phoenix_reckoning"), eff(AddAffinity, "ember", "", 15), eff(GrantEchoes, "", "", 30),
                        eff(SetWorldState, "market", "contested"), eff(AddCodex, "c2_docks_taken"), eff(AddBond, "kael", "", 5), eff(AddBond, "odalys", "", 5), eff(AddBond, "bran", "", 5)],
          done="The Docks are yours. Phoenix Reckoning wakes in your hands.")
# --- C2B The Sanctuary (defence; Tide framing) ---
scene("c2_sanctuary_open", "The Sanctuary", [("", "The Sanctuary: a flooded shrine in the Reaches, the last clean water, forty people on the upper steps."),
                                             ("", "The Choir will come in four waves to drown it. Hold the steps. If the water reaches the altar three times, it is lost.")])
world_interaction("sanctuary_sluice", "Close the sluice", [flag("c2_path", "sanctuary")])
objective("obj_sanctuary_hold", "Hold the Sanctuary", "Four waves. Keep the Choir off the altar steps; close the sluices between waves.",
          CRISIS, "sanctuary", [flag("c2_path", "sanctuary")] + visited("sanctuary"), [flag("c2_elite_down"), cond(VarAtLeast, "c2_kills", "", 8)],
          fail=[cond(VarAtLeast, "sanctuary_breaches", "", 3)], counterVar="c2_kills", counterTarget=8, counterText="Waves broken",
          consequences=[setf("c2_path_done"), eff(UnlockAbility, "call_ally"), eff(AddAffinity, "tide", "", 15), eff(GrantEchoes, "", "", 30),
                        eff(AddVar, "districts_saved", "", 1), eff(SetWorldState, "docks", "flooded"), eff(SetWorldState, "market", "rebuilt"),
                        eff(AddReputation, "folk", "", 15), eff(AddCodex, "c2_sanctuary_held"), eff(AddBond, "kael", "", 5), eff(AddBond, "odalys", "", 5), eff(AddBond, "bran", "", 5)],
          failureConsequences=[eff(SetWorldState, "market", "ruined"), eff(SetWorldState, "docks", "flooded"), eff(AddReputation, "folk", "", -10), eff(AddAffinity, "hollow", "", 5)],
          followUps=["obj_sanctuary_recover"], done="The water holds at the third step. Call Ally answers when you reach for it.",
          failed="The altar goes under. The Sanctuary is lost - but not the people on the steps, if you move now.")
objective("obj_sanctuary_recover", "Carry them out", "The Sanctuary is drowned. Get the people on the steps to the causeway.",
          RECOVERY, "sanctuary", [cond(ObjectiveFailed, "obj_sanctuary_hold")], [flag("c2_elite_down")],
          consequences=[setf("c2_path_done"), eff(UnlockAbility, "call_ally"), eff(AddAffinity, "tide", "", 10), eff(GrantEchoes, "", "", 15), eff(AddCodex, "c2_sanctuary_lost")],
          done="Forty people on the causeway, coughing. Call Ally wakes anyway - they called first.")
# --- C2C The Long Wall (hold; Stone framing) ---
scene("c2_wall_open", "The Long Wall", [("", "The Long Wall: the old sea wall where the Outskirts end. The Wardens hold it with nine people. Now ten."),
                                        ("", "Four pushes are coming. If the Choir gets through the gate three times, the Outskirts are theirs.")])
world_interaction("wall_gate_brace", "Brace the gate", [flag("c2_path", "long_wall")])
objective("obj_wall_hold", "Hold the Long Wall", "Four pushes. Keep the gate; brace it between pushes; break the Elite when it comes.",
          CRISIS, "long_wall", [flag("c2_path", "long_wall")] + visited("long_wall"), [flag("c2_elite_down"), cond(VarAtLeast, "c2_kills", "", 8)],
          fail=[cond(VarAtLeast, "wall_breaches", "", 3)], counterVar="c2_kills", counterTarget=8, counterText="Pushes broken",
          consequences=[setf("c2_path_done"), eff(UnlockAbility, "bulwark"), eff(AddAffinity, "stone", "", 15), eff(GrantEchoes, "", "", 30),
                        eff(AddVar, "districts_saved", "", 1), eff(SetWorldState, "docks", "fortified"), eff(SetWorldState, "market", "rebuilt"),
                        eff(AddReputation, "wards", "", 15), eff(AddCodex, "c2_wall_held"), eff(AddBond, "kael", "", 5), eff(AddBond, "odalys", "", 5), eff(AddBond, "bran", "", 5)],
          failureConsequences=[eff(SetWorldState, "market", "ruined"), eff(SetWorldState, "docks", "fortified"), eff(AddReputation, "wards", "", -10), eff(AddAffinity, "hollow", "", 5)],
          followUps=["obj_wall_recover"], done="The gate holds on the fourth push. Bulwark settles into your stance.",
          failed="The gate comes down. The Outskirts burn - pull the Wardens back to the second wall.")
objective("obj_wall_recover", "The second wall", "The gate is gone. Fall back with the Wardens and hold the second line until the Elite falls.",
          RECOVERY, "long_wall", [cond(ObjectiveFailed, "obj_wall_hold")], [flag("c2_elite_down")],
          consequences=[setf("c2_path_done"), eff(UnlockAbility, "bulwark"), eff(AddAffinity, "stone", "", 10), eff(GrantEchoes, "", "", 15), eff(AddCodex, "c2_wall_lost")],
          done="The second wall holds. Bulwark wakes - late, and heavy.")
# --- the D2 pressure choice: save Mara vs pursue the Choir (timed; timeout = hesitate) ---
decision("dec_save_mara", "The Elite is down but the Choir runner has the Cantor's ledger - and the crane cable above Mara just snapped. Five seconds.", [
    option("save", "MARA.", "You hit her at a run. The crane block lands where she was standing.",
           effects=[setf("mara_saved"), eff(AddBond, "mara", "", 20), eff(AddAffinity, "tide", "", 10), eff(AddReputation, "choir", "", 5), eff(AddCodex, "c2_mara_saved"), eff(SpawnEntity, "mara_crane", "0")]),
    option("pursue", "The ledger. Mara can move.", "You get the ledger. You hear the crane block land behind you. You do not hear Mara.",
           effects=[setf("mara_fate", "lost"), setf("mara_alive", "0"), eff(AddBond, "mara", "", -60), eff(AddAffinity, "hollow", "", 15), eff(AddAffinity, "ember", "", 5),
                    eff(AddItem, "cantor_voice"), eff(AddReputation, "choir", "", -15), eff(AddCodex, "c2_mara_lost"), eff(SpawnEntity, "mara_c2", "0"), eff(SpawnEntity, "mara_c3", "0"), eff(SpawnEntity, "mara_crane", "0")]),
    option("hesitate", "...", "You stand between the two for one second too long. Mara gets herself half clear. Half.",
           effects=[eff(AddBond, "mara", "", -10), eff(AddAffinity, "hollow", "", 5), setf("mara_hurt"), eff(AddCodex, "c2_mara_hesitated"), eff(SpawnEntity, "mara_crane", "0")])],
    timeLimit=6, timeoutIdx=2, codex="c2_pressure")
scene("c2_mara_pressure", "Mara", [("Mara", "Ari - the runner's got the ledger, it's going for the tower - I've got the-"),
                                   ("", "The crane cable above her parts with a sound like a bell.")], "dec_save_mara",
      {"save": ("Mara", "...You're heavy. Get OFF. ...Thanks."),
       "pursue": ("", "The ledger is warm in your hands. The song in it knows your name, and now it knows hers."),
       "hesitate": ("Mara", "I'm fine. I'm - it's just my arm. Go. GO.")})
scene("c2_mara_talk", "Mara", [("@", "c2mt")], variants={"c2mt": [
    ([cond(BondAtLeast, "mara", "", 50)], "Mara", "Kite string's still on my wrist. Ten years. Don't say anything."),
    ([cond(BondAtLeast, "mara", "", 1)], "Mara", "Relief lines run at dawn. If you're still standing after this, you could carry a crate. For once."),
    ([], "Mara", "You don't have to talk to me, you know. I'm just here.")]})
scene("c2_mara_after", "Mara", [("@", "c2ma")], variants={"c2ma": [
    ([flag("mara_fate", "ally")], "Mara", "I'm not staying behind again. Wherever you go from here - I'm coming, and I'm fighting."),
    ([flag("mara_saved")], "Mara", "You picked me. Over the ledger. I'm going to remember that longer than you will."),
    ([flag("mara_hurt")], "Mara", "It's a sling, not a shroud. Stop looking at it like that.")]})
# --- C2X Dax Confrontation: duel (bond low) OR team-up (bond high) ---
decision("dec_dax_confront", "Dax is waiting in the amphitheatre at the end of the Reaches - and so, on the ridge behind him, is a Choir Hunter.", [
    option("truce", "'They're here for both of us. Stand with me.'", "Dax looks at the ridge, then at you. He takes his glasses off and folds them into his pocket.",
           conditions=[cond(BondAtLeast, "dax", "", 1)],
           effects=[setf("dax_fate", "truce"), eff(AddBond, "dax", "", 10), eff(AddAffinity, "tide", "", 5), eff(AddCodex, "c2_dax_truce")]),
    option("duel", "'You wanted this since the pier. Come on, then.'", "Dax smiles like it hurts. The Hunter on the ridge sits down to watch.",
           effects=[setf("dax_duel"), setf("dax_fate", "rival"), eff(AddAffinity, "ember", "", 5), eff(AddCodex, "c2_dax_duel")])],
    codex="c2_dax_confront")
scene("c2_dax_confront", "Dax", [("Dax", "Ari. Twenty years old and still the one the light watches."),
                                 ("Dax", "I've been awake four years too. Nobody ran toward ME that night. Nobody taught me. I taught myself."),
                                 ("", "On the ridge behind him, white plate catches the light. A Choir Hunter. It is here for both of you.")], "dec_dax_confront",
      {"truce": ("Dax", "Don't get in my way. And don't die. I want to beat you myself, later."),
       "duel": ("Dax", "Good. GOOD.")})
decision("dec_dax_duel_end", "Dax is down. His Echo is guttering out of him in threads you could catch. The Hunter on the ridge is standing up.", [
    option("finish", "Finish it. The Choir would have.", "Dax's light goes out between your hands. The Hunter on the ridge nods, once, like a colleague.",
           effects=[setf("dax_alive", "0"), setf("dax_gone"), setf("dax_fate", "dead"), setf("dax_resolved"), eff(AddAffinity, "ember", "", 10), eff(AddAffinity, "hollow", "", 15),
                    eff(AddReputation, "folk", "", -10), eff(AddCodex, "c2_dax_finished"), eff(SpawnEntity, "dax_rival", "0"), eff(SpawnEntity, "dax_down", "0")]),
    # NOTE effect order matters: the objective/campaign cascade fires on the dax_resolved flag, so every
    # flag/bond the branch conditions read is written BEFORE it (the decision record itself lands after effects).
    option("yield", "Lower your hands. Let him go.", "Dax gets up slowly. He does not thank you. He does not attack you either.",
           effects=[setf("dax_yielded"), eff(AddBond, "dax", "", 15), eff(AddAffinity, "stone", "", 5), eff(AddAffinity, "tide", "", 5), eff(AddCodex, "c2_dax_yielded"),
                    eff(SpawnEntity, "dax_down", "0"), eff(SpawnEntity, "dax_yielded", "1"), setf("dax_resolved")]),
    option("absorb", "Take his Echo. It was never his to keep.", "The threads come to you like they were always yours. Dax is empty. Something in you is very, very full.",
           conditions=[cond(AffinityAtLeast, "hollow", "", 25)],
           effects=[setf("dax_alive", "0"), setf("dax_gone"), setf("dax_fate", "dead"), setf("dax_resolved"), setf("hollow_path"), eff(AddAffinity, "hollow", "", 20),
                    eff(AddItem, "dax_echo"), eff(UnlockAbility, "hollow_throne"), eff(GrantEchoes, "", "", 40), eff(AddCodex, "c2_dax_absorbed"), eff(SpawnEntity, "dax_rival", "0"), eff(SpawnEntity, "dax_down", "0")])],
    codex="c2_dax_duel_end")
scene("c2_dax_duel_end", "Dax", [("Dax", "...told you. Not tired this time. Just... not enough."),
                                 ("", "The Hunter on the ridge is standing up. Whatever you do, do it now.")], "dec_dax_duel_end",
      {"finish": ("", "You walk out of the amphitheatre alone."),
       "yield": ("Dax", "Next time. There's always a next time with you."),
       "absorb": ("", "You walk out of the amphitheatre. You are not alone. You will never be alone again.")})
scene("c2_dax_hunter_fallen", "Dax", [("", "The Hunter comes apart in white plate and song. Dax is on one knee beside it, laughing, bleeding."),
                                      ("Dax", "We are NEVER telling anyone that worked."), ("Dax", "...Ari. Thanks. Don't make it a thing.")])
objective("obj_dax", "Dax", "Dax is waiting at the amphitheatre - and so is the Choir. Duel him, or stand with him.",
          MAIN, "dax_arena", visited("dax_arena"), [flag("dax_resolved")],
          steps=[("Meet Dax", [cond(DecisionWas, "dec_dax_confront")]), ("Settle it", [flag("dax_resolved")])],
          consequences=[setf("c2_complete"), eff(GrantEchoes, "", "", 20), eff(AddCodex, "c2_dax_resolved"), eff(MoveNpc, "archivist", "reckoning_camp")],
          done="It is settled, one way or another. Six years pass.")
chapter("ch_becoming", "Becoming", "Three Paths, One Rival",
        "Twenty. The Archivist offers three places to take back; a pressure choice decides Mara; Dax waits at the amphitheatre.",
        [flag("c2_open")],
        [beat("beat_bc_path", "Choose your becoming", "The Archivist offered three places. You chose one.", T_DEC, "dec_c2_path"),
         beat("beat_bc_docks", "The Contested Docks", "You took the Docks back - by fire or by the doorway.", T_OBJ_DONE, "obj_docks_assault", offer=[flag("c2_path", "docks")], req=["beat_bc_path"]),
         beat("beat_bc_sanctuary", "The Sanctuary", "You held the Sanctuary steps through four waves.", T_OBJ_DONE, "obj_sanctuary_hold", offer=[flag("c2_path", "sanctuary")], req=["beat_bc_path"]),
         beat("beat_bc_sanctuary_lost", "The Sanctuary, drowned", "The altar went under. You carried the steps out.", T_OBJ_DONE, "obj_sanctuary_recover", offer=[flag("c2_path", "sanctuary")], req=["beat_bc_path"]),
         beat("beat_bc_wall", "The Long Wall", "The gate held on the fourth push.", T_OBJ_DONE, "obj_wall_hold", offer=[flag("c2_path", "long_wall")], req=["beat_bc_path"]),
         beat("beat_bc_wall_lost", "The second wall", "The gate fell. The second wall did not.", T_OBJ_DONE, "obj_wall_recover", offer=[flag("c2_path", "long_wall")], req=["beat_bc_path"]),
         beat("beat_bc_mara", "Five seconds", "A crane cable, a ledger, five seconds. You chose.", T_DEC, "dec_save_mara", req=["beat_bc_path"], priority=2),
         beat("beat_bc_dax", "Dax", "Dax waited at the amphitheatre. It is settled.", T_OBJ_DONE, "obj_dax", req=["beat_bc_mara"], priority=3)],
        [branch("br_bc_docks", "beat_bc_path", "beat_bc_docks", "Path of Assault", [flag("c2_path", "docks")]),
         branch("br_bc_sanctuary", "beat_bc_path", "beat_bc_sanctuary", "Path of Defence", [flag("c2_path", "sanctuary")]),
         branch("br_bc_wall", "beat_bc_path", "beat_bc_wall", "Path of the Line", [flag("c2_path", "long_wall")]),
         branch("br_bc_mara_ally", "beat_bc_mara", "beat_bc_dax", "Mara: Ally", [flag("mara_saved"), cond(BondAtLeast, "mara", "", 40)], [setf("mara_fate", "ally")]),
         branch("br_bc_mara_civ", "beat_bc_mara", "beat_bc_dax", "Mara: Civilian", [flag("mara_saved")], [setf("mara_fate", "civilian")]),
         branch("br_bc_mara_lost", "beat_bc_mara", "beat_bc_dax", "Mara: Lost", [flag("mara_fate", "lost")]),
         branch("br_bc_mara_hurt", "beat_bc_mara", "beat_bc_dax", "Mara: hurt, and civilian", [flag("mara_hurt")], [setf("mara_fate", "civilian")]),
         branch("br_bc_dax_redeemed", "beat_bc_dax", "", "Dax: Redeemed", [flag("dax_yielded"), cond(BondAtLeast, "dax", "", 1)], [setf("dax_fate", "redeemed")]),
         branch("br_bc_dax_final", "beat_bc_dax", "", "Dax: Final Enemy", [flag("dax_yielded")], [setf("dax_fate", "final_enemy")]),
         branch("br_bc_dax_truce", "beat_bc_dax", "", "Dax: Truce", [flag("dax_fate", "truce")]),
         branch("br_bc_dax_dead", "beat_bc_dax", "", "Dax: gone", [flag("dax_fate", "dead")])],
        [flag("c2_complete")], [eff(AddCodex, "c2_becoming_complete")],
        "Becoming complete: a path taken, Mara's fate set, Dax settled. Six years pass.")

# ================================================================ CHAPTER: RECKONING (I3, C3L1 Market, C3L2 Ascent, C3B Choirmaster) + EPILOGUE
scene("i3_archivist", "The Archivist", [("The Archivist", "Twenty-six. Ten years since the light. I have kept the count. Look at what Vessa became while you were becoming."),
                                        ("@", "market"), ("@", "docks"), ("@", "mara"), ("@", "daxword"),
                                        ("The Archivist", "The Spire is breached. The Choirmaster is at the Fracture's heart. Whatever you are - go and be it.")],
      variants={"market": [([cond(WorldStateIs, "market", "rebuilt")], "The Archivist", "The Old Market is rebuilt. Stalls, lanterns, the smell of bread at dawn. They say your name there."),
                           ([cond(WorldStateIs, "market", "ruined")], "The Archivist", "The Old Market is ruined. Nobody trades there now. The Choir's graffiti has faded; nothing replaced it."),
                           ([cond(WorldStateIs, "market", "contested")], "The Archivist", "The Old Market is contested still - Choir patrols by night, stallholders by day, and neither yields.")],
                "docks": [([cond(WorldStateIs, "docks", "flooded")], "The Archivist", "The Docks are a flooded sanctum now; the relief lines row between the cranes."),
                          ([cond(WorldStateIs, "docks", "fortified")], "The Archivist", "The Docks are a fortified wall; the Wardens hold them the way you held the Long Wall."),
                          ([cond(WorldStateIs, "docks", "working")], "The Archivist", "The Docks work again - cranes lifting, hulls scraped clean of the Choir's marks.")],
                "mara": [([flag("mara_fate", "lost")], "The Archivist", "Mara is gone. The kite string is in your pocket. I counted it, once, among your items; I have stopped."),
                         ([flag("mara_fate", "ally")], "The Archivist", "Mara is at the barricade line with her wraps on. She intends to climb the Spire with you."),
                         ([], "The Archivist", "Mara is alive. She runs a relief line. She still comes to the pier.")],
                "daxword": [([flag("dax_fate", "dead")], "The Archivist", "Dax is dead. The amphitheatre is a memorial nobody visits."),
                            ([flag("dax_fate", "redeemed")], "The Archivist", "Dax is here. He asked me, quietly, whether you would want him. I did not know what to say."),
                            ([flag("dax_fate", "truce")], "The Archivist", "Dax holds the Reaches with a crew of the newly lit. He does not say your name; he does not need to."),
                            ([flag("dax_fate", "final_enemy")], "The Archivist", "Dax has gone up the Spire ahead of you. He is singing."),
                            ([], "The Archivist", "Dax is somewhere in the city. He always is.")]})
decision("dec_i3_shrine", "The reckoning shrine. It will deepen any echo you carry - once - for ten of your own.", [
    option("deep_line", "Deepen your mentor's gift.", "The plinth drinks and gives back, doubled.",
           conditions=[cond(EchoesAtLeast, "echo", "", 10)],
           effects=[eff(UpgradeAbility, "cinder_burst", "", 1), eff(UpgradeAbility, "riptide", "", 1), eff(UpgradeAbility, "tremor_stomp", "", 1),
                    eff(GrantEchoes, "", "", -10), eff(AddSkillLevel, "echo_attunement", "", 1), eff(AddCodex, "c3_shrine_deep")]),
    option("deep_capstone", "Deepen the capstone.", "The plinth drinks and gives back, doubled.",
           conditions=[cond(EchoesAtLeast, "echo", "", 10)],
           effects=[eff(UpgradeAbility, "phoenix_reckoning", "", 1), eff(UpgradeAbility, "call_ally", "", 1), eff(UpgradeAbility, "bulwark", "", 1),
                    eff(GrantEchoes, "", "", -10), eff(AddCodex, "c3_shrine_capstone")]),
    option("leave", "Leave it.", "The plinth dims, patient.")],
    codex="c3_shrine")
scene("i3_shrine", "Echo Shrine", [("Echo Shrine", "The reckoning shrine hums at the frequency of ten years.")], "dec_i3_shrine", {})
decision("dec_hollow_shrine", "A second plinth, dark where the other is bright. It is only here because of what you have let in. It offers a drink.", [
    option("drink", "Drink.", "It goes down like cold and comes up like hunger. Drain Touch wakes in your hands.",
           conditions=[cond(AffinityAtLeast, "hollow", "", 25)],
           effects=[eff(UnlockAbility, "drain_touch"), setf("hollow_path"), eff(AddAffinity, "hollow", "", 10), eff(AddCodex, "c3_hollow_drunk"), eff(SetWorldState, "spire", "collapsed")]),
    option("refuse", "Refuse.", "The dark plinth waits. It is very good at waiting.",
           effects=[eff(AddAffinity, "stone", "", 5), eff(AddCodex, "c3_hollow_refused")])],
    codex="c3_hollow_shrine")
scene("i3_hollow_shrine", "The Dark Plinth", [("", "The dark plinth has no hum. It has a pull.")], "dec_hollow_shrine", {})
# --- C3L1 The Old Market (variant by world state; Cantor mid-boss) ---
scene("c3_market_open", "The Old Market", [("@", "mk")], variants={"mk": [
    ([cond(WorldStateIs, "market", "rebuilt")], "", "The rebuilt market, and the Choir has come back to burn it. Husks in the lantern light. The Cantor is singing from the fountain."),
    ([cond(WorldStateIs, "market", "ruined")], "", "The ruined market. Nothing to save but the way through. The Cantor is singing from the dry fountain."),
    ([], "", "The contested market. Stallholders behind shutters, husks in the aisles. The Cantor is singing from the fountain.")]})
objective("obj_market", "The Old Market", "Cut through the husks to the fountain and silence the Cantor.",
          MAIN, "market", visited("market"), [flag("cantor_defeated")],
          steps=[("Break the husks (three)", [cond(VarAtLeast, "c3_kills", "", 3)]), ("Silence the Cantor", [flag("cantor_defeated")])],
          consequences=[setf("market_cleared"), eff(GrantEchoes, "", "", 20), eff(SetWorldState, "spire", "breached")],
          done="The Cantor's voice is in your hand. The Spire is breached above you.")
# --- C3L2 Ascent of the Spire (gravity anomalies; spire wardens) ---
world_interaction("anomaly_anchor", "Anchor the gravity anomaly", visited("spire_ascent"))
objective("obj_ascent", "Ascent of the Spire", "Climb the breached Spire through the gravity anomalies. Anchor two anomalies and break the wardens on the tilted floors.",
          MAIN, "spire_ascent", visited("spire_ascent"), [cond(VarAtLeast, "anomaly_count", "", 2), cond(VarAtLeast, "ascent_kills", "", 2)],
          counterVar="anomaly_count", counterTarget=2, counterText="Anomalies anchored",
          steps=[("Anchor two anomalies", [cond(VarAtLeast, "anomaly_count", "", 2)]), ("Break two Spire Wardens", [cond(VarAtLeast, "ascent_kills", "", 2)])],
          consequences=[setf("ascent_done"), eff(GrantEchoes, "", "", 20), eff(AddCodex, "c3_ascent")],
          done="The floors lie flat again. The Choirmaster's overture starts above.")
scene("c3_ascent_open", "The Spire", [("", "The Spire, breached. Floors tilt; dust falls upward; the Choir's song holds the gravity where it wants it."),
                                      ("@", "hollowspire")],
      variants={"hollowspire": [([cond(AffinityAtLeast, "hollow", "", 25)], "", "Where you walk, the anomalies bend toward you. The Spire is collapsing, and it is collapsing your way."),
                                ([], "", "Anchor the anomalies. Break the wardens. Climb.")]})
# --- C3B The Choirmaster: three phases, refusal offered at the phase transition ---
scene("c3_cm_open", "The Choirmaster", [("The Choirmaster", "Ari. First-lit. I have sung your name for ten years; you have finally come to hear it."),
                                        ("The Choirmaster", "Everything the Fracture gave, I will gather. Every echo. Yours last.")])
decision("dec_cm_transition", "The Choirmaster falters. The song opens - and in the gap, a door. You could walk out of this. Or press.", [
    option("press", "Press. Finish the song.", "The door closes. The Choirmaster unmasks.", effects=[setf("cm_press"), eff(SpawnEntity, "cm_door", "0"), eff(AddCodex, "c3_pressed")]),
    option("mentor_shield", "Let your mentor hold the Choirmaster while you strike.", "Your mentor steps in without a word. The song takes them. You do not miss.",
           conditions=[flag("mentor_fate", "alive")],
           effects=[setf("cm_press"), setf("mentor_fate", "fallen"), eff(SpawnEntity, "cm_door", "0"), eff(AddAffinity, "hollow", "", 5), eff(AddVar, "cm_advantage", "", 1), eff(AddCodex, "c3_mentor_fallen")]),
    option("refuse", "Walk out. Refuse the call.", "The door is a door. You walk through it. Behind you, the song goes on without you.",
           effects=[setf("cm_refused"), setf("campaign_ended"), setf("ending", "long_way_home"), eff(SpawnEntity, "choirmaster_p1", "0"),
                    eff(SpawnEntity, "choirmaster_p2", "0"), eff(SpawnEntity, "choirmaster_p3", "0"), eff(SpawnEntity, "cm_door", "0"), eff(AddCodex, "c3_refused")])],
    codex="c3_transition")
scene("c3_cm_transition", "The Choirmaster", [("", "The overture breaks. In the silence you can hear the sea."), ("@", "cmt")], "dec_cm_transition",
      {"press": ("The Choirmaster", "GOOD. Then hear the chorus."), "mentor_shield": ("", "Your mentor's last word is your name."), "refuse": ("", "The sea is very loud.")},
      variants={"cmt": [([flag("mentor_fate", "alive")] + MENTOR("kael"), "Kael", "Kid. Say the word and I'll hold it. You hit first - like I taught you."),
                        ([flag("mentor_fate", "alive")] + MENTOR("odalys"), "Sister Odalys", "Breathe. If you need me to stand in front - I have stood in front before."),
                        ([flag("mentor_fate", "alive")] + MENTOR("bran"), "Warden Bran", "I'll hold it. That's what I'm for. Your call."),
                        ([], "The Choirmaster", "No one comes for you this time, first-lit. Press, or go.")]})
scene("c3_cm_phase2", "The Choirmaster, Unmasked", [("@", "p2")], variants={"p2": [
    ([flag("dax_fate", "final_enemy")], "Dax, Final Enemy", "Ari. I sing now. Turns out I was always going to."),
    ([flag("mara_fate", "turned")], "Mara, Turned", "You left me under the crane. I got up anyway. Look what got up with me."),
    ([flag("mara_fate", "ally")], "Mara", "Left side's mine. Don't you DARE die in front of me."),
    ([flag("dax_fate", "redeemed")], "Dax", "Right side. And Ari - we're telling everyone this one worked."),
    ([], "The Choirmaster, Unmasked", "You came alone. Then hear it alone.")]})
scene("c3_cm_phase3", "The Choirmaster, Ascendant", [("@", "p3")], variants={"p3": [
    ([flag("dominant", "ember")], "", "Phase three: a race. The Choirmaster's song is gathering the Fracture; burn faster than it can sing."),
    ([flag("dominant", "tide")], "", "Phase three: a ward. The song is reaching for the people on the lower floors; keep it off them until it breaks."),
    ([flag("dominant", "stone")], "", "Phase three: attrition. The song cannot be outrun; it can be outlasted. Stand."),
    ([flag("dominant", "hollow")], "", "Phase three: a window. When the song thins, everything already breaking will break. Wait for it. Take it."),
    ([], "", "Phase three: everything at once. You were never one thing. Neither is this.")]})
objective("obj_choirmaster", "The Choirmaster", "The final fight, in three phases. Whoever your life left standing stands here too.",
          MAIN, "choirmaster", visited("choirmaster"), [flag("campaign_ended")],
          steps=[("Break the overture", [flag("cm_p1")]), ("Press, or refuse", [cond(DecisionWas, "dec_cm_transition")]),
                 ("End the song", [flag("campaign_ended")])],
          consequences=[eff(GrantEchoes, "", "", 30), eff(AddCodex, "c3_choirmaster")], done="The song ends. Vessa, after.")
# --- the final decision: the seven endings (evaluated from state) ---
NOTREF = [cond(FlagMissing, "cm_refused"), flag("choirmaster_defeated")]
decision("dec_ending", "The Fracture's heart is open and quiet. What you do with it now is the last thing this life decides.", [
    option("ashen_crown", "Burn the heart out. Let the Choir end in fire - and every rival with it.",
           "The Fracture burns from the inside. Vessa will be safe, and it will be afraid of you. Ending: ASHEN CROWN.",
           conditions=NOTREF + [cond(AffinityAtLeast, "ember", "", 60), flag("dax_alive", "0")],
           effects=[setf("ending", "ashen_crown"), setf("campaign_ended"), eff(AddCodex, "end_ashen_crown"), eff(SetWorldState, "vessa", "ashen")]),
    option("tides_embrace", "Pour the heart into the city's water. Heal what the light broke - with Mara beside you.",
           "The Fracture goes into the sea like a tide going out. Mara's hand is in yours the whole time. Ending: TIDE'S EMBRACE.",
           conditions=NOTREF + [cond(AffinityAtLeast, "tide", "", 60), cond(BondAtLeast, "mara", "", 50), flag("mara_alive")],
           effects=[setf("ending", "tides_embrace"), setf("campaign_ended"), eff(AddCodex, "end_tides_embrace"), eff(SetWorldState, "vessa", "healed")]),
    option("the_unmoved", "Seal the heart under stone. Nothing gets in; nothing gets out; the city stands.",
           "The Fracture closes under a weight only you could set. The districts you saved stand on it. Ending: THE UNMOVED.",
           conditions=NOTREF + [cond(AffinityAtLeast, "stone", "", 60), cond(VarAtLeast, "districts_saved", "", 2)],
           effects=[setf("ending", "the_unmoved"), setf("campaign_ended"), eff(AddCodex, "end_the_unmoved"), eff(SetWorldState, "vessa", "sealed")]),
    option("hollow_throne", "Take the heart. All of it. Become what the Choirmaster only sang about.",
           "You sit down in the light. It does not burn. It never did, for you. Ending: HOLLOW THRONE.",
           conditions=NOTREF + [cond(AffinityAtLeast, "hollow", "", 25)],
           effects=[setf("ending", "hollow_throne"), setf("campaign_ended"), eff(AddCodex, "end_hollow_throne"), eff(SetWorldState, "vessa", "hollow"),
                    eff(SetWorldState, "spire", "collapsed")]),
    option("balance", "Hold the heart open. Let the light stay - shared, watched, never owned. Mara and Dax will help you watch it.",
           "Nothing ends. That was the point. Ending: BALANCE.",
           conditions=NOTREF + [flag("dominant", "none"), flag("mara_alive"), flag("dax_alive")],
           effects=[setf("ending", "balance"), setf("campaign_ended"), eff(AddCodex, "end_balance"), eff(SetWorldState, "vessa", "balanced")]),
    option("martyrs_dawn", "Put yourself into the heart. Close it from the inside.",
           "It takes everything you are - which, by now, is a great deal. The dawn over Vessa is the first clean one in ten years. Ending: MARTYR'S DAWN.",
           conditions=NOTREF + [cond(FlagIsNot, "dominant", "none")],
           effects=[setf("ending", "martyrs_dawn"), setf("campaign_ended"), eff(AddCodex, "end_martyrs_dawn"), eff(SetWorldState, "vessa", "dawn")]),
    option("long_way_home", "Leave it. Walk down the Spire and go home.",
           "You walk down. Whatever Vessa becomes, it becomes without you standing on the light. Ending: THE LONG WAY HOME.",
           effects=[setf("ending", "long_way_home"), setf("campaign_ended"), eff(AddCodex, "end_long_way_home"), eff(SetWorldState, "vessa", "quiet")])],
    codex="c3_final_decision")
scene("c3_final_decision", "The Fracture's Heart", [("", "The Choirmaster's song is over. The heart of the Fracture is open in front of you - patient as the pier, warm as the last summer.")],
      "dec_ending", {})
# --- EP Epilogue: the seven variants in one scene; the Archivist announces ---
scene("ep_epilogue", "The Archivist", [("The Archivist", "Vessa, after. I kept the count to the end. This is how it reads."),
                                      ("@", "ending"), ("@", "epmara"), ("@", "epmentor"),
                                      ("The Archivist", "That is the whole of it. The count is closed. Thank you for letting me keep it.")],
      variants={"ending": [
          ([flag("ending", "ashen_crown")], "The Archivist", "ASHEN CROWN. The Choir is ash and so is the Spire's crown. Vessa is safe. Vessa locks its doors when you walk by. You do not mind. You tell yourself you do not mind."),
          ([flag("ending", "tides_embrace")], "The Archivist", "TIDE'S EMBRACE. The light went into the sea and the sea forgave it. Relief lines became a harbour; a harbour became a city again. Mara's kite flies over the pier every summer. You hold the string."),
          ([flag("ending", "the_unmoved")], "The Archivist", "THE UNMOVED. The Fracture is stone now and the districts you saved stand on it. The Wardens carve your name on the Long Wall. You did not move. Vessa learned it could stop moving too."),
          ([flag("ending", "hollow_throne")], "The Archivist", "HOLLOW THRONE. The Spire collapsed and something sits where it stood. It wears your face and sings in Dax's voice. Vessa is very quiet. I am the only one who still says your name, and I am afraid of it."),
          ([flag("ending", "balance")], "The Archivist", "BALANCE. The light stays, shared and watched. Mara watches the water; Dax watches the Reaches; you watch them both. Nothing ended. That was the hardest thing anyone in Vessa ever did."),
          ([flag("ending", "martyrs_dawn")], "The Archivist", "MARTYR'S DAWN. You closed it from the inside. The first clean dawn in ten years came up over a city that did not yet know what it cost. They know now. They come to the pier."),
          ([flag("ending", "long_way_home")], "The Archivist", "THE LONG WAY HOME. You walked down. The song went on, then faltered, then stopped on its own - years later, with no one to hear it. You were at the pier. You were always going to be at the pier."),
          ([], "The Archivist", "The count does not read. That should not be possible.")],
          "epmara": [([flag("mara_fate", "lost")], "The Archivist", "Mara: Lost. The kite string is still in your pocket."),
                     ([flag("mara_fate", "turned")], "The Archivist", "Mara: Turned, and unmade on the Spire. Vessa does not say her name near you."),
                     ([flag("mara_fate", "ally")], "The Archivist", "Mara: Ally. She fought beside you to the end, and she still comes to the pier."),
                     ([], "The Archivist", "Mara: alive, and a civilian. The relief lines run at dawn; she runs them.")],
          "epmentor": [([flag("mentor_fate", "fallen")] + MENTOR("kael"), "The Archivist", "Kael: Fallen. The memorial is a coat, folded, on the Spire steps. Nobody has moved it."),
                       ([flag("mentor_fate", "fallen")] + MENTOR("odalys"), "The Archivist", "Sister Odalys: Fallen. The Sanctuary keeps a wet hem in a glass case, and the relief lines sing her name."),
                       ([flag("mentor_fate", "fallen")] + MENTOR("bran"), "The Archivist", "Warden Bran: Fallen. He held a door for six hours once. On the Spire he held one for you."),
                       ([flag("mentor_fate", "estranged")], "The Archivist", "Your mentor: Estranged. They were not on the Spire. They heard how it ended from someone else."),
                       ([flag("mentor_fate", "alive")], "The Archivist", "Your mentor: Alive. They stood in phase two and they stand at the pier now, not saying much. They never did."),
                       ([], "The Archivist", "Your mentor: unrecorded. The count has a gap where a name should be.")]})
objective("obj_epilogue", "Vessa, after", "Hear the Archivist read the count.", MAIN, "epilogue", visited("epilogue"), [flag("epilogue_seen")],
          consequences=[eff(AddCodex, "ep_complete")], done="The count is closed.")
world_interaction("epilogue_stone", "Touch the memorial stone", visited("epilogue"))
chapter("ch_reckoning", "Reckoning", "The City Remembers",
        "Twenty-six. Vessa shows what your choices built; the Market, the Ascent, the Choirmaster in three phases; the final decision.",
        [flag("c2_complete")],
        [beat("beat_rk_reveal", "What Vessa became", "The Archivist read the city back to you: market, docks, Mara, Dax.", T_COND, resolveConds=visited("interlude_reckoning")),
         beat("beat_rk_mentor", "Who still stands with you", "Ten years of a mentor's patience, counted.", T_COND, resolveConds=visited("interlude_reckoning"), req=["beat_rk_reveal"], priority=1),
         beat("beat_rk_market", "The Old Market", "The Cantor's voice is in your hand.", T_OBJ_DONE, "obj_market", req=["beat_rk_reveal"], priority=2),
         beat("beat_rk_ascent", "Ascent", "You climbed the breached Spire against its own gravity.", T_OBJ_DONE, "obj_ascent", req=["beat_rk_market"], priority=3),
         beat("beat_rk_transition", "The door in the song", "The song opened. You pressed - or you walked.", T_DEC, "dec_cm_transition", req=["beat_rk_ascent"], priority=4),
         beat("beat_rk_ending", "The last decision", "The heart of the Fracture was open, and you decided.", T_COND, resolveConds=[flag("campaign_ended")], req=["beat_rk_transition"], priority=5),
         # phase two inserts (GAME_DESIGN §7.2): whoever your life left standing joins the arena - as enemy or ally
         beat("beat_rk_ins_dax_final", "Dax sings the Choirmaster's part", "Dax came up the Spire ahead of you, and he was singing.", T_COND,
              offer=[flag("dax_fate", "final_enemy")], resolveConds=[flag("cm_press")], req=["beat_rk_transition"], effects=[eff(SpawnEntity, "dax_final", "1")], priority=9),
         beat("beat_rk_ins_mara_turned", "Mara, Turned", "Mara got up from under the crane. So did the Hollow.", T_COND,
              offer=[flag("mara_fate", "turned")], resolveConds=[flag("cm_press")], req=["beat_rk_transition"], effects=[eff(SpawnEntity, "mara_turned", "1")], priority=9),
         beat("beat_rk_ins_mara_ally", "Mara takes the left side", "Mara climbed the Spire with you and took the left side.", T_COND,
              offer=[flag("mara_fate", "ally")], resolveConds=[flag("cm_press")], req=["beat_rk_transition"], effects=[eff(SpawnEntity, "mara_ally_cm", "1"), eff(AddBond, "mara", "", 5)], priority=9),
         beat("beat_rk_ins_dax_ally", "Dax takes the right side", "Dax, redeemed, took the right side and told everyone afterwards.", T_COND,
              offer=[flag("dax_fate", "redeemed")], resolveConds=[flag("cm_press")], req=["beat_rk_transition"], effects=[eff(SpawnEntity, "dax_ally_cm", "1"), eff(AddBond, "dax", "", 5)], priority=9),
         beat("beat_rk_ins_kael", "Kael holds the line", "Kael stood in phase two, coat buttoned to the throat.", T_COND,
              offer=[flag("mentor_fate", "alive")] + MENTOR("kael"), resolveConds=[flag("cm_press")], req=["beat_rk_transition"], effects=[eff(SpawnEntity, "kael_cm", "1")], priority=9),
         beat("beat_rk_ins_odalys", "Odalys holds the line", "Odalys stood in phase two, hem dripping.", T_COND,
              offer=[flag("mentor_fate", "alive")] + MENTOR("odalys"), resolveConds=[flag("cm_press")], req=["beat_rk_transition"], effects=[eff(SpawnEntity, "odalys_cm", "1")], priority=9),
         beat("beat_rk_ins_bran", "Bran holds the line", "Bran stood in phase two, the way he stands everywhere.", T_COND,
              offer=[flag("mentor_fate", "alive")] + MENTOR("bran"), resolveConds=[flag("cm_press")], req=["beat_rk_transition"], effects=[eff(SpawnEntity, "bran_cm", "1")], priority=9)],
        [branch("br_rk_ember", "beat_rk_reveal", "beat_rk_market", "Dominant line: Ember", [cond(AffinityAtLeast, "ember", "", 60)], [setf("dominant", "ember")]),
         branch("br_rk_tide", "beat_rk_reveal", "beat_rk_market", "Dominant line: Tide", [cond(AffinityAtLeast, "tide", "", 60)], [setf("dominant", "tide")]),
         branch("br_rk_stone", "beat_rk_reveal", "beat_rk_market", "Dominant line: Stone", [cond(AffinityAtLeast, "stone", "", 60)], [setf("dominant", "stone")]),
         branch("br_rk_hollow", "beat_rk_reveal", "beat_rk_market", "Dominant line: Hollow", [cond(AffinityAtLeast, "hollow", "", 25)], [setf("dominant", "hollow")]),
         branch("br_rk_balance", "beat_rk_reveal", "beat_rk_market", "No dominant line: a deliberate hybrid", [], [setf("dominant", "none")]),
         branch("br_rk_kael_alive", "beat_rk_mentor", "beat_rk_market", "Kael stands with you", MENTOR("kael") + [cond(BondAtLeast, "kael", "", 20)], [setf("mentor_fate", "alive")]),
         branch("br_rk_odalys_alive", "beat_rk_mentor", "beat_rk_market", "Odalys stands with you", MENTOR("odalys") + [cond(BondAtLeast, "odalys", "", 20)], [setf("mentor_fate", "alive")]),
         branch("br_rk_bran_alive", "beat_rk_mentor", "beat_rk_market", "Bran stands with you", MENTOR("bran") + [cond(BondAtLeast, "bran", "", 20)], [setf("mentor_fate", "alive")]),
         branch("br_rk_estranged", "beat_rk_mentor", "beat_rk_market", "Your mentor is estranged", [], [setf("mentor_fate", "estranged")]),
         branch("br_rk_mara_turned", "beat_rk_transition", "beat_rk_ending", "Mara, Turned", [flag("cm_press"), flag("hollow_path"), flag("mara_hurt")], [setf("mara_fate", "turned")]),
         branch("br_rk_pressed", "beat_rk_transition", "beat_rk_ending", "You pressed", [flag("cm_press")]),
         branch("br_rk_refused", "beat_rk_transition", "beat_rk_ending", "You refused the call", [flag("cm_refused")])],
        [flag("campaign_ended")], [setf("ep_open"), eff(MoveNpc, "archivist", "epilogue_pier"), eff(AddCodex, "c3_reckoning_complete")],
        "Reckoning complete. The song is over. Go down to Vessa, after.")
chapter("ch_epilogue", "Epilogue", "Vessa, After",
        "Thirty and after. The ending the state matrix rendered, read aloud by the one who kept the count.",
        [flag("ep_open")],
        [beat("beat_ep_count", "The count, closed", "The Archivist read the ending. The count is closed.", T_OBJ_DONE, "obj_epilogue")],
        [branch("br_ep_ashen", "beat_ep_count", "", "Ending 1: Ashen Crown", [flag("ending", "ashen_crown")]),
         branch("br_ep_tide", "beat_ep_count", "", "Ending 2: Tide's Embrace", [flag("ending", "tides_embrace")]),
         branch("br_ep_unmoved", "beat_ep_count", "", "Ending 3: The Unmoved", [flag("ending", "the_unmoved")]),
         branch("br_ep_hollow", "beat_ep_count", "", "Ending 4: Hollow Throne", [flag("ending", "hollow_throne")]),
         branch("br_ep_balance", "beat_ep_count", "", "Ending 5: Balance", [flag("ending", "balance")]),
         branch("br_ep_home", "beat_ep_count", "", "Ending 6: The Long Way Home", [flag("ending", "long_way_home")]),
         branch("br_ep_martyr", "beat_ep_count", "", "Ending 7: Martyr's Dawn", [flag("ending", "martyrs_dawn")])],
        [flag("epilogue_seen")], [setf("campaign_complete"), eff(AddCodex, "ep_the_end")], "THE END. Vessa remembers.")

# ---- small closing/extra scenes ----
scene("p1_tutorial", "The Pier", [("", "CONTROLS. Left thumb: move. Right thumb: look. The prompt at the bottom is INTERACT - tap it near anything that glows."),
                                  ("", "There is nothing to fight this summer. Walk to the end of the pier and find the kite.")])
scene("c3_mara_reckoning", "Mara", [("@", "c3m")], variants={"c3m": [
    ([flag("mara_fate", "ally")], "Mara", "Ten years. Kite string's still on my wrist. Let's go and finish the song, Ari."),
    ([cond(BondAtLeast, "mara", "", 50)], "Mara", "I'll be at the barricade when you come down. I'll be at the pier after. I'm always going to be somewhere you can find."),
    ([], "Mara", "Go on. Whatever you are now - go and be it. I'll hold the line down here.")]})
decision("dec_epilogue_close", "The count is read.", [
    option("close", "Close the count.", "", effects=[setf("epilogue_seen"), eff(GrantEchoes, "", "", 0)])])
# the epilogue graph gets its closing decision appended (silent D4)
_ep = [g for g in C["graphs"] if g["id"] == "g_ep_epilogue"][0]
for n in _ep["nodes"]:
    if n["nextId"] == "end": n["nextId"] = "close"
_ep["nodes"].insert(len(_ep["nodes"]) - 1, node("close", decisionId="dec_epilogue_close", branchPrefix="closed"))
_ep["nodes"].insert(len(_ep["nodes"]) - 1, node("closed", "", "", nextId="end"))
objective("obj_i2_consult", "Interlude: Becoming", "Consult the Archivist at the shrine camp. Choose the shape of your becoming.",
          MAIN, "interlude_becoming", visited("interlude_becoming"), [cond(DecisionWas, "dec_c2_path")],
          consequences=[eff(GrantEchoes, "", "", 5)], done="A path chosen. The map opens toward it.")
objective("obj_i3_consult", "Interlude: Reckoning", "Hear what Vessa became. Then go to the Old Market.",
          MAIN, "interlude_reckoning", visited("interlude_reckoning"), [flag("loc_visited_market")],
          consequences=[eff(GrantEchoes, "", "", 5)], done="The city remembered. Now it reckons.")

# ================================================================ LOCATIONS (13; the hall stays the Hub)
def L(id, name, kind, desc, unlock, hint, connections, npcs, encounters, objectives, wsc, env):
    location(id, name, kind, id + "_spawn", desc, unlock, hint, connections, npcs, encounters, objectives, wsc, env)
L("last_summer", "The Last Summer", STORY,
  "A pier in late light, remembered from the hall's threshold. Ari at ten; Mara with a kite; Dax on the rail. No combat - the tutorial and the first friendship choices.",
  [], "", ["hall"], ["mara_young"], ["p1_tutorial", "p1_kite", "p1_pier", "p1_summer_end"], ["obj_tut_move", "obj_tut_talk"],
  [eff(SetWorldState, "market", "intact"), eff(AddCodex, "p1_pier_memory")],
  ("summer_gold", "5a4a30", "3d3220", 0.012, "ffd9a0", 1.15))
L("fracture_night", "Night of the Fracture", COMBAT,
  "The pier road the night the Spire split. Three arenas of Choir sweeps between the sea and the Spire gate. The mentor choice; Dax awakened; the combat tutorial.",
  [rule([flag("prologue_complete"), flag("ch1_complete")], "The hall's north stair opens onto a night ten years wide. The Fracture is falling.")],
  "Remember the last summer, and answer the hall's first question, before the night opens.",
  ["hall", "under_spire"], ["dax", "kael", "odalys", "bran"], ["c1_fracture_open", "c1_mentor_lesson", "c1_dax_spar", "c1_dax_after"], ["obj_fn_arenas", "obj_fn_civilians"],
  [eff(SetWorldState, "spire", "sealed"), eff(AddCodex, "c1_fracture_night")],
  ("fracture_violet", "2a1f3d", "1c1530", 0.05, "b58cff", 0.7))
L("under_spire", "Under the Spire", COMBAT,
  "The Spire's root: casters on the galleries, bruisers on the stairs, and at the bottom, the First Echo. Boss arena.",
  [rule([flag("fn_arenas_cleared")], "The Spire gate hangs open. The light goes down.")], "Clear the pier road first.",
  ["fracture_night", "interlude_becoming"], [], ["c1_first_echo_intro", "c1_first_echo_fallen"], ["obj_us_descent"],
  [eff(AddCodex, "c1_under_spire_arrival")],
  ("spire_root", "1e2a3a", "141c28", 0.04, "9fd8ff", 0.8))
L("interlude_becoming", "Interlude: Becoming", NPCLOC,
  "Four years on. A shrine camp above the Reaches: the Archivist, your mentor, Mara at twenty. The Ch.2 path is chosen here.",
  [rule([flag("c1_fracture_complete")], "Four years pass. The shrine camp above the Reaches is lit for you.")], "Finish the night under the Spire.",
  ["under_spire", "hall", "docks", "sanctuary", "long_wall"], ["archivist", "kael", "odalys", "bran", "mara_c2"],
  ["i2_archivist", "c2_mentor_interlude", "c2_mara_talk", "c2_mara_after"], ["obj_i2_consult"],
  [eff(AddCodex, "c2_interlude")],
  ("camp_dusk", "3a3040", "2a2230", 0.02, "f2b48c", 0.9))
L("docks", "Contested Docks", COMBAT,
  "Cranes stopped mid-lift, Choir graffiti on every hull. Four arenas + the Elite in the harbour tower. Assault framing (Ember path).",
  [rule([flag("c2_path", "docks")], "The Docks gate is yours to break.")], "Choose the Docks at the shrine camp.",
  ["interlude_becoming", "dax_arena"], [], ["c2_docks_open", "c2_docks_shed", "c2_mara_pressure"], ["obj_docks_assault"],
  [eff(SetWorldState, "docks", "contested"), eff(AddCodex, "c2_docks_arrival")],
  ("docks_rust", "3d2e24", "2a1f18", 0.035, "ff9a5c", 0.9))
L("sanctuary", "The Sanctuary", COMBAT,
  "A flooded shrine in the Reaches, forty people on the upper steps, four waves coming to drown it. Defence framing (Tide path).",
  [rule([flag("c2_path", "sanctuary")], "The causeway to the Sanctuary rises out of the water for you.")], "Choose the Sanctuary at the shrine camp.",
  ["interlude_becoming", "dax_arena"], [], ["c2_sanctuary_open", "c2_mara_pressure"], ["obj_sanctuary_hold", "obj_sanctuary_recover"],
  [eff(AddCodex, "c2_sanctuary_arrival")],
  ("sanctum_teal", "1f3a40", "16292e", 0.045, "8fe0e8", 0.85))
L("long_wall", "The Long Wall", COMBAT,
  "The old sea wall where the Outskirts end. Nine Wardens, one gate, four pushes. Hold-the-line framing (Stone path).",
  [rule([flag("c2_path", "long_wall")], "The Wardens open the sally port for you.")], "Choose the Long Wall at the shrine camp.",
  ["interlude_becoming", "dax_arena"], [], ["c2_wall_open", "c2_mara_pressure"], ["obj_wall_hold", "obj_wall_recover"],
  [eff(AddCodex, "c2_wall_arrival")],
  ("wall_grey", "34383c", "24282c", 0.03, "d8dde2", 0.95))
L("dax_arena", "Dax Confrontation", COMBAT,
  "The amphitheatre at the end of the Reaches. Dax is waiting - and on the ridge, a Choir Hunter. Duel or team-up; same arena, flag-driven.",
  [rule([flag("c2_path_done"), cond(DecisionWas, "dec_save_mara")], "Dax has sent word: the amphitheatre, at dusk.")],
  "Finish your path - and answer the crane yard - first.",
  ["docks", "sanctuary", "long_wall", "interlude_reckoning"], [], ["c2_dax_confront", "c2_dax_duel_end", "c2_dax_hunter_fallen"], ["obj_dax"],
  [eff(AddCodex, "c2_amphitheatre")],
  ("arena_dusk", "3a2a2a", "2a1c1c", 0.025, "ffb08a", 0.8))
L("interlude_reckoning", "Interlude: Reckoning", NPCLOC,
  "Six years on. The reckoning shrine below the Spire: the Archivist reads the city back; Mara at twenty-six; a dark plinth for those who let the Hollow in.",
  [rule([flag("c2_complete")], "Six years pass. The reckoning shrine below the Spire is lit.")], "Settle things with Dax.",
  ["dax_arena", "hall", "market"], ["archivist", "mara_c3"], ["i3_archivist", "i3_shrine", "i3_hollow_shrine", "c3_mara_reckoning"], ["obj_i3_consult"],
  [eff(AddCodex, "c3_interlude")],
  ("reckoning_blue", "1c2436", "121826", 0.03, "a9c8ff", 0.85))
L("market", "The Old Market", COMBAT,
  "The Old Market as your choices left it - intact, contested, ruined or rebuilt - and the Choir Cantor singing from the fountain. Mid-boss.",
  [rule([flag("loc_visited_interlude_reckoning")], "The market gate. Whatever the market is now, the Cantor is in it.")], "Hear the Archivist first.",
  ["interlude_reckoning", "spire_ascent"], [], ["c3_market_open"], ["obj_market"],
  [eff(AddCodex, "c3_market_arrival")],
  ("market_ember", "3a2e22", "2a2018", 0.03, "ffc48a", 0.95))
L("spire_ascent", "Ascent of the Spire", COMBAT,
  "The breached Spire: tilted floors, dust falling upward, Spire Wardens walking the anomalies. Gravity set pieces.",
  [rule([flag("market_cleared")], "The Spire is breached. The climb is open.")], "Silence the Cantor first.",
  ["market", "choirmaster"], [], ["c3_ascent_open"], ["obj_ascent"],
  [eff(SetWorldState, "spire", "breached"), eff(AddCodex, "c3_ascent_arrival")],
  ("ascent_white", "2c3440", "1c2430", 0.02, "eaf4ff", 1.0))
L("choirmaster", "The Choirmaster", COMBAT,
  "The Fracture's heart at the top of the Spire. Three phases; whoever your life left standing stands here; the refusal is offered at the door in the song.",
  [rule([flag("ascent_done")], "The overture starts above you.")], "Climb the Spire.",
  ["spire_ascent", "epilogue"], [], ["c3_cm_open", "c3_cm_transition", "c3_cm_phase2", "c3_cm_phase3", "c3_final_decision"], ["obj_choirmaster"],
  [eff(AddCodex, "c3_heart_arrival")],
  ("heart_gold", "3a3020", "2a2214", 0.02, "ffe2a0", 1.1))
L("epilogue", "Epilogue: Vessa, After", STORY,
  "The pier, years later. One scene, seven endings; the Archivist reads the count.",
  [rule([flag("campaign_ended")], "The song is over. The pier is waiting.")], "Finish the song, one way or another.",
  ["choirmaster", "hall"], [], ["ep_epilogue"], ["obj_epilogue"],
  [eff(AddCodex, "ep_arrival")],
  ("after_dawn", "4a4a50", "34343a", 0.012, "fff1dc", 1.2))
# the hall hub reaches the memory pier and the campaign's night stair (existing edges untouched)
for loc in C["locations"]:
    if loc["id"] == "hall":
        loc["connections"] += ["last_summer", "fracture_night", "interlude_becoming", "interlude_reckoning", "epilogue"]

# ================================================================ integrity + write
ids = lambda key: [x["id"] for x in C[key]]
enc_ids = set(ids("encounters")); dec_ids = set(ids("decisions")); obj_ids = set(ids("objectives")); npc_ids = set(ids("npcs"))
graph_ids = set(ids("graphs")); ability_ids = set(a["id"] for a in C["progression"]["abilities"]); status_ids = set(ids("statusEffects"))
problems = []
for n in C["npcs"]:
    for it in n["interactions"]:
        if it["encounterId"] not in enc_ids: problems.append("npc %s interaction -> missing encounter %s" % (n["id"], it["encounterId"]))
for e in C["encounters"]:
    if e["graphId"] not in graph_ids: problems.append("encounter %s -> missing graph" % e["id"])
for g in C["graphs"]:
    node_ids = set(n["id"] for n in g["nodes"])
    for n in g["nodes"]:
        if n["nextId"] and n["nextId"] not in node_ids: problems.append("graph %s node %s -> missing next %s" % (g["id"], n["id"], n["nextId"]))
        if n["decisionId"] and n["decisionId"] not in dec_ids: problems.append("graph %s -> missing decision %s" % (g["id"], n["decisionId"]))
        if g["id"] in added["graphs"] and n["branchPrefix"] and not any(x.startswith(n["branchPrefix"]) for x in node_ids):
            problems.append("graph %s bad prefix %s" % (g["id"], n["branchPrefix"]))  # (pre-existing graphs keep their own contracts)
for o in C["objectives"]:
    for f in o["followUps"]:
        if f not in obj_ids: problems.append("objective %s follow-up missing %s" % (o["id"], f))
for l in C["locations"]:
    for e in l["encounters"]:
        if e not in enc_ids: problems.append("location %s encounter missing %s" % (l["id"], e))
    for o in l["objectives"]:
        if o not in obj_ids: problems.append("location %s objective missing %s" % (l["id"], o))
    for n in l["npcs"]:
        if n not in npc_ids: problems.append("location %s npc missing %s" % (l["id"], n))
for ac in C["abilityCombat"]:
    if ac["abilityId"] not in ability_ids: problems.append("abilityCombat -> missing ability " + ac["abilityId"])
    for sid in ac["applyStatusToTargets"] + ac["applyStatusToPlayer"]:
        if sid not in status_ids: problems.append("abilityCombat %s -> missing status %s" % (ac["abilityId"], sid))
for en in C["enemies"]:
    for sid in en["attack"]["applyStatusIds"]:
        if sid not in status_ids: problems.append("enemy %s -> missing status %s" % (en["id"], sid))
beat_ids = set(b["id"] for ch in C["chapters"] for b in ch["beats"])
for ch in C["chapters"]:
    for br in ch["branches"]:
        if br["fromBeatId"] not in beat_ids or (br["toBeatId"] and br["toBeatId"] not in beat_ids): problems.append("chapter %s branch %s dangling" % (ch["id"], br["id"]))
    for b in ch["beats"]:
        for r in b["requiredBeatIds"]:
            if r not in beat_ids: problems.append("beat %s requires missing %s" % (b["id"], r))
        if b["resolveTrigger"] == T_DEC and b["resolveKey"] not in dec_ids: problems.append("beat %s -> missing decision %s" % (b["id"], b["resolveKey"]))
        if b["resolveTrigger"] in (T_OBJ_DONE, T_OBJ_FAIL) and b["resolveKey"] not in obj_ids: problems.append("beat %s -> missing objective %s" % (b["id"], b["resolveKey"]))
for key in ("encounters", "decisions", "graphs", "npcs", "objectives", "enemies", "chapters", "locations", "statusEffects"):
    dupes = [k for k, v in collections.Counter(ids(key)).items() if v > 1]
    if dupes: problems.append("duplicate %s ids: %s" % (key, dupes))
if problems:
    raise SystemExit("INTEGRITY:\n  " + "\n  ".join(problems))

json.dump(C, open(PATH, "w"), indent=1, ensure_ascii=False)
open(PATH, "a").write("\n")
json.dump(dict(added), open(MANIFEST, "w"), indent=1)
print("merged campaign pass:", {k: len(v) for k, v in added.items()})
