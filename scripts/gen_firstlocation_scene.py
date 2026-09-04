"""Generates Assets/Scenes/Prototype/FirstLocation.unity (+ .meta) from
scripts/hall_layout.json, the Fracture Hall kit + the Phase-DECISION story additions
(Mara encounter NPC, consequence marker objects, story bootstrappers).
Also writes kit/material metas and (write-if-missing) script metas.
Deterministic GUID scheme c0a1fed2... - run gen_story_content.py first (registry).
"""
import json, os, hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
LAY = json.load(open(os.path.join(HERE, "hall_layout.json")))
ENV = os.path.join(ROOT, "Assets/Game/Environment")
SCN = os.path.join(ROOT, "Assets/Scenes/Prototype")
os.makedirs(SCN, exist_ok=True)

# ---------------------------------------------------------------- guid registry
REG_PATH = os.path.join(HERE, "hall_guids.json")
REG = json.load(open(REG_PATH)) if os.path.exists(REG_PATH) else {}

def g32(n):
    return ("c0a1fed2" + ("%024x" % n))[:32]

def ensure(key, value):
    if key in REG and REG[key] != value:
        raise SystemExit("GUID conflict for %s: %s vs %s" % (key, REG[key], value))
    REG[key] = value

kit = ["SM_FloorTile","SM_Column","SM_LightBeam","SM_WallPanel","SM_GlazingPanel","SM_BalconyBlock",
       "SM_Railing","SM_Truss","SM_DoorFrame","SM_Door","SM_OrbCore","SM_OrbRing","SM_HoloPanel"]
for i, k in enumerate(kit, 1): ensure(k, g32(i))
mats = ["M_Hall_Concrete","M_Hall_Metal","M_Hall_LightColumn","M_Hall_Glazing","M_Hall_OrbGold","M_Hall_Holo"]
for i, m in enumerate(mats, 40): ensure(m, g32(i))
ensure("ThirdPersonCameraController.cs", g32(80))
ensure("Interactable.cs", g32(81))
ensure("DoorInteractable.cs", g32(82))
ensure("FirstLocationBootstrap.cs", g32(84))
ensure("FirstLocation.unity", g32(90))
# story additions must already exist in the registry (see gen_story_content.py); sanity check:
for need in ["PlayerInteraction.cs","StoryWorldState.cs","StoryModeBootstrap.cs",
             "GameUIBootstrap.cs","ScriptableObjectAssets.cs","NpcAgent.cs","NpcInteractable.cs",
             "M_Seq_Ember","M_Seq_Tide","M_Seq_Stone","M_Npc_Mara","M_Npc_Civilian","CL_C1_StoryContent.asset"]:
    if need not in REG:
        raise SystemExit("registry missing %s - run scripts/gen_story_content.py first" % need)
json.dump(REG, open(REG_PATH, "w"), indent=1)

URP = "9335e4a172916944ba2695448482493a"

NATIVE = """fileFormatVersion: 2
guid: {g}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
MONO = """fileFormatVersion: 2
guid: {g}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def write_meta_if_missing(path, template, g):
    meta = path + ".meta"
    if not os.path.exists(meta):
        open(meta, "w").write(template.format(g=g))

KIT_DIR = os.path.join(ENV, "Kit")
for k in kit:
    write_meta_if_missing(os.path.join(KIT_DIR, k + ".fbx"), NATIVE, REG[k])
MAT_DIR = os.path.join(ENV, "Materials")
for m in mats + ["M_Seq_Ember", "M_Seq_Tide", "M_Seq_Stone", "M_Npc_Mara", "M_Npc_Civilian"]:
    write_meta_if_missing(os.path.join(MAT_DIR, m + ".mat"), NATIVE, REG[m])
write_meta_if_missing(os.path.join(ROOT, "Assets/Game/Scripts/ThirdPersonCameraController.cs"), MONO, REG["ThirdPersonCameraController.cs"])
write_meta_if_missing(os.path.join(ROOT, "Assets/Game/Scripts/FirstLocationBootstrap.cs"), MONO, REG["FirstLocationBootstrap.cs"])
write_meta_if_missing(os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/Interaction/Interactable.cs"), MONO, REG["Interactable.cs"])
write_meta_if_missing(os.path.join(ROOT, "Assets/_Project/Scripts/Gameplay/Interaction/DoorInteractable.cs"), MONO, REG["DoorInteractable.cs"])
for d in ["Assets/Game", "Assets/Game/Environment", "Assets/Game/Environment/Kit", "Assets/Game/Environment/Materials",
          "Assets/Game/Scripts", "Assets/Scenes", "Assets/Scenes/Prototype"]:
    meta = os.path.join(ROOT, d + ".meta")
    if not os.path.exists(meta):
        open(meta, "w").write("""fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""".format(g=hashlib.md5(("folder:" + d).encode()).hexdigest()))

# ---------------------------------------------------------------- scene emitters
COLLIDERS = {
 "SM_FloorTile": ("box",(10,0.12,10),(0,0,0)),
 "SM_Column": ("box",(1.15,9.0,1.15),(0,4.5,0)),
 "SM_WallPanel": ("box",(10,6,0.55),(0,3,0)),
 "SM_BalconyBlock": ("box",(10,0.45,3.0),(0,0,0)),
 "SM_Railing": ("box",(10,1.25,0.12),(0,0.55,0)),
 "SM_DoorFrame": ("box",(4.9,4.95,1.0),(0,2.45,0)),
 "SM_Door": ("box",(3.6,4.0,0.26),(0,2.0,0)),
}
DYNAMIC = {"SM_Door","SM_OrbCore","SM_OrbRing","SM_HoloPanel"}

out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]
fid = [1000]
blocks = []
root_gids = []

