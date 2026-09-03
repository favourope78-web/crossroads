"""Generates Assets/Scenes/Prototype/FirstLocation.unity (+ .meta) from
scripts/hall_layout.json, plus kit material .mat files and all .meta files
for the Fracture Hall prototype. Deterministic GUID scheme c0a1fed2...."""
import json, os, re

ROOT = "/home/user"
LAY = json.load(open(f"{ROOT}/scripts/hall_layout.json"))
ENV = f"{ROOT}/Assets/Game/Environment"
SCN = f"{ROOT}/Assets/Scenes/Prototype"
os.makedirs(SCN, exist_ok=True)

G = {}
kit = ["SM_FloorTile","SM_Column","SM_LightBeam","SM_WallPanel","SM_GlazingPanel","SM_BalconyBlock",
       "SM_Railing","SM_Truss","SM_DoorFrame","SM_Door","SM_OrbCore","SM_OrbRing","SM_HoloPanel"]
for i, k in enumerate(kit, 1): G[k] = f"c0a1fed2{'0'*19}{i:03x}"[:32] if False else f"c0a1fed2{'0'*20}{i:04x}"[:32]
# ensure exactly 32 hex
def g32(tag, i): return ("c0a1fed2" + f"{i:024x}")[:32]
for i, k in enumerate(kit, 1): G[k] = g32("kit", i)
mats = ["M_Hall_Concrete","M_Hall_Metal","M_Hall_LightColumn","M_Hall_Glazing","M_Hall_OrbGold","M_Hall_Holo"]
for i, m in enumerate(mats, 40): G[m] = g32("mat", i)
scripts = {"ThirdPersonCameraController.cs": 80, "Interactable.cs": 81, "DoorInteractable.cs": 82,
           "InteractInput.cs": 83, "FirstLocationBootstrap.cs": 84}
for s, i in scripts.items(): G[s] = g32("scr", i)
G["FirstLocation.unity"] = g32("scn", 90)

URP = "9335e4a172916944ba2695448482493a"
def mat_yaml(name, color, smooth, emiss=None, transparent=False):
    kw = []
    if emiss: kw.append("_EMISSION")
    if transparent: kw.append("_SURFACE_TYPE_TRANSPARENT")
    ec = emiss or (0,0,0)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {URP}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: [{', '.join(kw)}]
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {'3000' if transparent else '-1'}
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: {'1' if transparent else '0'}
    - _Cull: 2
    - _Cutoff: 0.5
    - _DstBlend: {'10' if transparent else '0'}
    - _EnvironmentReflections: 1
    - _GlossinessSource: 0
    - _Metallic: 0
    - _OcclusionStrength: 1
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: {smooth}
    - _SpecularHighlights: 1
    - _SrcBlend: {'5' if transparent else '1'}
    - _Surface: {'1' if transparent else '0'}
    - _WorkflowMode: 1
    - _ZWrite: {'0' if transparent else '1'}
    m_Colors:
    - _BaseColor: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}
    - _EmissionColor: {{r: {ec[0]}, g: {ec[1]}, b: {ec[2]}, a: 1}}
    - _SpecColor: {{r: 0.2, g: 0.2, b: 0.2, a: 1}}
  m_BuildTextureStacks: []
"""
MATDEFS = {
 "M_Hall_Concrete":  ((0.42,0.42,0.45), 0.25, None, False),
 "M_Hall_Metal":     ((0.18,0.19,0.22), 0.45, None, False),
 "M_Hall_LightColumn":((0.40,0.85,0.91), 0.5, (0.40,0.85,0.91), False),
 "M_Hall_Glazing":   ((0.72,0.42,0.40), 0.6, (0.55,0.28,0.26), False),
 "M_Hall_OrbGold":   ((0.91,0.72,0.29), 0.7, (0.60,0.42,0.12), False),
 "M_Hall_Holo":      ((0.40,0.85,0.91), 0.5, (0.30,0.70,0.80), True),
}
SLOT2MAT = {"Concrete":"M_Hall_Concrete","Metal":"M_Hall_Metal","LightColumn":"M_Hall_LightColumn",
            "Glazing":"M_Hall_Glazing","OrbGold":"M_Hall_OrbGold","Holo":"M_Hall_Holo"}
for m,(c,s,e,t) in MATDEFS.items():
    open(f"{ENV}/Materials/{m}.mat".replace("/Materials/", "/Materials/"), "w") if False else None
os.makedirs(f"{ENV}/Materials", exist_ok=True)
for m,(c,s,e,t) in MATDEFS.items():
    open(f"{ENV}/Materials/{m}.mat","w").write(mat_yaml(m,c,s,e,t))

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
MODEL = """fileFormatVersion: 2
guid: {g}
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable: []
  externalObjects: {{}}
  materials:
    materialImportMode: 0
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateClassPlugin: 0
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    removeConstantScaleCurves: 0
    motionNodeName: 
    rigImportErrors: 
    rigImportWarnings: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 0
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useSRGBMaterialColor: 1
    sortHierarchyByName: 1
    importPhysicalCameras: 1
    importVisibility: 0
    importBlendShapes: 0
    importCameras: 0
    importLights: 0
    nodeNameCollisionStrategy: 1
    fileIdsGeneration: 2
    swapUVChannels: 0
    generateSecondaryUV: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 0
    skinWeightsMode: 0
    maxBonesPerVertex: 4
    minBoneWeight: 0.001
    optimizeBones: 1
    meshOptimizationFlags: -1
    indexFormat: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
    useFileScale: 1
    strictVertexDataChecks: 0
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 0
  humanDescription:
    serializedVersion: 3
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    globalScale: 1
    rootMotionBoneName: 
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {{instanceID: 0}}
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: 0
  humanoidOversampling: 1
  avatarSetup: 0
  addHumanoidMeta: 0
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
for k in kit:
    open(f"{ENV}/Kit/{k}.fbx.meta","w").write(MODEL.format(g=G[k]))
