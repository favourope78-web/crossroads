"""Generates the data-driven story content for CROSSROADS (Phase: decision system):

  1. Assets/_Project/Data/Decisions/CL_C1_StoryContent.asset   (StoryContentLibraryAsset SO)
  2. New URP materials: M_Seq_Ember / M_Seq_Tide / M_Seq_Stone / M_Npc_Mara / M_Npc_Civilian
  3. .meta files for every new C# script + data asset + new folders (guid registry below)
  4. Merges everything into scripts/hall_guids.json (the project's deterministic GUID registry)

Content source of truth: scripts/story_content.json (mirrors Narrative/StoryContentBuilder.cs;
scripts/validate_assets.py cross-checks them). Deterministic GUID scheme c0a1fed2....
Run BEFORE gen_firstlocation_scene.py (it consumes the registry)."""
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REG = os.path.join(HERE, "hall_guids.json")

def g32(n):  # deterministic guid like the existing kit scheme: c0a1fed2 + 24 hex
    return ("c0a1fed2" + ("%024x" % n))[:32]

# ---------------------------------------------------------------- guid allocation
NEW_GUIDS = {
    # materials (kit mats used 0x28..0x2d)
    "M_Seq_Ember":       g32(0x31),
    "M_Seq_Tide":        g32(0x32),
    "M_Seq_Stone":       g32(0x33),
    "M_Npc_Mara":        g32(0x34),
    "M_Npc_Civilian":    g32(0x35),
    # scene-referenced scripts
    "PlayerInteraction.cs": g32(0x55),
    "StoryEncounterNPC.cs": g32(0x56),
    "StoryWorldState.cs":   g32(0x57),
    "StoryModeBootstrap.cs": g32(0x58),
    "GameUIBootstrap.cs":   g32(0x59),
    # SO script carriers (library asset references this file's guid)
    "ScriptableObjectAssets.cs": g32(0x60),
    # UI runtime-only scripts (no scene/asset refs, registry for completeness)
    "SafeAreaFitter.cs": g32(0x63),
    "RuntimeMenuFactory.cs": g32(0x64),
    "InteractionHUD.cs": g32(0x65),
    "DialogueUI.cs": g32(0x66),
    "StateHUD.cs": g32(0x67),
    "ToastUI.cs": g32(0x68),
    # narrative logic scripts (no scene refs)
    "ContentData.cs": g32(0x70),
    "IEncounterSource.cs": g32(0x71),
    "StoryContentBuilder.cs": g32(0x72),
    "RuntimeContentSource.cs": g32(0x73),
    "ConditionEvaluator.cs": g32(0x74),
    "EffectApplier.cs": g32(0x75),
    "DecisionManager.cs": g32(0x76),
    "EncounterFlow.cs": g32(0x77),
    "GameServices.cs": g32(0x78),
    # core scripts
    "GameStateEntries.cs": g32(0x80),
    "GameState.cs": g32(0x81),
    "EventBus.cs": g32(0x82),
    "StoryEvents.cs": g32(0x83),
    "InputLock.cs": g32(0x84),
    "Point3.cs": g32(0x85),
    "ProximitySelector.cs": g32(0x86),
    "AppServices.cs": g32(0x87),
    "SaveData.cs": g32(0x88),
    "SaveSystem.cs": g32(0x89),
    "StoryLog.cs": g32(0x8a),
    "StateMutator.cs": g32(0x8b),
    "UnityJsonSerializer.cs": g32(0x8c),
    "PersistentDataPathProvider.cs": g32(0x8d),
    # data assets
    "CL_C1_StoryContent.asset": g32(0x90),
}

# ---------------------------------------------------------------- registry merge
REGISTRY = {}
if os.path.exists(REG):
    REGISTRY = json.load(open(REG))
# the interaction base moved under Gameplay/Interaction; InteractInput was superseded
REGISTRY.pop("InteractInput.cs", None)
for k, v in NEW_GUIDS.items():
    if k in REGISTRY and REGISTRY[k] != v:
        raise SystemExit("GUID conflict in registry for %s: %s vs %s" % (k, REGISTRY[k], v))
    REGISTRY[k] = v
