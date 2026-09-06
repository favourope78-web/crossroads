"""Static validation for the CROSSROADS decision/progression phase (no Unity needed):

  1. GUID integrity: every guid: reference in scene/.asset/.mat files resolves to a .meta
     (or to the built-in/package allowlist). No duplicate guids in the registry.
  2. Story content: the generated CL_C1_StoryContent.asset parses as YAML and matches
     scripts/story_content.json field-for-field (encounters, decisions, graphs, progression).
  3. JSON <-> C# builder consistency: every content string in story_content.json appears in
     StoryContentBuilder.cs (the code-built fallback must stay in sync).
  4. Scene sanity: cast, gate, annex, triggers and story bootstrappers are present.
Run: python3 scripts/validate_assets.py"""
import glob, json, os, re, sys

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
    "0000000000000000f000000000000000": "built-in shaders",
    "933532a4fcc9baf4fa0491de14d08ed7": "URP/Lit (package)",
    "650dd9526735d5b46b79224bc6e94025": "URP/Unlit (package)",
    "8d2bb70cbf9db8d4da26e15b26e74248": "URP/SimpleLit (package)",
    "0406db5a14f94604a8c57ccfbc9f3b46": "URP/Particles/Unlit (package)",
    # render pipeline scripts + data referenced by Assets/Settings/* and the scene camera/volume
    "bf2edee5c58d82540a51f03df9d42094": "UniversalRenderPipelineAsset.cs",
    "de640fe3d0db1804a85f9fc8f5cadab6": "UniversalRendererData.cs",
    "2ec995e51a6e251468d2a3fd8a686257": "UniversalRenderPipelineGlobalSettings.cs",
    "41439944d30ece34e96484bdb6645b55": "PostProcessData.asset (package)",
    "d7fd9488000d3734a9e00ee676215985": "VolumeProfile.cs",
    "172515602e62fb746b5d573b38a5fe58": "Volume.cs",
    "0b2db86121404754db890f4c8dfe81b2": "Bloom.cs",
    "899c54efeace73346a0a16faa3afe726": "Vignette.cs",
    "66f335fb1ffd8684294ad653bf1c7564": "ColorAdjustments.cs",
    "97c23e3b12dc18c42a140437e53d3951": "Tonemapping.cs",
    "a79441f348de89743a2939f4d699eac1": "UniversalAdditionalCameraData.cs",
    "474bcb49853aa07438625e644c072ee6": "UniversalAdditionalLightData.cs",
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

set_dir = os.path.join(ROOT, "Assets/Settings")
if os.path.isdir(set_dir):
    for f in sorted(os.listdir(set_dir)):
        if f.endswith(".asset"):
            check_text_refs(os.path.join(set_dir, f), "Settings/" + f)
for pf in ["ProjectSettings/GraphicsSettings.asset", "ProjectSettings/ProjectSettings.asset", "ProjectSettings/QualitySettings.asset"]:
    check_text_refs(os.path.join(ROOT, pf), pf)
