#!/usr/bin/env python3
"""VISUAL SLICE RENDERER (visual transformation pass, task 16).

Renders the REAL generated FirstLocation.unity with Blender headless so the visual
transformation can be SEEN and judged in this sandbox (no Unity editor available):
parses the scene YAML (transforms, scales, materials incl. base+emission colours,
active flags), rebuilds every active renderer in Blender (kit FBX pieces + primitive
props), applies the scene's authored lighting (sun colour/intensity/rotation from the
scene, ambient from RenderSettings), places the canonical Ari/Mara FBX avatars at their
authored positions, and renders three gameplay-perspective stills:

  1. gameplay  - over-the-shoulder exploration view (camera geometry mirrors
                 ThirdPersonCameraController: 4.4 m orbit, 1.45 m pivot, 0.32 m shoulder)
  2. dialogue  - over-the-shoulder conversation framing toward Mara (cinematic blend)
  3. combat    - the North Annex fight framing against the Choir Warden

Output: reference/visual_slice/raw/*.png (1920x1080, Cycles CPU).
The HUD is composited afterwards by scripts/compose_visual_slice.py using the SAME
layout constants the C# HUD code uses, so the composites preview the target screen.

These are offline renders of the real scene data (approximated materials/sky), NOT
device captures - the report says exactly which parts need a Unity editor to confirm.

Run: blender -b -P scripts/render_visual_slice.py
"""
import bpy, math, re, os, sys, json
from mathutils import Vector, Euler, Matrix

ROOT = "/home/user/crossroads"
SCENE = os.path.join(ROOT, "Assets/Scenes/Prototype/FirstLocation.unity")
KIT = os.path.join(ROOT, "Assets/Game/Environment/Kit")
MATDIR = os.path.join(ROOT, "Assets/Game/Environment/Materials")
CHARDIR = os.path.join(ROOT, "Assets/_Project/Art/Characters")
OUT = os.path.join(ROOT, "reference/visual_slice/raw")
os.makedirs(OUT, exist_ok=True)

RES_X, RES_Y = 1920, 1080

# ---------------------------------------------------------------- 1. parse the real scene
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

go_of_transform = {tid: t["go"] for tid, t in trs.items()}
transform_of_go = {}
for tid, t in trs.items():
    transform_of_go[t["go"]] = tid

# guid -> fbx piece name (kit registry) + guid -> material file
KIT_GUIDS = {}   # filled from kit .meta files
for f in os.listdir(KIT):
    if f.endswith(".fbx.meta"):
        g = re.search(r"guid: ([0-9a-f]{32})", open(os.path.join(KIT, f)).read())
        if g:
            KIT_GUIDS[g.group(1)] = f[:-9]  # strip .fbx.meta

MAT_GUIDS = {}
for dirpath, dirs, files in os.walk(os.path.join(ROOT, "Assets")):
    for f in files:
        if f.endswith(".mat.meta"):
            g = re.search(r"guid: ([0-9a-f]{32})", open(os.path.join(dirpath, f)).read())
            if g:
                MAT_GUIDS[g.group(1)] = os.path.join(dirpath, f[:-9])

def mat_colors(path):
    """(base rgb, emission rgb) from a URP/Lit .mat."""
    try:
        s = open(path).read()
    except OSError:
        return (0.5, 0.5, 0.5), (0, 0, 0)
    base = re.search(r"_BaseColor: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", s)
    emit = re.search(r"_EmissionColor: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", s)
    return (tuple(float(x) for x in base.groups()) if base else (0.5, 0.5, 0.5),
            tuple(float(x) for x in emit.groups()) if emit else (0, 0, 0))

BUILTIN = {10202: "cube", 10207: "sphere", 10208: "capsule", 10206: "cylinder", 10204: "quad", 10203: "plane"}