def nid():
    fid[0] += 2
    return fid[0]

def add_block(txt):
    blocks.append(txt)

# settings (unchanged from the original prototype scene)
add_block("""--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}""")
add_block("""--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 1
  m_FogColor: {r: 0.38, g: 0.27, b: 0.26, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.012
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.55, g: 0.38, b: 0.36, a: 1}
  m_AmbientEquatorColor: {r: 0.40, g: 0.33, b: 0.32, a: 1}
  m_AmbientGroundColor: {r: 0.22, g: 0.20, b: 0.19, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {r: 0.42, g: 0.47, b: 0.5, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_IndirectSpecularColor: {r: 0, g: 0, b: 0, a: 1}
  m_UseRadianceAmbientProbe: 0""")
add_block("""--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}""")
add_block("""--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}""")

def emit_gameobject(name, comps, is_active=1):
    g = fid[0] + 1
    ids = {}
    comp_lines = []
    for kind in comps:
        fid[0] += 2
        ids[kind] = fid[0]
        comp_lines.append("  - component: {fileID: %d}" % ids[kind])
    add_block("""--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
%s
  m_Layer: 0
  m_Name: %s
  m_Tag: Untagged
  m_IsActive: %d""" % (g, "\n".join(comp_lines), name, is_active))
    return g, ids

def euler_to_quat(euler_xyz):
    """Unity Quaternion.Euler(x, y, z) == qy * qx * qz (Hamilton product), computed here
    so placement yaw/pitch actually lands in the scene (latent rot bug in v1 fixed)."""
    import math
    rx, ry, rz = math.radians(euler_xyz[0]), math.radians(euler_xyz[1]), math.radians(euler_xyz[2])
    sx, cx = math.sin(rx / 2), math.cos(rx / 2)
    sy, cy = math.sin(ry / 2), math.cos(ry / 2)
    # qy (0, sy, 0, cy) * qx (sx, 0, 0, cx) * qz (0, 0, sz, cz)
    # qy*qx:
    px, py, pz, pw = cy * sx, sy * cx, -sy * sx, cy * cx
    sz, cz = math.sin(rz / 2), math.cos(rz / 2)
    # (px,py,pz,pw) * (0,0,sz,cz):
    x = pw * 0 + px * cz + py * sz - pz * 0
    y = pw * 0 - px * sz + py * cz + pz * 0
    z = pw * sz + px * 0 - py * 0 + pz * cz
    w = pw * cz - px * 0 - py * 0 - pz * sz
    return x, y, z, w

def emit_transform(tid, gid, pos, rot_euler, scale, father=0, children=None):
    ch = ("\n".join("  - {fileID: %d}" % c for c in children)) if children else ""
    qx, qy, qz, qw = euler_to_quat(rot_euler)
    add_block("""--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: %.7f, y: %.7f, z: %.7f, w: %.7f}
  m_LocalPosition: {x: %s, y: %s, z: %s}
  m_LocalScale: {x: %s, y: %s, z: %s}
  m_ConstrainProportionsScale: 0
  m_Children:
%s
  m_Father: {fileID: %d}
  m_LocalEulerAnglesHint: {x: %s, y: %s, z: %s}""" % (
        tid, gid, qx, qy, qz, qw, pos[0], pos[1], pos[2], scale[0], scale[1], scale[2],
        ch if ch else "  []", father, rot_euler[0], rot_euler[1], rot_euler[2]))

def emit_meshfilter(mid, gid, meshguid, builtin_fileid=None):
    if builtin_fileid is not None:
        m = "{fileID: %d, guid: 0000000000000000e000000000000000, type: 0}" % builtin_fileid
    else:
        m = "{fileID: 4300000, guid: %s, type: 3}" % meshguid
    add_block("""--- !u!33 &%d
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Mesh: %s""" % (mid, gid, m))

def emit_renderer(rid, gid, matguid):
    add_block("""--- !u!23 &%d
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: %s, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {fileID: 0}""" % (rid, gid, matguid))

def emit_boxcollider(cid, gid, size, center):
    add_block("""--- !u!65 &%d
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Material: {fileID: 0}
  m_IncludeGestures: 0
  m_IsTrigger: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {x: %s, y: %s, z: %s}
  m_Center: {x: %s, y: %s, z: %s}""" % (cid, gid, size[0], size[1], size[2], center[0], center[1], center[2]))

def emit_capsulecollider(cid, gid, radius, height, center):
    add_block("""--- !u!136 &%d
CapsuleCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Material: {fileID: 0}
  m_IsTrigger: 0
  m_Enabled: 1
  m_Radius: %s
  m_Height: %s
  m_Direction: 1
  m_Center: {x: %s, y: %s, z: %s}""" % (cid, gid, radius, height, center[0], center[1], center[2]))

def emit_monobehaviour(bid, gid, scriptguid, fields=""):
    add_block("""--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: 
  m_EditorClassIdentifier: %s""" % (bid, gid, scriptguid, ("\n" + fields) if fields else ""))