for m in mats:
    open(f"{ENV}/Materials/{m}.mat.meta","w").write(NATIVE.format(g=G[m]))
for s in scripts:
    open(f"{ROOT}/Assets/Game/Scripts/{s}.meta","w").write(MONO.format(g=G[s]))

# ---------------- scene ----------------
COLLIDERS = {  # piece -> (type, size, center)
 "SM_FloorTile": ("box",(10,0.12,10),(0,0,0)),
 "SM_Column": ("box",(1.15,9.0,1.15),(0,4.5,0)),
 "SM_WallPanel": ("box",(10,6,0.55),(0,3,0)),
 "SM_BalconyBlock": ("box",(10,0.45,3.0),(0,0,0)),
 "SM_Railing": ("box",(10,1.25,0.12),(0,0.55,0)),
 "SM_DoorFrame": ("box",(4.9,4.95,1.0),(0,2.45,0)),
 "SM_Door": ("box",(3.6,4.0,0.26),(0,2.0,0)),
}
DYNAMIC = {"SM_Door","SM_OrbCore","SM_OrbRing","SM_HoloPanel"}
out = []
out.append("%YAML 1.1")
out.append("%TAG !u! tag:unity3d.com,2011:")
fid = [1000]
def nid():
    fid[0] += 2
    return fid[0]
def go(name, static=True):
    g, t, tr = nid(), nid(), nid()
    return g, t, tr
blocks = []
def add_block(txt): blocks.append(txt)
# settings
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
    maxJumpAcrossHeight: 0
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

def emit_gameobject(name, comps, static):
    g = fid[0]+1
    ids = {}
    comp_lines = []
    for kind in comps:
        fid[0] += 2
        ids[kind] = fid[0]
        comp_lines.append(f"  - component: {{fileID: {ids[kind]}}}")
    add_block(f"""--- !u!1 &{g}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{chr(10).join(comp_lines)}
  m_Layer: 0
  m_Name: {name}
  m_Tag: Untagged""")
    return g, ids

def emit_transform(tid, gid, pos, rot_euler, scale, father=0):
    add_block(f"""--- !u!4 &{tid}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}
  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: {rot_euler[0]}, y: {rot_euler[1]}, z: {rot_euler[2]}}}""")

def emit_meshfilter(mid, gid, meshguid):
    add_block(f"""--- !u!33 &{mid}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Mesh: {{fileID: 4300000, guid: {meshguid}, type: 3}}""")

def emit_renderer(rid, gid, matguid):
    add_block(f"""--- !u!23 &{rid}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
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
  - {{fileID: 2100000, guid: {matguid}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
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
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {{fileID: 0}}""")

def emit_boxcollider(cid, gid, size, center):
    add_block(f"""--- !u!65 &{cid}
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Material: {{fileID: 0}}
  m_IncludeGestures: 0
  m_IsTrigger: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: {size[0]}, y: {size[1]}, z: {size[2]}}}
  m_Center: {{x: {center[0]}, y: {center[1]}, z: {center[2]}}}""")

def emit_spherecollider(cid, gid, radius, center=(0,0,0)):
    add_block(f"""--- !u!135 &{cid}
SphereCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Radius: {radius}
  m_Center: {{x: {center[0]}, y: {center[1]}, z: {center[2]}}}""")

