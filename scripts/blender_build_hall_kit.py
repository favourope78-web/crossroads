"""CROSSROADS Fracture Hall modular kit — headless Blender 4.2.
Exports single-mesh kit FBX pieces for the Power-Grab / Fracture Hall
(reference t:0.5, reference/concept/fracture_hall_concept.png).
Each piece = one mesh, one material slot named M_KIT_<name> so Unity
side can swap in project materials by slot name.
Run: blender -b -P scripts/blender_build_hall_kit.py
"""
import bpy, math, os, traceback, sys

OUT = "/home/user/Assets/Game/Environment/Kit"
os.makedirs(OUT, exist_ok=True)

def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def finish(obj, name, mat):
    obj.name = name
    obj.data.name = name
    m = bpy.data.materials.new("M_KIT_" + mat); m.use_nodes = True
    obj.data.materials.clear(); obj.data.materials.append(m)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    bpy.ops.export_scene.fbx(filepath=f"{OUT}/{name}.fbx", use_selection=True,
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_anim=False,
                             apply_scale_options='FBX_SCALE_ALL')
    print(f"[KIT] {name} tris={tris}")

def cube(loc, scale, name):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.active_object; o.scale = scale; return o

def cyl(loc, r, depth, name, verts=12, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    return bpy.context.active_object

try:
    # ---- SM_FloorTile 10x10 m with panel bevels ----
    clean()
    o = cube((0,0,0.0), (5,5,0.05), "SM_FloorTile")
    bev = o.modifiers.new("bev",'BEVEL'); bev.width=0.06; bev.segments=1
    bpy.ops.object.modifier_apply(modifier="bev")
    finish(o, "SM_FloorTile", "Concrete")
    # ---- SM_Column: pillar shaft + base + cap, 9 m ----
    clean()
    parts = [cube((0,0,0.25),(0.55,0.55,0.25),"b"), cyl((0,0,4.5),0.38,8.0,"s",verts=10), cube((0,0,8.75),(0.55,0.55,0.25),"c")]
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join()
    finish(bpy.context.active_object, "SM_Column", "Metal")
    # ---- SM_LightBeam: emissive beam cylinder 9 m ----
    clean()
    o = cyl((0,0,4.5), 0.50, 9.0, "SM_LightBeam", verts=10)
    finish(o, "SM_LightBeam", "LightColumn")
    # ---- SM_WallPanel 10 m wide x 6 m ----
    clean()
    o = cube((0,0,3.0),(5.0,0.25,3.0),"SM_WallPanel")
    bev = o.modifiers.new("bev",'BEVEL'); bev.width=0.08; bev.segments=1
    bpy.ops.object.modifier_apply(modifier="bev")
    finish(o, "SM_WallPanel", "Concrete")
    # ---- SM_GlazingPanel 10 x 5 (dusk rose windows) ----
    clean()
    o = cube((0,0,0),(5.0,0.06,2.5),"SM_GlazingPanel")
    finish(o, "SM_GlazingPanel", "Glazing")
    # ---- SM_BalconyBlock 10 x 3 slab ----
    clean()
    o = cube((0,0,0),(5.0,1.5,0.2),"SM_BalconyBlock")
    finish(o, "SM_BalconyBlock", "Concrete")
    # ---- SM_Railing 10 m ----
    clean()
    parts = [cube((0,0,1.1),(5.0,0.05,0.05),"top")]
    for i in range(6):
        parts.append(cube((-4+i*1.6,0,0.55),(0.04,0.04,0.55),"p"))
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join()
    finish(bpy.context.active_object, "SM_Railing", "Metal")
    # ---- SM_Truss 10x10 roof cassette with X brace ----
    clean()
    parts = [cube((0,0,0),(5.0,0.12,0.12),"e1"), cube((0,0,0),(0.12,5.0,0.12),"e2")]
    for sgn in (1,-1):
        b = cube((0,0,-0.15),(3.4,0.09,0.09),"d")
        b.rotation_euler = (0,0,sgn*math.radians(45)); parts.append(b)
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join()
    finish(bpy.context.active_object, "SM_Truss", "Metal")
    # ---- SM_DoorFrame portal 4 x 4.5 ----
    clean()
    parts = [cube((-2.0,0,2.25),(0.4,0.5,2.25),"l"), cube((2.0,0,2.25),(0.4,0.5,2.25),"r"), cube((0,0,4.5),(2.4,0.5,0.4),"t")]
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join()
    finish(bpy.context.active_object, "SM_DoorFrame", "Metal")
    # ---- SM_Door leaf 3.6 x 4 ----
    clean()
    o = cube((0,0,2.0),(1.8,0.12,2.0),"SM_Door")
    finish(o, "SM_Door", "Metal")
    # ---- SM_OrbCore icosphere r0.9 ----
    clean()
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.9, location=(0,0,0))
    finish(bpy.context.active_object, "SM_OrbCore", "OrbGold")
    # ---- SM_OrbRing torus r1.5 ----
    clean()
    bpy.ops.mesh.primitive_torus_add(major_radius=1.5, minor_radius=0.06, major_segments=24, minor_segments=6)
    finish(bpy.context.active_object, "SM_OrbRing", "OrbGold")
    # ---- SM_HoloPanel quad 1.8 x 1.2 ----
    clean()
    o = cube((0,0,0),(0.9,0.01,0.6),"SM_HoloPanel")
    finish(o, "SM_HoloPanel", "Holo")
    print("[KIT] SUCCESS")
except Exception:
    traceback.print_exc(); sys.exit(1)