# one Unity settings object per file with its real class id (the editor discards mis-tagged docs)
for pf, cls in (("ProjectSettings/ProjectSettings.asset", "129"), ("ProjectSettings/QualitySettings.asset", "47"),
                ("ProjectSettings/GraphicsSettings.asset", "30"), ("ProjectSettings/DynamicsManager.asset", "55"),
                ("ProjectSettings/EditorBuildSettings.asset", "1045")):
    txt = open(os.path.join(ROOT, pf)).read()
    heads = re.findall(r"^--- !u!(\d+) &1", txt, re.M)
    if heads != [cls]:
        errors.append("%s must contain exactly one !u!%s document (found %s)" % (pf, cls, heads))

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
        def _same(a, b):
            # numeric-tolerant compare: 12 == 12.0 (YAML keeps ints, JSON may carry floats)
            if isinstance(a, bool) or isinstance(b, bool): return str(a) == str(b)
            try: return float(a) == float(b)
            except (TypeError, ValueError): return str(a) == str(b)

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
                if group == "abilities":
                    # full power-system parity: category, hint, refs, cost, conditions, level rows
                    for k in ("category", "unlockHint", "vfxRef", "sfxRef", "echoCostPerLevel"):
                        if str(ra.get(k)) != str(rb.get(k)):
                            errors.append("ability %s.%s: asset=%r json=%r" % (rb.get("id"), k, ra.get(k), rb.get(k)))
                    if len(ra.get("unlockConditions", [])) != len(rb.get("unlockConditions", [])):
                        errors.append("ability %s unlockConditions count mismatch" % rb.get("id"))
                    else:
                        for x, y in zip(ra["unlockConditions"], rb["unlockConditions"]):
                            for k in ("type", "key", "value", "amount"):
                                if not _same(x.get(k), y.get(k)):
                                    errors.append("ability %s unlockCond.%s: asset=%r json=%r" % (rb.get("id"), k, x.get(k), y.get(k)))
                    if len(ra.get("levels", [])) != len(rb.get("levels", [])):
                        errors.append("ability %s level count mismatch" % rb.get("id"))
                    else:
                        for la, lb in zip(ra["levels"], rb["levels"]):
                            for k in ("level", "cooldown", "power", "radius", "duration", "energyCost"):
                                if not _same(la.get(k), lb.get(k)):
                                    errors.append("ability %s Lv%s.%s: asset=%r json=%r" % (rb.get("id"), lb.get("level"), k, la.get(k), lb.get(k)))
                            if (la.get("description") or "") != (lb.get("description") or ""):
                                errors.append("ability %s Lv%s description mismatch" % (rb.get("id"), lb.get("level")))

        # objectives (data-driven mission system, Gameplay/World)
        if len(data.get("objectives", [])) != len(content.get("objectives", [])):
            errors.append("objective count mismatch")
        for oa, ob in zip(data.get("objectives", []), content.get("objectives", [])):
            for k in ("id", "title", "description", "type", "areaId", "giverNpcId",
                      "counterVar", "counterTarget", "counterText",
                      "completionNotice", "failureNotice"):
                if str(oa.get(k)) != str(ob.get(k)):
                    errors.append("objective %s: %s asset=%r json=%r" % (ob.get("id"), k, oa.get(k), ob.get(k)))
            if int(oa.get("autoActivate", 1)) != int(ob.get("autoActivate", 1)):
                errors.append("objective %s autoActivate mismatch" % ob.get("id"))
            for lst_a, lst_b, lbl in ((oa["offerConditions"], ob["offerConditions"], "offerConditions"),
                                      (oa["completeConditions"], ob["completeConditions"], "completeConditions"),
                                      (oa["failConditions"], ob["failConditions"], "failConditions"),
                                      (oa["consequences"], ob["consequences"], "consequences"),
                                      (oa["failureConsequences"], ob["failureConsequences"], "failureConsequences")):
                if len(lst_a) != len(lst_b):
                    errors.append("objective %s %s count mismatch" % (ob.get("id"), lbl))
                    continue
                for x, y in zip(lst_a, lst_b):
                    for k in ("type", "key", "value", "amount"):
                        if not _same(x.get(k), y.get(k)):
                            errors.append("objective %s %s.%s: asset=%r json=%r" % (ob.get("id"), lbl, k, x.get(k), y.get(k)))
            if len(oa.get("steps", [])) != len(ob.get("steps", [])):
                errors.append("objective %s step count mismatch" % ob.get("id"))
            else:
                for sa, sb in zip(oa.get("steps", []), ob.get("steps", [])):
                    if sa.get("text", "") != sb.get("text", ""):
                        errors.append("objective %s step text mismatch" % ob.get("id"))
                    if len(sa.get("conditions", [])) != len(sb.get("conditions", [])):
                        errors.append("objective %s step conditions count mismatch" % ob.get("id"))
                    else:
                        for x, y in zip(sa.get("conditions", []), sb.get("conditions", [])):
                            for k in ("type", "key", "value", "amount"):
                                if not _same(x.get(k), y.get(k)):
                                    errors.append("objective %s step cond.%s: asset=%r json=%r" % (ob.get("id"), k, x.get(k), y.get(k)))
            if [f for f in oa.get("followUps", [])] != [f for f in ob.get("followUps", [])]:
                errors.append("objective %s followUps mismatch" % ob.get("id"))

        # world interactions (unlock registry)
        if len(data.get("worldInteractions", [])) != len(content.get("worldInteractions", [])):
            errors.append("worldInteraction count mismatch")
        for wa, wb in zip(data.get("worldInteractions", []), content.get("worldInteractions", [])):
            for k in ("key", "label"):
                if wa.get(k) != wb.get(k):
                    errors.append("worldInteraction %s: %s asset=%r json=%r" % (wb.get("key"), k, wa.get(k), wb.get(k)))
            if len(wa.get("conditions", [])) != len(wb.get("conditions", [])):
                errors.append("worldInteraction %s conditions count mismatch" % wb.get("key"))
                continue
            for x, y in zip(wa.get("conditions", []), wb.get("conditions", [])):
                for k in ("type", "key", "value", "amount"):
                    if not _same(x.get(k), y.get(k)):
                        errors.append("worldInteraction %s cond.%s: asset=%r json=%r" % (wb.get("key"), k, x.get(k), y.get(k)))

        # combat: status effects
        if len(data.get("statusEffects", [])) != len(content.get("statusEffects", [])):
            errors.append("statusEffect count mismatch")
        for sa, sb in zip(data.get("statusEffects", []), content.get("statusEffects", [])):
            for k in ("id", "name", "description"):
                if sa.get(k) != sb.get(k):
                    errors.append("statusEffect %s: %s asset=%r json=%r" % (sb.get("id"), k, sa.get(k), sb.get(k)))
            for k in ("durationSeconds", "tickIntervalSeconds", "healthPerTick",
                      "moveSpeedMultiplier", "attackRateMultiplier"):
                if not _same(sa.get(k), sb.get(k)):
                    errors.append("statusEffect %s: %s asset=%r json=%r" % (sb.get("id"), k, sa.get(k), sb.get(k)))
            if int(bool(sa.get("grantsImmunity"))) != int(bool(sb.get("grantsImmunity"))):
                errors.append("statusEffect %s grantsImmunity mismatch" % sb.get("id"))

        # combat: ability combat payloads
        if len(data.get("abilityCombat", [])) != len(content.get("abilityCombat", [])):
            errors.append("abilityCombat count mismatch")
        for aa, ab in zip(data.get("abilityCombat", []), content.get("abilityCombat", [])):
            for k in ("abilityId", "damageType"):
                if not _same(aa.get(k), ab.get(k)):
                    errors.append("abilityCombat %s: %s asset=%r json=%r" % (ab.get("abilityId"), k, aa.get(k), ab.get(k)))
            for k in ("damagePerPower", "healPlayerPerPower"):
                if not _same(aa.get(k), ab.get(k)):
                    errors.append("abilityCombat %s: %s asset=%r json=%r" % (ab.get("abilityId"), k, aa.get(k), ab.get(k)))
            for lst_a, lst_b, lbl in ((aa.get("applyStatusToTargets", []), ab.get("applyStatusToTargets", []), "applyStatusToTargets"),
                                      (aa.get("applyStatusToPlayer", []), ab.get("applyStatusToPlayer", []), "applyStatusToPlayer")):
                if [str(x) for x in lst_a] != [str(x) for x in lst_b]:
                    errors.append("abilityCombat %s %s mismatch" % (ab.get("abilityId"), lbl))

        # combat: enemy archetypes (nested attack + resistances + conditions/effects)
        if len(data.get("enemies", [])) != len(content.get("enemies", [])):
            errors.append("enemy count mismatch")
        for ea, eb in zip(data.get("enemies", []), content.get("enemies", [])):
            for k in ("id", "displayName", "description", "sheetRef"):
                if ea.get(k) != eb.get(k):
                    errors.append("enemy %s: %s asset=%r json=%r" % (eb.get("id"), k, ea.get(k), eb.get(k)))
            for k in ("maxHealth", "defense", "moveSpeed", "turnSpeed", "detectionRadius",
                      "leashRadius", "attackRange", "staggerSeconds"):
                if not _same(ea.get(k), eb.get(k)):
                    errors.append("enemy %s: %s asset=%r json=%r" % (eb.get("id"), k, ea.get(k), eb.get(k)))
            ra_, rb_ = ea.get("resistances", []), eb.get("resistances", [])
            if len(ra_) != len(rb_):
                errors.append("enemy %s resistance count mismatch" % eb.get("id"))
            else:
                for x, y in zip(ra_, rb_):
                    if not _same(x.get("type"), y.get("type")) or not _same(x.get("multiplier"), y.get("multiplier")):
                        errors.append("enemy %s resistance row mismatch" % eb.get("id"))
            ka_, kb_ = ea.get("attack", {}), eb.get("attack", {})
            for k in ("id", "name", "damageType", "delivery", "baseDamage", "range", "arcDegrees",
                      "radius", "windupSeconds", "cooldownSeconds"):
                if not _same(ka_.get(k), kb_.get(k)):
                    errors.append("enemy %s attack.%s: asset=%r json=%r" % (eb.get("id"), k, ka_.get(k), kb_.get(k)))
            if [str(x) for x in ka_.get("applyStatusIds", [])] != [str(x) for x in kb_.get("applyStatusIds", [])]:
                errors.append("enemy %s attack.applyStatusIds mismatch" % eb.get("id"))
            for lst_a, lst_b, lbl in ((ea.get("activationConditions", []), eb.get("activationConditions", []), "activationConditions"),
                                      (ea.get("onDefeatEffects", []), eb.get("onDefeatEffects", []), "onDefeatEffects")):
                if len(lst_a) != len(lst_b):
                    errors.append("enemy %s %s count mismatch" % (eb.get("id"), lbl))
                    continue
                for x, y in zip(lst_a, lst_b):
                    for k in ("type", "key", "value", "amount"):
                        if not _same(x.get(k), y.get(k)):
                            errors.append("enemy %s %s.%s: asset=%r json=%r" % (eb.get("id"), lbl, k, x.get(k), y.get(k)))

        # combat: player settings (basic attack + dodge + defeat policy)
        ca, cb = data.get("combat"), content.get("combat")
        if ca is None or cb is None:
            errors.append("combat settings missing from asset or json")
        else:
            for k in ("playerMaxHealth", "playerDefense", "dodgeDistance", "dodgeDurationSeconds",
                      "dodgeCooldownSeconds", "dodgeStatusId", "healthVarKey"):
                if not _same(ca.get(k), cb.get(k)):
                    errors.append("combat settings %s: asset=%r json=%r" % (k, ca.get(k), cb.get(k)))
            pra, prb = ca.get("playerResistances", []), cb.get("playerResistances", [])
            if len(pra) != len(prb):
                errors.append("combat playerResistances count mismatch")
            else:
                for x, y in zip(pra, prb):
                    if not _same(x.get("type"), y.get("type")) or not _same(x.get("multiplier"), y.get("multiplier")):
                        errors.append("combat playerResistances row mismatch")
            baa, bab = ca.get("basicAttack", {}), cb.get("basicAttack", {})
            for k in ("id", "name", "damageType", "delivery", "baseDamage", "range", "arcDegrees",
                      "radius", "windupSeconds", "cooldownSeconds"):
                if not _same(baa.get(k), bab.get(k)):
                    errors.append("combat basicAttack.%s: asset=%r json=%r" % (k, baa.get(k), bab.get(k)))
            oda, odb = ca.get("onPlayerDefeat", []), cb.get("onPlayerDefeat", [])
            if len(oda) != len(odb):
                errors.append("combat onPlayerDefeat count mismatch")
            else:
                for x, y in zip(oda, odb):
                    for k in ("type", "key", "value", "amount"):
                        if not _same(x.get(k), y.get(k)):
                            errors.append("combat onPlayerDefeat.%s: asset=%r json=%r" % (k, x.get(k), y.get(k)))

        # chapters (branching campaign): full beat/branch parity + reference integrity
        if len(data.get("chapters", [])) != len(content.get("chapters", [])):
            errors.append("chapter count mismatch")
        beat_ids = set()
        for cha, chb in zip(data.get("chapters", []), content.get("chapters", [])):
            for k in ("id", "title", "subtitle", "description", "completionJournal"):
                if cha.get(k) != chb.get(k):
                    errors.append("chapter %s: %s asset=%r json=%r" % (chb.get("id"), k, cha.get(k), chb.get(k)))
            for lst_a, lst_b, lbl in ((cha.get("entryConditions", []), chb.get("entryConditions", []), "entryConditions"),
                                      (cha.get("completionConditions", []), chb.get("completionConditions", []), "completionConditions")):
                if len(lst_a) != len(lst_b):
                    errors.append("chapter %s %s count mismatch" % (chb.get("id"), lbl))
                    continue
                for x, y in zip(lst_a, lst_b):
                    for k in ("type", "key", "value", "amount"):
                        if not _same(x.get(k), y.get(k)):
                            errors.append("chapter %s %s.%s: asset=%r json=%r" % (chb.get("id"), lbl, k, x.get(k), y.get(k)))
            ca_, cb_ = cha.get("completionEffects", []), chb.get("completionEffects", [])
            if len(ca_) != len(cb_):
                errors.append("chapter %s completionEffects count mismatch" % chb.get("id"))
            else:
                for x, y in zip(ca_, cb_):
                    for k in ("type", "key", "value", "amount"):
                        if not _same(x.get(k), y.get(k)):
                            errors.append("chapter %s completionEffects.%s: asset=%r json=%r" % (chb.get("id"), k, x.get(k), y.get(k)))
            if len(cha.get("beats", [])) != len(chb.get("beats", [])):
                errors.append("chapter %s beat count mismatch" % chb.get("id"))
            for ba, bb in zip(cha.get("beats", []), chb.get("beats", [])):
                beat_ids.add(bb.get("id", ""))
                for k in ("id", "title", "journalText", "resolveTrigger", "resolveKey", "priority"):
                    if not _same(ba.get(k), bb.get(k)):
                        errors.append("beat %s: %s asset=%r json=%r" % (bb.get("id"), k, ba.get(k), bb.get(k)))
                if [str(x) for x in ba.get("requiredBeatIds", [])] != [str(x) for x in bb.get("requiredBeatIds", [])]:
                    errors.append("beat %s requiredBeatIds mismatch" % bb.get("id"))
                for lst_a, lst_b, lbl in ((ba.get("offerConditions", []), bb.get("offerConditions", []), "offerConditions"),
                                          (ba.get("resolveConditions", []), bb.get("resolveConditions", []), "resolveConditions"),
                                          (ba.get("onResolveEffects", []), bb.get("onResolveEffects", []), "onResolveEffects")):
                    if len(lst_a) != len(lst_b):
                        errors.append("beat %s %s count mismatch" % (bb.get("id"), lbl))
                        continue
                    for x, y in zip(lst_a, lst_b):
                        for k in ("type", "key", "value", "amount"):
                            if not _same(x.get(k), y.get(k)):
                                errors.append("beat %s %s.%s: asset=%r json=%r" % (bb.get("id"), lbl, k, x.get(k), y.get(k)))
            if len(cha.get("branches", [])) != len(chb.get("branches", [])):
                errors.append("chapter %s branch count mismatch" % chb.get("id"))
            for xa, xb in zip(cha.get("branches", []), chb.get("branches", [])):
                for k in ("id", "fromBeatId", "toBeatId", "label"):
                    if xa.get(k) != xb.get(k):
                        errors.append("branch %s: %s asset=%r json=%r" % (xb.get("id"), k, xa.get(k), xb.get(k)))
                for lst_a, lst_b, lbl in ((xa.get("requiredConditions", []), xb.get("requiredConditions", []), "requiredConditions"),
                                          (xa.get("effects", []), xb.get("effects", []), "effects")):
                    if len(lst_a) != len(lst_b):
                        errors.append("branch %s %s count mismatch" % (xb.get("id"), lbl))
                        continue
                    for x, y in zip(lst_a, lst_b):
                        for k in ("type", "key", "value", "amount"):
                            if not _same(x.get(k), y.get(k)):
                                errors.append("branch %s %s.%s: asset=%r json=%r" % (xb.get("id"), lbl, k, x.get(k), y.get(k)))

        # campaign reference integrity (json side): branches point at real beats
        for chb in content.get("chapters", []):
            for xb in chb.get("branches", []):
                if xb.get("fromBeatId", "") not in beat_ids:
                    errors.append("branch %s: fromBeatId %r is not a beat" % (xb.get("id"), xb.get("fromBeatId")))
                if xb.get("toBeatId", "") and xb.get("toBeatId") not in beat_ids:
                    errors.append("branch %s: toBeatId %r is not a beat" % (xb.get("id"), xb.get("toBeatId")))
            for bb in chb.get("beats", []):
                for rb in bb.get("requiredBeatIds", []):
                    if rb not in beat_ids:
                        errors.append("beat %s: requiredBeatId %r is not a beat" % (bb.get("id"), rb))

        # locations (world expansion): full parity + reference integrity
        if len(data.get("locations", [])) != len(content.get("locations", [])):
            errors.append("location count mismatch")
        loc_ids, seen_kinds = set(), set()
        enc_ids = set(e["id"] for e in content["encounters"])
        obj_ids = set(o["id"] for o in content["objectives"])
        npc_ids = set(n["id"] for n in content["npcs"])
        for la, lb in zip(data.get("locations", []), content.get("locations", [])):
            for k in ("id", "name", "kind", "sceneKey", "checkpointId", "description", "lockedHint"):
                if str(la.get(k)) != str(lb.get(k)):
                    errors.append("location %s: %s asset=%r json=%r" % (lb.get("id"), k, la.get(k), lb.get(k)))
            loc_ids.add(lb.get("id", ""))
            seen_kinds.add(lb.get("kind"))
            if not lb.get("sceneKey") or not lb.get("checkpointId"):
                errors.append("location %s: empty sceneKey/checkpointId" % lb.get("id"))
            ra_, rb_ = la.get("unlockRules", []), lb.get("unlockRules", [])
            if len(ra_) != len(rb_):
                errors.append("location %s unlockRules count mismatch" % lb.get("id"))
            else:
                for xa, xb in zip(ra_, rb_):
                    if bool(xa.get("opens")) != bool(xb.get("opens")):
                        errors.append("location %s unlockRules.opens: asset=%r json=%r" % (lb.get("id"), xa.get("opens"), xb.get("opens")))
                    if (xa.get("text") or "") != (xb.get("text") or ""):
                        errors.append("location %s unlockRules.text: asset=%r json=%r" % (lb.get("id"), xa.get("text"), xb.get("text")))
                    if len(xa.get("conditions", [])) != len(xb.get("conditions", [])):
                        errors.append("location %s unlock rule conditions count mismatch" % lb.get("id"))
                        continue
                    for x, y in zip(xa.get("conditions", []), xb.get("conditions", [])):
                        for k in ("type", "key", "value", "amount"):
                            if not _same(x.get(k), y.get(k)):
                                errors.append("location %s unlock rule cond.%s: asset=%r json=%r" % (lb.get("id"), k, x.get(k), y.get(k)))
            for lst_a, lst_b, lbl in ((la.get("entryConditions", []), lb.get("entryConditions", []), "entryConditions"),):
                if len(lst_a) != len(lst_b):
                    errors.append("location %s %s count mismatch" % (lb.get("id"), lbl))
                else:
                    for x, y in zip(lst_a, lst_b):
                        for k in ("type", "key", "value", "amount"):
                            if not _same(x.get(k), y.get(k)):
                                errors.append("location %s %s.%s: asset=%r json=%r" % (lb.get("id"), lbl, k, x.get(k), y.get(k)))
            for k in ("connections", "npcs", "encounters", "objectives"):
                if [str(v) for v in la.get(k, [])] != [str(v) for v in lb.get(k, [])]:
                    errors.append("location %s %s list mismatch: asset=%r json=%r" % (lb.get("id"), k, la.get(k), lb.get(k)))
            ca_, cb_ = la.get("worldStateChanges", []), lb.get("worldStateChanges", [])
            if len(ca_) != len(cb_):
                errors.append("location %s worldStateChanges count mismatch" % lb.get("id"))
            else:
                for x, y in zip(ca_, cb_):
                    for k in ("type", "key", "value", "amount"):
                        if not _same(x.get(k), y.get(k)):
                            errors.append("location %s worldStateChanges.%s: asset=%r json=%r" % (lb.get("id"), k, x.get(k), y.get(k)))
            ea, eb = la.get("environment", {}), lb.get("environment", {})
            for k in ("profile", "ambient", "fog", "fogDensity", "sun", "sunIntensity"):
                if str(ea.get(k)) != str(eb.get(k)):
                    errors.append("location %s environment.%s: asset=%r json=%r" % (lb.get("id"), k, ea.get(k), eb.get(k)))
            for ref in lb.get("npcs", []):
                if ref not in npc_ids: errors.append("location %s references unknown npc %r" % (lb.get("id"), ref))
            for ref in lb.get("encounters", []):
                if ref not in enc_ids: errors.append("location %s references unknown encounter %r" % (lb.get("id"), ref))
            for ref in lb.get("objectives", []):
                if ref not in obj_ids: errors.append("location %s references unknown objective %r" % (lb.get("id"), ref))
        for lb in content.get("locations", []):
            for conn in lb.get("connections", []):
                if conn not in loc_ids:
                    errors.append("location %s connects to unknown location %r" % (lb.get("id"), conn))
                back = next((l for l in content["locations"] if l["id"] == conn), None)
                if back is not None and lb["id"] not in back.get("connections", []):
                    errors.append("location connection %s->%s is one-way (must be symmetric)" % (lb.get("id"), conn))
        for need_kind in (0, 2, 3):  # hub + npc + combat purposes must exist
            if need_kind not in seen_kinds:
                errors.append("no location of kind %d (hub/npc/combat coverage)" % need_kind)

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
# campaign content pass: the generated partial (scripts/gen_builder_campaign.py) mirrors the same JSON
builder += open(os.path.join(ROOT, "Assets/_Project/Scripts/Narrative/Content/StoryContentBuilder.Campaign.cs")).read()
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

