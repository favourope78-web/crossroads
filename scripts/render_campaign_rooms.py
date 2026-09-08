#!/usr/bin/env python3
"""ART PRODUCTION PASS verification renders: two dressed campaign rooms.

Same approach as render_visual_slice.py (real scene data -> Blender), scoped to a room
bbox around a campaign LocationAnchor, with the canonical avatars present. 1280x720.

Run: blender -b -P scripts/render_campaign_rooms.py
"""
import bpy, math, re, os
from mathutils import Vector, Euler, Matrix

ROOT = "/home/user/crossroads"
SCENE = os.path.join(ROOT, "Assets/Scenes/Prototype/FirstLocation.unity")
KIT = os.path.join(ROOT, "Assets/Game/Environment/Kit")
MATDIR = os.path.join(ROOT, "Assets/Game/Environment/Materials")
CHARDIR = os.path.join(ROOT, "Assets/_Project/Art/Characters")
OUT = os.path.join(ROOT, "reference/visual_slice/raw")
os.makedirs(OUT, exist_ok=True)

ROOMS = [
    # (name, anchor, yaw of camera, npc file, npc offset)
    ("room_market", (-100, 43), 200, "Civilian/Civilian.fbx", (2.5, 0, 1.5)),
    ("room_sanctuary", (-70, 43), 160, "Sera/Sera.fbx", (-2.5, 0, 2.0)),
]

txt = open(SCENE).read()
blocks = txt.split("--- !u!")[1:]
gos, trs, mfilters, mrenderers = {}, {}, {}, {}
for b in blocks:
    m = re.match(r"(\d+) &(\d+)", b.split("\n", 1)[0].strip())
    if not m:
        continue
    kind, oid = int(m.group(1)), int(m.group(2))
    if kind == 1:
        nm = re.search(r"m_Name: (.*)", b)
        act = re.search(r"m_IsActive: (\d)", b)
        gos[oid] = {"name": (nm.group(1).strip() if nm else "?"), "active": int(act.group(1)) if act else 1}
    elif kind == 4:
        pos = re.search(r"m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", b)
        scl = re.search(r"m_LocalScale: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", b)
        eul = re.search(r"m_LocalEulerAnglesHint: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", b)
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", b)
        fth = re.search(r"m_Father: \{fileID: (\d+)\}", b)
        trs[oid] = {
            "pos": tuple(float(x) for x in pos.groups()) if pos else (0, 0, 0),
            "scale": tuple(float(x) for x in scl.groups()) if scl else (1, 1, 1),
            "euler": tuple(float(x) for x in eul.groups()) if eul else (0, 0, 0),
            "go": int(g.group(1)) if g else 0,
            "father": int(fth.group(1)) if fth else 0,
        }
    elif kind == 33:
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", b)
        mesh = re.search(r"m_Mesh: \{fileID: (\d+), guid: ([0-9a-f]*), type: \d+\}", b)
        mfilters[oid] = {"go": int(g.group(1)) if g else 0,
                         "fileID": int(mesh.group(1)) if mesh else 0,
                         "guid": mesh.group(2) if mesh else ""}
    elif kind == 23:
        g = re.search(r"m_GameObject: \{fileID: (\d+)\}", b)
        mats = re.findall(r"guid: ([0-9a-f]{32})", b)
        mrenderers[oid] = {"go": int(g.group(1)) if g else 0, "mats": mats}

transform_of_go = {t["go"]: k for k, t in trs.items()}

KIT_GUIDS = {}
for f in os.listdir(KIT):
    if f.endswith(".fbx.meta"):
        g = re.search(r"guid: ([0-9a-f]{32})", open(os.path.join(KIT, f)).read())
        if g:
            KIT_GUIDS[g.group(1)] = f[:-9]

MAT_GUIDS = {}
for dirpath, dirs, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if f.endswith(".mat.meta"):
            g = re.search(r"guid: ([0-9a-f]{32})", open(os.path.join(dirpath, f)).read())
            if g:
                MAT_GUIDS[g.group(1)] = os.path.join(dirpath, f[:-9])

def mat_colors(path):
    try:
        s = open(path).read()
    except OSError:
        return (0.5, 0.5, 0.55), (0, 0, 0)
    base = re.search(r"_BaseColor: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", s)
    emit = re.search(r"_EmissionColor: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", s)
    return (tuple(float(x) for x in base.groups()) if base else (0.5, 0.5, 0.55),
            tuple(float(x) for x in emit.groups()) if emit else (0, 0, 0))

