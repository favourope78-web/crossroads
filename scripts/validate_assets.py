"""Static validation for the CROSSROADS decision/progression phase (no Unity needed):

  1. GUID integrity: every guid: reference in scene/.asset/.mat files resolves to a .meta
     (or to the built-in/package allowlist). No duplicate guids in the registry.
  2. Story content: the generated CL_C1_StoryContent.asset parses as YAML and matches
     scripts/story_content.json field-for-field (encounters, decisions, graphs, progression).
  3. JSON <-> C# builder consistency: every content string in story_content.json appears in
     StoryContentBuilder.cs (the code-built fallback must stay in sync).
  4. Scene sanity: cast, gate, annex, triggers and story bootstrappers are present.
Run: python3 scripts/validate_assets.py"""
import json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
try:
    import yaml
except ImportError:
    yaml = None

errors, warns = [], []

# ---------------------------------------------------------------- 1. GUID integrity
registry = json.load(open(os.path.join(HERE, "hall_guids.json")))
reg_guids = list(registry.values())
if len(reg_guids) != len(set(reg_guids)):
    errors.append("duplicate GUIDs in hall_guids.json")

meta_guids = {}
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if not f.endswith(".meta"):
            continue
        p = os.path.join(dirpath, f)
        txt = open(p).read()
        m = re.search(r"^guid: ([0-9a-f]{32})", txt, re.M)
        if not m:
            errors.append("meta without guid: " + p)
            continue
        meta_guids[m.group(1)] = os.path.relpath(p, ROOT)

ALLOWED_EXTERNAL = {
    "0000000000000000e000000000000000": "built-in meshes",
    "9335e4a172916944ba2695448482493a": "URP/Lit (package)",
}
all_guids = dict(registry)
all_guids.update(meta_guids)

def check_text_refs(path, label):
    txt = open(path).read()
    refs = re.findall(r"guid: ([0-9a-f]{32})", txt)
    for g in refs:
        if g in ALLOWED_EXTERNAL:
            continue
        if g not in all_guids:
            errors.append("%s: unresolved guid %s" % (label, g))
    return txt

scene_txt = check_text_refs(os.path.join(ROOT, "Assets/Scenes/Prototype/FirstLocation.unity"), "scene")
is_dir = os.path.join(ROOT, "Assets/_Project/Data/Decisions")
for f in sorted(os.listdir(is_dir)):
    if f.endswith(".asset"):
        check_text_refs(os.path.join(is_dir, f), f)
mat_dir = os.path.join(ROOT, "Assets/Game/Environment/Materials")
for f in sorted(os.listdir(mat_dir)):
    if f.endswith(".mat"):
        check_text_refs(os.path.join(mat_dir, f), f)

for name, g in registry.items():
    if name.endswith(".cs") and g not in meta_guids:
        warns.append("registry script %s has no meta yet" % name)

# ---------------------------------------------------------------- 2. asset vs JSON
content = json.load(open(os.path.join(HERE, "story_content.json")))
if yaml is None:
    warns.append("pyyaml missing - asset-vs-json check skipped")
