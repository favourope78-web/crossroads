"""CROSSROADS release pass — canonical character models (headless Blender 4.3).

One rigged, skinned, textured humanoid per cast member, built with the SAME method that produced
the approved Ari prototype (scripts/blender_build_ari.py): body proportions measured row by row
from that character's turnaround sheet (reference/concept/build/<name>_atlas.json, written by
scripts/build_character_atlases.py), ortho front/back UV projection onto the character's albedo
atlas, a Mixamo-named humanoid skeleton (Hips/Spine/Spine1/Spine2/Neck/Head/L|R Shoulder/Arm/
ForeArm/Hand/UpLeg/Leg/Foot/ToeBase) so Unity's Humanoid avatar maps it automatically, auto
weights, and ONE FBX per character.

Consistency law (CHARACTER_REFERENCE §2): one character = one canonical mesh reused everywhere.
Mara's two outfits are two mesh sets on the same body proportions (Mara.fbx hoodie / Mara_Dress.fbx
combat dress), the face/hair build is identical.

Also exports the shared animation library, authored once on the canonical skeleton and imported
as Humanoid so every character (and Ari) retargets it:
   Anim_Idle, Anim_Walk, Anim_Run, Anim_Talk (interaction), Anim_Attack, Anim_Hit, Anim_Dodge,
   Anim_Defeat, Anim_Alert (reaction)

Run:  blender -b -P scripts/blender_build_characters.py            (all)
      blender -b -P scripts/blender_build_characters.py -- Mara     (one)
"""
import bpy, bmesh, math, os, sys, json, traceback
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
BUILD = os.path.join(ROOT, "reference", "concept", "build")
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters")
RENDER = os.path.join(ROOT, "reference", "prototype_renders")
os.makedirs(RENDER, exist_ok=True)

# canonical heights (CHARACTER_REFERENCE §3 / §4) — metres, top of head
HEIGHT = {"Ari": 1.78, "Mara": 1.68, "Mara_Dress": 1.68, "Dax": 1.78, "Archivist": 1.86, "Kael": 1.80, "Odalys": 1.66,
          "Bran": 1.84, "Sera": 1.64, "Civilian": 1.74, "Soldier_A": 1.78, "Soldier_B": 1.86, "Soldier_C": 1.80}
# hair silhouette recipe per character (sheet: REF-02 high ponytail, REF-03 side sweep, REF-05 floor-length)
HAIR = {"Ari": "shag", "Mara": "ponytail", "Mara_Dress": "ponytail", "Dax": "sweep", "Archivist": "floor", "Kael": "crop",
        "Odalys": "bun", "Bran": "bald", "Sera": "bob", "Civilian": "bowl", "Soldier_A": "crop",
        "Soldier_B": "bald", "Soldier_C": "crop"}
SKIRT = {"Mara_Dress": (0.62, 0.36), "Odalys": (0.98, 0.10), "Kael": (0.86, 0.22)}   # (hem z fraction of height, flare)
GLASSES = {"Dax"}
BULK = {"Bran": 1.12, "Soldier_B": 1.15, "Soldier_A": 1.02, "Kael": 1.02}


def log(*a):
    print("[CHAR]", *a)
    sys.stdout.flush()


def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)


