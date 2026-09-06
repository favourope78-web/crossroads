#!/usr/bin/env python3
"""Unity-side character assets for the canonical cast (release pass, PRIORITY 1).

Consumes the FBX/PNG files written by build_character_atlases.py + blender_build_characters.py and
writes, deterministically and idempotently:

  * <Char>.fbx.meta           ModelImporter, animationType 3 (HUMANOID), avatar auto-mapped from the
                              Mixamo-named skeleton; materials remapped to the .mat files below
  * M_<Char>.mat, M_<Char>_Hair.mat (+ M_<Char>_Metal for Dax)  URP/Lit, albedo atlas / flat colour
  * _Animations/Anim_*.fbx.meta  HUMANOID clips (no avatar of their own -> copied from the source
                              rig), loop flags per clip, so EVERY character (incl. Ari) retargets
                              the same library
  * _Animations/Character_Controller.controller  shared Animator: Speed float (Idle/Walk/Run blend
                              tree), Talking bool, Attack/Dodge/Hit/Defeat/Alert triggers
  * Prefabs/Characters/<Char>.prefab  prefab variant of the model with the shared controller,
                              Animator culling = CullUpdateTransforms, used by NpcAgent.avatarPrefab /
                              EnemyAgent.avatarPrefab through the scene generator
  * Ari: Ari.fbx.meta is switched to HUMANOID as well, the Ari_Controller keeps its states but
    its motions now point at the shared humanoid library (retargeted), and the legacy
    Ari_Idle/Walk/Turn FBX + procedural .anim clips are removed (superseded).

GUID scheme (registry scripts/hall_guids.json, family c0a1fed2...04xx):
  0x400+i   <Char>_Albedo.png      (build_character_atlases.py)
  0x420+i   <Char>.fbx
  0x440+i   M_<Char>.mat
  0x460+i   M_<Char>_Hair.mat
  0x480+i   M_<Char>_Metal.mat
  0x4a0+i   <Char>.prefab
  0x4c0+j   Anim_<Clip>.fbx
  0x4e0     Character_Controller.controller
"""
import json, os, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters")
PREFABS = os.path.join(ROOT, "Assets", "_Project", "Prefabs", "Characters")
REG_PATH = os.path.join(HERE, "hall_guids.json")
REG = json.load(open(REG_PATH))
URP_LIT = "933532a4fcc9baf4fa0491de14d08ed7"

CHARACTERS = ["Ari", "Mara", "Mara_Dress", "Dax", "Archivist", "Kael", "Odalys", "Bran", "Sera", "Civilian",
              "Soldier_A", "Soldier_B", "Soldier_C"]
CLIPS = {  # name: (loop, root-motion-y baked?)  -- all in-place
    "Anim_Idle": True, "Anim_Walk": True, "Anim_Run": True, "Anim_Talk": True,
    "Anim_Alert": False, "Anim_Attack": False, "Anim_Hit": False, "Anim_Dodge": False, "Anim_Defeat": False,
}
# flat material colours (linear-ish sRGB floats) for hair / metal slots (albedo covers the body)
HAIR_RGB = {"Ari": (0.078, 0.086, 0.102), "Mara": (0.114, 0.102, 0.110), "Mara_Dress": (0.114, 0.102, 0.110),
            "Dax": (0.290, 0.208, 0.153), "Archivist": (0.910, 0.945, 0.965), "Kael": (0.36, 0.36, 0.37),
            "Odalys": (0.80, 0.81, 0.83), "Bran": (0.62, 0.53, 0.47), "Sera": (0.33, 0.21, 0.16),
            "Civilian": (0.09, 0.094, 0.10), "Soldier_A": (0.09, 0.094, 0.10), "Soldier_B": (0.62, 0.53, 0.47),
            "Soldier_C": (0.10, 0.10, 0.11)}
EMISSIVE = {"Archivist": (0.30, 0.60, 0.70)}   # REF-05 glows (cyan-white skin, gem line)
SMOOTH = {"Archivist": 0.62, "Soldier_C": 0.5, "Soldier_B": 0.42}


def g32(n):
    return ("c0a1fed2" + ("%024x" % n))[:32]


def ensure(key, value):
    if key in REG and REG[key] != value:
        raise SystemExit("GUID conflict for %s: %s vs %s" % (key, REG[key], value))
    REG[key] = value
    return value


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path) and open(path).read() == text:
        return False
    open(path, "w").write(text)
    return True