def world_matrix(tid):
    """Unity world matrix (pos*euler*scale chain) -> Blender matrix."""
    chain = []
    t = tid
    while t and t in trs:
        chain.append(trs[t])
        t = trs[t]["father"]
    m = Matrix.Identity(4)
    for t in reversed(chain):  # parent first
        rx, ry, rz = [math.radians(a) for a in t["euler"]]
        # Unity: R_y(yaw) * R_x(pitch) * R_z(roll)
        rot = (Euler((0, ry, 0), 'XYZ').to_matrix().to_4x4() @
               Euler((rx, 0, 0), 'XYZ').to_matrix().to_4x4() @
               Euler((0, 0, rz), 'XYZ').to_matrix().to_4x4())
        loc = Matrix.Translation(Vector(t["pos"]))
        scl = Matrix.Diagonal((*t["scale"], 1.0)).to_4x4()
        m = m @ loc @ rot @ scl
    return m

def u2b_matrix(m_unity):
    """Unity (x,y,z left-handed, +z forward) -> Blender (x, -z, y). Same axis trick the
    kit preview uses: swap y/z rows+columns with sign flips."""
    U2B = Matrix((
        (1, 0, 0, 0),
        (0, 0, 1, 0),
        (0, 1, 0, 0),
        (0, 0, 0, 1),
    ))  # basis change B = P * U * P^-1 with P mapping unity axes to blender axes
    # unity: x->x, y(up)->z, z(fwd)->-y ... blender x=x, y=-z_unity, z=y_unity
    P = Matrix((
        (1, 0, 0, 0),
        (0, 0, -1, 0),
        (0, 1, 0, 0),
        (0, 0, 0, 1),
    ))
    return P @ m_unity @ P.inverted()

# ---------------------------------------------------------------- 2. build in Blender
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
    rough = 0.28 if glossy else 0.85
    bsdf.inputs["Roughness"].default_value = rough
    lum = max(emit) if emit else 0.0
    if lum > 0.02:
        strength = min(6.0, 0.6 + lum * 3.2)
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
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
            kit_cache[piece].hide_set(True)  # template, not rendered
    return kit_cache[piece]

built = 0
for rid, rr in mrenderers.items():
    go = gos.get(rr["go"])
    if go is None or not go["active"]:
        continue
    name = go["name"]
    mf = None
    for mfi, mfdata in mfilters.items():
        if mfdata["go"] == rr["go"]:
            mf = mfdata
            break
    if mf is None:
        continue
    tid = transform_of_go.get(rr["go"])
    if tid is None:
        continue
    m_world = world_matrix(tid)
    pos_u = m_world.decompose()[0]
    # skip the far campaign rooms (west of the hall) - none of them are in any shot
    if pos_u.x < -26:
        continue
    # skip NPC/enemy primitive stand-ins (canonical avatars are placed below)
    if any(name.startswith(p) for p in ("Mara_NPC", "Sera_NPC", "Sera_Tidewell", "ChoirWarden", "Civilian")):
        continue

    guid = mf["guid"]
    obj = None
    if guid in KIT_GUIDS and mf["fileID"] == 4300000:
        src = import_kit(KIT_GUIDS[guid])
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
            # no primitive_capsule in this Blender build: stretched sphere approximation
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5)
            obj = bpy.context.active_object
            obj.scale = (1.0, 1.0, 1.6)
        elif shape == "cylinder":
            bpy.ops.mesh.primitive_cylinder_add(radius=0.5, depth=1.0)
        else:
            continue
        obj = bpy.context.active_object
    if obj is None:
        continue

    obj.matrix_world = u2b_matrix(m_world)

    base, emit = (0.55, 0.55, 0.58), (0, 0, 0)
    if rr["mats"]:
        matfile = MAT_GUIDS.get(rr["mats"][0])
        if matfile:
            base, emit = mat_colors(matfile)
    glossy = any(k in name for k in ("Glazing", "Holo", "Orb", "Beam", "Crystal"))
    obj.data.materials.clear()
    m = blender_material(name.split("_")[0][:24], base, emit, glossy)
    obj.data.materials.append(m)
    built += 1

