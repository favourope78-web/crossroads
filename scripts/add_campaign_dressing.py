"""ART PRODUCTION PASS - campaign location dressing.

Imported (not run) by gen_firstlocation_scene.py after add_visual_pass_content.
Every campaign room gets ONE authored dressing cluster (real FBX meshes from
scripts/blender_build_dressing.py - a merged, hand-built prop set, not a disguised
primitive) themed to its story role:

  last_summer        PierSet       (the remembered pier)
  fracture_night     LanternPair   (the night the sky broke)
  under_spire        ShrineSet
  interlude_becoming CrateStack    (camp supplies)
  docks              CrateStack    (dock cargo, rotated)
  sanctuary          ShrineSet
  long_wall          Barricade
  dax_arena          Barricade
  interlude_reckoning Barricade
  market             MarketStall
  spire_ascent       LanternPair
  choirmaster        ShrineSet
  epilogue           Planter       (something growing again)

Clusters sit near ROOM CORNERS (offset ~4m diag) so the 4.4 m camera orbit and
the walkable centre stay clear. Cost: +13 renderers (one merged mesh each, 2 material
slots) - inside the Android
budget with margin (see docs/PERF_BUDGET.json, checked by profile_scene.py).
"""
import os

CLUSTERS = [
    "SM_Dress_LanternPair", "SM_Dress_CrateStack", "SM_Dress_Planter", "SM_Dress_BannerWall",
    "SM_Dress_PierSet", "SM_Dress_ShrineSet", "SM_Dress_Barricade", "SM_Dress_MarketStall",
]

# room id -> (cluster, offset from room anchor, yaw, structure mat, accent mat, collider size)
# collider sizes approximate each cluster footprint for player collision
DRESSING = {
    "last_summer":         ("SM_Dress_PierSet",    ( 4.0, 0,  3.6), 25,  "M_Env_PlantBox", "M_Env_Banner",   (2.6, 1.6, 1.2)),
    "fracture_night":      ("SM_Dress_LanternPair",( 4.0, 0, -3.8), 40,  "M_Hall_Metal",   "M_Env_Lantern",  (2.2, 2.8, 0.8)),
    "under_spire":         ("SM_Dress_ShrineSet",  ( 4.0, 0,  3.6), 200, "M_Hall_Concrete","M_Env_Lantern",  (1.6, 1.0, 1.1)),
    "interlude_becoming":  ("SM_Dress_CrateStack", ( 4.0, 0,  3.6), 15,  "M_Env_PlantBox", "M_Hall_Metal",   (1.6, 1.3, 1.2)),
    "docks":               ("SM_Dress_CrateStack", (-4.0, 0,  3.7), 160, "M_Env_PlantBox", "M_Hall_Metal",   (1.6, 1.3, 1.2)),
    "sanctuary":           ("SM_Dress_ShrineSet",  ( 4.0, 0,  3.6), 340, "M_Hall_Concrete","M_Env_Lantern",  (1.6, 1.0, 1.1)),
    "long_wall":           ("SM_Dress_Barricade",  ( 4.0, 0, -3.8), 90,  "M_Env_PlantBox", "M_Hall_Metal",   (2.2, 1.4, 0.9)),
    "dax_arena":           ("SM_Dress_Barricade",  (-4.0, 0, -3.8), 270, "M_Env_PlantBox", "M_Hall_Metal",   (2.2, 1.4, 0.9)),
    "interlude_reckoning": ("SM_Dress_Barricade",  ( 4.0, 0, -3.8), 10,  "M_Env_PlantBox", "M_Hall_Metal",   (2.2, 1.4, 0.9)),
    "market":              ("SM_Dress_MarketStall",( 4.0, 0,  3.6), 205, "M_Env_PlantBox", "M_Env_Banner",   (2.1, 1.8, 1.3)),
    "spire_ascent":        ("SM_Dress_LanternPair",(-4.0, 0, -3.8), 130, "M_Hall_Metal",   "M_Env_Lantern",  (2.2, 2.8, 0.8)),
    "choirmaster":         ("SM_Dress_ShrineSet",  ( 4.0, 0,  3.6), 155, "M_Hall_Concrete","M_Hollow",       (1.6, 1.0, 1.1)),
    "epilogue":            ("SM_Dress_Planter",    ( 4.0, 0,  3.6), 305, "M_Env_PlantBox", "M_Env_Foliage",  (1.7, 2.2, 1.7)),
}