def emit_monobehaviour(bid, gid, scriptguid):
    add_block(f"""--- !u!114 &{bid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {scriptguid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: """)

# --- pieces ---
import math
for idx, e in enumerate(LAY["pieces"]):
    piece = e["piece"]
    pos, yaw, scale = e["pos"], e["yaw"], e["scale"]
    comps = ["transform","meshfilter","renderer"]
    if piece in COLLIDERS: comps.append("collider")
    if e["interact"] == "Door": comps.append("door")
    if e["interact"] == "Inspect": comps.append("interact")
    gid, ids = emit_gameobject(f"{piece}_{idx:03d}", comps, piece not in DYNAMIC)
    emit_transform(ids["transform"], gid, pos, (0, yaw, 0), scale)
    emit_meshfilter(ids["meshfilter"], gid, G[piece])
    slot = {"SM_FloorTile":"Concrete","SM_Column":"Metal","SM_LightBeam":"LightColumn",
            "SM_WallPanel":"Concrete","SM_GlazingPanel":"Glazing","SM_BalconyBlock":"Concrete",
            "SM_Railing":"Metal","SM_Truss":"Metal","SM_DoorFrame":"Metal","SM_Door":"Metal",
            "SM_OrbCore":"OrbGold","SM_OrbRing":"OrbGold","SM_HoloPanel":"Holo"}[piece]
    emit_renderer(ids["renderer"], gid, G[SLOT2MAT[slot]])
    if "collider" in ids:
        c = COLLIDERS[piece]
        emit_boxcollider(ids["collider"], gid, c[1], c[2])
    if "door" in ids:
        emit_monobehaviour(ids["door"], gid, G["DoorInteractable.cs"])
    if "interact" in ids:
        emit_monobehaviour(ids["interact"], gid, G["Interactable.cs"])

# --- directional light ---
gid, ids = emit_gameobject("Directional Light", ["transform","light"], True)
emit_transform(ids["transform"], gid, (0,12,0), (55,-30,0), (1,1,1))
add_block(f"""--- !u!108 &{ids['light']}
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
  serializedVersion: 10
  m_Type: 1
  m_Shape: 0
  m_Color: {{r: 1, g: 0.93, b: 0.85, a: 1}}
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
  m_Cookie: {{fileID: 0}}
  m_DrawHalo: 0
  m_Flare: {{fileID: 0}}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {{x: 1, y: 1}}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {{x: 0, y: 0, z: 0, w: 0}}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ShadowRadius: 0
  m_ShadowAngle: 0""")

# --- main camera + follow ---
gid, ids = emit_gameobject("Main Camera", ["transform","camera","listener","follow"], True)
emit_transform(ids["transform"], gid, (0,2.6,-20.5), (12,0,0), (1,1,1))
add_block(f"""--- !u!20 &{ids['camera']}
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {{r: 0.30, g: 0.20, b: 0.19, a: 1}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {{x: 2, y: 11}}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {{x: 36, y: 24}}
  m_LensShift: {{x: 0, y: 0}}
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
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 0
  m_AllowMSAA: 0
  m_AllowDynamicResolution: 1
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022""")
add_block(f"""--- !u!81 &{ids['listener']}
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1""")
emit_monobehaviour(ids["follow"], gid, G["ThirdPersonCameraController.cs"])

# --- bootstrap ---
gid, ids = emit_gameobject("FirstLocationBootstrap", ["transform","bootstrap"], True)
emit_transform(ids["transform"], gid, tuple(LAY["spawn"]), (0,0,0), (1,1,1))
emit_monobehaviour(ids["bootstrap"], gid, G["FirstLocationBootstrap.cs"])

scene = "\n".join(out + blocks) + "\n"
open(f"{SCN}/FirstLocation.unity","w").write(scene)
open(f"{SCN}/FirstLocation.unity.meta","w").write(f"""fileFormatVersion: 2
guid: {G['FirstLocation.unity']}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""")
# folder metas
import hashlib
FOLDER = """fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
for d in ["Assets/Game","Assets/Game/Environment","Assets/Game/Environment/Kit","Assets/Game/Environment/Materials",
          "Assets/Game/Scripts","Assets/Scenes","Assets/Scenes/Prototype"]:
    meta = f"{ROOT}/{d}.meta"
    if not os.path.exists(meta):
        open(meta,"w").write(FOLDER.format(g=hashlib.md5(("folder:"+d).encode()).hexdigest()))
json.dump(G, open(f"{ROOT}/scripts/hall_guids.json","w"), indent=1)
print("scene objects:", len(LAY['pieces'])+3, "| fileID high:", fid[0])
print("SCENE GENERATED")