# --- kit pieces (unchanged layout) ---
for idx, e in enumerate(LAY["pieces"]):
    piece = e["piece"]
    pos, yaw, scale = e["pos"], e["yaw"], e["scale"]
    comps = ["transform","meshfilter","renderer"]
    is_open_frame = (piece == "SM_DoorFrame" and e["pos"][0] == 0 and e["pos"][2] == 20)
    if piece in COLLIDERS and not is_open_frame: comps.append("collider")
    if e["interact"] == "Door": comps.append("door")
    if e["interact"] == "Inspect": comps.append("interact")
    gid, ids = emit_gameobject("%s_%03d" % (piece, idx), comps)
    emit_transform(ids["transform"], gid, pos, (0, yaw, 0), scale)
    emit_meshfilter(ids["meshfilter"], gid, REG[piece])
    slot = {"SM_FloorTile":"Concrete","SM_Column":"Metal","SM_LightBeam":"LightColumn",
            "SM_WallPanel":"Concrete","SM_GlazingPanel":"Glazing","SM_BalconyBlock":"Concrete",
            "SM_Railing":"Metal","SM_Truss":"Metal","SM_DoorFrame":"Metal","SM_Door":"Metal",
            "SM_OrbCore":"OrbGold","SM_OrbRing":"OrbGold","SM_HoloPanel":"Holo"}[piece]
    emit_renderer(ids["renderer"], gid, REG[{"Concrete":"M_Hall_Concrete","Metal":"M_Hall_Metal",
        "LightColumn":"M_Hall_LightColumn","Glazing":"M_Hall_Glazing","OrbGold":"M_Hall_OrbGold",
        "Holo":"M_Hall_Holo"}[slot]])
    if "collider" in ids:
        c = COLLIDERS[piece]
        emit_boxcollider(ids["collider"], gid, c[1], c[2])
    if "door" in ids:
        emit_monobehaviour(ids["door"], gid, REG["DoorInteractable.cs"])
    if "interact" in ids:
        emit_monobehaviour(ids["interact"], gid, REG["Interactable.cs"])
    root_gids.append(gid)

# --- directional light ---
gid, ids = emit_gameobject("Directional Light", ["transform","light"])
emit_transform(ids["transform"], gid, (0,12,0), (55,-30,0), (1,1,1))
add_block("""--- !u!108 &%d
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  serializedVersion: 10
  m_Type: 1
  m_Shape: 0
  m_Color: {r: 1, g: 0.93, b: 0.85, a: 1}
  m_Intensity: 1.1
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_CookieSize: 10
  m_Shadows:
    m_Type: 0
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 1
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_Cookie: {fileID: 0}
  m_DrawHalo: 0
  m_Flare: {fileID: 0}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {x: 1, y: 1}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ShadowRadius: 0
  m_ShadowAngle: 0""" % (ids['light'], gid))
root_gids.append(gid)

# --- main camera + follow ---
gid, ids = emit_gameobject("Main Camera", ["transform","camera","listener","follow"])
emit_transform(ids["transform"], gid, (0,2.6,-20.5), (12,0,0), (1,1,1))
add_block("""--- !u!20 &%d
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {r: 0.30, g: 0.20, b: 0.19, a: 1}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {x: 2, y: 11}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {x: 36, y: 24}
  m_LensShift: {x: 0, y: 0}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.1
  far clip plane: 120
  field of view: 55
  orthographic: 0
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 0
  m_AllowMSAA: 0
  m_AllowDynamicResolution: 1
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022""" % (ids['camera'], gid))
add_block("""--- !u!81 &%d
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1""" % (ids['listener'], gid))
emit_monobehaviour(ids["follow"], gid, REG["ThirdPersonCameraController.cs"])
root_gids.append(gid)

# --- editor bootstrap (spawns Ari in the editor) ---
gid, ids = emit_gameobject("FirstLocationBootstrap", ["transform","bootstrap"])
emit_transform(ids["transform"], gid, tuple(LAY["spawn"]), (0,0,0), (1,1,1))
emit_monobehaviour(ids["bootstrap"], gid, REG["FirstLocationBootstrap.cs"])
root_gids.append(gid)

# ================================================================
# ================================================================
# STORY/PROGRESSION scene additions (data-driven encounters + gates)
# ================================================================
CUBE, CAPSULE, SPHERE = 10202, 10208, 10207  # built-in primitive mesh fileIDs

def emit_char_root(root_name, comp_kinds, pos, rot_euler, is_active, primitives):
    """Root GO with primitive visual children. Returns (gid, ids, children_info) where
    children_info = [(child_name, transform_id, renderer_id), ...]."""
    gid, ids = emit_gameobject(root_name, ["transform"] + comp_kinds, is_active=is_active)
    root_gids.append(gid)
    child_tids = []
    children_info = []
    for (cname, matkey, meshid, lpos, lscale) in primitives:
        cgid, cids = emit_gameobject(cname, ["transform", "meshfilter", "renderer"])
        emit_transform(cids["transform"], cgid, tuple(lpos), (0, 0, 0), tuple(lscale), father=ids["transform"])
        emit_meshfilter(cids["meshfilter"], cgid, None, builtin_fileid=meshid)
        emit_renderer(cids["renderer"], cgid, REG[matkey])
        child_tids.append(cids["transform"])
        children_info.append((cname, cids["transform"], cids["renderer"]))
    emit_transform(ids["transform"], gid, tuple(pos), tuple(rot_euler), (1, 1, 1), children=child_tids)
    return gid, ids, children_info

def child_renderer_id(children_info, name):
    for (cn, tid, rid) in children_info:
        if cn == name: return rid
    return 0

# ---- Mara NPC (first encounter) ----
mara_gid, mara_ids, mara_children = emit_char_root("Mara_NPC", ["collider", "npc", "fate"],
    (6.5, 0, -8), (0, 180, 0), 1, [
    ("Body", "M_Npc_Mara", CAPSULE, (0, 0.78, 0), (0.55, 0.72, 0.55)),
    ("Head", "M_Npc_Mara", SPHERE, (0, 1.62, 0), (0.34, 0.34, 0.34)),
])
emit_capsulecollider(mara_ids["collider"], mara_gid, 0.35, 1.7, (0, 0.85, 0))
emit_monobehaviour(mara_ids["npc"], mara_gid, REG["NpcInteractable.cs"],
    "  npc: {fileID: %d}\n  promptLabel: Talk to Mara\n  interactRadius: 3.2\n  priority: 20" % mara_ids["fate"])
