#!/usr/bin/env python3
"""Static profiler for the generated FirstLocation scene + project assets.

Unity's runtime profiler needs a device; this tool measures every budget that can be
measured from the serialized data (which is what the GPU/CPU will be asked to do), so
the production-polish pass fixes MEASURED bottlenecks instead of guessed ones.

Reports (and enforces with --check against docs/PERF_BUDGET.json):
  * renderer count / worst-case draw calls (per-room and always-on)
  * static-batching eligibility (static flags on kit geometry)
  * per-frame MonoBehaviour tick load (agents with Update)
  * material / shader census (shader GUID resolution, instancing flags)
  * texture sizes + Android import format
  * lights + shadows, camera clip/HDR/MSAA
  * quality tiers + render pipeline assignment
  * scene object counts + file size (load time proxy)
"""
import collections, glob, json, os, re, struct, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENE = os.path.join(ROOT, "Assets/Scenes/Prototype/FirstLocation.unity")
BUDGET = os.path.join(ROOT, "docs/PERF_BUDGET.json")

URP_LIT = "933532a4fcc9baf4fa0491de14d08ed7"
URP_SIMPLE_LIT = "8d2bb70cbf9db8d4da26e15b26e74248"
URP_UNLIT = "650dd9526735d5b46b79224bc6e94025"
URP_PARTICLES_UNLIT = "0406db5a14f94604a8c57ccfbc9f3b46"
KNOWN_SHADERS = {URP_LIT: "URP/Lit", URP_SIMPLE_LIT: "URP/SimpleLit", URP_UNLIT: "URP/Unlit",
                 URP_PARTICLES_UNLIT: "URP/Particles/Unlit"}

# Unity static flags: ContributeGI 1, OccluderStatic 2, BatchingStatic 4, NavigationStatic 8,
# OccludeeStatic 16, OffMeshLinkGeneration 32, ReflectionProbeStatic 64
BATCHING_STATIC = 4


def parse_scene(path):
    src = open(path, encoding="utf-8").read()
    docs = re.split(r"\n--- !u!", src)
    out = []
    for d in docs[1:]:
        m = re.match(r"(\d+) &(\d+)", d)
        out.append((int(m.group(1)), int(m.group(2)), d))
    return src, out


def field(doc, key, default=None):
    m = re.search(r"\b" + re.escape(key) + r": ([^\n]*)", doc)
    return m.group(1).strip() if m else default