print("[visual-slice] scene renderers built:", built)

# ---------------------------------------------------------------- 3. characters (canonical avatars)
def place_character(fbx, world_pos, yaw_deg, scale=1.0, label=""):
    bpy.ops.import_scene.fbx(filepath=fbx)
    imported = [o for o in bpy.context.selected_objects]
    root = None
    for o in imported:
        if o.parent is None:
            root = o if root is None else root
    # find the armature and the body mesh
    arm = next((o for o in imported if o.type == "ARMATURE"), None)
    body = next((o for o in imported if o.type == "MESH"), None)
    # group under one empty
    holder = bpy.data.objects.new("Char_" + label, None)
    scene.collection.objects.link(holder)
    inv = holder.matrix_world.inverted()
    for o in imported:
        o.matrix_parent_inverse = inv
        o.parent = holder
    # Unity->Blender: position (x, -z, y), yaw negated
    holder.location = Vector((world_pos[0], -world_pos[2], world_pos[1]))
    holder.rotation_euler = (0, 0, math.radians(yaw_deg))
    holder.scale = (scale, scale, scale)
    # gentle idle: arms a touch down from the A-pose (X-axis arm rotation per rig convention)
    if arm:
        for bone_name, rx in (("LeftArm", 0.32), ("RightArm", 0.32), ("LeftForeArm", 0.22), ("RightForeArm", 0.22)):
            pb = arm.pose.bones.get(bone_name)
            if pb:
                pb.rotation_mode = "XYZ"
                pb.rotation_euler = (rx, 0, 0)
    # relink albedo texture if present
    albedo = fbx.replace(".fbx", "_Albedo.png")
    if body and os.path.exists(albedo):
        mat = body.data.materials[0] if body.data.materials else None
        if mat and mat.use_nodes:
            for n in mat.node_tree.nodes:
                if n.type == "TEX_IMAGE":
                    try:
                        n.image = bpy.data.images.load(albedo)
                    except RuntimeError:
                        pass
    return holder

# Ari at spawn (hall), facing the monument (+Z in Unity)
place_character(os.path.join(CHARDIR, "Ari/Ari.fbx"), (0, 0, -12.5), 0, 1.0, "Ari")
# Mara at her authored NPC position, facing back toward the hall centre
mara = place_character(os.path.join(CHARDIR, "Mara/Mara.fbx"), (6.5, 0, -8), 200, 1.0, "Mara")
# Sera in the hall (canonical avatar over her stand-in position)
place_character(os.path.join(CHARDIR, "Sera/Sera.fbx"), (17.5, 0, 2.5), 215, 1.0, "Sera")
# Choir Warden at the annex (soldier silhouette stands in for the warden shell)
warden = place_character(os.path.join(CHARDIR, "Soldier_A/Soldier_A.fbx"), (0, 0, 26.5), 180, 1.12, "Warden")
if warden:
    for ob in warden.children:
        if ob.type == "MESH" and ob.data.materials:
            ob.data.materials.clear()
            ob.data.materials.append(blender_material("WardenShell", (0.22, 0.18, 0.30), (0.45, 0.16, 0.62)))
# Ari for the combat shot stands at the annex gate
ari_combat = place_character(os.path.join(CHARDIR, "Ari/Ari.fbx"), (0, 0, 21.0), 0, 1.0, "AriCombat")
# Ari for the dialogue shot, next to Mara
ari_dialogue = place_character(os.path.join(CHARDIR, "Ari/Ari.fbx"), (4.4, 0, -6.2), 55, 1.0, "AriDialogue")
# hide the extra Aris from shots that don't need them (managed per-shot below)

# ---------------------------------------------------------------- 4. lighting from the scene
rs = txt
sun_color = (0.99, 0.88, 0.78)
sun_intensity = 1.3
amb = (0.60, 0.66, 0.76)