emit_monobehaviour(mara_ids["fate"], mara_gid, REG["NpcAgent.cs"],
    "  npcId: mara\n  baseTitle: \"\"\n  playerRef: {fileID: 0}\n  bodyRenderer: {fileID: %d}\n  baseMaterial: {fileID: 2100000, guid: %s, type: 2}\n  avatarPrefab: {fileID: 0}\n  visualVariants:\n  - conditions:\n    - type: 5\n      key: mara\n      value: \"\"\n      amount: 8\n    material: {fileID: 2100000, guid: %s, type: 2}" %
    (child_renderer_id(mara_children, "Body"), REG["M_Npc_Mara"], REG["M_Seq_Tide"]))

# ---- consequence markers (start inactive; one is activated by the chosen path) ----
def emit_marker(go_name, matkey, pos):
    gid, _, _ = emit_char_root(go_name, [], pos, (0, 0, 0), 0, [
        ("Beam", matkey, CUBE, (0, 1.05, 0), (0.22, 2.0, 0.22)),
        ("Base", "M_Hall_Metal", CUBE, (0, 0.07, 0), (0.72, 0.14, 0.72)),
    ])
    return gid

m_ember = emit_marker("Seq_Ember_Marker", "M_Seq_Ember", (3.2, 0, -3.2))
m_tide  = emit_marker("Seq_Tide_Marker", "M_Seq_Tide", (-3.2, 0, -3.2))
m_stone = emit_marker("Seq_Stone_Marker", "M_Seq_Stone", (0, 0, 3.2))

# ---- tide bystanders (the twins; exist only on the Tide path) ----
by_gid, _, _ = emit_char_root("Seq_Tide_Bystanders", [], (14.5, 0, 0), (0, -90, 0), 0, [
    ("Civilian_1", "M_Npc_Civilian", CAPSULE, (0, 0.78, 0), (0.5, 0.66, 0.5)),
    ("Civilian_1_Head", "M_Npc_Civilian", SPHERE, (0, 1.5, 0), (0.30, 0.30, 0.30)),
    ("Civilian_2", "M_Npc_Civilian", CAPSULE, (1.1, 0.78, 0), (0.5, 0.62, 0.5)),
    ("Civilian_2_Head", "M_Npc_Civilian", SPHERE, (1.1, 1.44, 0), (0.28, 0.28, 0.28)),
])

# ---- story bootstrappers (services + UI) ----
gid, ids = emit_gameobject("StoryModeBootstrap", ["transform", "story"])
emit_transform(ids["transform"], gid, (0, 0, 0), (0, 0, 0), (1, 1, 1))
emit_monobehaviour(ids["story"], gid, REG["StoryModeBootstrap.cs"],
    "  contentLibrary: {fileID: 11400000, guid: %s, type: 2}\n  saveSlot: 0\n  sceneKey: FirstLocation\n  checkpointId: hall_spawn\n  devClearSaveOnStart: 0" % REG["CL_C1_StoryContent.asset"])
root_gids.append(gid)

gid, ids = emit_gameobject("GameUIBootstrap", ["transform", "ui"])
emit_transform(ids["transform"], gid, (0, 0, 0), (0, 0, 0), (1, 1, 1))
emit_monobehaviour(ids["ui"], gid, REG["GameUIBootstrap.cs"])
root_gids.append(gid)

# ================================================================
# PROGRESSION/CONSEQUENCE scene additions (annex + gate + cast)
# ================================================================

# ---- wall fills sealing the north-wall flanks (scaled panels -> real doorway) ----
for (fx, tag) in [(6.5, "L"), (-6.5, "R")]:
    gid, ids = emit_gameobject("SM_WallPanel_flank_" + tag, ["transform", "meshfilter", "renderer", "collider"])
    emit_transform(ids["transform"], gid, (fx, 3, 20), (0, 0, 0), (0.7, 1, 1))
    emit_meshfilter(ids["meshfilter"], gid, REG["SM_WallPanel"])
    emit_renderer(ids["renderer"], gid, REG["M_Hall_Concrete"])
    emit_boxcollider(ids["collider"], gid, (10, 6, 0.55), (0, 3, 0))
    root_gids.append(gid)

# ---- annex room (20 x 10, beyond the north gate) ----
ANNEX = [
    ("SM_FloorTile", (-5, 0.05, 25), 0),
    ("SM_FloorTile", (5, 0.05, 25), 0),
    ("SM_WallPanel", (-10, 3, 25), 90),
    ("SM_WallPanel", (10, 3, 25), 90),
    ("SM_WallPanel", (-5, 3, 30), 0),
    ("SM_WallPanel", (5, 3, 30), 0),
    ("SM_Column", (-7, 0, 27), 0),
    ("SM_Column", (7, 0, 27), 0),
    ("SM_Truss", (0, 9.2, 25), 0),
]
for (piece, pos, yaw) in ANNEX:
    comps = ["transform", "meshfilter", "renderer"]
    if piece in COLLIDERS: comps.append("collider")
    gid, ids = emit_gameobject("%s_annex_%s_%s" % (piece, pos[0], pos[2]), comps)
    emit_transform(ids["transform"], gid, pos, (0, yaw, 0), (1, 1, 1))
    emit_meshfilter(ids["meshfilter"], gid, REG[piece])
    mat = {"SM_FloorTile": "M_Hall_Concrete", "SM_WallPanel": "M_Hall_Concrete",
           "SM_Column": "M_Hall_Metal", "SM_Truss": "M_Hall_Metal"}[piece]
    emit_renderer(ids["renderer"], gid, REG[mat])
    if "collider" in ids:
        c = COLLIDERS[piece]
        emit_boxcollider(ids["collider"], gid, c[1], c[2])
    root_gids.append(gid)

