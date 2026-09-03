"""Static validation for the CROSSROADS decision-system phase (no Unity needed):

  1. GUID integrity: every guid: reference in scene/.asset/.mat files resolves to a .meta
     (or to the built-in/package allowlist). No duplicate guids in the registry.
  2. Story content: the generated CL_C1_StoryContent.asset parses as YAML and matches
     scripts/story_content.json field-for-field.
  3. JSON <-> C# builder consistency: every dialogue/choice string in story_content.json
     appears in StoryContentBuilder.cs (the code-built fallback must stay in sync).
  4. Scene sanity: root count, interactables & the encounter wiring are present.
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
# map name -> guid used by the scene generator (script refs use type 3)
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

# every registry script guid should have a meta file (the moved interaction base included)
for name, g in registry.items():
    if name.endswith(".cs") and g not in meta_guids:
        warns.append("registry script %s has no meta yet (missing source file?)" % name)

# ---------------------------------------------------------------- 2. asset vs JSON
content = json.load(open(os.path.join(HERE, "story_content.json")))
if yaml is None:
    warns.append("pyyaml missing - asset-vs-json check skipped")
else:
    asset_path = os.path.join(is_dir, "CL_C1_StoryContent.asset")
    txt = open(asset_path).read()
    txt = txt.split("\n", 2)[2]                          # drop %YAML / %TAG directives
    txt = txt.replace("--- !u!114 &11400000", "---")
    doc = yaml.safe_load(txt)
    data = doc["MonoBehaviour"]["data"]

    # encounter
    enc = data["encounters"][0]
    for k in ("id", "npcName", "graphId", "startNodeId"):
        if enc[k] != content["encounter"][k]:
            errors.append("encounter.%s: asset=%r json=%r" % (k, enc[k], content["encounter"][k]))

    # decision
    dec = data["decisions"][0]
    jd = content["decision"]
    for k in ("id", "promptText", "codexEntryId"):
        if dec[k] != jd[k]:
            errors.append("decision.%s: asset=%r json=%r" % (k, dec[k], jd[k]))
    if int(dec["timeLimitSeconds"]) != int(jd["timeLimitSeconds"]):
        errors.append("decision.timeLimitSeconds mismatch")
    if len(dec["options"]) != len(jd["options"]):
        errors.append("decision option count mismatch")
    for a, b in zip(dec["options"], jd["options"]):
        for k in ("id", "text", "afterText"):
            if a[k] != b[k]:
                errors.append("option %s.%s: asset=%r json=%r" % (a["id"], k, a[k], b[k]))
        for k in ("type", "key", "value", "amount"):
            ea, eb = a["effects"], b["effects"]
            if len(ea) != len(eb):
                errors.append("option %s effects count mismatch" % a["id"])
                break
            for x, y in zip(ea, eb):
                if x[k] != y[k]:
                    errors.append("option %s effect.%s: asset=%r json=%r" % (a["id"], k, x[k], y[k]))

    # graph
    g = data["graphs"][0]
    if g["id"] != content["dialogue"]["id"]:
        errors.append("graph id mismatch")
    if len(g["nodes"]) != len(content["dialogue"]["nodes"]):
        errors.append("graph node count mismatch")
    for a, b in zip(g["nodes"], content["dialogue"]["nodes"]):
        for k in ("id", "speaker", "text", "nextId", "branchPrefix", "decisionId", "end"):
            if a[k] != b[k]:
                errors.append("node %s.%s: asset=%r json=%r" % (a["id"], k, a[k], b[k]))
        ea, eb = a["conditions"], b["conditions"]
        if len(ea) != len(eb):
            errors.append("node %s conditions count mismatch" % a["id"])
            continue
        for x, y in zip(ea, eb):
            for k in ("type", "key", "value", "amount"):
                if x[k] != y[k]:
                    errors.append("node %s condition.%s: asset=%r json=%r" % (a["id"], k, x[k], y[k]))

# ---------------------------------------------------------------- 3. JSON <-> C# builder
builder = open(os.path.join(ROOT, "Assets/_Project/Scripts/Narrative/Content/StoryContentBuilder.cs")).read()
def walk_strings(o, out):
    if isinstance(o, dict):
        for v in o.values():
            walk_strings(v, out)
    elif isinstance(o, list):
        for v in o:
            walk_strings(v, out)
    elif isinstance(o, str) and len(o) > 12 and not o.startswith("c1_") and not o.startswith("g_") and not o.startswith("dec_") and not o.startswith("CL_"):
        out.append(o)
strings = []
walk_strings({k: v for k, v in content.items() if k != "_comment"}, strings)
for s in strings:
    if s not in builder:
        errors.append("C# builder missing content string: " + s[:64])

# ---------------------------------------------------------------- 4. scene sanity
root_count = len(re.findall(r"^  - \{fileID: \d+\}$", scene_txt.split("SceneRoots:")[-1], re.M)) if "SceneRoots:" in scene_txt else 0
print("Scene roots:", root_count)
for needle in ["Mara_NPC", "StoryWorldState", "StoryModeBootstrap", "GameUIBootstrap",
               "Seq_Ember_Marker", "Seq_Tide_Marker", "Seq_Stone_Marker", "Seq_Tide_Bystanders",
               "m_IsActive: 0", "encounterId: c1_hall_first_light"]:
    if needle not in scene_txt:
        errors.append("scene missing: " + needle)

# component guid wiring on the story objects
for script_key in ["StoryEncounterNPC.cs", "StoryWorldState.cs", "StoryModeBootstrap.cs", "GameUIBootstrap.cs"]:
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