else:
    asset_path = os.path.join(is_dir, "CL_C1_StoryContent.asset")
    txt = open(asset_path).read()
    txt = txt.split("\n", 2)[2].replace("--- !u!114 &11400000", "---")
    try:
        doc = yaml.safe_load(txt)
    except Exception as e:
        errors.append("asset YAML parse failed: " + str(e).split("\n")[0])
        doc = None
    if doc is not None:
        data = doc["MonoBehaviour"]["data"]

        # encounters
        if len(data["encounters"]) != len(content["encounters"]):
            errors.append("encounter count mismatch")
        for a, b in zip(data["encounters"], content["encounters"]):
            for k in ("id", "npcName", "graphId", "startNodeId"):
                if a[k] != b[k]:
                    errors.append("encounter %s: %s asset=%r json=%r" % (a["id"], k, a[k], b[k]))

        # decisions
        if len(data["decisions"]) != len(content["decisions"]):
            errors.append("decision count mismatch")
        for a, b in zip(data["decisions"], content["decisions"]):
            for k in ("id", "promptText", "codexEntryId"):
                if a[k] != b[k]:
                    errors.append("decision %s: %s asset=%r json=%r" % (a["id"], k, a[k], b[k]))
            if int(a["timeLimitSeconds"]) != int(b["timeLimitSeconds"]):
                errors.append("decision %s timeLimit mismatch" % a["id"])
            if len(a["options"]) != len(b["options"]):
                errors.append("decision %s option count mismatch" % a["id"])
            for oa, ob in zip(a["options"], b["options"]):
                for k in ("id", "text", "afterText"):
                    if oa[k] != ob[k]:
                        errors.append("option %s.%s: asset=%r json=%r" % (oa["id"], k, oa[k], ob[k]))
                for lst_a, lst_b, lbl in ((oa["effects"], ob["effects"], "effects"), (oa["conditions"], ob["conditions"], "conditions")):
                    if len(lst_a) != len(lst_b):
                        errors.append("option %s %s count mismatch" % (oa["id"], lbl))
                        continue
                    for x, y in zip(lst_a, lst_b):
                        for k in ("type", "key", "value", "amount"):
                            if x[k] != y[k]:
                                errors.append("option %s %s.%s: asset=%r json=%r" % (oa["id"], lbl, k, x[k], y[k]))

        # graphs
        if len(data["graphs"]) != len(content["graphs"]):
            errors.append("graph count mismatch")
        for ga, gb in zip(data["graphs"], content["graphs"]):
            if ga["id"] != gb["id"]:
                errors.append("graph id mismatch")
            if len(ga["nodes"]) != len(gb["nodes"]):
                errors.append("graph %s node count mismatch" % ga["id"])
            for na, nb in zip(ga["nodes"], gb["nodes"]):
                for k in ("id", "speaker", "text", "nextId", "branchPrefix", "decisionId", "end"):
                    if na[k] != nb[k]:
                        errors.append("node %s.%s: asset=%r json=%r" % (na["id"], k, na[k], nb[k]))
                if len(na["conditions"]) != len(nb["conditions"]):
                    errors.append("node %s conditions count mismatch" % na["id"])
                    continue
                for x, y in zip(na["conditions"], nb["conditions"]):
                    for k in ("type", "key", "value", "amount"):
                        if x[k] != y[k]:
                            errors.append("node %s condition.%s: asset=%r json=%r" % (na["id"], k, x[k], y[k]))

        # progression
        p_asset, p_json = data["progression"], content["progression"]
        for group in ("abilities", "skills", "items", "reputationGroups", "areas"):
            ka = {"abilities": ["id", "name", "line", "description"], "skills": ["id", "name", "maxLevel"],
                  "items": ["id", "name", "description"], "reputationGroups": ["id", "name"],
                  "areas": ["id", "name"]}[group]
            if len(p_asset[group]) != len(p_json[group]):
                errors.append("progression %s count mismatch" % group)
            for ra, rb in zip(p_asset[group], p_json[group]):
                for k in ka:
                    if str(ra[k]) != str(rb[k]):
                        errors.append("progression %s.%s: asset=%r json=%r" % (group, k, ra[k], rb[k]))

        # npcs (data-driven NPC definitions)
        def _n(v):
            if isinstance(v, bool): return str(int(v))
            return str(v)
        if len(data["npcs"]) != len(content["npcs"]):
            errors.append("npc count mismatch")
        for na, nb in zip(data["npcs"], content["npcs"]):
            for k in ("id", "displayName", "sheetRef", "description"):
                if na[k] != nb[k]:
                    errors.append("npc %s: %s asset=%r json=%r" % (na.get("id"), k, na[k], nb[k]))
            ba, bb = na["behaviour"], nb["behaviour"]
            for k in ("personality", "facesPlayer", "reactRadius", "approachDistance", "avoidDistance",
                      "talkDistance", "moveSpeed", "turnSpeed", "usesRoutine"):
                if _n(ba[k]) != _n(bb[k]):
                    errors.append("npc %s.behaviour.%s: asset=%r json=%r" % (nb["id"], k, ba[k], bb[k]))
            if len(na["states"]) != len(nb["states"]):
                errors.append("npc %s state count mismatch" % nb["id"])
            for sa, sb in zip(na["states"], nb["states"]):
                for k in ("title", "moodLine", "approachDistance", "avoidDistance", "moveSpeed", "reactRadius"):
                    if _n(sa[k]) != _n(sb[k]):
                        errors.append("npc %s state %s: asset=%r json=%r" % (nb["id"], k, sa[k], sb[k]))
                if len(sa["conditions"]) != len(sb["conditions"]):
                    errors.append("npc %s state conditions count mismatch" % nb["id"])
                    continue
                for x, y in zip(sa["conditions"], sb["conditions"]):
                    for k in ("type", "key", "value", "amount"):
                        if _n(x[k]) != _n(y[k]):
                            errors.append("npc %s state cond.%s: asset=%r json=%r" % (nb["id"], k, x[k], y[k]))
            if len(na["interactions"]) != len(nb["interactions"]):
                errors.append("npc %s interaction count mismatch" % nb["id"])
            for ia, ib in zip(na["interactions"], nb["interactions"]):
                for k in ("id", "label", "encounterId"):
                    if ia[k] != ib[k]:
                        errors.append("npc %s interaction %s: asset=%r json=%r" % (nb["id"], k, ia[k], ib[k]))
                if len(ia["conditions"]) != len(ib["conditions"]):
                    errors.append("npc %s interaction %s conditions count mismatch" % (nb["id"], ib["id"]))
                    continue
                for x, y in zip(ia["conditions"], ib["conditions"]):
                    for k in ("type", "key", "value", "amount"):
                        if _n(x[k]) != _n(y[k]):
                            errors.append("npc %s interaction %s cond.%s: asset=%r json=%r" % (nb["id"], ib["id"], k, x[k], y[k]))
            if len(na["routine"]) != len(nb["routine"]):
                errors.append("npc %s routine count mismatch" % nb["id"])
            for ra, rb in zip(na["routine"], nb["routine"]):
                if json.dumps(ra["position"], sort_keys=True) != json.dumps(rb["position"], sort_keys=True):
                    errors.append("npc %s routine position: asset=%r json=%r" % (nb["id"], ra["position"], rb["position"]))
                if _n(ra["dwellSeconds"]) != _n(rb["dwellSeconds"]):
                    errors.append("npc %s routine dwell: asset=%r json=%r" % (nb["id"], ra["dwellSeconds"], rb["dwellSeconds"]))