# ---------------------------------------------------------------- model importer (humanoid)
def model_meta(guid, name, materials, humanoid=True, clips=None, avatar_source=None):
    """clips: list of (clipName, takeName, first, last, loop, internalID)."""
    ext = ""
    if materials:
        ext = "  externalObjects:\n" + "".join(
            "  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: %s\n    second: {fileID: 2100000, guid: %s, type: 2}\n" % (m, mg)
            for m, mg in materials)
    else:
        ext = "  externalObjects: {}\n"
    clip_yaml = "  clipAnimations: []\n"
    if clips:
        clip_yaml = "  clipAnimations:\n" + "".join("""  - serializedVersion: 16
    name: %s
    takeName: %s
    internalID: %d
    firstFrame: %d
    lastFrame: %d
    wrapMode: 0
    orientationOffsetY: 0
    level: 0
    cycleOffset: 0
    loop: 0
    hasAdditiveReferencePose: 0
    loopTime: %d
    loopBlend: 0
    loopBlendOrientation: 1
    loopBlendPositionY: 1
    loopBlendPositionXZ: 1
    keepOriginalOrientation: 1
    keepOriginalPositionY: 1
    keepOriginalPositionXZ: 1
    heightFromFeet: 0
    mirror: 0
    bodyMask: 01000000010000000100000001000000010000000100000001000000010000000100000001000000010000000100000001000000
    legStretch: 0.05
    armStretch: 0.05
    rootMotionBoneName: 
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
    isReadable: 0
""" % (cn, tn, iid, f0, f1, 1 if loop else 0) for (cn, tn, f0, f1, loop, iid) in clips)
    avatar_src = "{instanceID: 0}" if avatar_source is None else ("{fileID: 9000000, guid: %s, type: 3}" % avatar_source)
    id_table = "  internalIDToNameTable: []\n"
    if clips:
        id_table = "  internalIDToNameTable:\n" + "".join("  - first:\n      74: %d\n    second: %s\n" % (iid, cn) for (cn, tn, f0, f1, loop, iid) in clips)
    return """fileFormatVersion: 2
guid: %s
ModelImporter:
  serializedVersion: 22200
%s%s  materials:
    materialImportMode: %d
    materialName: 0
    materialSearch: 1
    materialLocation: 0
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
%s  meshes:
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
  importAnimation: %d
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
  lastHumanDescriptionAvatarSource: %s
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: %d
  humanoidOversampling: 1
  avatarSetup: %d
  addHumanoidMeta: 1
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""" % (guid, id_table, ext, 1 if materials else 0, clip_yaml, 1 if clips else 0, avatar_src,
       3 if humanoid else 2, 2 if avatar_source else 1)


# ---------------------------------------------------------------- materials
NATIVE = """fileFormatVersion: 2
guid: %s
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: %d
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def material(name, base_tex_guid, color, smooth, emissive=None):
    keywords = "  m_ValidKeywords:\n  - _EMISSION\n" if emissive else "  m_ValidKeywords: []\n"
    em = emissive or (0, 0, 0)
    tex = "{fileID: 2800000, guid: %s, type: 3}" % base_tex_guid if base_tex_guid else "{fileID: 0}"
    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %s
  m_Shader: {fileID: 4800000, guid: %s, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
%s  m_InvalidKeywords: []
  m_LightmapFlags: %d
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: %s
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _BumpMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _EmissionMap:
        m_Texture: %s
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _Cull: 2
    - _Cutoff: 0.5
    - _DstBlend: 0
    - _EnvironmentReflections: 1
    - _GlossinessSource: 0
    - _Metallic: 0
    - _OcclusionStrength: 1
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: %s
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {r: %s, g: %s, b: %s, a: 1}
    - _EmissionColor: {r: %s, g: %s, b: %s, a: 1}
    - _SpecColor: {r: 0.2, g: 0.2, b: 0.2, a: 1}
  m_BuildTextureStacks: []
""" % (name, URP_LIT, keywords, 2 if emissive else 4, tex, tex if emissive else "{fileID: 0}", smooth,
       color[0], color[1], color[2], em[0], em[1], em[2])


