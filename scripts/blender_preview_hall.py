"""CROSSROADS Fracture Hall look-dev preview — headless Blender 4.2.
Assembles scripts/hall_layout.json from the kit FBX + Ari prototype,
applies prototype palette viewport colors, renders 2 verification views.
Run: blender -b -P scripts/blender_preview_hall.py
"""
import bpy, math, json, sys, traceback
from mathutils import Vector

LAY = json.load(open("/home/user/scripts/hall_layout.json"))
KIT = "/home/user/Assets/Game/Environment/Kit"
ARI = "/home/user/Assets/_Project/Art/Characters/Ari/Ari.fbx"
OUT = "/home/user/reference/prototype_renders"

COLORS = {  # viewport colors approximating Unity prototype materials
 "Concrete": (0.42, 0.42, 0.45, 1), "Metal": (0.18, 0.19, 0.22, 1),
 "LightColumn": (0.40, 0.85, 0.91, 1), "Glazing": (0.72, 0.42, 0.40, 1),
 "OrbGold": (0.91, 0.72, 0.29, 1), "Holo": (0.40, 0.85, 0.91, 1),
}

def u2b(pos, yaw):  # Unity(x,y,z)+yawDeg -> Blender loc/rot
    return (pos[0], -pos[2], pos[1]), (0, 0, -math.radians(yaw))

try:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cache = {}
    for e in LAY["pieces"]:
        name = e["piece"]
        if name not in cache:
            bpy.ops.import_scene.fbx(filepath=f"{KIT}/{name}.fbx")
            objs = [o for o in bpy.context.selected_objects]
            cache[name] = objs[0]
            for o in objs[1:]:
                bpy.data.objects.remove(o)
        src = cache[name]
        o = src.copy()  # linked mesh copy
        bpy.context.scene.collection.objects.link(o)
        loc, rot = u2b(e["pos"], e["yaw"])
        o.location = loc; o.rotation_euler = rot
        o.scale = tuple(e["scale"])
    for c in cache.values():
        bpy.data.objects.remove(c)
    # relink Ari atlas (FBX stores stripped paths)
    for m in bpy.data.materials:
        if m.name == "M_Ari" and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE':
                    n.image = bpy.data.images.load("/home/user/Assets/_Project/Art/Characters/Ari/Ari_Albedo.png")
    # palette
    for m in bpy.data.materials:
        if m.name.startswith("M_KIT_"):
            key = m.name[6:]
            if key in COLORS:
                m.diffuse_color = COLORS[key]
    # Ari at spawn
    bpy.ops.import_scene.fbx(filepath=ARI)
    ari = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE']
    if ari:
        loc, rot = u2b(LAY["spawn"], LAY["spawnYaw"])
        for o in bpy.context.selected_objects:
            if o.parent is None:
                o.location = (o.location.x + loc[0], o.location.y + loc[1], o.location.z + loc[2])
    # sun + world
    sun = bpy.data.lights.new("sun", 'SUN'); sun.energy = 3.0
    sun.color = (1.0, 0.93, 0.85)
    so = bpy.data.objects.new("sun", sun); bpy.context.scene.collection.objects.link(so)
    so.rotation_euler = (math.radians(55), 0, math.radians(160))
    world = bpy.data.worlds.new("w"); world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.45, 0.30, 0.30, 1); bg.inputs[1].default_value = 0.6
    bpy.context.scene.world = world
    # render settings
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.color_type = 'TEXTURE'
    sc.display.shading.light = 'STUDIO'
    sc.render.resolution_x = 960; sc.render.resolution_y = 540
    cam = bpy.data.cameras.new("cam"); co = bpy.data.objects.new("cam", cam)
    sc.collection.objects.link(co); sc.camera = co
    tgt = bpy.data.objects.new("tgt", None); sc.collection.objects.link(tgt)
    con = co.constraints.new('TRACK_TO'); con.target = tgt
    def shoot(loc, t, path):
        co.location = (loc[0], -loc[2], loc[1]); tgt.location = (t[0], -t[2], t[1])
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
    shoot((0, 2.4, -18.5), (0, 5.0, 4), f"{OUT}/hall_verify_entry.png")
    shoot((14, 2.4, -14), (-6, 5.0, 0), f"{OUT}/hall_verify_arena.png")
    shoot((3, 2.2, 4), (0, 1.2, -16), f"{OUT}/hall_verify_player.png")
    print("[PREVIEW] SUCCESS")
except Exception:
    traceback.print_exc(); sys.exit(1)