BUILTIN = {10202: "cube", 10207: "sphere", 10208: "capsule", 10206: "cylinder"}

def world_matrix(tid):
    chain = []
    t = tid
    while t and t in trs:
        chain.append(trs[t])
        t = trs[t]["father"]
    m = Matrix.Identity(4)
    for t in reversed(chain):
        rx, ry, rz = [math.radians(a) for a in t["euler"]]
        rot = (Euler((0, ry, 0), 'XYZ').to_matrix().to_4x4() @
               Euler((rx, 0, 0), 'XYZ').to_matrix().to_4x4() @
               Euler((0, 0, rz), 'XYZ').to_matrix().to_4x4())
        m = m @ Matrix.Translation(Vector(t["pos"])) @ rot @ Matrix.Diagonal((*t["scale"], 1.0)).to_4x4()
    return m

P = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

def u2b(m):
    return P @ m @ P.inverted()

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

mat_cache = {}
def blender_material(name, base, emit, glossy=False):
    key = (name, tuple(round(c, 3) for c in base), tuple(round(c, 3) for c in emit), glossy)
    if key in mat_cache:
        return mat_cache[key]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*base, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.28 if glossy else 0.85
    lum = max(emit) if emit else 0.0
    if lum > 0.02:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = min(6.0, 0.6 + lum * 3.2)
    mat_cache[key] = mat
    return mat

kit_cache = {}
def import_kit(piece):
    if piece not in kit_cache:
        bpy.ops.import_scene.fbx(filepath=os.path.join(KIT, piece + ".fbx"))
        objs = list(bpy.context.selected_objects)
        kit_cache[piece] = objs[0] if objs else None
        for o in objs[1:]:
            bpy.data.objects.remove(o)
        if kit_cache[piece]:
            kit_cache[piece].hide_set(True)
    return kit_cache[piece]

def build_room(anchor, radius=13.0):
    built = 0
    for rid, rr in mrenderers.items():
        go = gos.get(rr["go"])
        if go is None or not go["active"]:
            continue
        name = go["name"]
        if any(name.startswith(p) for p in ("Mara_NPC", "Sera_NPC", "Sera_Tidewell", "ChoirWarden", "Civilian")):
            continue
        mf = None
        for mfi, mfdata in mfilters.items():
            if mfdata["go"] == rr["go"]:
                mf = mfdata
                break
        tid = transform_of_go.get(rr["go"])
        if mf is None or tid is None:
            continue
        m_world = world_matrix(tid)
        pos_u = m_world.decompose()[0]
        d = math.hypot(pos_u.x - anchor[0], pos_u.z - anchor[1])
        if d > radius:
            continue
        obj = None
        if mf["guid"] in KIT_GUIDS and mf["fileID"] == 4300000:
            src = import_kit(KIT_GUIDS[mf["guid"]])
            if src:
                obj = src.copy()
                obj.data = src.data
                scene.collection.objects.link(obj)
                obj.hide_set(False)
        elif mf["fileID"] in BUILTIN:
            shape = BUILTIN[mf["fileID"]]
            if shape == "cube":
                bpy.ops.mesh.primitive_cube_add(size=1.0)
            elif shape == "sphere":
                bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5)
            elif shape == "capsule":
                bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5)
                obj = bpy.context.active_object
                obj.scale = (1.0, 1.0, 1.6)
            else:
                bpy.ops.mesh.primitive_cylinder_add(radius=0.5, depth=1.0)
            if obj is None:
                obj = bpy.context.active_object
        if obj is None:
            continue
        obj.matrix_world = u2b(m_world)
        base, emit = (0.55, 0.55, 0.58), (0, 0, 0)
        if rr["mats"]:
            matfile = MAT_GUIDS.get(rr["mats"][0])
            if matfile:
                base, emit = mat_colors(matfile)
        glossy = any(k in name for k in ("Glazing", "Holo", "Orb", "Beam", "Dress"))
        obj.data.materials.clear()
        obj.data.materials.append(blender_material(name.split("_")[0][:24], base, emit, glossy))
        built += 1
    return built