for pos in [(-7, 0, 27), (7, 0, 27)]:
    gid, ids = emit_gameobject("SM_LightBeam_annex_%s_%s" % (pos[0], pos[2]), ["transform", "meshfilter", "renderer"])
    emit_transform(ids["transform"], gid, pos, (0, 0, 0), (1, 1, 1))
    emit_meshfilter(ids["meshfilter"], gid, REG["SM_LightBeam"])
    emit_renderer(ids["renderer"], gid, REG["M_Hall_LightColumn"])
    root_gids.append(gid)

# ---- energy seal gate (data-driven accessible-area consequence) ----
seal_gid, seal_ids, seal_children = emit_char_root("EnergySeal", ["collider", "gate"], (0, 1.7, 20.5), (0, 0, 0), 1, [
    ("Plate", "M_Hall_Holo", CUBE, (0, 0, 0), (6.0, 3.4, 0.22)),
])
emit_boxcollider(seal_ids["collider"], seal_gid, (6.0, 3.4, 0.22), (0, 0, 0))
seal_plate_renderer = child_renderer_id(seal_children, "Plate")
p_l, _, _ = emit_char_root("SealBase_L", [], (-3.3, 0, 20), (0, 0, 0), 1, [
    ("Block", "M_Hall_Metal", CUBE, (0, 0.85, 0), (0.5, 1.7, 0.6))])
p_r, _, _ = emit_char_root("SealBase_R", [], (3.3, 0, 20), (0, 0, 0), 1, [
    ("Block", "M_Hall_Metal", CUBE, (0, 0.85, 0), (0.5, 1.7, 0.6))])
gate_fields = (
    "  rules:\n"
    "  - conditions:\n    - type: 11\n      key: ember_pulse\n      value: \"\"\n      amount: 0\n"
    "    opens: 1\n    text: The seal drinks the echo and parts. North Annex lies open.\n"
    "  - conditions:\n    - type: 11\n      key: tide_mend\n      value: \"\"\n      amount: 0\n"
    "    opens: 1\n    text: The seal softens like water around your hand. North Annex lies open.\n"
    "  - conditions:\n    - type: 11\n      key: stone_ward\n      value: \"\"\n      amount: 0\n"
    "    opens: 1\n    text: The seal holds, then yields - unhurried, the way you asked it to. North Annex lies open.\n"
    "  - conditions: []\n    opens: 0\n"
    "    text: A seal of the hall's own light, bent around nothing you carry. It only parts for an echoed voice.\n"
    "  areaId: annex\n"
    "  variants:\n"
    "  - conditions:\n    - type: 0\n      key: c1_hall_drive\n      value: ember\n      amount: 0\n    material: {fileID: 2100000, guid: %s, type: 2}\n"
    "  - conditions:\n    - type: 0\n      key: c1_hall_drive\n      value: tide\n      amount: 0\n    material: {fileID: 2100000, guid: %s, type: 2}\n"
    "  - conditions:\n    - type: 0\n      key: c1_hall_drive\n      value: stone\n      amount: 0\n    material: {fileID: 2100000, guid: %s, type: 2}\n"
    "  blocker: {fileID: %d}\n"
    "  visuals:\n  - {fileID: %d}\n  - {fileID: %d}\n  - {fileID: %d}\n"
    "  sealRenderer: {fileID: %d}\n"
    "  promptLabel: Energy Seal\n  interactRadius: 3.4\n  priority: 15\n"
    "  openPrompt: Enter the Annex\n  closedPrompt: Energy Seal\n"
    "  openNotice: The seal is open.\n  sealedNotice: The energy seal shimmers. It does not know you."
) % (REG["M_Seq_Ember"], REG["M_Seq_Tide"], REG["M_Seq_Stone"],
     seal_ids["collider"], seal_gid, p_l, p_r, seal_plate_renderer)
emit_monobehaviour(seal_ids["gate"], seal_gid, REG["AreaGate.cs"], gate_fields)

# ---- the Fracture Shard (annex loot; the interactable vanishes once taken) ----
shard_gid, shard_ids, _ = emit_char_root("EchoShard", ["shard"], (0, 0, 26.5), (20, 0, 0), 1, [
    ("Pedestal", "M_Hall_Metal", CUBE, (0, 0.09, 0), (1.1, 0.18, 1.1)),
    ("Crystal", "M_Hall_OrbGold", CUBE, (0, 1.02, 0), (0.34, 0.62, 0.34)),
])
emit_monobehaviour(shard_ids["shard"], shard_gid, REG["StoryEventInteractable.cs"],
    "  encounterId: c1_hall_shard\n  promptLabel: The Fracture Shard\n  interactRadius: 3.2\n  priority: 25")

# ---- the Echo Shrine (annex power upgrade / seal: ability progression interactable) ----
shrine_gid, shrine_ids, _ = emit_char_root("EchoShrine", ["shrine"], (-4.6, 0, 27.4), (0, 24, 0), 1, [
    ("Pedestal", "M_Hall_Metal", CUBE, (0, 0.10, 0), (1.20, 0.20, 1.20)),
    ("Pillar", "M_Hall_Metal", CUBE, (0, 0.55, 0), (0.30, 1.10, 0.30)),
    ("Crystal", "M_Hall_OrbGold", CUBE, (0, 1.28, 0), (0.46, 0.80, 0.46)),
])
emit_monobehaviour(shrine_ids["shrine"], shrine_gid, REG["StoryEventInteractable.cs"],
    "  encounterId: c1_east_shrine\n  promptLabel: Echo Shrine\n  interactRadius: 3.2\n  priority: 24")

