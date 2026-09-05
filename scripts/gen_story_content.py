"""Generates the data-driven story content for CROSSROADS (decision + progression phase):

  1. Assets/_Project/Data/Decisions/CL_C1_StoryContent.asset (StoryContentLibraryAsset SO)
     - encounters[] / decisions[] / graphs[] / progression{abilities,skills,items,reputationGroups,areas}
  2. .meta files for every new C# script + data asset + new folders (guid registry below)
  3. Merges everything into scripts/hall_guids.json (the project's deterministic GUID registry)

Content source of truth: scripts/story_content.json (mirrors Narrative/StoryContentBuilder.cs;
scripts/validate_assets.py cross-checks them). Deterministic GUID scheme c0a1fed2....
Run BEFORE gen_firstlocation_scene.py (it consumes the registry)."""
import json, os, hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REG_PATH = os.path.join(HERE, "hall_guids.json")

def g32(n):
    return ("c0a1fed2" + ("%024x" % n))[:32]

# ---------------------------------------------------------------- guid allocation
NEW_GUIDS = {
    "GameStateManager.cs": g32(0x79),
    "ProgressionIndex.cs": g32(0x7a),
    "EffectNotices.cs":    g32(0x7b),
    "GateRuleEvaluator.cs": g32(0x7c),
    "AreaGate.cs":         g32(0x61),
    "StoryEventInteractable.cs": g32(0x62),
    "NpcFateDriver.cs":    g32(0x5b),
    "AreaTrigger.cs":      g32(0x5c),
    "NpcBrain.cs":         g32(0x91),
    "NpcLogic.cs":         g32(0x92),
    "NpcAgent.cs":         g32(0x93),
    "NpcInteractable.cs":  g32(0x94),
    "AbilityManager.cs":   g32(0x95),
    "AbilityPulseVFX.cs":  g32(0x96),
    "AbilityHUD.cs":       g32(0x97),
    "AbilitySheetModel.cs": g32(0x98),
    # world & objective phase (Gameplay/World + Core events + UI)
    "WorldEvents.cs":      g32(0xa0),
    "ObjectiveSystem.cs":  g32(0xa1),
    "WorldStateSystem.cs": g32(0xa2),
    "WorldActionInteractable.cs": g32(0xa3),
    "NpcRelocator.cs":     g32(0xa4),
    "ObjectiveHUD.cs":     g32(0xa5),
    "WorldServices.cs":    g32(0xa6),
    # combat phase (Gameplay/Combat + UI)
    "CombatEvents.cs":     g32(0xa7),
    "Combatant.cs":        g32(0xa8),
    "EnemyBrain.cs":       g32(0xa9),
    "CombatResolution.cs": g32(0xaa),
    "EnemyAgent.cs":       g32(0xab),
    "PlayerCombatController.cs": g32(0xac),
    "CombatDirector.cs":   g32(0xad),
    "CombatHUD.cs":        g32(0xae),
    # mobile player experience phase (Gameplay/Input + touch UI)
    "InputBus.cs":         g32(0xaf),
    "InputSettings.cs":    g32(0xb0),
    "CameraRigMath.cs":    g32(0xb1),
    "CombatPresence.cs":   g32(0xb2),
    "VirtualJoystick.cs":  g32(0xb3),
    "TouchLookPad.cs":     g32(0xb4),
    "MobileControlsUI.cs": g32(0xb5),
    "PauseMenuUI.cs":      g32(0xb6),
    "AndroidDevBuild.cs":  g32(0xb7),
    # campaign phase (Core events + Gameplay/Campaign + UI)
    "CampaignEvents.cs":   g32(0xb8),
    "CampaignManager.cs":  g32(0xb9),
    "CampaignServices.cs": g32(0xba),
    "CampaignHUD.cs":      g32(0xbb),
    # world expansion phase (Core events + Gameplay/Locations + UI)
    "LocationEvents.cs":          g32(0xbc),
    "LocationManager.cs":         g32(0xbd),
    "LocationServices.cs":        g32(0xbe),
    "LocationTransitionFader.cs": g32(0xbf),
    "MapHUD.cs":                  g32(0xc0),
    "M_Tide_Pool":                g32(0xc1),
    "M_Tidewell_Stone":           g32(0xc2),
}

REGISTRY = {}
if os.path.exists(REG_PATH):
    REGISTRY = json.load(open(REG_PATH))
for k, v in NEW_GUIDS.items():
    if k in REGISTRY and REGISTRY[k] != v:
        raise SystemExit("GUID conflict in registry for %s: %s vs %s" % (k, REGISTRY[k], v))
    REGISTRY[k] = v