# ---------------------------------------------------------------- 3b. scene reference TYPES
# Unity is type-strict about scene fileIDs: m_Father/m_Children/SceneRoots.m_Roots must
# reference Transforms (class 4); StoryWorldState entity/variant targets reference
# GameObjects (class 1); NpcRelocator targets reference Transforms.
cls_of = {}
for block in scene_txt.split("--- !u!")[1:]:
    m = re.match(r"(\d+) &(\d+)", block.split("\n", 1)[0].strip())
    if m:
        cls_of[int(m.group(2))] = int(m.group(1))

def _ref_class_err(rid, want, ctx):
    got = cls_of.get(rid)
    if got != want:
        errors.append("%s references fileID %d (class %s, expected class %d)" % (ctx, rid, got, want))

for block in scene_txt.split("--- !u!")[1:]:
    m = re.match(r"(\d+) &(\d+)", block.split("\n", 1)[0].strip())
    if not m:
        continue
    cls, bid = int(m.group(1)), int(m.group(2))
    if cls == 4:  # Transform: father + children must be Transforms
        f = re.search(r"m_Father: \{fileID: (\d+)\}", block)
        if f and int(f.group(1)) != 0:
            _ref_class_err(int(f.group(1)), 4, "Transform &%d m_Father" % bid)
        for c in re.findall(r"^  - \{fileID: (\d+)\}$", block, re.M):
            _ref_class_err(int(c), 4, "Transform &%d m_Children" % bid)
    elif cls == 1660057539:  # SceneRoots: roots must be Transforms (or PrefabInstances for prefab roots)
        for r in re.findall(r"^  - \{fileID: (\d+)\}$", block, re.M):
            if cls_of.get(int(r)) == 1001:
                continue
            _ref_class_err(int(r), 4, "SceneRoots.m_Roots")
    elif cls == 114:  # MonoBehaviours: entity targets are GameObjects; relocator targets are Transforms
        is_relocator = "locationKey:" in block
        for t in re.findall(r"^    target: \{fileID: (\d+)\}$", block, re.M):
            _ref_class_err(int(t), 4 if is_relocator else 1,
                           "%s target" % ("NpcRelocator" if is_relocator else "entity binding"))