def profile():
    src, docs = parse_scene(SCENE)
    by_kind = collections.defaultdict(list)
    for kind, fid, d in docs:
        by_kind[kind].append((fid, d))

    gos = {fid: d for fid, d in by_kind[1]}
    go_name = {fid: field(d, "m_Name", "") for fid, d in gos.items()}
    go_static = {fid: int(field(d, "m_StaticEditorFlags", "0") or 0) for fid, d in gos.items()}
    go_active = {fid: int(field(d, "m_IsActive", "1") or 1) for fid, d in gos.items()}

    # transform parentage -> root name per GO (rooms are identified by root naming)
    tr = {}
    for fid, d in by_kind[4]:
        go = int(field(d, "m_GameObject", "{fileID: 0}").split(":")[1].strip(" }"))
        father = int(field(d, "m_Father", "{fileID: 0}").split(":")[1].strip(" }"))
        tr[fid] = (go, father)
    tid_by_go = {go: tid for tid, (go, _f) in tr.items()}

    def root_go(go):
        seen = 0
        tid = tid_by_go.get(go)
        while tid is not None and seen < 64:
            g, f = tr[tid]
            if f == 0:
                return g
            tid = f
            seen += 1
        return go

    def effective_active(go):
        tid = tid_by_go.get(go)
        seen = 0
        while tid is not None and seen < 64:
            g, f = tr[tid]
            if not go_active.get(g, 1):
                return False
            if f == 0:
                return True
            tid = f
            seen += 1
        return True

    # renderers
    renderers = []
    for fid, d in by_kind[23]:
        go = int(field(d, "m_GameObject").split(":")[1].strip(" }"))
        mats = re.findall(r"\{fileID: \d+, guid: ([0-9a-f]+), type: 2\}", d)
        renderers.append({
            "go": go, "name": go_name.get(go, ""), "root": go_name.get(root_go(go), ""),
            "mats": mats, "cast": int(field(d, "m_CastShadows", "0") or 0),
            "active": effective_active(go), "static": bool(go_static.get(go, 0) & BATCHING_STATIC),
            "probes": int(field(d, "m_LightProbeUsage", "0") or 0),
            "reflection": int(field(d, "m_ReflectionProbeUsage", "0") or 0),
            "motion": int(field(d, "m_MotionVectors", "0") or 0),
        })
    active_r = [r for r in renderers if r["active"]]
    static_r = [r for r in active_r if r["static"]]
    dynamic_r = [r for r in active_r if not r["static"]]

    # rooms: rendered geometry grouped by location room prefix
    room_of = collections.Counter()
    for r in active_r:
        m = re.match(r"SM_\w+?_([a-z_]+?)_-?\d", r["name"])
        room_of[m.group(1) if m else "hall/shared"] += 1

    # material census
    mats = collections.Counter()
    for r in active_r:
        for m in r["mats"]:
            mats[m] += 1
    shader_by_mat = {}
    instancing = {}
    for mp in glob.glob(os.path.join(ROOT, "Assets/**/*.mat"), recursive=True):
        meta = mp + ".meta"
        g = re.search(r"guid: ([0-9a-f]+)", open(meta).read()).group(1) if os.path.exists(meta) else None
        txt = open(mp, encoding="utf-8").read()
        sh = re.search(r"m_Shader: \{fileID: \d+, guid: ([0-9a-f]+)", txt)
        shader_by_mat[g] = sh.group(1) if sh else "?"
        instancing[g] = int(re.search(r"m_EnableInstancingVariants: (\d)", txt).group(1)) if "m_EnableInstancingVariants" in txt else 0
    shaders = collections.Counter(shader_by_mat.values())
    unresolved = {s: n for s, n in shaders.items() if s not in KNOWN_SHADERS}

    # scripts per frame
    guid2name = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets/**/*.cs.meta"), recursive=True):
        g = re.search(r"guid: ([0-9a-f]+)", open(meta).read()).group(1)
        guid2name[g] = os.path.basename(meta)[:-8]
    tickers = set()
    for cs in glob.glob(os.path.join(ROOT, "Assets/**/*.cs"), recursive=True):
        t = open(cs, encoding="utf-8").read()
        if re.search(r"void (Update|LateUpdate|FixedUpdate)\s*\(", t):
            tickers.add(os.path.basename(cs)[:-3])
    mb = collections.Counter()
    mb_ticking = 0
    for fid, d in by_kind[114]:
        g = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]+)", d)
        n = guid2name.get(g.group(1), g.group(1)) if g else "?"
        go = int(field(d, "m_GameObject").split(":")[1].strip(" }"))
        mb[n] += 1
        if n in tickers and effective_active(go):
            mb_ticking += 1

    # lights / camera
    lights = []
    for fid, d in by_kind[108]:
        lights.append({"type": int(field(d, "m_Type", "1")), "shadows": int(re.search(r"m_Shadows:\n    m_Type: (\d+)", d).group(1))})
    cams = []
    for fid, d in by_kind[20]:
        cams.append({"far": float(field(d, "far clip plane", "0")), "near": float(field(d, "near clip plane", "0")),
                     "hdr": int(field(d, "m_HDR", "0")), "msaa": int(field(d, "m_AllowMSAA", "0")),
                     "dynres": int(field(d, "m_AllowDynamicResolution", "0")), "occlusion": int(field(d, "m_OcclusionCulling", "0") or 0)})
    rs = by_kind[104][0][1] if by_kind[104] else ""
    fog = {"on": int(field(rs, "m_Fog", "0") or 0), "mode": int(field(rs, "m_FogMode", "0") or 0),
           "density": float(field(rs, "m_FogDensity", "0") or 0)}

    # textures
    textures = []
    for tp in glob.glob(os.path.join(ROOT, "Assets/**/*.png"), recursive=True):
        with open(tp, "rb") as f:
            head = f.read(32)
        w, h = struct.unpack(">II", head[16:24])
        meta = tp + ".meta"
        android = None
        max_size = None
        mip = None
        if os.path.exists(meta):
            mt = open(meta).read()
            am = re.search(r"buildTarget: Android\n\s+maxTextureSize: (\d+)\n\s+resizeAlgorithm: \d+\n\s+textureFormat: (-?\d+)", mt)
            if am:
                max_size, android = int(am.group(1)), int(am.group(2))
            mm = re.search(r"m_EnableMipMap: (\d)", mt)
            mip = int(mm.group(1)) if mm else None
        textures.append({"path": os.path.relpath(tp, ROOT), "w": w, "h": h, "android_format": android,
                         "android_max": max_size, "mipmaps": mip, "bytes": os.path.getsize(tp)})

    # quality + pipeline
    ps = open(os.path.join(ROOT, "ProjectSettings/ProjectSettings.asset"), encoding="utf-8").read()
    tiers = re.findall(r"- serializedVersion: \d+\n    name: (\w+)", ps)
    # every quality tier must bind a pipeline asset (Unity 6 field name: customRenderPipeline)
    tier_pipelines = re.findall(r"customRenderPipeline: \{fileID: (\d+)(?:, guid: ([0-9a-f]+))?", ps)
    pipeline_assigned = bool(tiers) and len(tier_pipelines) >= len(tiers) and all(g for _f, g in tier_pipelines)
    gs_path = os.path.join(ROOT, "ProjectSettings/GraphicsSettings.asset")
    gs = open(gs_path, encoding="utf-8").read() if os.path.exists(gs_path) else ""
    gs_pipeline = bool(re.search(r"m_CustomRenderPipeline: \{fileID: \d+, guid: [0-9a-f]+", gs))
    urp_assets = glob.glob(os.path.join(ROOT, "Assets/**/*.asset"), recursive=True)
    urp_assets = [os.path.relpath(p, ROOT) for p in urp_assets if "UniversalRenderPipelineAsset" in open(p, errors="ignore").read()[:4000] or re.search(r"m_Script: \{fileID: 11500000, guid: bf2edee5c58d82540a51f03df9d42094", open(p, errors="ignore").read())]

    report = {
        "scene": {
            "path": os.path.relpath(SCENE, ROOT), "bytes": os.path.getsize(SCENE), "lines": src.count("\n"),
            "gameobjects": len(gos), "roots": len(re.findall(r"- \{fileID: \d+\}", src.split("SceneRoots:")[-1])) if "SceneRoots:" in src else 0,
            "monobehaviours": sum(mb.values()), "monobehaviours_ticking_active": mb_ticking,
            "monobehaviour_census": dict(mb.most_common()),
        },
        "renderers": {
            "total": len(renderers), "active": len(active_r), "inactive": len(renderers) - len(active_r),
            "active_static_batchable": len(static_r), "active_dynamic": len(dynamic_r),
            "worst_case_draw_calls_unbatched": len(active_r),
            "estimated_draw_calls_batched": len(set((tuple(r["mats"]) for r in static_r))) + len(dynamic_r),
            "shadow_casters": sum(1 for r in active_r if r["cast"]),
            "per_room_active": dict(room_of.most_common()),
            "light_probe_users": sum(1 for r in active_r if r["probes"] != 0),
            "reflection_probe_users": sum(1 for r in active_r if r["reflection"] != 0),
            "motion_vector_users": sum(1 for r in active_r if r["motion"] != 0),
        },
        "materials": {
            "distinct_in_scene": len(mats), "shaders": {KNOWN_SHADERS.get(s, s): n for s, n in shaders.items()},
            "unresolved_shader_guids": unresolved,
            "gpu_instancing_enabled": sum(1 for v in instancing.values() if v), "materials_total": len(instancing),
        },
        "lights": lights, "cameras": cams, "fog": fog,
        "textures": textures,
        "quality": {"tiers": tiers, "pipeline_assigned_quality": pipeline_assigned, "pipeline_assigned_graphics": gs_pipeline,
                    "urp_assets": urp_assets},
    }
    return report


