"""CROSSROADS prototype character build v2 — headless Blender 4.2.
Silhouette-proportioned rebuild of Ari (REF-01): mesh dimensions derived from
reference/concept/ari_proportions.json (measured off the approved turnaround),
ortho UV projection onto the front/back atlas, Mixamo-compatible rig,
auto weights, Idle/Walk/Turn actions, FBX exports, verification renders.
Run: blender -b -P scripts/blender_build_ari.py
"""
import bpy, bmesh, math, os, sys, json, traceback
from mathutils import Vector

OUT = "/home/user/Assets/_Project/Art/Characters/Ari"
RENDER = "/home/user/reference/prototype_renders"
PROP = json.load(open("/home/user/reference/concept/ari_proportions.json"))["front"]
os.makedirs(RENDER, exist_ok=True)

# ---- derive metric proportions from image measurements ----
H = 1.78
bw, bh = PROP["size"]
b0 = PROP["bbox"]
fig_h_px = (b0[2+1] - b0[1]) * bh          # bbox height px
fig_w_px = (b0[2] - b0[0]) * bw            # bbox width px
SCALE = H / fig_h_px                        # meters per px
SPAN = fig_w_px * SCALE                     # wrist-to-wrist outer span (m)
sp = PROP["spans"]
def row_w(name): return (sp[name][1] - sp[name][0]) * SPAN
def row_z(name): return (1 - (sp[name][0] + sp[name][1]) / 2 * 0 + (1 - ((0, 0)[0])))  # placeholder
# vertical centers (rows were sampled at fixed bbox-relative bands):
ZC = {"head": 1 - 0.065, "shoulder": 1 - 0.21, "wrist": 1 - 0.48,
      "hem": 1 - 0.59, "knee": 1 - 0.725, "ankle": 1 - 0.925}
ZC = {k: v * H for k, v in ZC.items()}
SH_X = row_w("shoulder") / 2 - 0.045
WR_X = SPAN / 2 - 0.045
HEAD_RX = row_w("head") / 2
HEM_RX = row_w("hem") / 2
print(f"[PROP] span={SPAN:.3f} sh_x={SH_X:.3f} wr_x={WR_X:.3f} head_rx={HEAD_RX:.3f} hem_rx={HEM_RX:.3f} z_sh={ZC['shoulder']:.3f} z_wr={ZC['wrist']:.3f} z_knee={ZC['knee']:.3f}")

def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def add_cube(loc, scale, name):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.active_object; o.name = name; o.scale = scale
    o.data.materials.append(mat)
    return o

def add_cyl(p1, p2, r1, r2, name, verts=16, ext=0.0):
    p1, p2 = Vector(p1), Vector(p2)
    d = (p2 - p1); L = d.length + 2 * ext
    c = (p1 + p2) / 2
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=L, location=c)
    o = bpy.context.active_object; o.name = name
    o.data.materials.append(mat)
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    return o

def add_sphere(loc, scale, name, seg=24, ring=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, location=loc)
    o = bpy.context.active_object; o.name = name; o.scale = scale
    o.data.materials.append(mat)
    return o