# ---------------------------------------------------------------- 4. scene sanity
root_count = len(re.findall(r"^  - \{fileID: \d+\}$", scene_txt.split("SceneRoots:")[-1], re.M)) if "SceneRoots:" in scene_txt else 0
print("Scene roots:", root_count)
for needle in ["Mara_NPC", "Sera_NPC", "EchoShard", "EnergySeal",
               "Seq_Ember_Marker", "Seq_Tide_Marker", "Seq_Stone_Marker", "Seq_Tide_Bystanders",
               "Seq_Tide_Calm", "TwinsReturnPoint",
               "AreaTrigger_Annex", "AreaTrigger_Hall", "SM_WallPanel_flank", "m_IsActive: 0",
               "npcId: mara", "npcId: sera",
               "encounterId: c1_hall_shard",
               "ChoirBeacon", "EmberCache", "KeepsakeCrate", "Barricade", "WardStone", "Rubble",
               "NpcRelocator", "Loc_Sera_AnnexGate", "locationKey: annex_gate",
               "useCountVar: brace_count", "useCountVar: rubble_count",
               "key: choir_beacon", "key: ember_cache", "key: keepsake_crate",
               "key: barricade", "key: barricade_rubble", "key: tide_calm",
               "ChoirWarden", "WardenWreckage", "CombatDirector",
               "enemyId: choir_warden", "key: choir_warden", "key: warden_wreckage",
               "areaId: annex", "defaultActive: 1", "m_IsTrigger: 1",
               # world expansion: three locations - anchors, tidewell zone + sera binding
               "LocationAnchor_Hall", "LocationAnchor_Annex", "LocationAnchor_Tidewell",
               "AreaTrigger_Tidewell", "areaId: tidewell", "TidewellPool", "Sera_Tidewell",
               "locationKey: tidewell", "TidewellLamp"]:
    if needle not in scene_txt:
        errors.append("scene missing: " + needle)