# ---------------------------------------------------------------- 3. JSON <-> C# builder
builder = open(os.path.join(ROOT, "Assets/_Project/Scripts/Narrative/Content/StoryContentBuilder.cs")).read()
def walk_strings(o, out):
    if isinstance(o, dict):
        for v in o.values():
            walk_strings(v, out)
    elif isinstance(o, list):
        for v in o:
            walk_strings(v, out)
    elif isinstance(o, str) and len(o) > 12 and not (o.startswith("c1_") or o.startswith("g_") or o.startswith("dec_") or o.startswith("CL_")):
        out.append(o)
strings = []
walk_strings({k: v for k, v in content.items() if k != "_comment"}, strings)
for s in strings:
    if s not in builder and s.encode("ascii", "backslashreplace").decode() not in builder:
        errors.append("C# builder missing content string: " + s[:64])

# ---------------------------------------------------------------- 4. scene sanity
root_count = len(re.findall(r"^  - \{fileID: \d+\}$", scene_txt.split("SceneRoots:")[-1], re.M)) if "SceneRoots:" in scene_txt else 0
print("Scene roots:", root_count)
for needle in ["Mara_NPC", "Sera_NPC", "EchoShard", "EnergySeal",
               "Seq_Ember_Marker", "Seq_Tide_Marker", "Seq_Stone_Marker", "Seq_Tide_Bystanders",
               "AreaTrigger_Annex", "AreaTrigger_Hall", "SM_WallPanel_flank", "m_IsActive: 0",
               "npcId: mara", "npcId: sera",
               "encounterId: c1_hall_shard",
               "areaId: annex", "defaultActive: 1", "m_IsTrigger: 1"]:
    if needle not in scene_txt:
        errors.append("scene missing: " + needle)

for script_key in ["NpcAgent.cs", "NpcInteractable.cs", "StoryWorldState.cs", "StoryModeBootstrap.cs",
                   "GameUIBootstrap.cs", "AreaGate.cs", "StoryEventInteractable.cs", "AreaTrigger.cs"]:
    if scene_txt.count(registry[script_key]) == 0:
        errors.append("scene does not reference %s" % script_key)

print("=" * 60)
if errors:
    for e in errors:
        print("ERROR:", e)
    print("FAILED with %d error(s)" % len(errors))
    sys.exit(1)
for w in warns:
    print("warn:", w)
print("VALIDATION PASSED (%d warnings)" % len(warns))