def check(report, budget):
    fails = []
    r = report
    def need(cond, msg):
        if not cond:
            fails.append(msg)
    b = budget
    need(r["renderers"]["worst_case_draw_calls_unbatched"] <= b["max_active_renderers"],
         "active renderers %d > %d" % (r["renderers"]["worst_case_draw_calls_unbatched"], b["max_active_renderers"]))
    need(r["renderers"]["estimated_draw_calls_batched"] <= b["max_estimated_draw_calls"],
         "estimated draw calls %d > %d" % (r["renderers"]["estimated_draw_calls_batched"], b["max_estimated_draw_calls"]))
    need(r["renderers"]["active_static_batchable"] >= b["min_static_batchable_fraction"] * max(1, r["renderers"]["active"]),
         "static-batchable fraction %.2f < %.2f" % (r["renderers"]["active_static_batchable"] / max(1, r["renderers"]["active"]), b["min_static_batchable_fraction"]))
    need(not r["materials"]["unresolved_shader_guids"], "unresolved shader guids: %s" % r["materials"]["unresolved_shader_guids"])
    need(r["quality"]["pipeline_assigned_graphics"] and r["quality"]["pipeline_assigned_quality"], "URP pipeline asset not assigned (Graphics/Quality)")
    need(r["scene"]["monobehaviours_ticking_active"] <= b["max_ticking_behaviours"],
         "ticking behaviours %d > %d" % (r["scene"]["monobehaviours_ticking_active"], b["max_ticking_behaviours"]))
    for t in r["textures"]:
        need(max(t["w"], t["h"]) <= b["max_texture_size"], "texture %s is %dx%d > %d" % (t["path"], t["w"], t["h"], b["max_texture_size"]))
        need(t["android_format"] in b["allowed_android_texture_formats"], "texture %s android format %s not in %s" % (t["path"], t["android_format"], b["allowed_android_texture_formats"]))
    for c in r["cameras"]:
        need(c["far"] <= b["max_far_clip"], "camera far clip %.0f > %d" % (c["far"], b["max_far_clip"]))
        need(c["hdr"] == 0, "camera HDR on (mobile budget says off)")
    need(len(r["lights"]) <= b["max_realtime_lights"], "realtime lights %d > %d" % (len(r["lights"]), b["max_realtime_lights"]))
    need(sum(1 for l in r["lights"] if l["shadows"]) <= b["max_shadowed_lights"], "shadowed lights > %d" % b["max_shadowed_lights"])
    need(r["scene"]["bytes"] <= b["max_scene_bytes"], "scene file %d bytes > %d" % (r["scene"]["bytes"], b["max_scene_bytes"]))
    need(set(b["required_quality_tiers"]).issubset(set(r["quality"]["tiers"])), "quality tiers %s missing %s" % (r["quality"]["tiers"], b["required_quality_tiers"]))
    return fails