for script_key in ["NpcAgent.cs", "NpcInteractable.cs", "StoryWorldState.cs", "StoryModeBootstrap.cs",
                   "GameUIBootstrap.cs", "AreaGate.cs", "StoryEventInteractable.cs", "AreaTrigger.cs",
                   "WorldActionInteractable.cs", "NpcRelocator.cs",
                   "EnemyAgent.cs", "CombatDirector.cs"]:
    if scene_txt.count(registry[script_key]) == 0:
        errors.append("scene does not reference %s" % script_key)

# ---------------------------------------------------------------- 4b. campaign scene contracts (derived from the JSON)
# Every campaign location needs its travel anchor + area trigger; every NPC/enemy definition needs
# a scene root bound to its id; every SpawnEntity key the content writes needs a StoryWorldState
# entity binding; every MoveNpc location key needs an NpcRelocator binding; every world-state
# variant the content sets (market/docks/spire/vessa) needs an areaVariants row.
def _walk_effects(o, out):
    if isinstance(o, dict):
        if set(o.keys()) == {"type", "key", "value", "amount"}:
            out.append(o)
        for v in o.values():
            _walk_effects(v, out)
    elif isinstance(o, list):
        for v in o:
            _walk_effects(v, out)
_records = []
_walk_effects({k: v for k, v in content.items() if k != "_comment"}, _records)
scene_entity_keys = set(re.findall(r"^  - key: (\S+)$", scene_txt, re.M))
scene_variant_rows = set(re.findall(r"^  - area: (\S+)\n    variant: (\S+)$", scene_txt, re.M))
scene_reloc_keys = set(re.findall(r"^  - npcId: (\S+)\n    locationKey: (\S+)$", scene_txt, re.M))
for loc in content["locations"]:
    for needle in ("LocationAnchor_" + loc["id"], "AreaTrigger_" + loc["id"]):
        if needle.lower() not in scene_txt.lower():
            errors.append("scene missing location contract: " + needle)