def place_character(fbx, world_pos, yaw_deg, label, tint=None):
    bpy.ops.import_scene.fbx(filepath=fbx)
    imported = [o for o in bpy.context.selected_objects]
    holder = bpy.data.objects.new("Char_" + label, None)
    scene.collection.objects.link(holder)
    inv = holder.matrix_world.inverted()
    for o in imported:
        o.matrix_parent_inverse = inv
        o.parent = holder
    holder.location = Vector((world_pos[0], -world_pos[2], world_pos[1]))
    holder.rotation_euler = (0, 0, math.radians(yaw_deg))
    arm = next((o for o in imported if o.type == "ARMATURE"), None)
    body = next((o for o in imported if o.type == "MESH"), None)
    if arm:
        for bone_name, rx in (("LeftArm", 0.32), ("RightArm", 0.32), ("LeftForeArm", 0.22), ("RightForeArm", 0.22)):
            pb = arm.pose.bones.get(bone_name)
            if pb:
                pb.rotation_mode = "XYZ"
                pb.rotation_euler = (rx, 0, 0)
    if tint and body:
        body.data.materials.clear()
        body.data.materials.append(blender_material("Tint_" + label, tint[0], tint[1]))
    return holder

# lighting
bpy.ops.object.light_add(type="SUN", location=(20, -10, 30))
sun = bpy.context.active_object
sun.data.energy = 3.0
sun.data.color = (0.99, 0.88, 0.78)
sun.data.angle = math.radians(4.0)
dir_u = Vector((-0.406, -0.643, 0.650)).normalized()
dir_b = Vector((dir_u.x, -dir_u.z, dir_u.y)).normalized() * -1.0
sun.rotation_euler = dir_b.to_track_quat("-Z", "Y").to_euler()
scene.world = bpy.data.worlds.new("RoomWorld")
scene.world.use_nodes = True
bg = scene.world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (0.55, 0.62, 0.72, 1.0)
bg.inputs[1].default_value = 1.0

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 64
scene.cycles.use_denoising = False
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.view_settings.view_transform = "Standard"
scene.render.image_settings.file_format = "PNG"

def room_centre(anchor, radius=13.0):
    """Centroid of this room's floor tiles (the true walkable centre)."""
    xs, zs = [], []
    for tid, t in trs.items():
        go = gos.get(t["go"])
        if go is None or "FloorTile" not in go["name"]:
            continue
        wx, _, wz = t["pos"][0], t["pos"][1], t["pos"][2]
        if math.hypot(wx - anchor[0], wz - anchor[1]) <= radius:
            xs.append(wx)
            zs.append(wz)
    if not xs:
        return (anchor[0], anchor[1])
    return (sum(xs) / len(xs), sum(zs) / len(zs))

for (name, anchor, yaw, npc, npc_off) in ROOMS:
    built = build_room(anchor)
    cx, cz = room_centre(anchor)
    player = place_character(os.path.join(CHARDIR, "Ari/Ari.fbx"), (cx - 1.0, 0, cz + 0.5), yaw + 180, "Player_" + name)
    npc_h = place_character(os.path.join(CHARDIR, npc), (cx + npc_off[0] * 0.6, npc_off[1], cz + npc_off[2] * 0.6), (yaw + 180) % 360, "Npc_" + name)
    # camera: behind the player, over the shoulder, INSIDE the room
    r = math.radians(yaw + 180)
    fwd = Vector((math.sin(r), 0, math.cos(r)))
    pivot = Vector((cx - 1.0, 1.45, cz + 0.5))
    cam_pos_u = pivot - fwd * 3.2 + Vector((0, 0.9, 0)) + Vector((fwd.z, 0, -fwd.x)) * 0.32
    look_u = pivot + fwd * 6.0
    bpy.ops.object.camera_add()
    cam = bpy.context.active_object
    cam.name = "Cam_" + name
    cam.location = Vector((cam_pos_u.x, -cam_pos_u.z, cam_pos_u.y))
    look = Vector((look_u.x, -look_u.z, look_u.y))
    cam.rotation_euler = (look - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.angle = math.radians(2 * math.degrees(math.atan(math.tan(math.radians(55 / 2)) * (1280 / 720))))
    scene.camera = cam
    if os.environ.get("ROOM_DBG"):
        dg = bpy.context.evaluated_depsgraph_get()
        origin = cam.location.copy()
        d = (Vector((look_u.x, -look_u.z, look_u.y)) - origin).normalized()
        hit, loc, normal, idx, obj, matrix = scene.ray_cast(dg, origin, d)
        print("[dbg]", name, "centre", (round(cx,1), round(cz,1)), "cam", tuple(round(v,1) for v in cam.location),
              "first-hit:", (obj.name if hit else "nothing"))
    scene.render.filepath = os.path.join(OUT, name + "_raw.png")
    bpy.ops.render.render(write_still=True)
    print("[art-room] %s built=%d rendered" % (name, built))

print("[art-room] DONE")