class Builder:
    def __init__(self, name):
        self.name = name
        self.info = json.load(open(os.path.join(BUILD, name.lower() + "_atlas.json")))
        self.H = HEIGHT.get(name, 1.75)
        self.out_dir = os.path.join(ART, name)
        os.makedirs(self.out_dir, exist_ok=True)
        f = self.info["front"]
        bw, bh = f["size"]
        sp = f["spans"]
        # metres per bbox unit: the sheet figure fills its bbox from hair top to sole
        self.SCALE = self.H / bh
        def w(k):
            return (sp[k][1] - sp[k][0]) * bw * self.SCALE
        bulk = BULK.get(name, 1.0)
        self.SPAN = w("wrist")                    # wrist-to-wrist outer span (A-pose)
        self.SH_X = max(0.16, w("shoulder") / 2 - 0.04) * bulk
        self.HEAD_RX = max(0.085, w("head") / 2)
        self.TORSO_RX = min(max(0.12, w("torso") / 2), self.SH_X * 0.92) * bulk
        self.HEM_RX = max(0.12, w("hem") / 2)
        self.KNEE_RX = max(0.07, w("knee") / 4)
        self.ANKLE_RX = max(0.05, w("ankle") / 4)
        # vertical landmarks (bbox-relative bands identical to the atlas measurement)
        Z = {"head": 1 - 0.065, "shoulder": 1 - 0.20, "wrist": 1 - f["wrist_y"], "hem": 1 - 0.59,
             "knee": 1 - 0.725, "ankle": 1 - 0.925}
        self.Z = {k: v * self.H for k, v in Z.items()}
        self.WR_X = self.SPAN / 2 - 0.04
        log(name, "H=%.2f span=%.3f sh_x=%.3f head_rx=%.3f torso_rx=%.3f hem_rx=%.3f z_sh=%.2f z_wr=%.2f z_knee=%.2f" % (
            self.H, self.SPAN, self.SH_X, self.HEAD_RX, self.TORSO_RX, self.HEM_RX, self.Z["shoulder"], self.Z["wrist"], self.Z["knee"]))

    # ---------------------------------------------------------------- primitives
    def _mat(self, o):
        o.data.materials.append(self.mat)

    def cube(self, loc, scale, name):
        bpy.ops.mesh.primitive_cube_add(location=loc)
        o = bpy.context.active_object; o.name = name; o.scale = scale
        self._mat(o); return o

    def cyl(self, p1, p2, r1, r2, name, verts=14, ext=0.0):
        p1, p2 = Vector(p1), Vector(p2)
        d = (p2 - p1); L = d.length + 2 * ext
        c = (p1 + p2) / 2
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=L, location=c)
        o = bpy.context.active_object; o.name = name
        self._mat(o)
        o.rotation_mode = 'QUATERNION'
        o.rotation_quaternion = d.to_track_quat('Z', 'Y')
        return o

    def sphere(self, loc, scale, name, seg=20, ring=12):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, location=loc)
        o = bpy.context.active_object; o.name = name; o.scale = scale
        self._mat(o); return o

    # ---------------------------------------------------------------- body
    def build_body(self):
        Z = self.Z
        parts = []
        z_hip = Z["knee"] + (Z["hem"] - Z["knee"]) * 0.95
        z_chest = Z["shoulder"] - 0.16
        z_neck0 = Z["shoulder"] - 0.02
        z_head_c = Z["head"] - 0.02
        # trunk: pelvis block + chest block + tapered waist cylinder
        parts.append(self.cube((0, 0, z_hip + 0.03), (self.HEM_RX * 0.95, 0.105, 0.11), "pelvis"))
        parts.append(self.cyl((0, 0, z_hip + 0.10), (0, 0, z_chest + 0.02), self.HEM_RX * 0.92, self.TORSO_RX * 0.98, "waist", verts=16))
        parts.append(self.cube((0, 0, z_chest + 0.10), (self.TORSO_RX, 0.115, 0.14), "chest"))
        parts.append(self.cyl((0, 0, z_neck0), (0, 0, z_head_c - self.HEAD_RX * 0.9), 0.05, 0.047, "neck", ext=0.02))
        parts.append(self.sphere((0, 0, z_head_c), (self.HEAD_RX * 0.82, self.HEAD_RX * 0.95, self.HEAD_RX * 1.08), "head"))
        # arms + legs
        for sx, tag in ((1, "l"), (-1, "r")):
            S = Vector((sx * self.SH_X, 0, Z["shoulder"])); Wp = Vector((sx * self.WR_X, 0, Z["wrist"]))
            D = (Wp - S).normalized()
            elbow = S + (Wp - S) * 0.52
            parts.append(self.sphere(S + Vector((0, 0, -0.01)), (0.06 * BULK.get(self.name, 1.0), 0.058, 0.062), "deltoid_" + tag, seg=12, ring=8))
            parts.append(self.cyl(S, elbow, 0.052 * BULK.get(self.name, 1.0), 0.042, "uparm_" + tag, ext=0.035))
            parts.append(self.cyl(elbow, Wp, 0.040, 0.031, "forearm_" + tag, ext=0.035))
            hand = self.cube(Wp + D * 0.065, (0.036, 0.046, 0.085), "hand_" + tag)
            hand.rotation_mode = 'QUATERNION'; hand.rotation_quaternion = D.to_track_quat('Z', 'Y')
            parts.append(hand)
            hx = self.HEM_RX * 0.5
            parts.append(self.cyl((sx * hx, 0, z_hip), (sx * (hx + 0.005), 0, Z["knee"]), self.HEM_RX * 0.46, self.KNEE_RX, "thigh_" + tag, ext=0.04))
            parts.append(self.cyl((sx * (hx + 0.005), 0, Z["knee"]), (sx * (hx + 0.01), 0, Z["ankle"]), self.KNEE_RX * 0.98, self.ANKLE_RX, "shin_" + tag, ext=0.04))
            parts.append(self.cube((sx * (hx + 0.01), -0.055, 0.045), (0.048, 0.12, 0.044), "foot_" + tag))
        # skirt / coat flare (Mara dress, Odalys robe, Kael long coat)
        if self.name in SKIRT:
            hem_frac, flare = SKIRT[self.name]
            z_top = z_hip + 0.06
            z_bot = self.H * (1 - hem_frac) if hem_frac < 0.9 else 0.08
            if self.name == "Mara_Dress": z_bot = Z["knee"] + 0.02
            if self.name == "Kael": z_bot = Z["knee"] - 0.12
            if self.name == "Odalys": z_bot = 0.06
            parts.append(self.cyl((0, 0, z_top), (0, 0, z_bot), self.HEM_RX * 1.02, self.HEM_RX * (1.0 + flare), "skirt", verts=18))
        return parts

    def build_hair(self):
        """Chunky shell hair in the character's sheet silhouette (style bible: sculpted clusters, no strands)."""
        style = HAIR.get(self.name, "crop")
        if style == "bald":
            return None
        rx, ry, rz = self.HEAD_RX * 0.86, self.HEAD_RX * 1.0, self.HEAD_RX * 1.12
        z_head_c = self.Z["head"] - 0.02
        hair = self.sphere((0, 0.012, z_head_c + 0.012), (rx * 1.06, ry * 1.06, rz * 1.02), "hair", seg=20, ring=14)
        bm = bmesh.new(); bm.from_mesh(hair.data)
        hair_mw = hair.matrix_world
        side_drop = {"crop": 0.03, "sweep": 0.045, "bowl": 0.05, "bob": 0.11, "ponytail": 0.05, "bun": 0.045, "floor": 0.06, "shag": 0.06}[style]
        fringe = {"crop": 0.05, "sweep": 0.02, "bowl": 0.01, "bob": 0.02, "ponytail": 0.035, "bun": 0.045, "floor": 0.03, "shag": 0.015}[style]
        for v in list(bm.verts):
            p = hair_mw @ v.co
            front_face = p.y < -0.03
            if p.z < z_head_c - side_drop:
                bm.verts.remove(v)            # below the ear line: skin
            elif front_face and p.z < z_head_c + fringe:
                bm.verts.remove(v)            # the face stays skin; fringe from the brow line up
        bm.to_mesh(hair.data); bm.free()
        sol = hair.modifiers.new("sol", 'SOLIDIFY'); sol.thickness = 0.022
        bpy.ops.object.modifier_apply(modifier="sol")
        hair.data.materials.clear(); hair.data.materials.append(self.mat_hair)
        extras = [hair]
        # signature masses
        if style == "ponytail":
            tail = self.cyl((0, 0.10, z_head_c + 0.06), (0, 0.16, z_head_c - 0.34), 0.045, 0.02, "ponytail", verts=10)
            tail.data.materials.clear(); tail.data.materials.append(self.mat_hair); extras.append(tail)
            knot = self.sphere((0, 0.085, z_head_c + 0.07), (0.05, 0.05, 0.045), "hair_knot", seg=12, ring=8)
            knot.data.materials.clear(); knot.data.materials.append(self.mat_hair); extras.append(knot)
        elif style == "bun":
            knot = self.sphere((0, 0.09, z_head_c - 0.02), (0.055, 0.05, 0.05), "hair_bun", seg=12, ring=8)
            knot.data.materials.clear(); knot.data.materials.append(self.mat_hair); extras.append(knot)
        elif style == "floor":
            fall = self.cyl((0, 0.075, z_head_c + 0.02), (0, 0.11, 0.12), 0.11, 0.16, "hair_fall", verts=14)
            fall.data.materials.clear(); fall.data.materials.append(self.mat_hair); extras.append(fall)
        elif style == "sweep":
            sweep = self.sphere((0.035, -0.045, z_head_c + 0.075), (0.085, 0.06, 0.035), "hair_sweep", seg=12, ring=8)
            sweep.data.materials.clear(); sweep.data.materials.append(self.mat_hair); extras.append(sweep)
        return extras

    def build_glasses(self):
        z = self.Z["head"] - 0.035
        parts = []
        for sx in (1, -1):
            bpy.ops.mesh.primitive_torus_add(location=(sx * 0.031, -self.HEAD_RX * 0.93, z), rotation=(math.radians(90), 0, 0),
                                             major_segments=12, minor_segments=5, major_radius=0.024, minor_radius=0.0025)
            rim = bpy.context.active_object; rim.name = "rim"
            rim.data.materials.append(self.mat_metal); parts.append(rim)
            arm = self.cube((sx * 0.055, -self.HEAD_RX * 0.93 + 0.055, z), (0.002, 0.055, 0.002), "temple")
            arm.data.materials.clear(); arm.data.materials.append(self.mat_metal); parts.append(arm)
        bridge = self.cube((0, -self.HEAD_RX * 0.93, z), (0.009, 0.002, 0.002), "bridge")
        bridge.data.materials.clear(); bridge.data.materials.append(self.mat_metal); parts.append(bridge)
        return parts

    # ---------------------------------------------------------------- uv
    def project_uv(self, body):
        me = body.data
        if not me.uv_layers: me.uv_layers.new(name="UVMap")
        uvl = me.uv_layers.active.data
        fu = self.info["front_uv"]; bu = self.info["back_uv"]
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
                    uvl[li].uv = (fu[0] + u * (fu[2] - fu[0]), fu[1] + v * (fu[3] - fu[1]))
                else:
                    uvl[li].uv = (bu[0] + (1.0 - u) * (bu[2] - bu[0]), bu[1] + v * (bu[3] - bu[1]))

    # ---------------------------------------------------------------- rig
    def build_rig(self):
        Z = self.Z
        bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
        arm_obj = bpy.context.active_object; arm_obj.name = self.name + "_Rig"
        arm = arm_obj.data; arm.name = self.name + "_Rig"
        eb = arm.edit_bones; eb.remove(eb[0])
        def bone(name, head, tail, parent=None, connect=False):
            b = eb.new(name); b.head = head; b.tail = tail
            if parent: b.parent = eb[parent]; b.use_connect = connect
            return b
        z_hip = Z["knee"] + (Z["hem"] - Z["knee"]) * 0.95
        z_sh = Z["shoulder"]
        seg = (z_sh - 0.03 - (z_hip + 0.08)) / 3.0
        bone("Hips", (0, 0, z_hip), (0, 0, z_hip + 0.08))
        bone("Spine", (0, 0, z_hip + 0.08), (0, 0, z_hip + 0.08 + seg), "Hips", True)
        bone("Spine1", (0, 0, z_hip + 0.08 + seg), (0, 0, z_hip + 0.08 + 2 * seg), "Spine", True)
        bone("Spine2", (0, 0, z_hip + 0.08 + 2 * seg), (0, 0, z_sh - 0.03), "Spine1", True)
        z_head0 = Z["head"] - 0.02 - self.HEAD_RX * 0.95
        bone("Neck", (0, 0, z_sh - 0.03), (0, 0, z_head0), "Spine2", True)
        bone("Head", (0, 0, z_head0), (0, 0, self.H), "Neck", True)
        for sx, L in ((1, "Left"), (-1, "Right")):
            S = Vector((sx * self.SH_X, 0, z_sh)); Wp = Vector((sx * self.WR_X, 0, Z["wrist"]))
            D = (Wp - S).normalized(); elbow = S + (Wp - S) * 0.52
            bone(L + "Shoulder", (sx * 0.02, 0, z_sh - 0.02), S, "Spine2")
            bone(L + "Arm", S, elbow, L + "Shoulder")
            bone(L + "ForeArm", elbow, Wp, L + "Arm", True)
            bone(L + "Hand", Wp, Wp + D * 0.12, L + "ForeArm", True)
            hx = self.HEM_RX * 0.5
            bone(L + "UpLeg", (sx * hx, 0, z_hip), (sx * (hx + 0.005), 0, Z["knee"]), "Hips")
            bone(L + "Leg", (sx * (hx + 0.005), 0, Z["knee"]), (sx * (hx + 0.01), 0, Z["ankle"]), L + "UpLeg", True)
            bone(L + "Foot", (sx * (hx + 0.01), 0, Z["ankle"]), (sx * (hx + 0.01), -0.12, 0.03), L + "Leg", True)
            bone(L + "ToeBase", (sx * (hx + 0.01), -0.12, 0.03), (sx * (hx + 0.01), -0.19, 0.02), L + "Foot", True)
        bpy.ops.object.mode_set(mode='OBJECT')
        return arm_obj

    # ---------------------------------------------------------------- skinning
    def skin(self, body, arm_obj):
        """Deterministic skinning: every vertex is weighted to the two nearest bone SEGMENTS
        (distance from point to segment), blended by inverse distance and softened across joints.
        Bone-heat auto weights fail on part-built bodies whose pieces interpenetrate ("failed to
        find solution"), which silently left meshes unweighted; this never fails and gives clean,
        predictable deformation for the game's animation set."""
        bones = [(b.name, Vector(b.head_local), Vector(b.tail_local)) for b in arm_obj.data.bones]
        # deform set: everything except the shoulders' inner stubs are fine; keep all bones
        def seg_dist(p, a, b):
            ab = b - a
            t = 0.0 if ab.length_squared == 0 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
            return (p - (a + ab * t)).length, t
        me = body.data
        groups = {}
        for name, _, _ in bones:
            groups[name] = body.vertex_groups.new(name=name)
        # per-bone influence radius: thick bones (hips/spine/head) reach further than limb bones
        radius = {"Hips": 0.20, "Spine": 0.18, "Spine1": 0.18, "Spine2": 0.20, "Neck": 0.08, "Head": 0.16}
        mw = body.matrix_world
        for v in me.vertices:
            p = mw @ v.co
            cands = []
            for name, a, b in bones:
                d, t = seg_dist(p, a, b)
                r = radius.get(name, 0.09)
                cands.append((d / r, name, d))
            cands.sort()
            (s0, n0, d0), (s1, n1, d1) = cands[0], cands[1]
            # blend to the runner-up only near the boundary between the two bones
            if s1 <= 0 or s1 - s0 > 0.35:
                w0, w1 = 1.0, 0.0
            else:
                k = (s1 - s0) / 0.35           # 0 = exactly between, 1 = clearly bone 0
                w0 = 0.5 + 0.5 * k
                w1 = 1.0 - w0
            groups[n0].add([v.index], w0, 'REPLACE')
            if w1 > 0.001:
                groups[n1].add([v.index], w1, 'REPLACE')
        mod = body.modifiers.new("Armature", 'ARMATURE')
        mod.object = arm_obj
        body.parent = arm_obj
        body.matrix_parent_inverse = arm_obj.matrix_world.inverted()

    # ---------------------------------------------------------------- build
    def build(self):
        clean()
        self.mat = bpy.data.materials.new("M_" + self.name); self.mat.use_nodes = True
        bsdf = self.mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Roughness"].default_value = 0.65
        tex = self.mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(self.out_dir, self.name + "_Albedo.png"))
        if self.name == "Ari":
            # REF-01: black-blue hair #14161A regardless of what the sheet's top band samples
            self.info["hair_rgb"] = [20, 22, 26]
        self.mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        hc = [c / 255.0 for c in self.info["hair_rgb"]]
        self.mat_hair = bpy.data.materials.new("M_" + self.name + "_Hair"); self.mat_hair.use_nodes = True
        hb = self.mat_hair.node_tree.nodes["Principled BSDF"]
        hb.inputs["Base Color"].default_value = (hc[0] ** 2.2, hc[1] ** 2.2, hc[2] ** 2.2, 1.0)
        hb.inputs["Roughness"].default_value = 0.45
        self.mat_hair.diffuse_color = (hc[0], hc[1], hc[2], 1.0)
        self.mat_metal = bpy.data.materials.new("M_" + self.name + "_Metal"); self.mat_metal.use_nodes = True
        mb = self.mat_metal.node_tree.nodes["Principled BSDF"]
        mb.inputs["Base Color"].default_value = (0.5, 0.52, 0.56, 1.0); mb.inputs["Metallic"].default_value = 1.0
        mb.inputs["Roughness"].default_value = 0.3

        parts = self.build_body()
        n_body = len(parts)
        hair = self.build_hair()
        if hair: parts += hair
        if self.name in GLASSES: parts += self.build_glasses()
        bpy.ops.object.select_all(action='DESELECT')
        for p in parts: p.select_set(True)
        bpy.context.view_layer.objects.active = parts[0]
        bpy.ops.object.join()
        body = bpy.context.active_object; body.name = self.name + "_Body"
        try:
            bpy.ops.object.shade_auto_smooth(angle=math.radians(40))
        except Exception:
            bpy.ops.object.shade_smooth()
        tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
        log(self.name, "mesh tris =", tris)
        # UV: body polygons use the atlas; hair/metal slots are flat colours (uv irrelevant)
        self.project_uv(body)

        arm_obj = self.build_rig()
        self.skin(body, arm_obj)
        log(self.name, "rig bones:", len(arm_obj.data.bones))
        self.body, self.arm = body, arm_obj
        return tris

    def export(self):
        path = os.path.join(self.out_dir, self.name + ".fbx")
        if os.path.exists(path): os.remove(path)
        bpy.ops.object.select_all(action='DESELECT')
        self.body.select_set(True); self.arm.select_set(True)
        bpy.context.view_layer.objects.active = self.arm
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                                 add_leaf_bones=False, use_armature_deform_only=True,
                                 mesh_smooth_type='FACE', path_mode='STRIP',
                                 bake_anim=False, apply_scale_options='FBX_SCALE_ALL')
        log(self.name, "exported", os.path.relpath(path, ROOT), os.path.getsize(path) // 1024, "KB")

    def render(self):
        scene = bpy.context.scene
        scene.render.engine = 'BLENDER_WORKBENCH'
        scene.display.shading.color_type = 'TEXTURE'
        scene.display.shading.light = 'FLAT'
        scene.render.resolution_x = 384; scene.render.resolution_y = 640
        cam_data = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_data)
        scene.collection.objects.link(cam)
        tgt = bpy.data.objects.new("tgt", None); scene.collection.objects.link(tgt)
        tgt.location = (0, 0, self.H * 0.52)
        con = cam.constraints.new('TRACK_TO'); con.target = tgt
        scene.camera = cam
        cam.location = (0, -4.2, self.H * 0.6)
        scene.render.filepath = os.path.join(RENDER, "char_%s_front.png" % self.name.lower())
        bpy.ops.render.render(write_still=True)
        cam.location = (3.0, -2.6, self.H * 0.6)
        scene.render.filepath = os.path.join(RENDER, "char_%s_threequarter.png" % self.name.lower())
        bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------- shared animation library