for n in content["npcs"]:
    if ("npcId: " + n["id"] + "\n") not in scene_txt:
        errors.append("scene has no NpcAgent for npc " + n["id"])
for e in content["enemies"]:
    if ("enemyId: " + e["id"] + "\n") not in scene_txt:
        errors.append("scene has no EnemyAgent for enemy " + e["id"])
_effects_only = [r for r in _records if r["type"] == 8 and r["key"] not in ("",)]
for r in _effects_only:
    if r["key"] not in scene_entity_keys:
        errors.append("SpawnEntity key '%s' has no StoryWorldState binding in the scene" % r["key"])
for r in [r for r in _records if r["type"] == 20]:
    if (r["key"], r["value"]) not in scene_reloc_keys:
        errors.append("MoveNpc %s -> %s has no NpcRelocator binding" % (r["key"], r["value"]))
_world_states = set((r["key"], r["value"]) for r in _records if r["type"] == 7 and r["key"] in ("market", "docks", "spire", "vessa"))
for (a, v) in sorted(_world_states):
    if (a, v) not in scene_variant_rows:
        errors.append("world-state variant %s=%s has no areaVariants dressing in the scene" % (a, v))
print("Campaign contracts: %d locations, %d npcs, %d enemies, %d entity keys, %d variants" %
      (len(content["locations"]), len(content["npcs"]), len(content["enemies"]), len(scene_entity_keys), len(scene_variant_rows)))

# ---------------------------------------------------------------- 5. location kits
# Assets/Game/Locations/<Location>/ must carry one environment prefab each (guid-valid).
LOC_KITS = {"FractureHall": "hall", "NorthAnnex": "annex", "TidewellShrine": "tidewell",
            # campaign content pass (GAME_DESIGN §11.2)
            "LastSummer": "last_summer", "FractureNight": "fracture_night", "UnderSpire": "under_spire",
            "InterludeBecoming": "interlude_becoming", "ContestedDocks": "docks", "Sanctuary": "sanctuary",
            "LongWall": "long_wall", "DaxArena": "dax_arena", "InterludeReckoning": "interlude_reckoning",
            "OldMarket": "market", "SpireAscent": "spire_ascent", "Choirmaster": "choirmaster", "Epilogue": "epilogue"}
for kit_dir, loc_id in LOC_KITS.items():
    if not any(l["id"] == loc_id for l in content["locations"]):
        errors.append("location kit %s maps to unknown location %s" % (kit_dir, loc_id))
    if not os.path.exists(os.path.join(ROOT, "Assets/Game/Locations", kit_dir + ".meta")):
        errors.append("location kit folder has no .meta: " + kit_dir)
for kit_dir, loc_id in LOC_KITS.items():
    kit_path = os.path.join(ROOT, "Assets/Game/Locations", kit_dir)
    if not os.path.isdir(kit_path):
        errors.append("location kit folder missing: " + kit_path)
        continue
    prefabs = [f for f in os.listdir(kit_path) if f.endswith(".prefab")]
    if not prefabs:
        errors.append("location kit %s has no prefab" % kit_dir)
    for f in prefabs:
        check_text_refs(os.path.join(kit_path, f), "location kit " + kit_dir)

