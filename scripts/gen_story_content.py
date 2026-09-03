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
}
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

# ---- progression ----
p = CONTENT["progression"]
def prog_block(name, items, keys):
    return "      " + name + ":\n" + ind_list(items, 8, 10, keys) if items else "      " + name + ": []"
prog_parts = [
    "    progression:",
    prog_block("abilities", p["abilities"], ["id", "name", "line", "description"]),
    prog_block("skills", p["skills"], ["id", "name", "maxLevel"]),
    prog_block("items", p["items"], ["id", "name", "description"]),
    prog_block("reputationGroups", p["reputationGroups"], ["id", "name"]),
    prog_block("areas", p["areas"], ["id", "name"]),
]
progression_block = "\n".join(prog_parts)

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
""".replace("@SCRIPT@", REGISTRY["ScriptableObjectAssets.cs"])
ASSET = ASSET.replace("@NAME@", yaml_str(CONTENT["libraryName"]))
ASSET = ASSET.replace("@ENCOUNTERS@", enc_block)
ASSET = ASSET.replace("@DECISIONS@", decisions_block)
ASSET = ASSET.replace("@GRAPHS@", graphs_block)
ASSET = ASSET.replace("@PROGRESSION@", progression_block)

DATA_DIR = os.path.join(ROOT, "Assets/_Project/Data/Decisions")
os.makedirs(DATA_DIR, exist_ok=True)
asset_path = os.path.join(DATA_DIR, CONTENT["libraryName"] + ".asset")
open(asset_path, "w").write(ASSET)
if not os.path.exists(asset_path + ".meta"):
    open(asset_path + ".meta", "w").write(NATIVE.format(g=REGISTRY["CL_C1_StoryContent.asset"]))
print("asset ", os.path.relpath(asset_path, ROOT))
print("CONTENT ASSETS GENERATED")