# ---- Sera (second NPC: behaviour/dialogue/choices depend on the first decision) ----
sera_gid, sera_ids, sera_children = emit_char_root("Sera_NPC", ["collider", "npc", "fate"],
    (17.5, 0, 2.5), (0, -90, 0), 1, [
    ("Body", "M_Npc_Civilian", CAPSULE, (0, 0.72, 0), (0.5, 0.66, 0.5)),
    ("Head", "M_Npc_Civilian", SPHERE, (0, 1.5, 0), (0.3, 0.3, 0.3)),
])
emit_capsulecollider(sera_ids["collider"], sera_gid, 0.32, 1.6, (0, 0.8, 0))
emit_monobehaviour(sera_ids["npc"], sera_gid, REG["NpcInteractable.cs"],
    "  npc: {fileID: %d}\n  promptLabel: Talk to Sera\n  interactRadius: 3.0\n  priority: 20" % sera_ids["fate"])
emit_monobehaviour(sera_ids["fate"], sera_gid, REG["NpcAgent.cs"],
    "  npcId: sera\n  baseTitle: \"\"\n  playerRef: {fileID: 0}\n  bodyRenderer: {fileID: %d}\n  baseMaterial: {fileID: 2100000, guid: %s, type: 2}\n  avatarPrefab: {fileID: 0}\n  visualVariants:\n"
    "  - conditions:\n    - type: 0\n      key: c1_hall_drive\n      value: ember\n      amount: 0\n    material: {fileID: 2100000, guid: %s, type: 2}\n"
    "  - conditions:\n    - type: 0\n      key: c1_hall_drive\n      value: tide\n      amount: 0\n    material: {fileID: 2100000, guid: %s, type: 2}\n"
    "  - conditions:\n    - type: 0\n      key: c1_hall_drive\n      value: stone\n      amount: 0\n    material: {fileID: 2100000, guid: %s, type: 2}" %
    (child_renderer_id(sera_children, "Body"), REG["M_Npc_Civilian"],
     REG["M_Seq_Ember"], REG["M_Seq_Tide"], REG["M_Seq_Stone"]))

# ---- area tracking (persisted currentArea) ----
trig_annex_gid, trig_annex_ids = emit_gameobject("AreaTrigger_Annex", ["transform", "col_annex", "area_annex"])
emit_transform(trig_annex_ids["transform"], trig_annex_gid, (0, 1.5, 22.4), (0, 0, 0), (1, 1, 1))
add_block("""--- !u!65 &%d
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Material: {fileID: 0}
  m_IncludeGestures: 0
  m_IsTrigger: 1
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {x: 5.6, y: 3, z: 1.1}
  m_Center: {x: 0, y: 0, z: 0}""" % (trig_annex_ids["col_annex"], trig_annex_gid))
emit_monobehaviour(trig_annex_ids["area_annex"], trig_annex_gid, REG["AreaTrigger.cs"], "  areaId: annex")
root_gids.append(trig_annex_gid)

trig_hall_gid, trig_hall_ids = emit_gameobject("AreaTrigger_Hall", ["transform", "col_hall", "area_hall"])
emit_transform(trig_hall_ids["transform"], trig_hall_gid, (0, 1.5, 17.6), (0, 0, 0), (1, 1, 1))
add_block("""--- !u!65 &%d
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Material: {fileID: 0}
  m_IncludeGestures: 0
  m_IsTrigger: 1
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {x: 5.6, y: 3, z: 1.1}
  m_Center: {x: 0, y: 0, z: 0}""" % (trig_hall_ids["col_hall"], trig_hall_gid))
emit_monobehaviour(trig_hall_ids["area_hall"], trig_hall_gid, REG["AreaTrigger.cs"], "  areaId: hall")
root_gids.append(trig_hall_gid)


# ================================================================
# WORLD / OBJECTIVE scene additions (data-driven world actions + relocation)
# ================================================================

def cond_yaml(t, key, value='\"\"', amount=0):
    return ("    - type: %d\n      key: %s\n      value: %s\n      amount: %d" % (t, key, value, amount))

def eff_yaml(t, key, value='\"\"', amount=0):
    return ("    - type: %d\n      key: %s\n      value: %s\n      amount: %d" % (t, key, value, amount))

def world_action_fields(prompt, conds, effects, use_var, max_uses, consume,
                        locked_notice, use_notice, locked_label, radius, priority):
    lines = [
        "  promptLabel: " + prompt,
        "  interactRadius: %s" % radius,
        "  priority: %s" % priority,
        "  conditions:" + ("\n" + "\n".join(conds) if conds else " []"),
        "  lockedNotice: " + locked_notice,
        "  perUseEffects:" + ("\n" + "\n".join(effects) if effects else " []"),
        "  useCountVar: " + use_var,
        "  maxUses: %d" % max_uses,
        "  consumeEntityKey: " + consume,
        "  useNotice: " + use_notice,
        "  spentNotice: There is nothing left to do here.",
        "  hidePromptWhenSpent: 1",
        "  lockedLabel: " + locked_label,
        "  spentLabel: " + locked_label,
    ]
    return "\n".join(lines)