# ---------------------------------------------------------------- 6. character animation set (release pass)
# One shared Humanoid animation library drives the hero and the whole cast. The Ari controller must
# carry the triggers PlayerCombatController fires, every library clip must exist as a Humanoid FBX
# with a resolvable guid referenced by both controllers, and every canonical character needs a
# Humanoid model + materials + prefab so NpcAgent/EnemyAgent avatarPrefab references resolve.
ari_dir = os.path.join(ROOT, "Assets/_Project/Art/Characters/Ari")
char_root = os.path.join(ROOT, "Assets/_Project/Art/Characters")
anim_dir = os.path.join(char_root, "_Animations")
ctrl_txt = open(os.path.join(ari_dir, "Ari_Controller.controller")).read()
shared_ctrl_path = os.path.join(anim_dir, "Character_Controller.controller")
shared_txt = open(shared_ctrl_path).read() if os.path.exists(shared_ctrl_path) else ""
if not shared_txt:
    errors.append("missing shared Character_Controller.controller")
for trig in ("Attack", "Dodge", "Hit", "Defeat", "Alert"):
    for label, txt in (("Ari_Controller", ctrl_txt), ("Character_Controller", shared_txt)):
        if txt and ("m_Name: %s\n    m_Type: 9" % trig) not in txt:
            errors.append("%s missing Trigger parameter %s" % (label, trig))
for param, ptype in (("Speed", 1), ("Talking", 4)):
    for label, txt in (("Ari_Controller", ctrl_txt), ("Character_Controller", shared_txt)):
        if txt and ("m_Name: %s\n    m_Type: %d" % (param, ptype)) not in txt:
            errors.append("%s missing parameter %s" % (label, param))
CAST_CLIPS = ("Idle", "Walk", "Run", "Talk", "Alert", "Attack", "Hit", "Dodge", "Defeat")
for clip in CAST_CLIPS:
    fbx = os.path.join(anim_dir, "Anim_%s.fbx" % clip)
    if not os.path.exists(fbx) or os.path.getsize(fbx) < 20000:
        errors.append("missing/empty animation clip Anim_%s.fbx" % clip)
        continue
    meta = fbx + ".meta"
    m = re.search(r"^guid: ([0-9a-f]{32})", open(meta).read(), re.M) if os.path.exists(meta) else None
    if not m:
        errors.append("animation clip without meta guid: Anim_%s.fbx" % clip)
        continue
    if "animationType: 3" not in open(meta).read():
        errors.append("Anim_%s.fbx is not imported as Humanoid" % clip)
    for label, txt in (("Ari_Controller", ctrl_txt), ("Character_Controller", shared_txt)):
        if txt and m.group(1) not in txt:
            errors.append("%s does not reference Anim_%s.fbx" % (label, clip))
check_text_refs(os.path.join(ari_dir, "Ari_Controller.controller"), "Ari_Controller")
if shared_txt:
    check_text_refs(shared_ctrl_path, "Character_Controller")
CAST = ("Ari", "Mara", "Mara_Dress", "Dax", "Archivist", "Kael", "Odalys", "Bran", "Sera", "Civilian", "Soldier_A", "Soldier_B", "Soldier_C")
prefab_dir = os.path.join(ROOT, "Assets/_Project/Prefabs/Characters")
for name in CAST:
    d = os.path.join(char_root, name)
    fbx = os.path.join(d, name + ".fbx")
    if not os.path.exists(fbx) or os.path.getsize(fbx) < 20000:
        errors.append("missing character model %s.fbx" % name); continue
    meta = fbx + ".meta"
    if not os.path.exists(meta):
        errors.append("character model without meta: %s.fbx" % name); continue
    mtxt = open(meta).read()
    if "animationType: 3" not in mtxt:
        errors.append("%s.fbx is not imported as Humanoid (retargeting requires animationType 3)" % name)
    if not os.path.exists(os.path.join(d, name + "_Albedo.png")):
        errors.append("missing atlas %s_Albedo.png" % name)
    for mat in ("M_%s.mat" % name, "M_%s_Hair.mat" % name):
        mp = os.path.join(d, mat)
        if not os.path.exists(mp):
            errors.append("missing material " + mat); continue
        check_text_refs(mp, mat)
        mg = re.search(r"^guid: ([0-9a-f]{32})", open(mp + ".meta").read(), re.M).group(1)
        if mg not in mtxt:
            errors.append("%s.fbx.meta does not remap %s" % (name, mat))
    pf = os.path.join(prefab_dir, name + ".prefab")
    if not os.path.exists(pf):
        errors.append("missing character prefab %s.prefab" % name); continue
    check_text_refs(pf, name + ".prefab")
    ptxt = open(pf).read()
    fg = re.search(r"^guid: ([0-9a-f]{32})", mtxt, re.M).group(1)
    if fg not in ptxt:
        errors.append("%s.prefab does not derive from %s.fbx" % (name, name))
# every NpcAgent / EnemyAgent avatarPrefab in the scenes must point at a registered character prefab
prefab_guids = set()
for f in os.listdir(prefab_dir) if os.path.isdir(prefab_dir) else []:
    if f.endswith(".prefab.meta"):
        prefab_guids.add(re.search(r"^guid: ([0-9a-f]{32})", open(os.path.join(prefab_dir, f)).read(), re.M).group(1))
scene_avatar_refs = 0
for scene_file in glob.glob(os.path.join(ROOT, "Assets/Scenes/**/*.unity"), recursive=True):
    for m in re.finditer(r"avatarPrefab: \{fileID: 100100000, guid: ([0-9a-f]{32}), type: 3\}", open(scene_file).read()):
        scene_avatar_refs += 1
        if m.group(1) not in prefab_guids:
            errors.append("%s: avatarPrefab guid %s is not a character prefab" % (os.path.basename(scene_file), m.group(1)))
print("Character set: %d humanoid models, %d shared clips, %d scene avatar bindings" % (len(CAST), len(CAST_CLIPS), scene_avatar_refs))

