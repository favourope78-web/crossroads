#!/usr/bin/env python3
"""Missing animations for the playable experience (GAME_DESIGN §6.1 base kit): Attack, Dodge,
Hit, Defeat for the CANONICAL Ari rig (Assets/_Project/Art/Characters/Ari - the only character
model; same bone names blender_build_ari.py authored: Ari_Rig/Hips/Spine/Spine1/Spine2/...).

Writes procedural, loop-free AnimationClip assets (euler rotation curves on the generic rig,
runtime m_EulerCurves + editor curves + binding constants) and rewrites Ari_Controller.controller
with four Trigger parameters and Any-State transitions back to Idle. PlayerCombatController fires
the triggers; NPC/enemy primitives keep their material/sink feedback (no rig yet - placeholder by
design, see CAMPAIGN_CONTENT_REPORT.md).

Deterministic GUIDs: c0a1fed0...10..13 (the Ari asset family). Idempotent.
"""
import os, math, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ARI = os.path.join(ROOT, "Assets/_Project/Art/Characters/Ari")
RIG = "Ari_Rig"
P = {n: RIG + "/" + p for n, p in {
    "Hips": "Hips", "Spine": "Hips/Spine", "Spine1": "Hips/Spine/Spine1", "Spine2": "Hips/Spine/Spine1/Spine2",
    "Neck": "Hips/Spine/Spine1/Spine2/Neck", "Head": "Hips/Spine/Spine1/Spine2/Neck/Head",
    "LeftShoulder": "Hips/Spine/Spine1/Spine2/LeftShoulder", "LeftArm": "Hips/Spine/Spine1/Spine2/LeftShoulder/LeftArm",
    "LeftForeArm": "Hips/Spine/Spine1/Spine2/LeftShoulder/LeftArm/LeftForeArm",
    "RightShoulder": "Hips/Spine/Spine1/Spine2/RightShoulder", "RightArm": "Hips/Spine/Spine1/Spine2/RightShoulder/RightArm",
    "RightForeArm": "Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm",
    "LeftUpLeg": "Hips/LeftUpLeg", "LeftLeg": "Hips/LeftUpLeg/LeftLeg", "RightUpLeg": "Hips/RightUpLeg", "RightLeg": "Hips/RightUpLeg/RightLeg",
}.items()}

def crc(s): return zlib.crc32(s.encode()) & 0xffffffff

def key(t, v):
    return ("      - serializedVersion: 3\n        time: %s\n        value: {x: %s, y: %s, z: %s}\n        inSlope: {x: 0, y: 0, z: 0}\n        outSlope: {x: 0, y: 0, z: 0}\n"
            "        tangentMode: 0\n        weightedMode: 0\n        inWeight: {x: 0.33333334, y: 0.33333334, z: 0.33333334}\n        outWeight: {x: 0.33333334, y: 0.33333334, z: 0.33333334}"
            % (round(t, 4), round(v[0], 3), round(v[1], 3), round(v[2], 3)))

def fkey(t, v):
    return ("      - serializedVersion: 3\n        time: %s\n        value: %s\n        inSlope: 0\n        outSlope: 0\n        tangentMode: 136\n        weightedMode: 0\n        inWeight: 0.33333334\n        outWeight: 0.33333334" % (round(t, 4), round(v, 3)))

def clip(name, length, tracks):
    """tracks = {bone: [(t, (x, y, z)), ...]} euler degrees, local, additive on the rest pose."""
    euler, editor, bindings = [], [], []
    for bone, keys in tracks.items():
        path = P[bone]
        euler.append("  - curve:\n      serializedVersion: 2\n      m_Curve:\n" + "\n".join(key(t, v) for t, v in keys) +
                     "\n      m_PreInfinity: 2\n      m_PostInfinity: 2\n      m_RotationOrder: 4\n    path: " + path)
        bindings.append("    - serializedVersion: 2\n      path: %d\n      attribute: 4\n      script: {fileID: 0}\n      typeID: 4\n      customType: 4\n      isPPtrCurve: 0\n      isIntCurve: 0\n      isSerializeReferenceCurve: 0" % crc(path))
        for i, axis in enumerate("xyz"):
            editor.append("  - curve:\n      serializedVersion: 2\n      m_Curve:\n" + "\n".join(fkey(t, v[i]) for t, v in keys) +
                          "\n      m_PreInfinity: 2\n      m_PostInfinity: 2\n      m_RotationOrder: 4\n    attribute: localEulerAnglesRaw.%s\n    path: %s\n    classID: 4\n    script: {fileID: 0}\n    flags: 16" % (axis, path))
    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %s
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves:
%s
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves: []
  m_SampleRate: 30
  m_WrapMode: 0
  m_Bounds:
    m_Center: {x: 0, y: 0, z: 0}
    m_Extent: {x: 0, y: 0, z: 0}
  m_ClipBindingConstant:
    genericBindings:
%s
    pptrCurveMapping: []
  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {fileID: 0}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: %s
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: 0
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves:
%s
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
""" % (name, "\n".join(euler), "\n".join(bindings), length, "\n".join(editor))

Z = (0, 0, 0)
CLIPS = {
    # light attack: wind-up, cross-body swing with the right arm, recover (0.45 s = basic attack windup+cooldown feel)
    "Ari_Attack": (0.45, {
        "Spine2": [(0, Z), (0.12, (0, -28, 0)), (0.24, (8, 32, 0)), (0.45, Z)],
        "Spine1": [(0, Z), (0.12, (0, -12, 0)), (0.24, (4, 14, 0)), (0.45, Z)],
        "RightShoulder": [(0, Z), (0.12, (-10, 0, 20)), (0.24, (-20, 0, -30)), (0.45, Z)],
        "RightArm": [(0, Z), (0.12, (-70, 0, 10)), (0.24, (-95, 0, -45)), (0.45, Z)],
        "RightForeArm": [(0, Z), (0.12, (-70, 0, 0)), (0.24, (-15, 0, 0)), (0.45, Z)],
        "LeftArm": [(0, Z), (0.12, (-20, 0, 0)), (0.24, (-35, 0, 15)), (0.45, Z)],
        "Head": [(0, Z), (0.24, (0, 12, 0)), (0.45, Z)],
        "LeftUpLeg": [(0, Z), (0.24, (-12, 0, 0)), (0.45, Z)],
        "RightUpLeg": [(0, Z), (0.24, (14, 0, 0)), (0.45, Z)],
    }),
    # dodge: 0.28 s dash - crouch, lean forward, arms back, recover
    "Ari_Dodge": (0.35, {
        "Hips": [(0, Z), (0.08, (26, 0, 0)), (0.22, (18, 0, 0)), (0.35, Z)],
        "Spine1": [(0, Z), (0.08, (16, 0, 0)), (0.35, Z)],
        "Spine2": [(0, Z), (0.08, (12, 0, 0)), (0.35, Z)],
        "LeftUpLeg": [(0, Z), (0.08, (-40, 0, 0)), (0.22, (-30, 0, 0)), (0.35, Z)],
        "RightUpLeg": [(0, Z), (0.08, (-36, 0, 0)), (0.22, (-24, 0, 0)), (0.35, Z)],
        "LeftLeg": [(0, Z), (0.08, (60, 0, 0)), (0.22, (44, 0, 0)), (0.35, Z)],
        "RightLeg": [(0, Z), (0.08, (56, 0, 0)), (0.22, (40, 0, 0)), (0.35, Z)],
        "LeftArm": [(0, Z), (0.08, (30, 0, 10)), (0.35, Z)],
        "RightArm": [(0, Z), (0.08, (30, 0, -10)), (0.35, Z)],
    }),
    # hit reaction: short flinch back and to the side
    "Ari_Hit": (0.28, {
        "Spine2": [(0, Z), (0.06, (-18, 0, 10)), (0.16, (-10, 0, 4)), (0.28, Z)],
        "Spine1": [(0, Z), (0.06, (-8, 0, 4)), (0.28, Z)],
        "Head": [(0, Z), (0.06, (-14, -10, 0)), (0.28, Z)],
        "LeftArm": [(0, Z), (0.06, (-30, 0, 20)), (0.28, Z)],
        "RightArm": [(0, Z), (0.06, (-30, 0, -20)), (0.28, Z)],
    }),
    # defeat: fold forward to the knees (the controller revives at the checkpoint afterwards)
    "Ari_Defeat": (0.9, {
        "Hips": [(0, Z), (0.35, (30, 0, 0)), (0.9, (48, 0, 6))],
        "Spine1": [(0, Z), (0.35, (22, 0, 0)), (0.9, (40, 0, 0))],
        "Spine2": [(0, Z), (0.35, (18, 0, 0)), (0.9, (30, 0, 0))],
        "Head": [(0, Z), (0.35, (20, 0, 0)), (0.9, (38, 0, 0))],
        "LeftUpLeg": [(0, Z), (0.35, (-50, 0, 0)), (0.9, (-88, 0, 0))],
        "RightUpLeg": [(0, Z), (0.35, (-50, 0, 0)), (0.9, (-84, 0, 0))],
        "LeftLeg": [(0, Z), (0.35, (70, 0, 0)), (0.9, (120, 0, 0))],
        "RightLeg": [(0, Z), (0.35, (70, 0, 0)), (0.9, (118, 0, 0))],
        "LeftArm": [(0, Z), (0.9, (-20, 0, 30))],
        "RightArm": [(0, Z), (0.9, (-20, 0, -30))],
    }),
}
GUIDS = {"Ari_Attack": "c0a1fed0000000000000000000000010", "Ari_Dodge": "c0a1fed0000000000000000000000011",
         "Ari_Hit": "c0a1fed0000000000000000000000012", "Ari_Defeat": "c0a1fed0000000000000000000000013"}
NATIVE = "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 7400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
for name, (length, tracks) in CLIPS.items():
    open(os.path.join(ARI, name + ".anim"), "w").write(clip(name, length, tracks))
    meta = os.path.join(ARI, name + ".anim.meta")
    if not os.path.exists(meta): open(meta, "w").write(NATIVE % GUIDS[name])
    print("anim +", name, "(%ss, %d bones)" % (length, len(tracks)))

# ---------------------------------------------------------------- controller: triggers + Any State transitions
ctrl_path = os.path.join(ARI, "Ari_Controller.controller")
ctrl = open(ctrl_path).read()
if "m_Name: Attack" not in ctrl:
    # parameters (Trigger = type 9)
    params = "".join("  - m_Name: %s\n    m_Type: 9\n    m_DefaultFloat: 0\n    m_DefaultInt: 0\n    m_DefaultBool: 0\n    m_Controller: {fileID: 9100000}\n" % t
                     for t in ("Attack", "Dodge", "Hit", "Defeat"))
    ctrl = ctrl.replace("  m_AnimatorLayers:\n", params + "  m_AnimatorLayers:\n", 1)
    # states 110200010.. + any-state transitions 110100020.. + return transitions 110100030..
    state_ids = {"Attack": 110200010, "Dodge": 110200012, "Hit": 110200014, "Defeat": 110200016}
    any_ids = {"Attack": 110100020, "Dodge": 110100022, "Hit": 110100024, "Defeat": 110100026}
    back_ids = {"Attack": 110100030, "Dodge": 110100032, "Hit": 110100034, "Defeat": 110100036}
    blocks = []
    for t, sid in state_ids.items():
        blocks.append("""--- !u!1102 &%d
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %s
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {fileID: %d}
  m_StateMachineBehaviours: []
  m_Position: {x: 50, y: 50, z: 0}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {fileID: 7400000, guid: %s, type: 2}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: """ % (sid, t, back_ids[t], GUIDS["Ari_" + t]))
        blocks.append("""--- !u!1101 &%d
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: %s
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: %d}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: %d""" % (any_ids[t], t, sid, 1 if t in ("Attack", "Hit") else 0))
        blocks.append("""--- !u!1101 &%d
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions: []
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 110200000}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.15
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 1
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1""" % (back_ids[t]))
    child = "".join("  - m_State: {fileID: %d}\n    m_Position: {x: 820, y: %d, z: 0}\n" % (sid, 40 + i * 90) for i, sid in enumerate(state_ids.values()))
    ctrl = ctrl.replace("  m_ChildStateMachines: []\n  m_AnyStateTransitions: []\n",
                        "  m_ChildStateMachines: []\n  m_AnyStateTransitions:\n" + "".join("  - {fileID: %d}\n" % a for a in any_ids.values()), 1)
    ctrl = ctrl.replace("  m_ChildStates:\n", "  m_ChildStates:\n" + child, 1)
    ctrl = ctrl.rstrip("\n") + "\n" + "\n".join(blocks) + "\n"
    open(ctrl_path, "w").write(ctrl)
    print("controller + Attack/Dodge/Hit/Defeat triggers")
else:
    print("controller already carries the combat states")