json.dump(REGISTRY, open(REG_PATH, "w"), indent=1)

# ---------------------------------------------------------------- meta builders
MONO = """fileFormatVersion: 2
guid: {g}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
NATIVE = """fileFormatVersion: 2
guid: {g}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
FOLDER = """fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

SCRIPT_META_PATHS = {
    "GameStateManager.cs": "Assets/_Project/Scripts/Narrative",
    "ProgressionIndex.cs": "Assets/_Project/Scripts/Narrative",
    "EffectNotices.cs": "Assets/_Project/Scripts/Narrative",
    "GateRuleEvaluator.cs": "Assets/_Project/Scripts/Narrative",
    "AreaGate.cs": "Assets/_Project/Scripts/Gameplay/Interaction",
    "StoryEventInteractable.cs": "Assets/_Project/Scripts/Gameplay/Interaction",
    "NpcFateDriver.cs": "Assets/_Project/Scripts/Gameplay/WorldState",
    "AreaTrigger.cs": "Assets/_Project/Scripts/Gameplay/WorldState",
    "NpcBrain.cs": "Assets/_Project/Scripts/Gameplay/NPC",
    "NpcLogic.cs": "Assets/_Project/Scripts/Gameplay/NPC",
    "NpcAgent.cs": "Assets/_Project/Scripts/Gameplay/NPC",
    "NpcInteractable.cs": "Assets/_Project/Scripts/Gameplay/NPC",
    "AbilityManager.cs":   "Assets/_Project/Scripts/Narrative/Abilities",
    "AbilityPulseVFX.cs":  "Assets/_Project/Scripts/Gameplay/Abilities",
    "AbilityHUD.cs":       "Assets/_Project/Scripts/UI",
    "AbilitySheetModel.cs": "Assets/_Project/Scripts/UI",
    "WorldEvents.cs":      "Assets/_Project/Scripts/Core",
    "ObjectiveSystem.cs":  "Assets/_Project/Scripts/Gameplay/World",
    "WorldStateSystem.cs": "Assets/_Project/Scripts/Gameplay/World",
    "WorldActionInteractable.cs": "Assets/_Project/Scripts/Gameplay/World",
    "NpcRelocator.cs":     "Assets/_Project/Scripts/Gameplay/World",
    "WorldServices.cs":    "Assets/_Project/Scripts/Gameplay/World",
    "ObjectiveHUD.cs":     "Assets/_Project/Scripts/UI",
    "CombatEvents.cs":     "Assets/_Project/Scripts/Gameplay/Combat",
    "Combatant.cs":        "Assets/_Project/Scripts/Gameplay/Combat",
    "EnemyBrain.cs":       "Assets/_Project/Scripts/Gameplay/Combat",
    "CombatResolution.cs": "Assets/_Project/Scripts/Gameplay/Combat",
    "EnemyAgent.cs":       "Assets/_Project/Scripts/Gameplay/Combat",
    "PlayerCombatController.cs": "Assets/_Project/Scripts/Gameplay/Combat",
    "CombatDirector.cs":   "Assets/_Project/Scripts/Gameplay/Combat",
    "CombatHUD.cs":        "Assets/_Project/Scripts/UI",
    "InputBus.cs":         "Assets/_Project/Scripts/Gameplay/Input",
    "InputSettings.cs":    "Assets/_Project/Scripts/Gameplay/Input",
    "CameraRigMath.cs":    "Assets/_Project/Scripts/Gameplay/Input",
    "CombatPresence.cs":   "Assets/_Project/Scripts/Gameplay/Input",
    "VirtualJoystick.cs":  "Assets/_Project/Scripts/UI",
    "TouchLookPad.cs":     "Assets/_Project/Scripts/UI",
    "MobileControlsUI.cs": "Assets/_Project/Scripts/UI",
    "PauseMenuUI.cs":      "Assets/_Project/Scripts/UI",
    "AndroidDevBuild.cs":  "Assets/Editor",
    "CampaignEvents.cs":   "Assets/_Project/Scripts/Core",
    "CampaignManager.cs":  "Assets/_Project/Scripts/Gameplay/Campaign",
    "CampaignServices.cs": "Assets/_Project/Scripts/Gameplay/Campaign",
    "CampaignHUD.cs":      "Assets/_Project/Scripts/UI",
    "LocationEvents.cs":   "Assets/_Project/Scripts/Core",
    "LocationManager.cs":  "Assets/_Project/Scripts/Gameplay/Locations",
    "LocationServices.cs": "Assets/_Project/Scripts/Gameplay/Locations",
    "LocationTransitionFader.cs": "Assets/_Project/Scripts/UI",
    "MapHUD.cs":           "Assets/_Project/Scripts/UI",
}
NPC_DIR = os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/NPC")
os.makedirs(NPC_DIR, exist_ok=True)
npc_folder_meta = os.path.join(NPC_DIR + ".meta")
if not os.path.exists(npc_folder_meta):
    open(npc_folder_meta, "w").write(FOLDER.format(g=hashlib.md5(("folder:" + NPC_DIR.replace(ROOT, "Assets")).encode()).hexdigest()))
    print("meta +", os.path.relpath(npc_folder_meta, ROOT))

FOLDER_META_PATHS = [
    os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/Campaign"),
    os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/Locations"),
    os.path.join(ROOT, "Assets/Editor"),
    os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/Input"),
    os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/Combat"),
    os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/Abilities"),
    os.path.join(ROOT, "Assets/_Project/Scripts/Narrative/Abilities"),
    os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/World"),
    os.path.join(ROOT, "Assets/Game/Locations"),
    os.path.join(ROOT, "Assets/Game/Locations/FractureHall"),
    os.path.join(ROOT, "Assets/Game/Locations/NorthAnnex"),
    os.path.join(ROOT, "Assets/Game/Locations/TidewellShrine"),
] + [os.path.join(ROOT, "Assets/Game/Locations", k) for k in
     ("LastSummer", "FractureNight", "UnderSpire", "InterludeBecoming", "ContestedDocks", "Sanctuary", "LongWall",
      "DaxArena", "InterludeReckoning", "OldMarket", "SpireAscent", "Choirmaster", "Epilogue")]  # campaign content pass
for fd in FOLDER_META_PATHS:
    os.makedirs(fd, exist_ok=True)
    fm = fd + ".meta"
    if not os.path.exists(fm):
        open(fm, "w").write(FOLDER.format(g=hashlib.md5(("folder:" + fd.replace(ROOT, "Assets")).encode()).hexdigest()))
        print("meta +", os.path.relpath(fm, ROOT))

for fname, sub in SCRIPT_META_PATHS.items():
    path = os.path.join(ROOT, sub, fname)
    if not os.path.exists(path):
        raise SystemExit("missing script (meta table out of date): " + path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        open(meta, "w").write(MONO.format(g=REGISTRY[fname]))
        print("meta +", os.path.relpath(meta, ROOT))

# ---------------------------------------------------------------- story content asset
CONTENT = json.load(open(os.path.join(HERE, "story_content.json")))

def yaml_str(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'

def ind_list(items, item_indent, field_indent, key_names):
    lines = []
    for it in items:
        for i, k in enumerate(key_names):
            v = it[k]
            if isinstance(v, bool): v = "1" if v else "0"
            if isinstance(v, (int, float)): v = str(v)
            else: v = yaml_str(v)
            if i == 0:
                lines.append(" " * item_indent + "- " + k + ": " + v)
            else:
                lines.append(" " * field_indent + k + ": " + v)
    return "\n".join(lines)

def cond_lines(conds, item_indent, field_indent):
    return ind_list(conds, item_indent, field_indent, ["type", "key", "value", "amount"])

def eff_lines(effects, item_indent, field_indent):
    return ind_list(effects, item_indent, field_indent, ["type", "key", "value", "amount"])

# ---- decisions ----
dec_blocks = []
for d in CONTENT["decisions"]:
    parts = [ind_list([d], 4, 6, ["id", "promptText", "timeLimitSeconds", "timeoutOptionIndex", "codexEntryId"])]
    opt_parts = []
    for o in d["options"]:
        opt_parts.append(ind_list([o], 6, 8, ["id", "text", "afterText"]))
        opt_parts.append("        conditions:" + ("\n" + cond_lines(o["conditions"], 8, 10) if o["conditions"] else " []"))
        opt_parts.append("        effects:" + ("\n" + eff_lines(o["effects"], 8, 10) if o["effects"] else " []"))
    parts.append("      options:\n" + "\n".join(opt_parts))
    dec_blocks.append("\n".join(parts))
decisions_block = "\n".join(dec_blocks)

# ---- graphs ----
graph_blocks = []
for g in CONTENT["graphs"]:
    node_parts = []
    for n in g["nodes"]:
        node_parts.append(ind_list([n], 6, 8,
            ["id", "speaker", "text", "nextId", "branchPrefix", "decisionId"]))
        node_parts.append("        conditions:" + ("\n" + cond_lines(n["conditions"], 8, 10) if n["conditions"] else " []"))
        node_parts.append("        end: " + ("1" if n["end"] else "0"))
    graph_blocks.append("    - id: " + yaml_str(g["id"]) + "\n      nodes:\n" + "\n".join(node_parts))
graphs_block = "\n".join(graph_blocks)

# ---- encounters ----
enc_block = ind_list(CONTENT["encounters"], 4, 6, ["id", "npcName", "graphId", "startNodeId"])

# ---- npcs (data-driven NPC definitions, GAME_DESIGN §9) ----
def num(v):
    if isinstance(v, bool): return "1" if v else "0"
    if isinstance(v, int): return str(v)
    if isinstance(v, float): return repr(v)
    return yaml_str(v)

def obj_fields(obj, indent, keys):
    lines = []
    for k in keys:
        v = obj[k]
        tok = num(v) if isinstance(v, (int, float, bool)) else yaml_str(v)
        lines.append(" " * indent + k + ": " + tok)
    return "\n".join(lines)

def npc_state_block(state):
    lines = ["      - conditions:" + ("\n" + cond_lines(state["conditions"], 8, 10) if state["conditions"] else " []")]
    for k in ["title", "moodLine", "approachDistance", "avoidDistance", "moveSpeed", "reactRadius"]:
        v = state[k]
        tok = num(v) if isinstance(v, (int, float, bool)) else yaml_str(v)
        lines.append("        " + k + ": " + tok)
    return "\n".join(lines)

def npc_interaction_block(it):
    return "      - id: " + yaml_str(it["id"]) + "\n" + \
           "        label: " + yaml_str(it["label"]) + "\n" + \
           "        encounterId: " + yaml_str(it["encounterId"]) + "\n" + \
           "        conditions:" + ("\n" + cond_lines(it["conditions"], 8, 10) if it["conditions"] else " []")

npc_parts = []
for n in CONTENT["npcs"]:
    part = ind_list([n], 4, 6, ["id", "displayName", "sheetRef", "description"])
    b = n["behaviour"]
    part += "\n      behaviour:\n" + obj_fields(b, 8,
        ["personality", "facesPlayer", "reactRadius", "approachDistance", "avoidDistance",
         "talkDistance", "moveSpeed", "turnSpeed", "usesRoutine"])
    part += ("\n      states:\n" + "\n".join(npc_state_block(st) for st in n["states"])) if n["states"] else "\n      states: []"
    part += ("\n      interactions:\n" + "\n".join(npc_interaction_block(it) for it in n["interactions"])) if n["interactions"] else "\n      interactions: []"
    rp = []
    for stop in n["routine"]:
        pos = stop["position"]
        rp.append("      - position: {x: %s, y: %s, z: %s}\n        dwellSeconds: %s" % (num(pos["x"]), num(pos["y"]), num(pos["z"]), num(stop["dwellSeconds"])))
    part += ("\n      routine:\n" + "\n".join(rp)) if rp else "\n      routine: []"
    npc_parts.append(part)
npcs_block = "\n".join(npc_parts)

# ---- progression ----
p = CONTENT["progression"]
def prog_block(name, items, keys):
    return "      " + name + ":\n" + ind_list(items, 8, 10, keys) if items else "      " + name + ": []"

def ability_block(items):
    """Full AbilityDefinitionData serialization: fields + unlockConditions + level rows."""
    lines = []
    for ab in items:
        lines.append("      - id: " + yaml_str(ab["id"]))
        lines.append("        name: " + yaml_str(ab["name"]))
        lines.append("        line: " + yaml_str(ab["line"]))
        lines.append("        description: " + yaml_str(ab["description"]))
        lines.append("        category: " + str(ab["category"]))
        lines.append("        unlockHint: " + yaml_str(ab["unlockHint"]))
        uc = ab["unlockConditions"]
        lines.append("        unlockConditions:" + ("\n" + cond_lines(uc, 10, 12) if uc else " []"))
        lines.append("        vfxRef: " + yaml_str(ab["vfxRef"]))
        lines.append("        sfxRef: " + yaml_str(ab["sfxRef"]))
        lines.append("        echoCostPerLevel: " + str(ab["echoCostPerLevel"]))
        lines.append("        levels:")
        for lv in ab["levels"]:
            lines.append("        - level: " + str(lv["level"]))
            lines.append("          cooldown: " + str(lv["cooldown"]))
            lines.append("          power: " + str(lv["power"]))
            lines.append("          radius: " + str(lv["radius"]))
            lines.append("          duration: " + str(lv["duration"]))
            lines.append("          energyCost: " + str(lv["energyCost"]))
            lines.append("          description: " + yaml_str(lv["description"]))
    return "\n".join(lines)

prog_parts = [
    "    progression:",
    ("      abilities:\n" + ability_block(p["abilities"])) if p["abilities"] else "      abilities: []",
    prog_block("skills", p["skills"], ["id", "name", "maxLevel"]),
    prog_block("items", p["items"], ["id", "name", "description"]),
    prog_block("reputationGroups", p["reputationGroups"], ["id", "name"]),
    prog_block("areas", p["areas"], ["id", "name"]),
]
progression_block = "\n".join(prog_parts)

# ---- objectives (data-driven mission system, Gameplay/World) ----
def step_block(step):
    lines = ["        - text: " + yaml_str(step["text"])]
    lines.append("          conditions:" + ("\n" + cond_lines(step["conditions"], 12, 14) if step["conditions"] else " []"))
    return "\n".join(lines)

def objective_block(items):
    out = []
    for o in items:
        out.append("      - id: " + yaml_str(o["id"]))
        out.append("        title: " + yaml_str(o["title"]))
        out.append("        description: " + yaml_str(o["description"]))
        out.append("        type: " + str(o["type"]))
        out.append("        areaId: " + yaml_str(o["areaId"]))
        out.append("        giverNpcId: " + yaml_str(o["giverNpcId"]))
        out.append("        offerConditions:" + ("\n" + cond_lines(o["offerConditions"], 10, 12) if o["offerConditions"] else " []"))
        out.append("        autoActivate: " + ("1" if o.get("autoActivate", True) else "0"))
        out.append("        completeConditions:" + ("\n" + cond_lines(o["completeConditions"], 10, 12) if o["completeConditions"] else " []"))
        out.append("        failConditions:" + ("\n" + cond_lines(o["failConditions"], 10, 12) if o["failConditions"] else " []"))
        out.append("        counterVar: " + yaml_str(o.get("counterVar", "")))
        out.append("        counterTarget: " + str(o.get("counterTarget", 0)))
        out.append("        counterText: " + yaml_str(o.get("counterText", "")))
        out.append("        steps:" + ("\n" + "\n".join(step_block(s) for s in o["steps"]) if o["steps"] else " []"))
        out.append("        consequences:" + ("\n" + eff_lines(o["consequences"], 10, 12) if o["consequences"] else " []"))
        out.append("        failureConsequences:" + ("\n" + eff_lines(o["failureConsequences"], 10, 12) if o["failureConsequences"] else " []"))
        out.append("        followUps:" + ("\n" + "\n".join("        - " + yaml_str(f) for f in o["followUps"]) if o["followUps"] else " []"))
        out.append("        completionNotice: " + yaml_str(o.get("completionNotice", "")))
        out.append("        failureNotice: " + yaml_str(o.get("failureNotice", "")))
    return "\n".join(out)

def world_interaction_block(items):
    out = []
    for w in items:
        out.append("      - key: " + yaml_str(w["key"]))
        out.append("        label: " + yaml_str(w["label"]))
        out.append("        conditions:" + ("\n" + cond_lines(w["conditions"], 10, 12) if w["conditions"] else " []"))
    return "\n".join(out)

objectives_block = ("    objectives:\n" + objective_block(CONTENT["objectives"])) if CONTENT.get("objectives") else "    objectives: []"

# ---- combat content (status effects / ability payloads / enemies / settings) ----
def str_list(items, indent):
    if not items:
        return " " * indent + "[]"
    return "\n".join(" " * indent + "- " + yaml_str(s) for s in items)

def status_effect_block(items):
    out = []
    for s in items:
        out.append("      - id: " + yaml_str(s["id"]))
        out.append("        name: " + yaml_str(s["name"]))
        out.append("        description: " + yaml_str(s["description"]))
        out.append("        durationSeconds: " + num(s["durationSeconds"]))
        out.append("        tickIntervalSeconds: " + num(s["tickIntervalSeconds"]))
        out.append("        healthPerTick: " + str(s["healthPerTick"]))
        out.append("        moveSpeedMultiplier: " + num(s["moveSpeedMultiplier"]))
        out.append("        attackRateMultiplier: " + num(s["attackRateMultiplier"]))
        out.append("        grantsImmunity: " + ("1" if s["grantsImmunity"] else "0"))
    return "\n".join(out)

def ability_combat_block(items):
    out = []
    for a in items:
        out.append("      - abilityId: " + yaml_str(a["abilityId"]))
        out.append("        damageType: " + str(a["damageType"]))
        out.append("        damagePerPower: " + num(a["damagePerPower"]))
        out.append("        healPlayerPerPower: " + num(a["healPlayerPerPower"]))
        out.append("        applyStatusToTargets:\n" + str_list(a["applyStatusToTargets"], 10) if a["applyStatusToTargets"] else "        applyStatusToTargets: []")
        out.append("        applyStatusToPlayer:\n" + str_list(a["applyStatusToPlayer"], 10) if a["applyStatusToPlayer"] else "        applyStatusToPlayer: []")
    return "\n".join(out)

def attack_fields(a, indent):
    pad = " " * indent
    lines = [
        pad + "id: " + yaml_str(a["id"]),
        pad + "name: " + yaml_str(a["name"]),
        pad + "damageType: " + str(a["damageType"]),
        pad + "delivery: " + str(a["delivery"]),
        pad + "baseDamage: " + num(a["baseDamage"]),
        pad + "range: " + num(a["range"]),
        pad + "arcDegrees: " + num(a["arcDegrees"]),
        pad + "radius: " + num(a["radius"]),
        pad + "windupSeconds: " + num(a["windupSeconds"]),
        pad + "cooldownSeconds: " + num(a["cooldownSeconds"]),
    ]
    lines.append(pad + "applyStatusIds:\n" + str_list(a["applyStatusIds"], indent) if a["applyStatusIds"] else pad + "applyStatusIds: []")
    return "\n".join(lines)

def enemy_block(items):
    out = []
    for e in items:
        out.append("      - id: " + yaml_str(e["id"]))
        out.append("        displayName: " + yaml_str(e["displayName"]))
        out.append("        description: " + yaml_str(e["description"]))
        out.append("        sheetRef: " + yaml_str(e["sheetRef"]))
        out.append("        maxHealth: " + num(e["maxHealth"]))
        out.append("        defense: " + num(e["defense"]))
        out.append("        resistances:")
        for r in e["resistances"]:
            out.append("        - type: " + str(r["type"]))
            out.append("          multiplier: " + num(r["multiplier"]))
        out.append("        moveSpeed: " + num(e["moveSpeed"]))
        out.append("        turnSpeed: " + num(e["turnSpeed"]))
        out.append("        detectionRadius: " + num(e["detectionRadius"]))
        out.append("        leashRadius: " + num(e["leashRadius"]))
        out.append("        attackRange: " + num(e["attackRange"]))
        out.append("        staggerSeconds: " + num(e["staggerSeconds"]))
        out.append("        attack:\n" + attack_fields(e["attack"], 10))
        out.append("        activationConditions:" + ("\n" + cond_lines(e["activationConditions"], 10, 12) if e["activationConditions"] else " []"))
        out.append("        onDefeatEffects:" + ("\n" + eff_lines(e["onDefeatEffects"], 10, 12) if e["onDefeatEffects"] else " []"))
    return "\n".join(out)

def combat_settings_block(cs):
    out = [
        "    combat:",
        "      playerMaxHealth: " + num(cs["playerMaxHealth"]),
        "      playerDefense: " + num(cs["playerDefense"]),
        "      playerResistances:",
    ]
    for r in cs["playerResistances"]:
        out.append("      - type: " + str(r["type"]))
        out.append("        multiplier: " + num(r["multiplier"]))
    out.append("      basicAttack:\n" + attack_fields(cs["basicAttack"], 8))
    out.append("      dodgeDistance: " + num(cs["dodgeDistance"]))
    out.append("      dodgeDurationSeconds: " + num(cs["dodgeDurationSeconds"]))
    out.append("      dodgeCooldownSeconds: " + num(cs["dodgeCooldownSeconds"]))
    out.append("      dodgeStatusId: " + yaml_str(cs["dodgeStatusId"]))
    out.append("      healthVarKey: " + yaml_str(cs["healthVarKey"]))
    out.append("      onPlayerDefeat:" + ("\n" + eff_lines(cs["onPlayerDefeat"], 8, 10) if cs["onPlayerDefeat"] else " []"))
    return "\n".join(out)

status_effects_block = ("    statusEffects:\n" + status_effect_block(CONTENT["statusEffects"])) if CONTENT.get("statusEffects") else "    statusEffects: []"
ability_combat_out = ("    abilityCombat:\n" + ability_combat_block(CONTENT["abilityCombat"])) if CONTENT.get("abilityCombat") else "    abilityCombat: []"
enemies_out = ("    enemies:\n" + enemy_block(CONTENT["enemies"])) if CONTENT.get("enemies") else "    enemies: []"
combat_out = combat_settings_block(CONTENT["combat"]) if CONTENT.get("combat") else "    combat:\n      playerMaxHealth: 100"
world_interactions_block = ("    worldInteractions:\n" + world_interaction_block(CONTENT.get("worldInteractions", []))) if CONTENT.get("worldInteractions") else "    worldInteractions: []"

# ---- chapters (branching campaign: beats + branches, all condition/effect whitelists) ----
def beat_block(b):
    out = ["      - id: " + yaml_str(b["id"])]
    out.append("        title: " + yaml_str(b["title"]))
    out.append("        journalText: " + yaml_str(b["journalText"]))
    out.append("        offerConditions:" + ("\n" + cond_lines(b["offerConditions"], 10, 12) if b["offerConditions"] else " []"))
    out.append("        resolveTrigger: " + str(b["resolveTrigger"]))
    out.append("        resolveKey: " + yaml_str(b.get("resolveKey", "")))
    out.append("        resolveConditions:" + ("\n" + cond_lines(b["resolveConditions"], 10, 12) if b["resolveConditions"] else " []"))
    out.append("        requiredBeatIds:\n" + str_list(b["requiredBeatIds"], 10) if b["requiredBeatIds"] else "        requiredBeatIds: []")
    out.append("        onResolveEffects:" + ("\n" + eff_lines(b["onResolveEffects"], 10, 12) if b["onResolveEffects"] else " []"))
    out.append("        priority: " + str(b.get("priority", 0)))
    return "\n".join(out)

def branch_block(br):
    out = ["      - id: " + yaml_str(br["id"])]
    out.append("        fromBeatId: " + yaml_str(br["fromBeatId"]))
    out.append("        toBeatId: " + yaml_str(br.get("toBeatId", "")))
    out.append("        label: " + yaml_str(br.get("label", "")))
    out.append("        requiredConditions:" + ("\n" + cond_lines(br["requiredConditions"], 10, 12) if br["requiredConditions"] else " []"))
    out.append("        effects:" + ("\n" + eff_lines(br["effects"], 10, 12) if br["effects"] else " []"))
    return "\n".join(out)

def chapter_block(ch):
    out = ["    - id: " + yaml_str(ch["id"])]
    out.append("      title: " + yaml_str(ch["title"]))
    out.append("      subtitle: " + yaml_str(ch.get("subtitle", "")))
    out.append("      description: " + yaml_str(ch.get("description", "")))
    out.append("      entryConditions:" + ("\n" + cond_lines(ch["entryConditions"], 8, 10) if ch["entryConditions"] else " []"))
    out.append("      beats:\n" + "\n".join(beat_block(b) for b in ch["beats"]) if ch["beats"] else "      beats: []")
    out.append("      branches:\n" + "\n".join(branch_block(br) for br in ch["branches"]) if ch["branches"] else "      branches: []")
    out.append("      completionConditions:" + ("\n" + cond_lines(ch["completionConditions"], 8, 10) if ch["completionConditions"] else " []"))
    out.append("      completionEffects:" + ("\n" + eff_lines(ch["completionEffects"], 8, 10) if ch["completionEffects"] else " []"))
    out.append("      completionJournal: " + yaml_str(ch.get("completionJournal", "")))
    return "\n".join(out)

chapters_out = ("    chapters:\n" + "\n".join(chapter_block(ch) for ch in CONTENT["chapters"])) if CONTENT.get("chapters") else "  chapters: []"

# ---- locations (world expansion: unlock rules reuse the gate language) ----
def gate_rule_lines(rules, item_indent, field_indent):
    lines = []
    for r in rules:
        lines.append(" " * item_indent + "- conditions:" + ("\n" + cond_lines(r["conditions"], item_indent + 4, item_indent + 6) if r["conditions"] else " []"))
        lines.append(" " * field_indent + "opens: " + ("1" if r.get("opens") else "0"))
        lines.append(" " * field_indent + "text: " + yaml_str(r.get("text", "")))
    return "\n".join(lines)

def location_block(loc):
    out = ["    - id: " + yaml_str(loc["id"])]
    out.append("      name: " + yaml_str(loc["name"]))
    out.append("      kind: " + str(loc.get("kind", 0)))
    out.append("      sceneKey: " + yaml_str(loc.get("sceneKey", "")))
    out.append("      checkpointId: " + yaml_str(loc.get("checkpointId", "")))
    out.append("      description: " + yaml_str(loc.get("description", "")))
    out.append("      unlockRules:" + ("\n" + gate_rule_lines(loc["unlockRules"], 6, 8) if loc.get("unlockRules") else " []"))
    out.append("      lockedHint: " + yaml_str(loc.get("lockedHint", "")))
    out.append("      entryConditions:" + ("\n" + cond_lines(loc["entryConditions"], 8, 10) if loc.get("entryConditions") else " []"))
    out.append("      connections:" + ("\n" + "\n".join("      - " + yaml_str(x) for x in loc["connections"]) if loc.get("connections") else " []"))
    out.append("      npcs:" + ("\n" + "\n".join("      - " + yaml_str(x) for x in loc["npcs"]) if loc.get("npcs") else " []"))
    out.append("      encounters:" + ("\n" + "\n".join("      - " + yaml_str(x) for x in loc["encounters"]) if loc.get("encounters") else " []"))
    out.append("      objectives:" + ("\n" + "\n".join("      - " + yaml_str(x) for x in loc["objectives"]) if loc.get("objectives") else " []"))
    out.append("      worldStateChanges:" + ("\n" + eff_lines(loc["worldStateChanges"], 8, 10) if loc.get("worldStateChanges") else " []"))
    env = loc.get("environment", {})
    out.append("      environment:")
    out.append("        profile: " + yaml_str(env.get("profile", "")))
    out.append("        ambient: " + yaml_str(env.get("ambient", "")))
    out.append("        fog: " + yaml_str(env.get("fog", "")))
    out.append("        fogDensity: " + str(env.get("fogDensity", 0.015)))
    out.append("        sun: " + yaml_str(env.get("sun", "")))
    out.append("        sunIntensity: " + str(env.get("sunIntensity", 1)))
    return "\n".join(out)

locations_out = ("    locations:\n" + "\n".join(location_block(l) for l in CONTENT["locations"])) if CONTENT.get("locations") else "  locations: []"

ASSET = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: @SCRIPT@, type: 3}
  m_Name: @NAME@
  m_EditorClassIdentifier: 
  data:
    encounters:
@ENCOUNTERS@
    decisions:
@DECISIONS@
    graphs:
@GRAPHS@
@PROGRESSION@
    npcs:
@NPCS@
@OBJECTIVES@
@WORLDINTERACTIONS@
@STATUSEFFECTS@
@ABILITYCOMBAT@
@ENEMIES@
@COMBAT@
@CHAPTERS@
@LOCATIONS@
""".replace("@SCRIPT@", REGISTRY["ScriptableObjectAssets.cs"])
ASSET = ASSET.replace("@NAME@", yaml_str(CONTENT["libraryName"]))
ASSET = ASSET.replace("@ENCOUNTERS@", enc_block)
ASSET = ASSET.replace("@DECISIONS@", decisions_block)
ASSET = ASSET.replace("@GRAPHS@", graphs_block)
ASSET = ASSET.replace("@PROGRESSION@", progression_block)
ASSET = ASSET.replace("@NPCS@", npcs_block)
ASSET = ASSET.replace("@OBJECTIVES@", objectives_block)
ASSET = ASSET.replace("@WORLDINTERACTIONS@", world_interactions_block)
ASSET = ASSET.replace("@STATUSEFFECTS@", status_effects_block)
ASSET = ASSET.replace("@ABILITYCOMBAT@", ability_combat_out)
ASSET = ASSET.replace("@ENEMIES@", enemies_out)
ASSET = ASSET.replace("@COMBAT@", combat_out)
ASSET = ASSET.replace("@CHAPTERS@", chapters_out)
ASSET = ASSET.replace("@LOCATIONS@", locations_out)

DATA_DIR = os.path.join(ROOT, "Assets/_Project/Data/Decisions")
os.makedirs(DATA_DIR, exist_ok=True)
asset_path = os.path.join(DATA_DIR, CONTENT["libraryName"] + ".asset")
open(asset_path, "w").write(ASSET)
if not os.path.exists(asset_path + ".meta"):
    open(asset_path + ".meta", "w").write(NATIVE.format(g=REGISTRY["CL_C1_StoryContent.asset"]))
print("asset ", os.path.relpath(asset_path, ROOT))
print("CONTENT ASSETS GENERATED")