# ---- Choir Beacon (annex; the ember ability gates the channel) ----
beacon_gid, beacon_ids, _ = emit_char_root("ChoirBeacon", ["action"], (6.5, 0, 27), (0, -25, 0), 1, [
    ("Pylon", "M_Hall_Metal", CUBE, (0, 1.15, 0), (0.42, 2.3, 0.42)),
    ("BeaconHead", "M_Seq_Ember", SPHERE, (0, 2.55, 0), (0.55, 0.55, 0.55)),
    ("Base", "M_Hall_Metal", CUBE, (0, 0.09, 0), (1.0, 0.18, 1.0)),
])
emit_monobehaviour(beacon_ids["action"], beacon_gid, REG["WorldActionInteractable.cs"],
    world_action_fields(
        "Choir Beacon",
        [cond_yaml(11, "ember_pulse")],
        [eff_yaml(0, "beacon_silenced", "\"1\"")],
        "beacon_uses", 1, "choir_beacon",
        "\"The beacon's light does not answer your empty hands.\"",
        "\"Ember pours into the beacon. It forgets your name.\"",
        "Choir Beacon", 3.2, 23))

# ---- Ember Cache (spawns where the beacon stood after it is silenced) ----
cache_gid, cache_ids, _ = emit_char_root("EmberCache", ["action"], (6.5, 0, 24.6), (0, 0, 0), 0, [
    ("CacheBox", "M_Hall_Metal", CUBE, (0, 0.28, 0), (0.95, 0.55, 0.95)),
    ("CacheGlow", "M_Seq_Ember", SPHERE, (0, 0.72, 0), (0.36, 0.36, 0.36)),
])
emit_monobehaviour(cache_ids["action"], cache_gid, REG["WorldActionInteractable.cs"],
    world_action_fields(
        "Ember Cache",
        [cond_yaml(0, "beacon_silenced", "\"1\"")],
        [eff_yaml(0, "ember_cache_opened", "\"1\""),
         eff_yaml(15, "ember_core"),
         eff_yaml(10, "", amount=15)],
        "cache_uses", 1, "ember_cache",
        "\"A seam of warmth in the floor. It is not ready to open.\"",
        "\"The cache opens. An ember core, banked and patient.\"",
        "Ember Cache", 3.0, 23))

# ---- Keepsake Crate (hall east columns; tide path only) ----
crate_gid, crate_ids, _ = emit_char_root("KeepsakeCrate", ["action"], (15.8, 0, 1.6), (0, 12, 0), 1, [
    ("Crate", "M_Hall_Metal", CUBE, (0, 0.34, 0), (0.9, 0.62, 0.65)),
    ("Lid", "M_Hall_Concrete", CUBE, (0, 0.70, 0), (0.98, 0.12, 0.72)),
])
emit_monobehaviour(crate_ids["action"], crate_gid, REG["WorldActionInteractable.cs"],
    world_action_fields(
        "Keepsake Crate",
        [cond_yaml(0, "c1_hall_drive", "tide")],
        [eff_yaml(15, "twins_keepsake"),
         eff_yaml(0, "keepsake_found", "\"1\"")],
        "crate_uses", 1, "keepsake_crate",
        "\"A crate of run-run belongings, damp and sad. Nothing here answers you.\"",
        "\"A tin locket, still warm. The twins' keepsake.\"",
        "Keepsake Crate", 3.0, 22))

# ---- Calm twins (spawned when the keepsake is returned; replaces the anxious pair) ----
calm_gid, _, _ = emit_char_root("Seq_Tide_Calm", [], (14.5, 0, 0), (0, -90, 0), 0, [
    ("Civilian_1", "M_Npc_Civilian", CAPSULE, (0, 0.40, 0), (0.5, 0.33, 0.5)),
    ("Civilian_1_Head", "M_Npc_Civilian", SPHERE, (0, 0.94, 0), (0.30, 0.30, 0.30)),
    ("Civilian_2", "M_Npc_Civilian", CAPSULE, (1.1, 0.40, 0), (0.5, 0.31, 0.5)),
    ("Civilian_2_Head", "M_Npc_Civilian", SPHERE, (1.1, 0.90, 0), (0.28, 0.28, 0.28)),
])

# ---- Twins return point (child of the tide bystanders; exists when they do) ----
twins_return_gid, twins_return_ids = emit_gameobject("TwinsReturnPoint", ["transform", "action"])
emit_transform(twins_return_ids["transform"], twins_return_gid, (0.4, 0, 0.9), (0, 0, 0), (1, 1, 1), father=by_gid)
emit_monobehaviour(twins_return_ids["action"], twins_return_gid, REG["WorldActionInteractable.cs"],
    world_action_fields(
        "Return the keepsake",
        [cond_yaml(10, "twins_keepsake")],
        [eff_yaml(16, "twins_keepsake"),
         eff_yaml(0, "keepsake_returned", "\"1\"")],
        "deliver_uses", 1, "",
        "\"The twins press close. Come back when you hold what they lost.\"",
        "\"The smaller twin takes the locket and stops crying mid-breath.\"",
        "The Twins", 3.4, 21))

# ---- Barricade (north passage; stone path braces it 0/2) ----
barricade_gid, barricade_ids, _ = emit_char_root("Barricade", ["action"], (3.6, 0, 17.2), (0, 0, 0), 1, [
    ("Plank_1", "M_Hall_Concrete", CUBE, (0, 0.55, 0), (2.6, 0.30, 0.45)),
    ("Plank_2", "M_Hall_Concrete", CUBE, (0, 0.95, 0), (2.6, 0.30, 0.45)),
    ("Brace", "M_Hall_Metal", CUBE, (0, 0.30, 0.34), (0.35, 1.05, 0.25)),
])
emit_monobehaviour(barricade_ids["action"], barricade_gid, REG["WorldActionInteractable.cs"],
    world_action_fields(
        "Brace the Barricade",
        [cond_yaml(0, "c1_hall_drive", "stone")],
        [],
        "brace_count", 2, "",
        "\"The barricade wants steadier hands than yours.\"",
        "\"You wedge the brace tight. The line steadies.\"",
        "The Barricade", 3.2, 22))

