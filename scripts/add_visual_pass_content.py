"""Visual transformation pass - environment dressing for the FIRST PLAYABLE LOCATION
(Fracture Hall), the quality benchmark every future location must match (VISUAL_TARGET §3).

Imported (not run) by gen_firstlocation_scene.py right after the campaign content pass,
using the generator's own emit_* helpers so every object lands in the scene with the same
static/batching/shadow conventions as the kit:

  * FOCAL POINT: the First Light monument - tiered platform + glowing pedestal column
    under the existing OrbCore, ringed by four benches (the player's eye lands here first)
  * VEGETATION: four foliage planters (wood box + foliage masses) breaking the hard floor
  * PROPS: supply crates near the walls, warm lantern posts along the walkways, cloth rugs
  * BANNERS: hanging cloth panels from the roof trusses (ember accents)
  * EXTERIOR: a city skyline ring + ground plane beyond the glazing so the hall feels
    PLACED in a city instead of floating in a black void
  * no new scripts, no new lights (emissive materials + the one shadowed sun do the work),
    everything static-batched, ~51 renderers inside the perf budget

Materials are cloned from M_Seq_Tide (URP/Lit, same canonical shader GUID as all 45
existing materials) under the campaign palette directory.
"""
import os


def build(g):
    """g = the generator's globals() dict."""
    emit_gameobject, emit_transform, emit_meshfilter, emit_renderer = g["emit_gameobject"], g["emit_transform"], g["emit_meshfilter"], g["emit_renderer"]
    emit_boxcollider = g["emit_boxcollider"]
    REG, root_gids = g["REG"], g["root_gids"]
    STATIC_KIT = g["STATIC_KIT"]
    CUBE, SPHERE = g["CUBE"], g["SPHERE"]
    ROOT, HERE = g["ROOT"], g["HERE"]
    ensure, g32 = g["ensure"], g["g32"]
    NATIVE, write_meta_if_missing = g["NATIVE"], g["write_meta_if_missing"]

    # ---- dressing palette (visual pass): base rgb + emission rgb ----
    DRESS = {
        "M_Env_Foliage": ((0.16, 0.34, 0.24), (0.05, 0.16, 0.09)),   # deep teal-green foliage, faint inner light
        "M_Env_PlantBox": ((0.36, 0.28, 0.20), (0.04, 0.02, 0.0)),   # aged wood planter
        "M_Env_Lantern": ((0.92, 0.74, 0.42), (0.85, 0.55, 0.22)),   # warm lantern glass (emissive)
        "M_Env_Banner":  ((0.42, 0.12, 0.10), (0.28, 0.05, 0.03)),   # ember-dyed hanging cloth
        "M_Env_City":    ((0.13, 0.16, 0.23), (0.05, 0.08, 0.14)),   # distant towers, cold windows
        "M_Env_Ground":  ((0.14, 0.15, 0.17), (0.0, 0.0, 0.0)),      # exterior plaza asphalt
    }
    MAT_DIR = os.path.join(ROOT, "Assets/Game/Environment/Materials")
    seq = open(os.path.join(MAT_DIR, "M_Seq_Tide.mat")).read()
    for i, (name, (base, emit)) in enumerate(sorted(DRESS.items())):
        ensure(name, g32(0x1a0 + i))
        p = os.path.join(MAT_DIR, name + ".mat")
        if not os.path.exists(p):
            txt = seq.replace("m_Name: M_Seq_Tide", "m_Name: " + name)
            txt = txt.replace("_BaseColor: {r: 0.03, g: 0.16, b: 0.18, a: 1}", "_BaseColor: {r: %s, g: %s, b: %s, a: 1}" % base)
            txt = txt.replace("_EmissionColor: {r: 0.1, g: 0.85, b: 0.95, a: 1}", "_EmissionColor: {r: %s, g: %s, b: %s, a: 1}" % emit)
            open(p, "w").write(txt)
        write_meta_if_missing(p, NATIVE, REG[name])

    def prop(name, pos, scale, mat, yaw=0, collider=None, mesh=CUBE, cast=True):
        """One static prop: mesh renderer (+optional box collider), batching-static."""
        comps = ["transform", "meshfilter", "renderer"]
        if collider is not None:
            comps.append("col")
        gid, ids = emit_gameobject(name, comps, static_flags=STATIC_KIT)
        emit_transform(ids["transform"], gid, tuple(pos), (0, yaw, 0), tuple(scale))
        emit_meshfilter(ids["meshfilter"], gid, None, builtin_fileid=mesh)
        emit_renderer(ids["renderer"], gid, REG[mat], cast=cast, static=True)
        if collider is not None:
            emit_boxcollider(ids["col"], gid, collider[0], collider[1])
        root_gids.append(gid)
        return gid

    # ================================================================
    # 1. FOCAL POINT - the First Light monument (hall centre, under the OrbCore)
    # ================================================================
    prop("Monument_Platform", (0, 0.09, 0), (7.2, 0.18, 7.2), "M_Hall_Concrete",
         collider=((7.2, 0.18, 7.2), (0, 0, 0)), cast=False)
    prop("Monument_Tier", (0, 0.30, 0), (5.0, 0.14, 5.0), "M_Hall_Metal", cast=False)
    prop("Monument_Pedestal", (0, 2.2, 0), (1.1, 3.9, 1.1), "M_Hall_LightColumn",
         collider=((1.1, 3.9, 1.1), (0, 0, 0)))
    # inlaid glow ring on the platform (thin emissive band around the pedestal)
    prop("Monument_GlowRing", (0, 0.40, 0), (3.4, 0.05, 3.4), "M_Hall_Holo", cast=False)

    # benches facing the monument (four corners of the gathering space)
    for i, (bx, bz, yaw) in enumerate([(6.2, 6.2, -135), (-6.2, 6.2, 135), (6.2, -6.2, -45), (-6.2, -6.2, 45)]):
        prop("Bench_%d" % i, (bx, 0.28, bz), (2.6, 0.16, 0.7), "M_Env_PlantBox", yaw=yaw,
             collider=((2.6, 0.45, 0.8), (0, 0.15, 0)))
        prop("BenchSeat_%d" % i, (bx, 0.50, bz), (2.2, 0.12, 0.6), "M_Env_PlantBox", yaw=yaw)

    # ================================================================
    # 2. VEGETATION - four foliage planters at the floor corners
    # ================================================================
    for i, (px, pz) in enumerate([(13.0, 13.0), (-13.0, 13.0), (13.0, -13.0), (-13.0, -13.0)]):
        prop("Planter_%d" % i, (px, 0.5, pz), (1.9, 1.0, 1.9), "M_Env_PlantBox",
             collider=((1.9, 1.0, 1.9), (0, 0, 0)))
        prop("Foliage_%d_A" % i, (px, 1.45, pz), (1.7, 1.5, 1.7), "M_Env_Foliage", mesh=SPHERE)
        prop("Foliage_%d_B" % i, (px + 0.75, 1.25, pz - 0.45), (1.1, 0.95, 1.1), "M_Env_Foliage", mesh=SPHERE)

    # ================================================================
    # 3. PROPS - crates, lanterns, rugs
    # ================================================================
    crates = [
        ("Crate_A1", (-14.2, 0.55, -17.0), (1.5, 1.1, 1.5), 6),
        ("Crate_A2", (-14.0, 1.5, -17.1), (0.95, 0.8, 0.95), 19),
        ("Crate_A3", (-12.5, 0.5, -17.4), (1.2, 1.0, 1.2), -12),
        ("Crate_B1", (16.8, 0.5, 6.5), (1.3, 1.0, 1.3), 8),
        ("Crate_B2", (17.1, 1.35, 6.6), (0.9, 0.7, 0.9), 24),
        ("Crate_C1", (15.9, 0.45, -13.5), (1.1, 0.9, 1.1), -7),
    ]
    for (name, pos, scale, yaw) in crates:
        prop(name, pos, scale, "M_Env_Rebuilt", yaw=yaw,
             collider=(scale, (0, 0, 0)))

    lanterns = [(-15.5, -6.0, 0), (-15.5, 12.0, 0), (15.5, -12.0, 0), (15.5, 8.0, 0)]
    for i, (lx, lz, _) in enumerate(lanterns):
        prop("LanternPost_%d" % i, (lx, 1.3, lz), (0.18, 2.6, 0.18), "M_Hall_Metal")
        prop("LanternHead_%d" % i, (lx, 2.75, lz), (0.52, 0.52, 0.52), "M_Env_Lantern", cast=False)

    # cloth rugs: warm floor accents (spawn approach + monument gathering edge)
    prop("Rug_Spawn", (0, 0.16, -11.5), (5.6, 0.05, 3.4), "M_Env_Banner", cast=False)
    prop("Rug_North", (0, 0.16, 11.0), (4.6, 0.05, 2.8), "M_Env_Banner", cast=False)

    # ================================================================
    # 4. BANNERS - hanging cloth from the roof trusses
    # ================================================================
    banners = [
        ("Banner_N", (0, 7.0, 9.4), 180),
        ("Banner_W", (-9.4, 7.0, 0), 90),
        ("Banner_E", (9.4, 7.0, 0), -90),
        ("Banner_S2", (-10, 7.0, -9.4), 0),
    ]
    for (name, pos, yaw) in banners:
        prop(name, pos, (0.14, 4.4, 1.7), "M_Env_Banner", yaw=yaw, cast=False)

    # ================================================================
    # 5. EXTERIOR - skyline ring + ground beyond the glazing
    #    (seen through the glazing band + over the west rooms' roofs)
    # ================================================================
    prop("Exterior_Ground", (0, -0.06, 0), (240, 0.1, 240), "M_Env_Ground", cast=False)
    prop("Plaza_North", (0, 0.0, 26.5), (12, 0.12, 8), "M_Hall_Concrete", cast=False)
    prop("Plaza_South", (0, 0.0, -26.5), (12, 0.12, 8), "M_Hall_Concrete", cast=False)

    # hand-placed tower ring (avoids the campaign rooms to the west / north-west)
    towers = [
        ("Tower_N1", (0, 12, 66), (10, 34, 10)),
        ("Tower_N2", (30, 9, 58), (8, 26, 8)),
        ("Tower_N3", (-30, 10, 58), (9, 28, 9)),
        ("Tower_E1", (62, 11, 30), (11, 30, 11)),
        ("Tower_E2", (64, 8, -18), (8, 24, 8)),
        ("Tower_E3", (58, 13, 62), (12, 36, 12)),
        ("Tower_S1", (0, 9, -66), (9, 26, 9)),
        ("Tower_S2", (34, 11, -56), (10, 30, 10)),
        ("Tower_S3", (-38, 10, -58), (9, 28, 9)),
        ("Tower_W1", (-72, 12, -66), (10, 32, 10)),
    ]
    for (name, pos, scale) in towers:
        prop(name, pos, scale, "M_Env_City", cast=False)

    return {
        "materials": sorted(DRESS.keys()),
        "renderers": 4 + 8 + 4 + 8 + 6 + 8 + 2 + 4 + 3 + 10,
    }