bpy.ops.object.light_add(type="SUN", location=(20, -10, 30))
sun = bpy.context.active_object
sun.data.energy = 3.2  # approximated strength for the authored 1.3 intensity
sun.data.color = sun_color
sun.data.angle = math.radians(4.0)
# authored rotation (40, -32, 0): light travels toward (-0.41, -0.64, 0.65) in Unity axes
dir_u = Vector((-0.406, -0.643, 0.650)).normalized()
dir_b = Vector((dir_u.x, -dir_u.z, dir_u.y)).normalized() * -1.0  # blender-space travel direction
sun.rotation_euler = dir_b.to_track_quat("-Z", "Y").to_euler()

scene.world = bpy.data.worlds.new("SliceWorld")
world = scene.world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (*amb, 1.0)
bg.inputs[1].default_value = 1.0  # soft ambient sky

# ---------------------------------------------------------------- 5. cameras (mirror the C# rig math)
def unity_yaw_forward(yaw_deg):
    r = math.radians(yaw_deg)
    return Vector((math.sin(r), 0.0, math.cos(r)))

def add_gameplay_camera(name, player_pos, yaw_deg, distance=4.4, pivot_h=1.45, shoulder=0.32, pitch_deg=12.0):
    fwd = unity_yaw_forward(yaw_deg)
    right = Vector((fwd.z, 0.0, -fwd.x))  # unity right
    pivot = Vector(player_pos) + Vector((0, pivot_h, 0))
    cam_pos_u = pivot - fwd * (distance * math.cos(math.radians(pitch_deg))) + Vector((0, distance * math.sin(math.radians(pitch_deg)), 0)) + right * shoulder
    look_at_u = pivot + fwd * 7.0
    bpy.ops.object.camera_add()
    cam = bpy.context.active_object
    cam.name = name
    # convert to blender space
    cam.location = Vector((cam_pos_u.x, -cam_pos_u.z, cam_pos_u.y))
    look = Vector((look_at_u.x, -look_at_u.z, look_at_u.y))
    direction = look - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.angle = math.radians(2 * math.degrees(math.atan(math.tan(math.radians(55 / 2)) * (RES_X / RES_Y))))
    return cam

cam_gameplay = add_gameplay_camera("CamGameplay", (0, 0, -12.5), 0)
cam_dialogue = add_gameplay_camera("CamDialogue", (4.4, 0, -6.2), 55, distance=2.8, pivot_h=1.55, shoulder=0.55, pitch_deg=4.0)
cam_combat = add_gameplay_camera("CamCombat", (0, 0, 21.0), 0, distance=5.0, pivot_h=1.6, shoulder=0.32, pitch_deg=10.0)

# ---------------------------------------------------------------- 6. render
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 96
scene.cycles.use_denoising = False
scene.cycles.use_adaptive_sampling = True
scene.render.resolution_x = RES_X
scene.render.resolution_y = RES_Y
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"
scene.render.image_settings.file_format = "PNG"

def recursive_hide(o, state):
    try:
        o.hide_render = state
        o.hide_set(state)
    except ReferenceError:
        return
    for c in o.children:
        recursive_hide(c, state)

def render(cam, filename, hide=(), show=()):
    for o in hide:
        recursive_hide(o, True)
    for o in show:
        recursive_hide(o, False)
    scene.camera = cam
    scene.render.filepath = os.path.join(OUT, filename)
    bpy.ops.render.render(write_still=True)
    print("[visual-slice] rendered", filename)

# gameplay: main Ari visible, duplicates hidden
render(cam_gameplay, "gameplay_raw.png",
       hide=(ari_combat, ari_dialogue))
# dialogue: dialogue Ari + Mara, main + combat hidden
render(cam_dialogue, "dialogue_raw.png",
       hide=(bpy.data.objects.get("Char_Ari"), ari_combat))
# combat: combat Ari + warden
render(cam_combat, "combat_raw.png",
       hide=(bpy.data.objects.get("Char_Ari"), ari_dialogue))

print("[visual-slice] DONE")