# ---- Ward Stone (stone ability one-shot: wedge the whole line at once) ----
ward_gid, ward_ids, _ = emit_char_root("WardStone", ["action"], (-3.6, 0, 17.2), (0, 0, 0), 1, [
    ("Socket", "M_Hall_Metal", CUBE, (0, 0.16, 0), (0.85, 0.32, 0.85)),
    ("Stone", "M_Seq_Stone", SPHERE, (0, 0.62, 0), (0.42, 0.42, 0.42)),
])
emit_monobehaviour(ward_ids["action"], ward_gid, REG["WorldActionInteractable.cs"],
    world_action_fields(
        "Wedge the Line with Stillness",
        [cond_yaml(11, "stone_ward")],
        [eff_yaml(5, "brace_count", amount=2)],
        "wedge_uses", 1, "",
        "\"A socket in the floor, waiting for an echo you do not carry.\"",
        "\"Stillness pours into the stone. The whole line settles at once.\"",
        "Ward Stone", 3.0, 22))

# ---- Rubble (spawned when the barricade falls; clear 0/2 to reopen) ----
rubble_gid, rubble_ids, _ = emit_char_root("Rubble", ["action"], (3.6, 0, 17.2), (0, 7, 0), 0, [
    ("Chunk_1", "M_Hall_Concrete", CUBE, (-0.7, 0.25, 0.1), (0.9, 0.5, 0.7)),
    ("Chunk_2", "M_Hall_Concrete", CUBE, (0.6, 0.20, -0.2), (1.1, 0.4, 0.6)),
    ("Chunk_3", "M_Hall_Metal", CUBE, (0.0, 0.55, 0.25), (0.5, 1.0, 0.3)),
])
emit_monobehaviour(rubble_ids["action"], rubble_gid, REG["WorldActionInteractable.cs"],
    world_action_fields(
        "Clear the Rubble",
        [cond_yaml(19, "hall", "barricade_fell")],
        [],
        "rubble_count", 2, "",
        "\"Splinters and dust. There is nothing to do here.\"",
        "\"You haul the splinters aside. The way opens.\"",
        "The Rubble", 3.2, 22))

# ---- NPC relocation: Sera takes the annex gate after the beacon falls quiet ----
loc_gid, loc_ids = emit_gameobject("Loc_Sera_AnnexGate", ["transform"])
emit_transform(loc_ids["transform"], loc_gid, (2.8, 0, 22.6), (0, 180, 0), (1, 1, 1))
reloc_gid, reloc_ids = emit_gameobject("NpcRelocator", ["transform", "relocator"])
emit_transform(reloc_ids["transform"], reloc_gid, (0, 0, 0), (0, 0, 0), (1, 1, 1))
emit_monobehaviour(reloc_ids["relocator"], reloc_gid, REG["NpcRelocator.cs"],
    "  bindings:\n"
    "  - npcId: sera\n"
    "    locationKey: annex_gate\n"
    "    target: {fileID: %d}\n"
    "    notice: Sera takes her watch by the annex gate.\n"
    "  toastOnLiveMove: 1" % loc_ids["transform"])

# ---- world-state applier: replays persisted consequences on every load ----
# ---- world-state applier: replays persisted consequences on every load ----
gid, ids = emit_gameobject("StoryWorldState", ["transform", "worldstate"])
emit_transform(ids["transform"], gid, (0, 0, 0), (0, 0, 0), (1, 1, 1))
emit_monobehaviour(ids["worldstate"], gid, REG["StoryWorldState.cs"],
    "  entities:\n"
    "  - key: ember_marker\n    target: {fileID: %d}\n    defaultActive: 0\n"
    "  - key: tide_marker\n    target: {fileID: %d}\n    defaultActive: 0\n"
    "  - key: stone_marker\n    target: {fileID: %d}\n    defaultActive: 0\n"
    "  - key: tide_bystanders\n    target: {fileID: %d}\n    defaultActive: 0\n"
    "  - key: echo_shard\n    target: {fileID: %d}\n    defaultActive: 1\n"
    "  - key: choir_beacon\n    target: {fileID: %d}\n    defaultActive: 1\n"
    "  - key: ember_cache\n    target: {fileID: %d}\n    defaultActive: 0\n"
    "  - key: keepsake_crate\n    target: {fileID: %d}\n    defaultActive: 1\n"
    "  - key: barricade\n    target: {fileID: %d}\n    defaultActive: 1\n"
    "  - key: barricade_rubble\n    target: {fileID: %d}\n    defaultActive: 0\n"
    "  - key: tide_calm\n    target: {fileID: %d}\n    defaultActive: 0\n"
    "  areaVariants: []" % (m_ember, m_tide, m_stone, by_gid, shard_gid,
                           beacon_gid, cache_gid, crate_gid, barricade_gid, rubble_gid, calm_gid))
root_gids.append(gid)

# ---- SceneRoots (root order, Unity 6) ----
roots_txt = "\n".join("  - {fileID: %d}" % g for g in root_gids)
add_block("""--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
%s""" % roots_txt)

scene = "\n".join(out + blocks) + "\n"
open(os.path.join(SCN, "FirstLocation.unity"), "w").write(scene)
write_meta_if_missing(os.path.join(SCN, "FirstLocation.unity"), NATIVE, REG["FirstLocation.unity"])
json.dump(REG, open(REG_PATH, "w"), indent=1)
print("scene objects:", len(LAY['pieces']) + 3 + 4 + 7, "| fileID high:", fid[0])
print("SCENE GENERATED")