# ---------------------------------------------------------------- shared humanoid controller
def controller(clip_guids):
    """Speed float -> 1D blend tree Idle/Walk/Run; Talking bool -> Talk; triggers -> one-shots."""
    def motion(clip):
        return "{fileID: 7400000, guid: %s, type: 3}" % clip_guids[clip]
    params = "".join("  - m_Name: %s\n    m_Type: %d\n    m_DefaultFloat: 0\n    m_DefaultInt: 0\n    m_DefaultBool: 0\n    m_Controller: {fileID: 9100000}\n" % (n, t)
                     for n, t in (("Speed", 1), ("Talking", 4), ("Turning", 4), ("Attack", 9), ("Dodge", 9), ("Hit", 9), ("Defeat", 9), ("Alert", 9)))
    S = {"Locomotion": 110200000, "Talk": 110200002, "Attack": 110200010, "Dodge": 110200012, "Hit": 110200014,
         "Defeat": 110200016, "Alert": 110200018}
    ANY = {"Attack": 110100020, "Dodge": 110100022, "Hit": 110100024, "Defeat": 110100026, "Alert": 110100028}
    BACK = {"Attack": 110100030, "Dodge": 110100032, "Hit": 110100034, "Alert": 110100038}
    T_LOCO_TALK, T_TALK_LOCO = 110100040, 110100042
    BLEND = 110600000

    def state(sid, name, motion_ref, transitions, speed=1):
        return """--- !u!1102 &%d
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %s
  m_Speed: %s
  m_CycleOffset: 0
  m_Transitions:%s
  m_StateMachineBehaviours: []
  m_Position: {x: 50, y: 50, z: 0}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: %s
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
""" % (sid, name, speed, ("\n" + "".join("  - {fileID: %d}\n" % t for t in transitions).rstrip("\n")) if transitions else " []", motion_ref)

    def transition(tid, dst, conditions, duration, exit_time, has_exit, self_ok=0):
        cond = "".join("  - m_ConditionMode: %d\n    m_ConditionEvent: %s\n    m_EventTreshold: %s\n" % c for c in conditions)
        return """--- !u!1101 &%d
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions:%s
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: %d}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: %s
  m_TransitionOffset: 0
  m_ExitTime: %s
  m_HasExitTime: %d
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: %d
""" % (tid, ("\n" + cond.rstrip("\n")) if conditions else " []", dst, duration, exit_time, 1 if has_exit else 0, self_ok)

    blocks = []
    blocks.append("""--- !u!206 &%d
BlendTree:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Blend Tree
  m_Childs:
  - serializedVersion: 2
    m_Motion: %s
    m_Threshold: 0
    m_Position: {x: 0, y: 0}
    m_TimeScale: 1
    m_CycleOffset: 0
    m_DirectBlendParameter: Speed
    m_Mirror: 0
  - serializedVersion: 2
    m_Motion: %s
    m_Threshold: 0.5
    m_Position: {x: 0, y: 0}
    m_TimeScale: 1
    m_CycleOffset: 0
    m_DirectBlendParameter: Speed
    m_Mirror: 0
  - serializedVersion: 2
    m_Motion: %s
    m_Threshold: 1
    m_Position: {x: 0, y: 0}
    m_TimeScale: 1
    m_CycleOffset: 0
    m_DirectBlendParameter: Speed
    m_Mirror: 0
  m_BlendParameter: Speed
  m_BlendParameterY: Speed
  m_MinThreshold: 0
  m_MaxThreshold: 1
  m_UseAutomaticThresholds: 0
  m_NormalizedBlendValues: 0
  m_BlendType: 0
""" % (BLEND, motion("Anim_Idle"), motion("Anim_Walk"), motion("Anim_Run")))
    blocks.append(state(S["Locomotion"], "Locomotion", "{fileID: %d}" % BLEND, [T_LOCO_TALK]))
    blocks.append(state(S["Talk"], "Talk", motion("Anim_Talk"), [T_TALK_LOCO]))
    for name in ("Attack", "Dodge", "Hit", "Alert"):
        blocks.append(state(S[name], name, motion("Anim_" + name), [BACK[name]]))
    blocks.append(state(S["Defeat"], "Defeat", motion("Anim_Defeat"), []))
    blocks.append(transition(T_LOCO_TALK, S["Talk"], [(1, "Talking", 0)], 0.2, 0, False))
    blocks.append(transition(T_TALK_LOCO, S["Locomotion"], [(2, "Talking", 0)], 0.25, 0, False))
    for name in ("Attack", "Dodge", "Hit", "Alert"):
        blocks.append(transition(ANY[name], S[name], [(1, name, 0)], 0.05, 0, False, self_ok=1 if name in ("Attack", "Hit") else 0))
        blocks.append(transition(BACK[name], S["Locomotion"], [], 0.15, 0.9, True))
    blocks.append(transition(ANY["Defeat"], S["Defeat"], [(1, "Defeat", 0)], 0.05, 0, False))
    child = "".join("  - m_State: {fileID: %d}\n    m_Position: {x: %d, y: %d, z: 0}\n" % (sid, 300 + 260 * (i // 4), 40 + 90 * (i % 4))
                    for i, sid in enumerate(S.values()))
    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Character_Controller
  serializedVersion: 5
  m_AnimatorParameters:
%s  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {fileID: 110700000}
    m_Mask: {fileID: 0}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {fileID: 9100000}
--- !u!1107 &110700000
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Base Layer
  m_ChildStates:
%s  m_ChildStateMachines: []
  m_AnyStateTransitions:
%s  m_EntryTransitions: []
  m_StateMachineTransitions: {}
  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}
  m_DefaultState: {fileID: %d}
%s""" % (params, child, "".join("  - {fileID: %d}\n" % a for a in ANY.values()), S["Locomotion"], "".join(blocks))


# ---------------------------------------------------------------- prefab (model variant + controller)
# gen-2 (fileIdsGeneration 2) ids are deterministic per node name/hierarchy; the model root ids are
# the same for every FBX authored by the Blender build (root node = the armature object):
ROOT_T = -8679921383154817045
ROOT_GO = 919132149155446097
ROOT_ANIMATOR = 5866666021909216657


def prefab(name, fbx_guid, controller_guid):
    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!1001 &100100000
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 0}
    m_Modifications:
    - target: {fileID: %d, guid: %s, type: 3}
      propertyPath: m_Name
      value: %s
      objectReference: {fileID: 0}
    - target: {fileID: %d, guid: %s, type: 3}
      propertyPath: m_Controller
      value: 
      objectReference: {fileID: 9100000, guid: %s, type: 2}
    - target: {fileID: %d, guid: %s, type: 3}
      propertyPath: m_ApplyRootMotion
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: %d, guid: %s, type: 3}
      propertyPath: m_CullingMode
      value: 1
      objectReference: {fileID: 0}
    - target: {fileID: %d, guid: %s, type: 3}
      propertyPath: m_UpdateMode
      value: 0
      objectReference: {fileID: 0}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: %s, type: 3}
""" % (ROOT_GO, fbx_guid, name, ROOT_ANIMATOR, fbx_guid, controller_guid, ROOT_ANIMATOR, fbx_guid,
       ROOT_ANIMATOR, fbx_guid, ROOT_ANIMATOR, fbx_guid, fbx_guid)


def main():
    changed = 0
    # ---- animation library
    clip_guids = {}
    anim_dir = os.path.join(ART, "_Animations")
    for j, clip in enumerate(CLIPS):
        g = ensure(clip + ".fbx", g32(0x4c0 + j))
        clip_guids[clip] = g
        fbx = os.path.join(anim_dir, clip + ".fbx")
        if not os.path.exists(fbx):
            raise SystemExit("missing %s - run blender_build_characters.py" % fbx)
        # frame range = what Blender baked (frame_start..frame_end); the meta clip covers it all
        rng = CLIP_RANGES[clip]
        changed += write(fbx + ".meta", model_meta(g, clip, [], humanoid=True,
                                                    clips=[(clip, "Dax_Rig|Scene", rng[0], rng[1], CLIPS[clip], 7400000)],
                                                    avatar_source=None))
    ctrl_guid = ensure("Character_Controller.controller", g32(0x4e0))
    changed += write(os.path.join(anim_dir, "Character_Controller.controller"), controller(clip_guids))
    changed += write(os.path.join(anim_dir, "Character_Controller.controller.meta"), NATIVE % (ctrl_guid, 9100000))

    # ---- characters
    for i, name in enumerate(CHARACTERS):
        d = os.path.join(ART, name)
        fbx = os.path.join(d, name + ".fbx")
        if not os.path.exists(fbx):
            raise SystemExit("missing %s - run blender_build_characters.py" % fbx)
        if name == "Ari":
            fbx_guid, tex_guid = "c0a1fed0000000000000000000000002", "c0a1fed0000000000000000000000001"
            body_guid, hair_guid = "c0a1fed0000000000000000000000006", "c0a1fed0000000000000000000000007"
            metal_guid = None
        else:
            fbx_guid = ensure(name + ".fbx", g32(0x420 + i))
            tex_guid = REG.get(name + "_Albedo.png") or ensure(name + "_Albedo.png", g32(0x400 + i))
            body_guid = ensure("M_%s.mat" % name, g32(0x440 + i))
            hair_guid = ensure("M_%s_Hair.mat" % name, g32(0x460 + i))
            metal_guid = ensure("M_%s_Metal.mat" % name, g32(0x480 + i)) if name == "Dax" else None
        # materials (Ari's M_Ari / M_Ari_Hair already exist with the approved settings - keep them)
        if name != "Ari":
            em = EMISSIVE.get(name)
            changed += write(os.path.join(d, "M_%s.mat" % name), material("M_" + name, tex_guid, (1, 1, 1), SMOOTH.get(name, 0.35), em))
            changed += write(os.path.join(d, "M_%s.mat.meta" % name), NATIVE % (body_guid, 2100000))
            hc = HAIR_RGB[name]
            changed += write(os.path.join(d, "M_%s_Hair.mat" % name), material("M_%s_Hair" % name, None, hc, 0.45, (0.2, 0.22, 0.24) if name == "Archivist" else None))
            changed += write(os.path.join(d, "M_%s_Hair.mat.meta" % name), NATIVE % (hair_guid, 2100000))
            if metal_guid:
                changed += write(os.path.join(d, "M_%s_Metal.mat" % name), material("M_%s_Metal" % name, None, (0.72, 0.75, 0.78), 0.8))
                changed += write(os.path.join(d, "M_%s_Metal.mat.meta" % name), NATIVE % (metal_guid, 2100000))
        mats = [("M_" + name, body_guid), ("M_%s_Hair" % name, hair_guid)]
        if metal_guid: mats.append(("M_%s_Metal" % name, metal_guid))
        changed += write(fbx + ".meta", model_meta(fbx_guid, name, mats, humanoid=True))
        # prefab
        pg = ensure(name + ".prefab", g32(0x4a0 + i))
        changed += write(os.path.join(PREFABS, name + ".prefab"), prefab(name, fbx_guid, ctrl_guid))
        changed += write(os.path.join(PREFABS, name + ".prefab.meta"), """fileFormatVersion: 2
guid: %s
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""" % pg)

    # ---- Ari controller: keep the state graph + trigger names PlayerCombatController fires, but
    # every motion now comes from the shared humanoid library (retargets onto Ari's humanoid avatar)
    ctrl_path = os.path.join(ART, "Ari", "Ari_Controller.controller")
    changed += write(ctrl_path, ari_controller(clip_guids))
    # legacy generic-rig clips are superseded (procedural euler .anim + per-clip FBX takes)
    for legacy in ("Ari_Idle.fbx", "Ari_Walk.fbx", "Ari_Turn.fbx", "Ari_Attack.anim", "Ari_Dodge.anim", "Ari_Hit.anim", "Ari_Defeat.anim"):
        p = os.path.join(ART, "Ari", legacy)
        for q in (p, p + ".meta"):
            if os.path.exists(q):
                os.remove(q); changed += 1
    json.dump(REG, open(REG_PATH, "w"), indent=1)
    print("character assets: %d files written/updated, %d characters, %d clips" % (changed, len(CHARACTERS), len(CLIPS)))


def ari_controller(clip_guids):
    """Ari keeps Ari_Controller (referenced by the scene PrefabInstance + validator) - same graph
    as Character_Controller so the hero and the cast share one animation vocabulary."""
    txt = controller(clip_guids)
    return txt.replace("m_Name: Character_Controller", "m_Name: Ari_Controller")


CLIP_RANGES = {"Anim_Idle": (1, 60), "Anim_Walk": (1, 36), "Anim_Run": (1, 24), "Anim_Talk": (1, 72),
               "Anim_Alert": (1, 18), "Anim_Attack": (1, 22), "Anim_Hit": (1, 14), "Anim_Dodge": (1, 16), "Anim_Defeat": (1, 40)}

if __name__ == "__main__":
    main()