def build_anim_library():
    """Authors the clip set on a neutral canonical skeleton (Dax proportions) and exports one FBX
    per clip; imported as Humanoid in Unity, every character retargets them."""
    b = Builder("Dax")
    b.build()
    arm_obj = b.arm
    pb = arm_obj.pose.bones
    for x in pb: x.rotation_mode = 'XYZ'
    R = math.radians

    def new_action(name):
        act = bpy.data.actions.new(name)
        arm_obj.animation_data_create()
        arm_obj.animation_data.action = act
        for x in pb:
            x.rotation_euler = (0, 0, 0); x.location = (0, 0, 0)
        return act

    def key(bn, f, rot=None, loc=None):
        x = pb[bn]
        if rot is not None: x.rotation_euler = rot
        if loc is not None: x.location = loc
        x.keyframe_insert("rotation_euler", frame=f)
        if loc is not None: x.keyframe_insert("location", frame=f)

    def sway(f, n, amp=1.0):
        t = (f - 1) / (n - 1) * 2 * math.pi
        key("Spine1", f, rot=(R(1.2) * amp * math.sin(t), 0, 0))
        key("Spine2", f, rot=(R(0.8) * amp * math.sin(t + 0.5), 0, 0))
        key("Head", f, rot=(R(0.6) * math.sin(t + 1.0), 0, R(1.0) * math.sin(t * 0.5)))
        key("LeftArm", f, rot=(R(1.5) * math.sin(t), 0, 0))
        key("RightArm", f, rot=(R(1.5) * math.sin(t + math.pi), 0, 0))
        key("Hips", f, loc=(0, 0, 0.004 * math.sin(t)))

    clips = {}
    # Idle (60 f loop)
    new_action("Anim_Idle")
    for f in range(1, 61): sway(f, 60)
    clips["Anim_Idle"] = (1, 60)

    # Walk (36 f loop) — arms swing opposite to legs; forward lean
    def gait(n, leg_amp, arm_amp, knee_amp, bob, lean):
        for f in range(1, n + 1):
            t = (f - 1) / n * 2 * math.pi
            sw = math.sin(t)
            key("LeftUpLeg", f, rot=(R(leg_amp) * sw, 0, 0))
            key("RightUpLeg", f, rot=(R(leg_amp) * -sw, 0, 0))
            key("LeftLeg", f, rot=(R(knee_amp) * max(0, -math.sin(t + 0.6)), 0, 0))
            key("RightLeg", f, rot=(R(knee_amp) * max(0, -math.sin(t + 0.6 + math.pi)), 0, 0))
            key("LeftArm", f, rot=(R(arm_amp) * -sw, 0, 0))
            key("RightArm", f, rot=(R(arm_amp) * sw, 0, 0))
            key("LeftForeArm", f, rot=(R(arm_amp * 0.7) * (0.5 + 0.5 * math.sin(t + 1.2)), 0, 0))
            key("RightForeArm", f, rot=(R(arm_amp * 0.7) * (0.5 + 0.5 * math.sin(t + 1.2 + math.pi)), 0, 0))
            key("Hips", f, loc=(0, 0, bob * abs(math.sin(t))), rot=(0, 0, R(3) * sw))
            key("Spine1", f, rot=(R(lean), 0, R(-2) * sw))
            key("Head", f, rot=(R(-lean * 0.5), 0, 0))
    new_action("Anim_Walk"); gait(36, 22, 18, 30, 0.015, 2.0); clips["Anim_Walk"] = (1, 36)
    new_action("Anim_Run"); gait(24, 38, 40, 55, 0.035, 9.0); clips["Anim_Run"] = (1, 24)

    # Talk (72 f loop): weight shift, head nods, small hand gesture
    new_action("Anim_Talk")
    for f in range(1, 73):
        t = (f - 1) / 72 * 2 * math.pi
        sway(f, 72, 0.6)
        key("Head", f, rot=(R(3.0) * math.sin(t * 2), R(2.0) * math.sin(t), R(1.5) * math.sin(t * 1.5)))
        key("RightArm", f, rot=(R(10) + R(8) * math.sin(t * 2), 0, R(-6)))
        key("RightForeArm", f, rot=(R(45) + R(18) * math.sin(t * 2 + 0.6), 0, 0))
        key("LeftArm", f, rot=(R(4), 0, R(4)))
        key("LeftForeArm", f, rot=(R(20), 0, 0))
        key("Hips", f, rot=(0, 0, R(2) * math.sin(t)), loc=(0.01 * math.sin(t), 0, 0))
    clips["Anim_Talk"] = (1, 72)

    # Alert (reaction, 18 f): snap head + torso toward the threat, weight back
    new_action("Anim_Alert")
    for f, a in ((1, 0), (5, 1), (12, 1), (18, 0)):
        key("Head", f, rot=(R(-8) * a, 0, R(18) * a))
        key("Spine2", f, rot=(R(-6) * a, 0, R(10) * a))
        key("Hips", f, loc=(0, 0.03 * a, -0.02 * a))
        key("LeftArm", f, rot=(R(12) * a, 0, R(15) * a))
        key("RightArm", f, rot=(R(12) * a, 0, R(-15) * a))
    clips["Anim_Alert"] = (1, 18)

    # Attack (22 f): windup back, swing across with the right arm, torso twist, step
    new_action("Anim_Attack")
    for f, (tw, arm, fore, lean) in ((1, (0, 0, 0, 0)), (6, (-28, -70, -60, -6)), (11, (35, 20, -10, 12)), (15, (30, 15, -5, 10)), (22, (0, 0, 0, 0))):
        key("Hips", f, rot=(0, 0, R(tw * 0.35)))
        key("Spine1", f, rot=(R(lean * 0.5), 0, R(tw * 0.35)))
        key("Spine2", f, rot=(R(lean * 0.5), 0, R(tw * 0.3)))
        key("RightArm", f, rot=(R(arm), 0, R(-20 + arm * 0.2)))
        key("RightForeArm", f, rot=(R(fore), 0, 0))
        key("LeftArm", f, rot=(R(-arm * 0.3), 0, R(10)))
        key("Head", f, rot=(0, 0, R(-tw * 0.3)))
    clips["Anim_Attack"] = (1, 22)

    # Hit (14 f): recoil back and to the side, arms up
    new_action("Anim_Hit")
    for f, a in ((1, 0), (3, 1), (8, 0.7), (14, 0)):
        key("Spine1", f, rot=(R(-14) * a, 0, R(8) * a))
        key("Spine2", f, rot=(R(-10) * a, 0, R(6) * a))
        key("Head", f, rot=(R(-16) * a, 0, R(10) * a))
        key("Hips", f, loc=(0, 0.06 * a, -0.02 * a))
        key("LeftArm", f, rot=(R(-30) * a, 0, R(25) * a))
        key("RightArm", f, rot=(R(-30) * a, 0, R(-25) * a))
        key("LeftForeArm", f, rot=(R(50) * a, 0, 0))
        key("RightForeArm", f, rot=(R(50) * a, 0, 0))
    clips["Anim_Hit"] = (1, 14)

    # Dodge (16 f): crouch + sideways lunge (root moves in-place, controller displaces)
    new_action("Anim_Dodge")
    for f, (crouch, lean, side) in ((1, (0, 0, 0)), (4, (0.12, 14, 22)), (9, (0.10, 10, 28)), (16, (0, 0, 0))):
        key("Hips", f, loc=(0, 0, -crouch), rot=(R(lean * 0.5), R(side * 0.3), R(side)))
        key("Spine1", f, rot=(R(lean), 0, R(-side * 0.4)))
        key("LeftUpLeg", f, rot=(R(crouch * 250), 0, R(-side * 0.5)))
        key("RightUpLeg", f, rot=(R(crouch * 250), 0, R(side * 0.5)))
        key("LeftLeg", f, rot=(R(crouch * 420), 0, 0))
        key("RightLeg", f, rot=(R(crouch * 420), 0, 0))
        key("LeftArm", f, rot=(R(lean * 1.5), 0, R(20 + side * 0.5)))
        key("RightArm", f, rot=(R(lean * 1.5), 0, R(-20 - side * 0.5)))
    clips["Anim_Dodge"] = (1, 16)

    # Defeat (40 f): knees fold, torso collapses forward, holds (no loop)
    new_action("Anim_Defeat")
    for f, a in ((1, 0), (10, 0.45), (22, 0.9), (30, 1.0), (40, 1.0)):
        key("Hips", f, loc=(0, 0, -0.55 * a), rot=(R(12) * a, 0, 0))
        key("LeftUpLeg", f, rot=(R(-95) * a, 0, R(6) * a))
        key("RightUpLeg", f, rot=(R(-95) * a, 0, R(-6) * a))
        key("LeftLeg", f, rot=(R(120) * a, 0, 0))
        key("RightLeg", f, rot=(R(120) * a, 0, 0))
        key("Spine1", f, rot=(R(30) * a, 0, 0))
        key("Spine2", f, rot=(R(28) * a, 0, 0))
        key("Head", f, rot=(R(35) * a, 0, 0))
        key("LeftArm", f, rot=(R(45) * a, 0, R(10) * a))
        key("RightArm", f, rot=(R(45) * a, 0, R(-10) * a))
    clips["Anim_Defeat"] = (1, 40)

    out = os.path.join(ART, "_Animations")
    os.makedirs(out, exist_ok=True)
    for name, (f0, f1) in clips.items():
        act = bpy.data.actions[name]
        arm_obj.animation_data.action = act
        bpy.context.scene.frame_start = f0; bpy.context.scene.frame_end = f1
        bpy.ops.object.select_all(action='DESELECT')
        b.body.select_set(True); arm_obj.select_set(True)
        bpy.context.view_layer.objects.active = arm_obj
        path = os.path.join(out, name + ".fbx")
        if os.path.exists(path): os.remove(path)
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, add_leaf_bones=False,
                                 use_armature_deform_only=True, mesh_smooth_type='FACE', path_mode='STRIP',
                                 bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                                 bake_anim_force_startend_keying=True, apply_scale_options='FBX_SCALE_ALL')
        log("anim", name, "frames", f0, f1, os.path.getsize(path) // 1024, "KB")
    # QA renders of a few poses
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'TEXTURE'; scene.display.shading.light = 'FLAT'
    scene.render.resolution_x = 384; scene.render.resolution_y = 640
    cam_data = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    tgt = bpy.data.objects.new("tgt", None); scene.collection.objects.link(tgt); tgt.location = (0, 0, 0.9)
    con = cam.constraints.new('TRACK_TO'); con.target = tgt
    scene.camera = cam; cam.location = (2.6, -3.2, 1.2)
    for name, frame in (("Anim_Run", 7), ("Anim_Attack", 11), ("Anim_Defeat", 30), ("Anim_Talk", 20)):
        arm_obj.animation_data.action = bpy.data.actions[name]
        scene.frame_set(frame)
        scene.render.filepath = os.path.join(RENDER, "anim_%s.png" % name.lower())
        bpy.ops.render.render(write_still=True)
    return list(clips.keys())


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv or list(HEIGHT.keys()) + ["ANIMS"]
    report = {}
    for n in names:
        if n == "ANIMS":
            report["_animations"] = build_anim_library()
            continue
        b = Builder(n)
        tris = b.build()
        b.export()
        b.render()
        report[n] = {"tris": tris, "height": b.H, "fbx": os.path.relpath(os.path.join(b.out_dir, n + ".fbx"), ROOT)}
    json.dump(report, open(os.path.join(BUILD, "models.json"), "w"), indent=1)
    log("SUCCESS", report)


try:
    main()
except Exception:
    traceback.print_exc()
    sys.exit(1)