# ---------------------------------------------------------------- 6b. quality tiers x post-processing (release pass)
# Low must not run bloom; the scene's QualityTierApplier must reference both profiles.
pp_low = os.path.join(ROOT, "Assets/Settings/PostProcess_Low.asset")
pp_std = os.path.join(ROOT, "Assets/Settings/PostProcess_Global.asset")
for pth in (pp_low, pp_std):
    if not os.path.exists(pth):
        errors.append("missing post profile " + os.path.relpath(pth, ROOT))
if os.path.exists(pp_low) and "m_Name: Bloom" in open(pp_low).read():
    errors.append("PostProcess_Low.asset still contains a Bloom component (Low tier must not bloom)")
if os.path.exists(pp_std) and "m_Name: Bloom" not in open(pp_std).read():
    errors.append("PostProcess_Global.asset lost its Bloom component (Balanced/High keep the Fracture glow)")
scene_txt = open(os.path.join(ROOT, "Assets/Scenes/Prototype/FirstLocation.unity")).read()
low_guid = re.search(r"^guid: ([0-9a-f]{32})", open(pp_low + ".meta").read(), re.M).group(1) if os.path.exists(pp_low + ".meta") else None
if low_guid and ("lowProfile: {fileID: 11400000, guid: %s, type: 2}" % low_guid) not in scene_txt:
    errors.append("scene QualityTierApplier does not reference PostProcess_Low.asset")
qs = open(os.path.join(ROOT, "ProjectSettings/QualitySettings.asset")).read()
for tier_name in ("Low", "Balanced", "High"):
    if ("name: %s" % tier_name) not in qs:
        errors.append("QualitySettings.asset missing tier " + tier_name)
print("Quality tiers: Low (no bloom, 30 fps) / Balanced / High profiles wired")

# ---------------------------------------------------------------- 7. audio + presentation wiring (polish pass)
# Every AudioClip the GameAudio component references must exist as a WAV with a meta whose guid
# matches the registry; the scene must carry GameAudio, CombatVFX, the Ari PrefabInstance, a
# MainCamera-tagged camera and a Player-tagged Ari (device builds have no editor bootstrap).
import wave as _wave
audio_keys = [k for k in registry if k.endswith(".wav")]
audio_root = os.path.join(ROOT, "Assets/_Project/Audio")
audio_files = {}
for dp, _dn, fns in os.walk(audio_root):
    for fn in fns:
        if fn.endswith(".wav"):
            audio_files[fn] = os.path.join(dp, fn)
audio_bytes = 0
for k in audio_keys:
    path = audio_files.get(k)
    if path is None:
        errors.append("audio clip in registry but missing on disk: " + k)
        continue
    meta = path + ".meta"
    m = re.search(r"^guid: ([0-9a-f]{32})", open(meta).read(), re.M) if os.path.exists(meta) else None
    if not m or m.group(1) != registry[k]:
        errors.append("audio meta guid mismatch: " + k)
    try:
        with _wave.open(path) as w:
            if w.getnchannels() != 1 or w.getframerate() > 22050:
                warns.append("audio not mono/<=22.05kHz (mobile budget): " + k)
            if w.getnframes() == 0:
                errors.append("empty audio clip: " + k)
    except Exception as ex:
        errors.append("unreadable WAV %s: %s" % (k, ex))
    audio_bytes += os.path.getsize(path)
    if registry[k] not in scene_txt:
        errors.append("scene does not reference audio clip " + k)
if audio_bytes > 6 * 1024 * 1024:
    warns.append("audio placeholder set is %.1f MB (>6 MB budget)" % (audio_bytes / 1e6))
# every public AudioClip field of GameAudio must be bound in the scene (no silent events by omission)
ga_src = open(os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/World/GameAudio.cs")).read()
ga_fields = re.findall(r"^\s*public AudioClip (\w+);", ga_src, re.M)
ga_block = re.search(r"m_Script: \{fileID: 11500000, guid: %s, type: 3\}.*?(?=\n--- !u!)" % registry["GameAudio.cs"], scene_txt, re.S)
for f in ga_fields:
    if not ga_block or not re.search(r"^  %s: \{fileID: 8300000, guid: [0-9a-f]{32}, type: 3\}" % f, ga_block.group(0), re.M):
        errors.append("GameAudio.%s is not bound to a clip in the scene" % f)
# recorded vs placeholder census (reference/audio_source/AUDIO_STATUS.json, written by gen_audio.py)
status_path = os.path.join(ROOT, "reference/audio_source/AUDIO_STATUS.json")
audio_status = json.load(open(status_path)) if os.path.exists(status_path) else {"recorded": 0, "procedural_placeholders": list(audio_keys)}
if audio_status.get("fallbacks_used"):
    warns.append("audio fallbacks used for recorded clips: %s" % audio_status["fallbacks_used"])
for needle in ["m_TagString: MainCamera", "propertyPath: m_TagString\n      value: Player", "PrefabInstance:",
               "m_SourcePrefab: {fileID: 100100000, guid: c0a1fed0000000000000000000000002, type: 3}",
               "guid: a79441f348de89743a2939f4d699eac1", "guid: 172515602e62fb746b5d573b38a5fe58"]:
    if needle not in scene_txt:
        errors.append("scene missing presentation wiring: " + needle.replace("\n", " "))
for script_key in ["GameAudio.cs", "CombatVFX.cs", "PlayerCombatController.cs", "PlayerInteraction.cs"]:
    if registry[script_key] not in scene_txt:
        errors.append("scene does not reference %s" % script_key)
print("Audio + presentation: %d clips (%.1f MB; %d recorded CC0, %d procedural placeholders), %d GameAudio fields bound, Ari prefab instance, camera/volume wiring OK"
      % (len(audio_keys), audio_bytes / 1e6, audio_status.get("recorded", 0), len(audio_status.get("procedural_placeholders", [])), len(ga_fields)))

print("=" * 60)
if errors:
    for e in errors:
        print("ERROR:", e)
    print("FAILED with %d error(s)" % len(errors))
    sys.exit(1)
for w in warns:
    print("warn:", w)
print("VALIDATION PASSED (%d warnings)" % len(warns))
