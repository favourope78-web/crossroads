#!/usr/bin/env python3
"""ART PRODUCTION PASS - authored prop clusters for the campaign locations.

Builds 8 low-poly, hand-authored dressing meshes (Blender headless -> FBX) that the
scene generator instances into every campaign room. These are real mesh assets with
two material slots (structure / accent) - the scene overrides them with the project's
canonical URP/Lit materials, exactly like the architecture kit.

Each cluster stays deliberately cheap (mobile budget): quads or 8-gon cylinders,
icosphere foliage at subdivision 1, everything merged into ONE mesh object so a fully
dressed room costs 1 renderer instead of 6-10 separate primitives.

Run: blender -b -P scripts/blender_build_dressing.py
Output: Assets/Game/Environment/Kit/SM_Dress_*.fbx
"""
import bpy, math, os

OUT = "/home/user/crossroads/Assets/Game/Environment/Kit"
os.makedirs(OUT, exist_ok=True)

def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene

def m_structure():
    m = bpy.data.materials.new("Structure")
    return m

def m_accent():
    return bpy.data.materials.new("Accent")

def box(name, loc, size, rot=(0, 0, 0), mat="Structure"):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    o.data.materials.append(bpy.data.materials[mat])
    return o

def cyl(name, loc, r, depth, verts=8, rot=(0, 0, 0), mat="Structure"):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(bpy.data.materials[mat])
    return o

def ico(name, loc, r, sub=1, mat="Accent"):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(bpy.data.materials[mat])
    return o

def join_all(name, remove_inner=True):
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in objs:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
    bpy.ops.object.join()
    root = bpy.context.active_object
    root.name = name
    root.location = (0, 0, 0)
    if remove_inner:
        # keep world transform baked into the mesh
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return root

def export(root, fname):
    for o in bpy.context.scene.objects:
        o.select_set(o == root)
    path = os.path.join(OUT, fname)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                             apply_unit_scale=True, global_scale=1.0,
                             apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="OFF", add_leaf_bones=False,
                             bake_space_transform=True)
    print("dressing cluster ->", fname)

# ---------------------------------------------------------------- 1. lantern pair
def build_lantern_pair():
    fresh()
    m_structure(); m_accent()
    for i, x in enumerate((-0.85, 0.85)):
        cyl("Pole%d" % i, (x, 0, 1.1), 0.05, 2.2)
        box("Head%d" % i, (x, 0, 2.35), (0.34, 0.34, 0.42))
        box("Cap%d" % i, (x, 0, 2.62), (0.42, 0.42, 0.10))
        box("Glow%d" % i, (x, 0, 2.35), (0.26, 0.26, 0.30), mat="Accent")
        box("Base%d" % i, (x, 0, 0.06), (0.34, 0.34, 0.12))
    export(join_all("SM_Dress_LanternPair"), "SM_Dress_LanternPair.fbx")

# ---------------------------------------------------------------- 2. crate stack
def build_crate_stack():
    fresh()
    m_structure(); m_accent()
    box("CrateA", (-0.35, 0.1, 0.35), (0.7, 0.7, 0.7), rot=(0, 0, 0.12))
    box("CrateB", (0.38, -0.15, 0.3), (0.6, 0.6, 0.6), rot=(0, 0.18, -0.1))
    box("CrateC", (0.0, 0.05, 0.95), (0.55, 0.55, 0.55), rot=(0, 0, 0.4))
    box("LidA", (-0.35, 0.1, 0.73), (0.74, 0.74, 0.06), mat="Accent")
    box("LidB", (0.38, -0.15, 0.63), (0.64, 0.64, 0.06), mat="Accent")
    export(join_all("SM_Dress_CrateStack"), "SM_Dress_CrateStack.fbx")