# room anchor world positions (LocationAnchor_<room> in the generated scene)
ANCHORS = {
    "last_summer": (-40, -17), "fracture_night": (-40, 13), "under_spire": (-40, 43),
    "interlude_becoming": (-70, -17), "docks": (-70, 13), "sanctuary": (-70, 43),
    "long_wall": (-70, -47), "dax_arena": (-100, 13), "interlude_reckoning": (-100, -17),
    "market": (-100, 43), "spire_ascent": (-100, -47), "choirmaster": (-130, 13),
    "epilogue": (-130, -17),
}


def build(g):
    """g = the generator's globals() dict."""
    emit_gameobject, emit_transform, emit_meshfilter = g["emit_gameobject"], g["emit_transform"], g["emit_meshfilter"]
    add_block = g["add_block"]
    REG, root_gids = g["REG"], g["root_gids"]
    STATIC_KIT = g["STATIC_KIT"]
    ROOT = g["ROOT"]
    ensure, g32 = g["ensure"], g["g32"]
    write_meta_if_missing = g["write_meta_if_missing"]

    import re as _re
    KIT_META = open(os.path.join(ROOT, "Assets/Game/Environment/Kit/SM_Column.fbx.meta")).read()
    KIT_DIR = os.path.join(ROOT, "Assets/Game/Environment/Kit")
    for i, name in enumerate(CLUSTERS):
        ensure(name, g32(0x1d0 + i))
        p = os.path.join(KIT_DIR, name + ".fbx")
        mp = p + ".meta"
        if not os.path.exists(mp):
            txt = _re.sub(r"guid: [0-9a-f]{32}", "guid: " + REG[name], KIT_META, count=1)
            open(mp, "w").write(txt)

    def emit_renderer_multi(rid, gid, matguids, cast=1, static=True):
        mats = "".join("  - {fileID: 2100000, guid: %s, type: 2}\n" % m for m in matguids)
        add_block("--- !u!23 &%d\nMeshRenderer:\n"
                  "  m_ObjectHideFlags: 0\n"
                  "  m_CorrespondingSourceObject: {fileID: 0}\n"
                  "  m_PrefabInstance: {fileID: 0}\n"
                  "  m_PrefabAsset: {fileID: 0}\n"
                  "  m_GameObject: {fileID: %d}\n"
                  "  m_Enabled: 1\n"
                  "  m_CastShadows: %d\n"
                  "  m_ReceiveShadows: 1\n"
                  "  m_DynamicOccludee: 1\n"
                  "  m_StaticShadowCaster: %d\n"
                  "  m_MotionVectors: 1\n"
                  "  m_LightProbeUsage: 1\n"
                  "  m_ReflectionProbeUsage: 1\n"
                  "  m_RayTracingMode: 2\n"
                  "  m_RayTracingProcedural: 0\n"
                  "  m_RenderingLayerMask: 1\n"
                  "  m_RendererPriority: 0\n"
                  "  m_Materials:\n%s"
                  "  m_StaticBatchInfo:\n"
                  "    firstSubMesh: 0\n"
                  "    subMeshCount: 0\n"
                  "  m_StaticBatchRoot: {fileID: 0}\n"
                  "  m_ProbeAnchor: {fileID: 0}\n"
                  "  m_LightProbeVolumeOverride: {fileID: 0}\n"
                  "  m_SortingLayerID: 0\n"
                  "  m_SortingLayer: 0\n"
                  "  m_SortingOrder: 0\n"
                  "  m_AdditionalVertexStreams: {fileID: 0}" % (rid, gid, cast, 1 if static else 0, mats))

    placed = 0
    for room, (cluster, off, yaw, struct_mat, accent_mat, col) in DRESSING.items():
        ax, az = ANCHORS[room]
        pos = (ax + off[0], off[1], az + off[2])
        gid, ids = emit_gameobject("Dress_%s" % room, ["transform", "meshfilter", "renderer", "col"],
                                   static_flags=STATIC_KIT)
        emit_transform(ids["transform"], gid, pos, (0, yaw, 0), (1, 1, 1))
        emit_meshfilter(ids["meshfilter"], gid, REG[cluster])
        emit_renderer_multi(ids["renderer"], gid, [REG[struct_mat], REG[accent_mat]])
        g["emit_boxcollider"](ids["col"], gid, col, (0, col[1] / 2.0, 0))
        root_gids.append(gid)
        placed += 1

    return {"clusters": len(CLUSTERS), "placed": placed}