json.dump(REGISTRY, open(REG, "w"), indent=1)

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
    "PlayerInteraction.cs": "Assets/_Project/Scripts/Gameplay/Interaction",
    "StoryEncounterNPC.cs": "Assets/_Project/Scripts/Gameplay/Interaction",
    "StoryWorldState.cs": "Assets/_Project/Scripts/Gameplay/WorldState",
    "StoryModeBootstrap.cs": "Assets/_Project/Scripts/Gameplay",
    "GameUIBootstrap.cs": "Assets/_Project/Scripts/UI",
    "SafeAreaFitter.cs": "Assets/_Project/Scripts/UI",
    "RuntimeMenuFactory.cs": "Assets/_Project/Scripts/UI",
    "InteractionHUD.cs": "Assets/_Project/Scripts/UI",
    "DialogueUI.cs": "Assets/_Project/Scripts/UI",
    "StateHUD.cs": "Assets/_Project/Scripts/UI",
    "ToastUI.cs": "Assets/_Project/Scripts/UI",
    "ScriptableObjectAssets.cs": "Assets/_Project/Scripts/Narrative/Content",
    "ContentData.cs": "Assets/_Project/Scripts/Narrative/Content",
    "IEncounterSource.cs": "Assets/_Project/Scripts/Narrative/Content",
    "StoryContentBuilder.cs": "Assets/_Project/Scripts/Narrative/Content",
    "RuntimeContentSource.cs": "Assets/_Project/Scripts/Narrative/Content",
    "ConditionEvaluator.cs": "Assets/_Project/Scripts/Narrative",
    "EffectApplier.cs": "Assets/_Project/Scripts/Narrative",
    "DecisionManager.cs": "Assets/_Project/Scripts/Narrative",
    "EncounterFlow.cs": "Assets/_Project/Scripts/Narrative",
    "GameServices.cs": "Assets/_Project/Scripts/Narrative",
    "GameStateEntries.cs": "Assets/_Project/Scripts/Core",
    "GameState.cs": "Assets/_Project/Scripts/Core",
    "EventBus.cs": "Assets/_Project/Scripts/Core",
    "StoryEvents.cs": "Assets/_Project/Scripts/Core",
    "InputLock.cs": "Assets/_Project/Scripts/Core",
    "Point3.cs": "Assets/_Project/Scripts/Core",
    "ProximitySelector.cs": "Assets/_Project/Scripts/Core",
    "AppServices.cs": "Assets/_Project/Scripts/Core",
    "SaveData.cs": "Assets/_Project/Scripts/Core",
    "SaveSystem.cs": "Assets/_Project/Scripts/Core",
    "StoryLog.cs": "Assets/_Project/Scripts/Core",
    "StateMutator.cs": "Assets/_Project/Scripts/Core",
    "UnityJsonSerializer.cs": "Assets/_Project/Scripts/Core",
    "PersistentDataPathProvider.cs": "Assets/_Project/Scripts/Core",
}
for fname, sub in SCRIPT_META_PATHS.items():
    path = os.path.join(ROOT, sub, fname)
    if not os.path.exists(path):
        raise SystemExit("missing script (meta table out of date): " + path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        open(meta, "w").write(MONO.format(g=REGISTRY[fname]))
        print("meta +", os.path.relpath(meta, ROOT))

# folder metas (deterministic md5 like the original generator; only if absent)
import hashlib
for d in ["Assets/_Project/Data/Decisions", "Assets/_Project/Data/Dialogue",
          "Assets/_Project/Scripts/Gameplay/Interaction", "Assets/_Project/Scripts/Gameplay/WorldState",
          "Assets/_Project/Scripts/Narrative/Content"]:
    if not os.path.exists(os.path.join(ROOT, d)):
        raise SystemExit("missing folder: " + d)
    meta = os.path.join(ROOT, d + ".meta")
    if not os.path.exists(meta):
        open(meta, "w").write(FOLDER.format(g=hashlib.md5(("folder:" + d).encode()).hexdigest()))
        print("meta +", os.path.relpath(meta, ROOT))

# ---------------------------------------------------------------- materials
URP = "9335e4a172916944ba2695448482493a"
MATS = os.path.join(ROOT, "Assets/Game/Environment/Materials")
os.makedirs(MATS, exist_ok=True)

def mat_yaml(name, color, smooth, emiss=None, transparent=False):
    kw = []
    if emiss: kw.append("_EMISSION")
    if transparent: kw.append("_SURFACE_TYPE_TRANSPARENT")
    ec = emiss or (0, 0, 0)
    t = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: @NAME@
  m_Shader: {fileID: 4800000, guid: @URP@, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: [@KW@]
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _BumpMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _Cull: 2
    - _Cutoff: 0.5
    - _DstBlend: 0
    - _EnvironmentReflections: 1
    - _GlossinessSource: 0
    - _Metallic: 0
    - _OcclusionStrength: 1
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: @SMOOTH@
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {r: @C0@, g: @C1@, b: @C2@, a: 1}
    - _EmissionColor: {r: @E0@, g: @E1@, b: @E2@, a: 1}
    - _SpecColor: {r: 0.2, g: 0.2, b: 0.2, a: 1}
  m_BuildTextureStacks: []
"""
    return (t.replace("@NAME@", name).replace("@KW@", ", ".join(kw))
             .replace("@URP@", URP).replace("@SMOOTH@", str(smooth))
             .replace("@C0@", str(color[0])).replace("@C1@", str(color[1])).replace("@C2@", str(color[2]))
             .replace("@E0@", str(ec[0])).replace("@E1@", str(ec[1])).replace("@E2@", str(ec[2])))

MATDEFS = {
    "M_Seq_Ember":    ((0.35, 0.06, 0.03), 0.55, (1.0, 0.22, 0.10)),
    "M_Seq_Tide":     ((0.03, 0.16, 0.18), 0.55, (0.10, 0.85, 0.95)),
    "M_Seq_Stone":    ((0.16, 0.11, 0.04), 0.55, (0.95, 0.65, 0.22)),
    "M_Npc_Mara":     ((0.16, 0.35, 0.40), 0.5, (0.08, 0.22, 0.26)),
    "M_Npc_Civilian": ((0.30, 0.32, 0.36), 0.4, None),
}
for name, (base, smooth, emiss) in MATDEFS.items():
    p = os.path.join(MATS, name + ".mat")
    open(p, "w").write(mat_yaml(name, base, smooth, emiss))
    mp = p + ".meta"
    if not os.path.exists(mp):
        open(mp, "w").write(NATIVE.format(g=REGISTRY[name]))
    print("mat   ", name)

# ---------------------------------------------------------------- story content asset
CONTENT = json.load(open(os.path.join(HERE, "story_content.json")))

def yaml_str(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'

def ind_list(items, base_indent, item_indent, field_indent, key_names):
    """Emits a YAML list of mappings: items at item_indent, scalar fields at field_indent."""
    lines = []
    for it in items:
        for i, k in enumerate(key_names):
            v = it[k]
            if isinstance(v, bool):
                v = "1" if v else "0"
            if isinstance(v, (int, float)):
                v = str(v)
            else:
                v = yaml_str(v)
            if i == 0:
                lines.append(" " * item_indent + "- " + k + ": " + v)
            else:
                lines.append(" " * field_indent + k + ": " + v)
    return "\n".join(lines)

def cond_lines(conds, item_indent, field_indent):
    return ind_list(conds, None, item_indent, field_indent, ["type", "key", "value", "amount"])

def eff_lines(effects, item_indent, field_indent):
    return ind_list(effects, None, item_indent, field_indent, ["type", "key", "value", "amount"])

d = CONTENT["decision"]
opt_parts = []
for o in d["options"]:
    opt_parts.append(ind_list([o], None, 6, 8, ["id", "text", "afterText"]))
    opt_parts.append("        conditions:" + ("\n" + cond_lines(o["conditions"], 8, 10) if o["conditions"] else " []"))
    opt_parts.append("        effects:" + ("\n" + eff_lines(o["effects"], 8, 10) if o["effects"] else " []"))
options_block = "\n".join(opt_parts)

g = CONTENT["dialogue"]
node_parts = []
for n in g["nodes"]:
    node_parts.append(ind_list([n], None, 6, 8,
        ["id", "speaker", "text", "nextId", "branchPrefix", "decisionId"]))
    node_parts.append("        conditions:" + ("\n" + cond_lines(n["conditions"], 8, 10) if n["conditions"] else " []"))
    node_parts.append("        end: " + ("1" if n["end"] else "0"))
nodes_block = "\n".join(node_parts)

e = CONTENT["encounter"]
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
    - id: @ENCID@
      npcName: @NPC@
      graphId: @GRAPH@
      startNodeId: @START@
    decisions:
    - id: @DECID@
      promptText: @PROMPT@
      timeLimitSeconds: @TIME@
      timeoutOptionIndex: @TIMEOUT@
      codexEntryId: @CODEX@
      options:
@OPTIONS@
    graphs:
    - id: @GRAPH2@
      nodes:
@NODES@
""".replace("@SCRIPT@", REGISTRY["ScriptableObjectAssets.cs"]).replace("@NAME@", yaml_str(CONTENT["libraryName"])) \
   .replace("@ENCID@", yaml_str(e["id"])).replace("@NPC@", yaml_str(e["npcName"])) \
   .replace("@GRAPH@", yaml_str(e["graphId"])).replace("@START@", yaml_str(e["startNodeId"])) \
   .replace("@DECID@", yaml_str(d["id"])).replace("@PROMPT@", yaml_str(d["promptText"])) \
   .replace("@TIME@", str(d["timeLimitSeconds"])).replace("@TIMEOUT@", str(d["timeoutOptionIndex"])) \
   .replace("@CODEX@", yaml_str(d["codexEntryId"])) \
   .replace("@OPTIONS@", options_block) \
   .replace("@GRAPH2@", yaml_str(g["id"])).replace("@NODES@", nodes_block)

DATA_DIR = os.path.join(ROOT, "Assets/_Project/Data/Decisions")
os.makedirs(DATA_DIR, exist_ok=True)
asset_path = os.path.join(DATA_DIR, CONTENT["libraryName"] + ".asset")
open(asset_path, "w").write(ASSET)
if not os.path.exists(asset_path + ".meta"):
    open(asset_path + ".meta", "w").write(NATIVE.format(g=REGISTRY["CL_C1_StoryContent.asset"]))
print("asset ", os.path.relpath(asset_path, ROOT))
print("CONTENT ASSETS GENERATED")