# ---------------------------------------------------------------- 3. planter with foliage
def build_planter():
    fresh()
    m_structure(); m_accent()
    box("Box", (0, 0, 0.35), (1.5, 1.5, 0.7))
    box("Rim", (0, 0, 0.72), (1.62, 1.62, 0.10), mat="Accent")
    box("Soil", (0, 0, 0.74), (1.3, 1.3, 0.06))
    cyl("TrunkA", (-0.3, 0.25, 1.15), 0.05, 0.85)
    cyl("TrunkB", (0.32, -0.2, 1.05), 0.04, 0.65)
    ico("LeafA", (-0.3, 0.25, 1.85), 0.55)
    ico("LeafB", (0.32, -0.2, 1.6), 0.42)
    ico("LeafC", (0.05, 0.45, 1.45), 0.34)
    ico("LeafD", (-0.5, -0.35, 1.3), 0.3)
    export(join_all("SM_Dress_Planter"), "SM_Dress_Planter.fbx")

# ---------------------------------------------------------------- 4. hanging banners
def build_banner_wall():
    fresh()
    m_structure(); m_accent()
    cyl("Pole", (0, 0, 2.6), 0.04, 2.4, rot=(0, math.radians(90), 0))
    for i, x in enumerate((-0.55, 0.55)):
        box("Cloth%d" % i, (x, 0, 1.8), (0.55, 0.05, 1.5), mat="Accent")
        box("Hem%d" % i, (x, 0, 1.0), (0.62, 0.07, 0.12))
        box("Sigil%d" % i, (x, 0.001, 1.95), (0.3, 0.02, 0.3))
    export(join_all("SM_Dress_BannerWall"), "SM_Dress_BannerWall.fbx")

# ---------------------------------------------------------------- 5. pier deck
def build_pier_set():
    fresh()
    m_structure(); m_accent()
    for i in range(7):
        box("Plank%d" % i, (0, -0.6 + i * 0.2, 0.05), (2.4, 0.16, 0.1), rot=(0, 0, 0.015 * (i % 3 - 1)))
    for x in (-1.05, 1.05):
        cyl("Post%s" % ("L" if x < 0 else "R"), (x, 0.8, 0.5), 0.06, 1.0)
        cyl("PostTop%s" % ("L" if x < 0 else "R"), (x, 0.8, 1.35), 0.045, 1.6, rot=(math.radians(90), 0, 0))
    box("Rail", (0, 0.8, 1.5), (2.3, 0.07, 0.07))
    export(join_all("SM_Dress_PierSet"), "SM_Dress_PierSet.fbx")

# ---------------------------------------------------------------- 6. shrine set
def build_shrine_set():
    fresh()
    m_structure(); m_accent()
    box("Slab", (0, 0, 0.12), (1.4, 0.9, 0.24))
    box("Altar", (0, 0, 0.55), (0.9, 0.55, 0.6))
    box("Top", (0, 0, 0.88), (1.05, 0.68, 0.08), mat="Accent")
    for x in (-0.55, 0.55):
        cyl("Candle%s" % ("L" if x < 0 else "R"), (x, 0.25, 0.28), 0.07, 0.55)
        ico("Flame%s" % ("L" if x < 0 else "R"), (x, 0.25, 0.62), 0.09, mat="Accent")
    cyl("Bowl", (0, -0.28, 0.30), 0.22, 0.1)
    export(join_all("SM_Dress_ShrineSet"), "SM_Dress_ShrineSet.fbx")

# ---------------------------------------------------------------- 7. barricade
def build_barricade():
    fresh()
    m_structure(); m_accent()
    box("Sill", (0, 0, 0.1), (2.0, 0.35, 0.2))
    box("PlankA", (-0.35, 0, 0.75), (0.24, 1.7, 0.1), rot=(0, 0, 0.0))
    box("PlankB", (0.35, 0, 0.75), (0.24, 1.7, 0.1), rot=(0, math.radians(75), 0))
    box("Cross", (0, 0, 1.15), (1.8, 0.18, 0.18), mat="Accent")
    box("BagA", (-0.7, 0.05, 0.32), (0.5, 0.4, 0.28), rot=(0, 0, 0.1))
    box("BagB", (0.65, -0.05, 0.32), (0.5, 0.4, 0.28), rot=(0, 0, -0.08))
    export(join_all("SM_Dress_Barricade"), "SM_Dress_Barricade.fbx")