def main():
    report = profile()
    as_json = "--json" in sys.argv
    if as_json:
        print(json.dumps(report, indent=1))
    else:
        s, rd, m, q = report["scene"], report["renderers"], report["materials"], report["quality"]
        print("== CROSSROADS static profile ==")
        print("scene      : %s  (%.1f MB, %d lines, %d GameObjects, %d roots)" % (s["path"], s["bytes"] / 1e6, s["lines"], s["gameobjects"], s["roots"]))
        print("scripts    : %d MonoBehaviours, %d ticking (Update) while active" % (s["monobehaviours"], s["monobehaviours_ticking_active"]))
        print("renderers  : %d total / %d active / %d inactive" % (rd["total"], rd["active"], rd["inactive"]))
        print("             static-batchable %d, dynamic %d, shadow casters %d" % (rd["active_static_batchable"], rd["active_dynamic"], rd["shadow_casters"]))
        print("draw calls : worst case %d unbatched -> ~%d with static batching" % (rd["worst_case_draw_calls_unbatched"], rd["estimated_draw_calls_batched"]))
        print("             probes/reflection/motion-vector users: %d/%d/%d" % (rd["light_probe_users"], rd["reflection_probe_users"], rd["motion_vector_users"]))
        print("per room   : " + ", ".join("%s=%d" % kv for kv in list(rd["per_room_active"].items())[:8]) + " ...")
        print("materials  : %d distinct in scene; shaders %s; instancing on %d/%d" % (m["distinct_in_scene"], m["shaders"], m["gpu_instancing_enabled"], m["materials_total"]))
        if m["unresolved_shader_guids"]:
            print("  !! unresolved shader guids: %s" % m["unresolved_shader_guids"])
        print("lights     : %s" % report["lights"])
        print("cameras    : %s" % report["cameras"])
        print("fog        : %s" % report["fog"])
        for t in report["textures"]:
            print("texture    : %s %dx%d android_fmt=%s max=%s mips=%s (%.0f KB)" % (t["path"], t["w"], t["h"], t["android_format"], t["android_max"], t["mipmaps"], t["bytes"] / 1024))
        print("quality    : tiers %s; URP asset assigned graphics=%s quality=%s; urp assets %s" % (q["tiers"], q["pipeline_assigned_graphics"], q["pipeline_assigned_quality"], q["urp_assets"]))
    if "--check" in sys.argv:
        budget = json.load(open(BUDGET))
        fails = check(report, budget)
        if fails:
            print("\nBUDGET FAILED (%d):" % len(fails))
            for f in fails:
                print("  - " + f)
            sys.exit(1)
        print("\nBUDGET PASSED (%s)" % os.path.relpath(BUDGET, ROOT))


if __name__ == "__main__":
    main()