try:
    clean()
    mat = bpy.data.materials.new("M_Ari"); mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.65
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(f"{OUT}/Ari_Albedo.png")
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mat_hair = bpy.data.materials.new("M_Ari_Hair"); mat_hair.use_nodes = True
    hb = mat_hair.node_tree.nodes["Principled BSDF"]
    hb.inputs["Base Color"].default_value = (0.028, 0.031, 0.038, 1.0)  # #14161A-ish
    hb.inputs["Roughness"].default_value = 0.45
    mat_hair.diffuse_color = (0.028, 0.031, 0.038, 1.0)
    parts = []
    z_sh, z_wr, z_knee, z_ank = ZC["shoulder"], ZC["wrist"], ZC["knee"], ZC["ankle"]
    parts.append(add_cube((0, 0, 0.99), (0.135, 0.10, 0.13), "pelvis"))
    parts.append(add_cube((0, 0, 1.24), (HEM_RX * 0.92, 0.10, 0.17), "chest"))
    parts.append(add_cyl((0, 0, 1.38), (0, 0, 1.56), 0.048, 0.045, "neck", ext=0.02))
    parts.append(add_sphere((0, 0, 1.655), (HEAD_RX * 0.80, HEAD_RX * 0.92, 0.115), "head"))
    hair = add_sphere((0, 0.012, 1.675), (HEAD_RX * 0.95, HEAD_RX * 1.05, 0.122), "hair", seg=24, ring=16)
    bm = bmesh.new(); bm.from_mesh(hair.data)
    for v in list(bm.verts):
        wz = (hair.matrix_world @ v.co).z
        wy = (hair.matrix_world @ v.co).y
        if wz < 1.615 or (wy < -0.045 and wz < 1.685):
            bm.verts.remove(v)
    bm.to_mesh(hair.data); bm.free()
    sol = hair.modifiers.new("sol", 'SOLIDIFY'); sol.thickness = 0.02
    bpy.ops.object.modifier_apply(modifier="sol")
    hair.data.materials.clear()
    hair.data.materials.append(mat_hair)
    parts.append(hair)
    # shirt-jacket flare
    parts.append(add_cyl((0, 0, 0.96), (0, 0, 1.34), HEM_RX, HEM_RX * 0.95, "shirt", verts=16, ext=0.02))
    # arms + legs
    for sx, tag in ((1, "l"), (-1, "r")):
        S = Vector((sx * SH_X, 0, z_sh)); Wp = Vector((sx * WR_X, 0, z_wr))
        D = (Wp - S).normalized()
        elbow = S + (Wp - S) * 0.52
        parts.append(add_cyl(S, elbow, 0.047, 0.040, f"uparm_{tag}", ext=0.035))
        parts.append(add_cyl(elbow, Wp, 0.038, 0.030, f"forearm_{tag}", ext=0.035))
        hand = add_cube(Wp + D * 0.06, (0.035, 0.045, 0.08), f"hand_{tag}")
        hand.rotation_mode = 'QUATERNION'; hand.rotation_quaternion = D.to_track_quat('Z', 'Y')
        parts.append(hand)
        parts.append(add_cyl((sx * 0.095, 0, 0.97), (sx * 0.100, 0, z_knee), 0.075, 0.056, f"thigh_{tag}", ext=0.04))
        parts.append(add_cyl((sx * 0.100, 0, z_knee), (sx * 0.105, 0, z_ank), 0.054, 0.038, f"shin_{tag}", ext=0.04))
        parts.append(add_cube((sx * 0.105, -0.055, 0.045), (0.046, 0.115, 0.042), f"foot_{tag}"))
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.active_object; body.name = "Ari_Body"
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(40))
    except Exception:
        bpy.ops.object.shade_smooth()
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"[BUILD] mesh tris = {tris}")

    # ---- UV ortho projection onto atlas (front left half, back right half) ----
    me = body.data
    if not me.uv_layers: me.uv_layers.new(name="UVMap")
    uvl = me.uv_layers.active.data
    xs = [v.co.x for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    minx, maxx, minz, maxz = min(xs), max(xs), min(zs), max(zs)
    w, hh = maxx - minx, maxz - minz
    for poly in me.polygons:
        front_face = poly.normal.y < 0.0
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            u = (co.x - minx) / w
            v = (co.z - minz) / hh
            if front_face:
                uvl[li].uv = (u * 0.5, v)
            else:
                uvl[li].uv = (0.5 + (1.0 - u) * 0.5, v)

    # ---- ARMATURE ----
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm_obj = bpy.context.active_object; arm_obj.name = "Ari_Rig"
    arm = arm_obj.data; arm.name = "Ari_Rig"
    eb = arm.edit_bones; eb.remove(eb[0])
    def bone(name, head, tail, parent=None, connect=False):
        b = eb.new(name); b.head = head; b.tail = tail
        if parent: b.parent = eb[parent]; b.use_connect = connect
        return b
    bone("Hips", (0, 0, 0.94), (0, 0, 1.06))
    bone("Spine", (0, 0, 1.06), (0, 0, 1.18), "Hips", True)
    bone("Spine1", (0, 0, 1.18), (0, 0, 1.30), "Spine", True)
    bone("Spine2", (0, 0, 1.30), (0, 0, 1.42), "Spine1", True)
    bone("Neck", (0, 0, 1.42), (0, 0, 1.52), "Spine2", True)
    bone("Head", (0, 0, 1.52), (0, 0, 1.78), "Neck", True)
    for sx, L in ((1, "Left"), (-1, "Right")):
        S = Vector((sx * SH_X, 0, z_sh)); Wp = Vector((sx * WR_X, 0, z_wr))
        D = (Wp - S).normalized(); elbow = S + (Wp - S) * 0.52
        bone(f"{L}Shoulder", (sx * 0.02, 0, z_sh - 0.01), S, "Spine2")
        bone(f"{L}Arm", S, elbow, f"{L}Shoulder")
        bone(f"{L}ForeArm", elbow, Wp, f"{L}Arm", True)
        bone(f"{L}Hand", Wp, Wp + D * 0.11, f"{L}ForeArm", True)
        bone(f"{L}UpLeg", (sx * 0.095, 0, 0.94), (sx * 0.100, 0, z_knee), "Hips")
        bone(f"{L}Leg", (sx * 0.100, 0, z_knee), (sx * 0.105, 0, z_ank), f"{L}UpLeg", True)
        bone(f"{L}Foot", (sx * 0.105, 0, z_ank), (sx * 0.105, -0.16, 0.03), f"{L}Leg", True)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True); arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    print("[BUILD] rig bones:", len(arm.bones))

    # ---- ANIMATIONS ----
    def new_action(name):
        act = bpy.data.actions.new(name)
        arm_obj.animation_data_create()
        arm_obj.animation_data.action = act
        return act
    pb = arm_obj.pose.bones
    for b in pb: b.rotation_mode = 'XYZ'
    def key(bn, f, rot=None, loc=None):
        b = pb[bn]
        if rot: b.rotation_euler = rot
        if loc: b.location = loc
        b.keyframe_insert("rotation_euler", frame=f)
        if loc: b.keyframe_insert("location", frame=f)
    new_action("Idle")
    for f in range(1, 61):
        t = (f - 1) / 59.0 * 2 * math.pi
        key("Spine1", f, rot=(math.radians(1.2) * math.sin(t), 0, 0))
        key("Spine2", f, rot=(math.radians(0.8) * math.sin(t + 0.5), 0, 0))
        key("Head", f, rot=(math.radians(0.6) * math.sin(t + 1.0), 0, math.radians(1.0) * math.sin(t * 0.5)))
        key("LeftArm", f, rot=(math.radians(1.5) * math.sin(t), 0, 0))
        key("RightArm", f, rot=(math.radians(1.5) * math.sin(t + math.pi), 0, 0))
        key("Hips", f, loc=(0, 0, 0.004 * math.sin(t)))
    new_action("Walk")
    for f in range(1, 37):
        t = (f - 1) / 36.0 * 2 * math.pi
        sw = math.sin(t)
        key("LeftUpLeg", f, rot=(math.radians(22) * sw, 0, 0))
        key("RightUpLeg", f, rot=(math.radians(22) * -sw, 0, 0))
        key("LeftLeg", f, rot=(math.radians(30) * max(0, -math.sin(t + 0.6)), 0, 0))
        key("RightLeg", f, rot=(math.radians(30) * max(0, -math.sin(t + 0.6 + math.pi)), 0, 0))
        key("LeftArm", f, rot=(math.radians(18) * -sw, 0, 0))
        key("RightArm", f, rot=(math.radians(18) * sw, 0, 0))
        key("LeftForeArm", f, rot=(math.radians(12) * (0.5 + 0.5 * math.sin(t + 1.2)), 0, 0))
        key("RightForeArm", f, rot=(math.radians(12) * (0.5 + 0.5 * math.sin(t + 1.2 + math.pi)), 0, 0))
        key("Hips", f, loc=(0, 0, 0.015 * abs(math.sin(t))), rot=(0, 0, math.radians(3) * sw))
        key("Spine1", f, rot=(math.radians(2.0), 0, math.radians(-2) * sw))
    new_action("Turn")
    for f in (1, 10, 20, 30):
        a = {1: 0, 10: 25, 20: 65, 30: 90}[f]
        key("Hips", f, rot=(0, 0, math.radians(a)))
        key("Spine2", f, rot=(0, 0, math.radians(-a * 0.15)))
        key("Head", f, rot=(0, 0, math.radians(-a * 0.10)))

    # ---- EXPORT ----
    def export_fbx(path, anim=None):
        bpy.ops.object.select_all(action='DESELECT')
        body.select_set(True); arm_obj.select_set(True)
        bpy.context.view_layer.objects.active = arm_obj
        if anim:
            act = bpy.data.actions[anim]
            arm_obj.animation_data.action = act
            fr = int(act.frame_range[1])
            bpy.context.scene.frame_start = int(act.frame_range[0])
            bpy.context.scene.frame_end = fr
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                                 add_leaf_bones=False, use_armature_deform_only=True,
                                 mesh_smooth_type='FACE', path_mode='STRIP',
                                 bake_anim=bool(anim), bake_anim_use_all_actions=False,
                                 bake_anim_use_nla_strips=False, apply_scale_options='FBX_SCALE_ALL')
    for old in ("Ari.fbx", "Ari_Idle.fbx", "Ari_Walk.fbx", "Ari_Turn.fbx"):
        p = f"{OUT}/{old}"
        if os.path.exists(p): os.remove(p)
    export_fbx(f"{OUT}/Ari.fbx", None)
    for a in ("Idle", "Walk", "Turn"):
        export_fbx(f"{OUT}/Ari_{a}.fbx", a)
    print("[BUILD] exports done")

    # ---- VERIFICATION RENDERS ----
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.light = 'FLAT'
    scene.render.resolution_x = 512; scene.render.resolution_y = 768
    cam_data = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    tgt = bpy.data.objects.new("tgt", None); scene.collection.objects.link(tgt)
    tgt.location = (0, 0, 1.0)
    con = cam.constraints.new('TRACK_TO'); con.target = tgt
    scene.camera = cam
    def shoot(loc, path, act, frame, tloc=(0, 0, 1.0)):
        cam.location = loc; tgt.location = tloc
        arm_obj.animation_data.action = bpy.data.actions[act]
        scene.frame_set(frame)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
    shoot((0, -3.6, 1.35), f"{RENDER}/verify_front_idle.png", "Idle", 1)
    shoot((3.4, -1.4, 1.30), f"{RENDER}/verify_side_walk.png", "Walk", 10)
    shoot((0.5, -0.7, 1.68), f"{RENDER}/verify_face.png", "Idle", 1, tloc=(0, 0, 1.63))
    shoot((0, -3.6, 1.35), f"{RENDER}/verify_front_walk.png", "Walk", 19)
    print("[BUILD] renders done")
    print("[BUILD] SUCCESS")
except Exception:
    traceback.print_exc()
    sys.exit(1)