# ---------------------------------------------------------------- 8. market stall
def build_market_stall():
    fresh()
    m_structure(); m_accent()
    box("Table", (0, 0, 0.72), (1.8, 0.8, 0.08))
    for x, y in ((-0.8, -0.32), (0.8, -0.32), (-0.8, 0.32), (0.8, 0.32)):
        cyl("Leg_%s%s" % ("L" if x < 0 else "R", "B" if y < 0 else "F"), (x, y, 0.36), 0.04, 0.72)
    for x in (-0.8, 0.8):
        cyl("Awning%s" % ("L" if x < 0 else "R"), (x, 0, 1.35), 0.035, 1.3)
    box("Cloth", (0, 0, 1.62), (2.0, 1.15, 0.06), rot=(0.12, 0, 0), mat="Accent")
    box("GoodsA", (-0.45, 0, 0.83), (0.4, 0.5, 0.14))
    box("GoodsB", (0.3, 0.1, 0.83), (0.5, 0.35, 0.18), mat="Accent")
    export(join_all("SM_Dress_MarketStall"), "SM_Dress_MarketStall.fbx")

if __name__ == "__main__":
    build_lantern_pair()
    build_crate_stack()
    build_planter()
    build_banner_wall()
    build_pier_set()
    build_shrine_set()
    build_barricade()
    build_market_stall()
    print("ALL DRESSING CLUSTERS BUILT")

# ---------------------------------------------------------------- 9. pier water (sculpted)
def build_pier_water():
    fresh()
    m_structure(); m_accent()
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = "Water"
    o.scale = (11.0, 7.0, 1.0)
    bpy.ops.object.modifier_add(type="SUBSURF")
    o.modifiers["Subdivision"].levels = 3
    bpy.ops.object.modifier_apply(modifier="Subdivision")
    # bake gentle ripples
    import math as _m
    mesh = o.data
    for v in mesh.vertices:
        wx = o.matrix_world @ v.co
        v.co.z += 0.045 * _m.sin(wx.x * 1.4) + 0.03 * _m.sin(wx.y * 2.1 + 1.3) + 0.015 * _m.sin((wx.x + wx.y) * 3.2)
    o.data.materials.append(bpy.data.materials["Accent"])
    export(join_all("SM_Dress_PierWater"), "SM_Dress_PierWater.fbx")

# ---------------------------------------------------------------- 10. arena ring
def build_arena_ring():
    fresh()
    m_structure(); m_accent()
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=2.6, depth=0.3, location=(0, 0, 0.15))
    dais = bpy.context.active_object
    dais.data.materials.append(bpy.data.materials["Structure"])
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=2.15, depth=0.14, location=(0, 0, 0.36))
    inner = bpy.context.active_object
    inner.data.materials.append(bpy.data.materials["Accent"])
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        x, y = 2.35 * math.cos(a), 2.35 * math.sin(a)
        cyl("Post%d" % i, (x, y, 0.85), 0.06, 1.0)
        box("Cap%d" % i, (x, y, 1.38), (0.22, 0.22, 0.1), mat="Accent")
    export(join_all("SM_Dress_ArenaRing"), "SM_Dress_ArenaRing.fbx")

# ---------------------------------------------------------------- 11. wall scaffolding
def build_scaffolding():
    fresh()
    m_structure(); m_accent()
    w, h = 3.6, 2.8
    for x in (-w / 2, w / 2):
        cyl("Standard%s" % ("L" if x < 0 else "R"), (x, 0, h / 2), 0.05, h)
        cyl("Brace%s" % ("L" if x < 0 else "R"), (x, 0.5, h / 2), 0.04, h)
    for y in (0.0, 0.55):
        box("PlankY%d" % int(y * 10), (0, y, 1.15), (w + 0.2, 0.1, 0.06), mat="Accent")
    box("Deck", (0, 0.28, 2.1), (w + 0.2, 0.62, 0.07))
    box("Ledge", (0, 0.28, 2.16), (w + 0.35, 0.7, 0.05), mat="Accent")
    cyl("Diag", (0, -0.05, 1.4), 0.035, 3.7, rot=(math.radians(62), 0, 0))
    export(join_all("SM_Dress_Scaffolding"), "SM_Dress_Scaffolding.fbx")

if __name__ == "__main__" and os.environ.get("DRESSING_SIGNATURES_ONLY"):
    build_pier_water()
    build_arena_ring()
    build_scaffolding()
    print("SIGNATURES BUILT")
