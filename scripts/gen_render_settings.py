#!/usr/bin/env python3
"""Production-polish pass: authoritative render pipeline + quality tiers + post volume.

The project shipped URP 17 in Packages/manifest.json and 41 URP/Lit materials, but NO
UniversalRenderPipelineAsset existed and nothing was assigned in GraphicsSettings /
QualitySettings - i.e. a device build would fall back to the Built-in pipeline and every
URP material renders magenta. This generator writes, deterministically (fixed GUIDs so the
scene/registry can reference them):

  Assets/Settings/URP_Renderer_Mobile.asset     UniversalRendererData (forward, no extra features)
  Assets/Settings/URP_Low.asset                 tier 0: 30 fps floor phones (no shadows, 0.8 render scale)
  Assets/Settings/URP_Balanced.asset            tier 1: mid-range target (soft-off shadows 20 m, 1024 map)
  Assets/Settings/URP_High.asset                tier 2: flagship (shadows 35 m, 2048 map, MSAA 2x)
  Assets/Settings/PostProcess_Global.asset      VolumeProfile: bloom (emissive Fracture light), vignette,
                                                colour grading (matches CHARACTER_REFERENCE dusk grade)
  ProjectSettings/GraphicsSettings.asset        default pipeline = Balanced, SRP batcher on
  ProjectSettings/ProjectSettings.asset         QualitySettings tiers Low/Balanced/High each bound to its asset

Tier values follow GAME_DESIGN §14 (60 fps target Balanced, 30 floor Low) and DEVELOPMENT_PLAN
0.5 (HDR off, MSAA 2x on the top tier only, render-scale hook).
Idempotent: re-running rewrites the same bytes.
"""
import os, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SET = os.path.join(ROOT, "Assets/Settings")
os.makedirs(SET, exist_ok=True)

# URP package script GUIDs (stable across URP versions; verified against Unity's public
# UniversalRenderingExamples repo + package sources)
URP_ASSET_SCRIPT = "bf2edee5c58d82540a51f03df9d42094"       # UniversalRenderPipelineAsset
URP_RENDERER_SCRIPT = "de640fe3d0db1804a85f9fc8f5cadab6"    # UniversalRendererData
POSTPROCESS_DATA = "41439944d30ece34e96484bdb6645b55"       # PostProcessData (package asset)
VOLUME_PROFILE_SCRIPT = "d7fd9488000d3734a9e00ee676215985"  # VolumeProfile (core)
BLOOM = "0b2db86121404754db890f4c8dfe81b2"
VIGNETTE = "899c54efeace73346a0a16faa3afe726"
COLOR_ADJ = "66f335fb1ffd8684294ad653bf1c7564"
TONEMAP = "97c23e3b12dc18c42a140437e53d3951"

# project GUIDs (registry range 0x0200.. reserved for settings)
def g32(n): return ("c0a1fed2" + ("%024x" % n))[:32]
GUID_RENDERER = g32(0x200)
GUID_LOW, GUID_BAL, GUID_HIGH = g32(0x201), g32(0x202), g32(0x203)
GUID_VOLUME = g32(0x204)
GUID_VOLUME_LOW = g32(0x205)   # Low tier: no bloom (saves the 4-iteration downsample chain), lighter vignette

META = """fileFormatVersion: 2
guid: %s
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def write(path, txt, guid):
    open(path, "w", encoding="utf-8", newline="\n").write(txt)
    open(path + ".meta", "w", encoding="utf-8", newline="\n").write(META % guid)

RENDERER = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(script)s, type: 3}
  m_Name: URP_Renderer_Mobile
  m_EditorClassIdentifier: 
  debugShaders:
    debugReplacementPS: {fileID: 0}
    hdrDebugViewPS: {fileID: 0}
    probeVolumeSamplingDebugComputeShader: {fileID: 0}
  m_RendererFeatures: []
  m_RendererFeatureMap: 
  m_UseNativeRenderPass: 1
  postProcessData: {fileID: 11400000, guid: %(ppd)s, type: 2}
  m_AssetVersion: 2
  m_OpaqueLayerMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_TransparentLayerMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_DefaultStencilState:
    overrideStencilState: 0
    stencilReference: 0
    stencilCompareFunction: 8
    passOperation: 0
    failOperation: 0
    zFailOperation: 0
  m_ShadowTransparentReceive: 1
  m_RenderingMode: 0
  m_DepthPrimingMode: 0
  m_CopyDepthMode: 1
  m_DepthAttachmentFormat: 0
  m_DepthTextureFormat: 0
  m_AccurateGbufferNormals: 0
  m_IntermediateTextureMode: 1
""" % {"script": URP_RENDERER_SCRIPT, "ppd": POSTPROCESS_DATA}

def urp_asset(name, shadows, shadow_dist, shadow_res, msaa, render_scale, soft, cascades, additional_per_vertex, lod_crossfade):
    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(script)s, type: 3}
  m_Name: %(name)s
  m_EditorClassIdentifier: 
  k_AssetVersion: 12
  k_AssetPreviousVersion: 12
  m_RendererType: 1
  m_RendererData: {fileID: 0}
  m_RendererDataList:
  - {fileID: 11400000, guid: %(renderer)s, type: 2}
  m_DefaultRendererIndex: 0
  m_RequireDepthTexture: 0
  m_RequireOpaqueTexture: 0
  m_OpaqueDownsampling: 1
  m_SupportsTerrainHoles: 0
  m_SupportsHDR: 0
  m_HDRColorBufferPrecision: 0
  m_MSAA: %(msaa)d
  m_RenderScale: %(scale)s
  m_UpscalingFilter: 0
  m_FsrOverrideSharpness: 0
  m_FsrSharpness: 0.92
  m_EnableLODCrossFade: %(lodfade)d
  m_LODCrossFadeDitheringType: 1
  m_ShEvalMode: 0
  m_LightProbeSystem: 0
  m_ProbeVolumeMemoryBudget: 512
  m_ProbeVolumeBlendingMemoryBudget: 128
  m_SupportProbeVolumeGPUStreaming: 0
  m_SupportProbeVolumeDiskStreaming: 0
  m_SupportProbeVolumeScenarios: 0
  m_SupportProbeVolumeScenarioBlending: 0
  m_ProbeVolumeSHBands: 1
  m_MainLightRenderingMode: 1
  m_MainLightShadowsSupported: %(shadows)d
  m_MainLightShadowmapResolution: %(shadowres)d
  m_AdditionalLightsRenderingMode: %(addl)d
  m_AdditionalLightsPerObjectLimit: 2
  m_AdditionalLightShadowsSupported: 0
  m_AdditionalLightsShadowmapResolution: 512
  m_AdditionalLightsShadowResolutionTierLow: 128
  m_AdditionalLightsShadowResolutionTierMedium: 256
  m_AdditionalLightsShadowResolutionTierHigh: 512
  m_ReflectionProbeBlending: 0
  m_ReflectionProbeBoxProjection: 0
  m_ReflectionProbeAtlas: 0
  m_ShadowDistance: %(shadowdist)s
  m_ShadowCascadeCount: %(cascades)d
  m_Cascade2Split: 0.25
  m_Cascade3Split: {x: 0.1, y: 0.3}
  m_Cascade4Split: {x: 0.067, y: 0.2, z: 0.467}
  m_CascadeBorder: 0.2
  m_ShadowDepthBias: 1
  m_ShadowNormalBias: 1
  m_AnyShadowsSupported: %(shadows)d
  m_SoftShadowsSupported: %(soft)d
  m_ConservativeEnclosingSphere: 1
  m_NumIterationsEnclosingSphere: 64
  m_SoftShadowQuality: 1
  m_AdditionalLightsCookieResolution: 1024
  m_AdditionalLightsCookieFormat: 3
  m_UseSRPBatcher: 1
  m_SupportsDynamicBatching: 0
  m_MixedLightingSupported: 0
  m_SupportsLightCookies: 0
  m_SupportsLightLayers: 0
  m_DebugLevel: 0
  m_StoreActionsOptimization: 1
  m_EnableRenderGraph: 0
  m_UseAdaptivePerformance: 1
  m_ColorGradingMode: 0
  m_ColorGradingLutSize: 16
  m_AllowPostProcessAlphaOutput: 0
  m_UseFastSRGBLinearConversion: 1
  m_SupportDataDrivenLensFlare: 0
  m_SupportScreenSpaceLensFlare: 0
  m_GPUResidentDrawerMode: 0
  m_SmallMeshScreenPercentage: 0
  m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0
  m_ShadowType: 1
  m_LocalShadowsSupported: 0
  m_LocalShadowsAtlasResolution: 256
  m_MaxPixelLights: 0
  m_ShadowAtlasResolution: 256
  m_VolumeFrameworkUpdateMode: 1
  m_VolumeProfile: {fileID: 11400000, guid: %(volume)s, type: 2}
  m_PrefilteringModeMainLightShadows: %(pf_main)d
  m_PrefilteringModeAdditionalLight: 1
  m_PrefilteringModeAdditionalLightShadows: 0
  m_PrefilterXRKeywords: 1
  m_PrefilteringModeForwardPlus: 0
  m_PrefilteringModeDeferredRendering: 0
  m_PrefilteringModeScreenSpaceOcclusion: 0
  m_PrefilterDebugKeywords: 1
  m_PrefilterWriteRenderingLayers: 1
  m_PrefilterHDROutput: 1
  m_PrefilterSSAODepthNormals: 1
  m_PrefilterSSAOSourceDepthLow: 1
  m_PrefilterSSAOSourceDepthMedium: 1
  m_PrefilterSSAOSourceDepthHigh: 1
  m_PrefilterSSAOInterleaved: 1
  m_PrefilterSSAOBlueNoise: 1
  m_PrefilterSSAOSampleCountLow: 1
  m_PrefilterSSAOSampleCountMedium: 1
  m_PrefilterSSAOSampleCountHigh: 1
  m_PrefilterDBufferMRT1: 1
  m_PrefilterDBufferMRT2: 1
  m_PrefilterDBufferMRT3: 1
  m_PrefilterSoftShadowsQualityLow: %(pf_softlow)d
  m_PrefilterSoftShadowsQualityMedium: 1
  m_PrefilterSoftShadowsQualityHigh: 1
  m_PrefilterSoftShadows: 0
  m_PrefilterScreenCoord: 1
  m_PrefilterNativeRenderPass: 0
  m_PrefilterUseLegacyLightmaps: 1
  m_ShaderVariantLogLevel: 0
  m_ShadowCascades: 0
""" % {"script": URP_ASSET_SCRIPT, "name": name, "renderer": GUID_RENDERER, "msaa": msaa, "scale": render_scale,
       "lodfade": lod_crossfade, "shadows": 1 if shadows else 0, "shadowres": shadow_res, "addl": additional_per_vertex,
       "shadowdist": shadow_dist, "cascades": cascades, "soft": 1 if soft else 0, "volume": GUID_VOLUME,
       "pf_main": 0 if shadows else 1, "pf_softlow": 0 if soft else 1}

# ---- post-process volume profile: dusk grade from CHARACTER_REFERENCE §2 (warm skin, cool Fracture cyan, crushed shadows)
VOLUME = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-8000000000000000001
MonoBehaviour:
  m_ObjectHideFlags: 3
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(bloom)s, type: 3}
  m_Name: Bloom
  m_EditorClassIdentifier: 
  active: 1
  skipIterations:
    m_OverrideState: 1
    m_Value: 2
  threshold:
    m_OverrideState: 1
    m_Value: 1.05
  intensity:
    m_OverrideState: 1
    m_Value: 0.55
  scatter:
    m_OverrideState: 1
    m_Value: 0.65
  clamp:
    m_OverrideState: 0
    m_Value: 65472
  tint:
    m_OverrideState: 1
    m_Value: {r: 0.86, g: 0.96, b: 1, a: 1}
  highQualityFiltering:
    m_OverrideState: 1
    m_Value: 0
  downscale:
    m_OverrideState: 1
    m_Value: 1
  maxIterations:
    m_OverrideState: 1
    m_Value: 4
  dirtTexture:
    m_OverrideState: 0
    m_Value: {fileID: 0}
    dimension: 1
  dirtIntensity:
    m_OverrideState: 0
    m_Value: 0
--- !u!114 &-8000000000000000002
MonoBehaviour:
  m_ObjectHideFlags: 3
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(vignette)s, type: 3}
  m_Name: Vignette
  m_EditorClassIdentifier: 
  active: 1
  color:
    m_OverrideState: 1
    m_Value: {r: 0.05, g: 0.03, b: 0.06, a: 1}
  center:
    m_OverrideState: 0
    m_Value: {x: 0.5, y: 0.5}
  intensity:
    m_OverrideState: 1
    m_Value: 0.28
  smoothness:
    m_OverrideState: 1
    m_Value: 0.45
  rounded:
    m_OverrideState: 0
    m_Value: 0
--- !u!114 &-8000000000000000003
MonoBehaviour:
  m_ObjectHideFlags: 3
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(coloradj)s, type: 3}
  m_Name: ColorAdjustments
  m_EditorClassIdentifier: 
  active: 1
  postExposure:
    m_OverrideState: 1
    m_Value: 0.15
  contrast:
    m_OverrideState: 1
    m_Value: 12
  colorFilter:
    m_OverrideState: 1
    m_Value: {r: 1, g: 0.97, b: 0.95, a: 1}
  hueShift:
    m_OverrideState: 0
    m_Value: 0
  saturation:
    m_OverrideState: 1
    m_Value: 8
--- !u!114 &-8000000000000000004
MonoBehaviour:
  m_ObjectHideFlags: 3
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(tonemap)s, type: 3}
  m_Name: Tonemapping
  m_EditorClassIdentifier: 
  active: 1
  mode:
    m_OverrideState: 1
    m_Value: 1
  neutralHDRRangeReductionMode:
    m_OverrideState: 0
    m_Value: 2
  acesPreset:
    m_OverrideState: 0
    m_Value: 3
  hueShiftAmount:
    m_OverrideState: 0
    m_Value: 0
  detectPaperWhite:
    m_OverrideState: 0
    m_Value: 0
  paperWhite:
    m_OverrideState: 0
    m_Value: 300
  detectBrightnessLimits:
    m_OverrideState: 0
    m_Value: 1
  minNits:
    m_OverrideState: 0
    m_Value: 0.005
  maxNits:
    m_OverrideState: 0
    m_Value: 1000
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(profile)s, type: 3}
  m_Name: PostProcess_Global
  m_EditorClassIdentifier: 
  components:
  - {fileID: -8000000000000000001}
  - {fileID: -8000000000000000002}
  - {fileID: -8000000000000000003}
  - {fileID: -8000000000000000004}
""" % {"bloom": BLOOM, "vignette": VIGNETTE, "coloradj": COLOR_ADJ, "tonemap": TONEMAP, "profile": VOLUME_PROFILE_SCRIPT}

GRAPHICS = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!30 &1
GraphicsSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 15
  m_Deferred:
    m_Mode: 1
    m_Shader: {fileID: 69, guid: 0000000000000000f000000000000000, type: 0}
  m_DeferredReflections:
    m_Mode: 1
    m_Shader: {fileID: 74, guid: 0000000000000000f000000000000000, type: 0}
  m_ScreenSpaceShadows:
    m_Mode: 1
    m_Shader: {fileID: 64, guid: 0000000000000000f000000000000000, type: 0}
  m_DepthNormals:
    m_Mode: 1
    m_Shader: {fileID: 62, guid: 0000000000000000f000000000000000, type: 0}
  m_MotionVectors:
    m_Mode: 1
    m_Shader: {fileID: 75, guid: 0000000000000000f000000000000000, type: 0}
  m_LightHalo:
    m_Mode: 1
    m_Shader: {fileID: 105, guid: 0000000000000000f000000000000000, type: 0}
  m_LensFlare:
    m_Mode: 1
    m_Shader: {fileID: 102, guid: 0000000000000000f000000000000000, type: 0}
  m_VideoShadersIncludeMode: 0
  m_AlwaysIncludedShaders:
  - {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15104, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15105, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15106, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10753, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10770, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 4800000, guid: 650dd9526735d5b46b79224bc6e94025, type: 3}
  - {fileID: 4800000, guid: 0406db5a14f94604a8c57ccfbc9f3b46, type: 3}
  m_PreloadedShaders: []
  m_PreloadShadersBatchTimeLimit: -1
  m_SpritesDefaultMaterial: {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}
  m_CustomRenderPipeline: {fileID: 11400000, guid: %(bal)s, type: 2}
  m_TransparencySortMode: 0
  m_TransparencySortAxis: {x: 0, y: 0, z: 1}
  m_DefaultRenderingPath: 1
  m_DefaultMobileRenderingPath: 1
  m_TierSettings: []
  m_LightmapStripping: 1
  m_FogStripping: 1
  m_InstancingStripping: 0
  m_BrgStripping: 0
  m_LightmapKeepPlain: 0
  m_LightmapKeepDirCombined: 0
  m_LightmapKeepDynamicPlain: 0
  m_LightmapKeepDynamicDirCombined: 0
  m_LightmapKeepShadowMask: 0
  m_LightmapKeepSubtractive: 0
  m_FogKeepLinear: 0
  m_FogKeepExp: 0
  m_FogKeepExp2: 1
  m_AlbedoSwatchInfos: []
  m_RenderPipelineGlobalSettingsMap: {}
  m_LightsUseLinearIntensity: 1
  m_LightsUseColorTemperature: 0
  m_LogWhenShaderIsCompiled: 0
  m_LightProbeOutsideHullStrategy: 0
  m_CameraRelativeLightCulling: 0
  m_CameraRelativeShadowCulling: 0
""" % {"bal": GUID_BAL}

def tier(name, shadows, shadow_res_enum, shadow_dist, aniso, lod_bias, mip_limit, particle_budget, skin, asset_guid, target_fps):
    # shadowResolution enum: 0 Low(256) 1 Medium(512) 2 High(1024) 3 VeryHigh(2048)
    return """  - serializedVersion: 4
    name: %(name)s
    pixelLightCount: 1
    shadows: %(shadows)d
    shadowResolution: %(sres)d
    shadowProjection: 1
    shadowCascades: 1
    shadowDistance: %(sdist)s
    shadowNearPlaneOffset: 2
    shadowCascade2Split: 0.33333334
    shadowCascade4Split: {x: 0.06666667, y: 0.2, z: 0.46666667}
    shadowmaskMode: 0
    skinWeights: %(skin)d
    globalTextureMipmapLimit: %(mip)d
    textureMipmapLimitSettings: []
    anisotropicTextures: %(aniso)d
    antiAliasing: 0
    softParticles: 0
    softVegetation: 0
    realtimeReflectionProbes: 0
    billboardsFaceCameraPosition: 0
    adaptiveVsync: 0
    useLegacyDetailDistribution: 0
    vSyncCount: 0
    realtimeGICPUUsage: 25
    adaptiveVsyncType: 0
    lodBias: %(lod)s
    maximumLODLevel: 0
    enableLODCrossFade: 1
    streamingMipmapsActive: 0
    streamingMipmapsAddAllCameras: 1
    streamingMipmapsMemoryBudget: 256
    streamingMipmapsRenderersPerFrame: 512
    streamingMipmapsMaxLevelReduction: 2
    streamingMipmapsMaxFileIORequests: 1024
    particleRaycastBudget: %(pbudget)d
    asyncUploadTimeSlice: 2
    asyncUploadBufferSize: 16
    asyncUploadPersistentBuffer: 1
    resolutionScalingFixedDPIFactor: 1
    terrainQualityOverrides: 0
    terrainPixelError: 1
    terrainDetailDensityScale: 1
    terrainBasemapDistance: 1000
    terrainDetailDistance: 80
    terrainTreeDistance: 5000
    terrainBillboardStart: 50
    terrainFadeLength: 5
    terrainMaxTrees: 50
    customRenderPipeline: {fileID: 11400000, guid: %(guid)s, type: 2}
    excludedTargetPlatforms: []
""" % {"name": name, "shadows": shadows, "sres": shadow_res_enum, "sdist": shadow_dist, "skin": skin, "mip": mip_limit,
       "aniso": aniso, "lod": lod_bias, "pbudget": particle_budget, "guid": asset_guid}

QUALITY = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!47 &1
QualitySettings:
  m_ObjectHideFlags: 0
  serializedVersion: 5
  m_CurrentQuality: 1
  m_QualitySettings:
%(low)s%(bal)s%(high)s  m_TextureMipmapLimitGroupNames: []
  m_PerPlatformDefaultQuality:
    Android: 1
    Standalone: 2
    iPhone: 1
""" % {"low": tier("Low", 0, 0, 0, 0, 0.7, 1, 4, 1, GUID_LOW, 30),
       "bal": tier("Balanced", 1, 2, 20, 1, 1.0, 0, 16, 2, GUID_BAL, 60),
       "high": tier("High", 1, 3, 35, 1, 1.2, 0, 32, 4, GUID_HIGH, 60)}


def volume_low():
    """Low-tier profile (release pass P3): same colour grade / tonemap so the dusk look holds, but no
    Bloom component at all (bloom is the one screen-space pass that scales with resolution on a
    2019-class GPU) and a cheaper vignette. QualityTierApplier swaps the global Volume to this
    profile when QualitySettings level 0 is active."""
    txt = VOLUME.replace("m_Name: PostProcess_Global", "m_Name: PostProcess_Low")
    # drop the Bloom component block (first component, fileID ...0001)
    start = txt.index("--- !u!114 &-8000000000000000001")
    end = txt.index("--- !u!114 &-8000000000000000002")
    txt = txt[:start] + txt[end:]
    txt = txt.replace("  - {fileID: -8000000000000000001}\n", "")
    # lighter vignette on Low (intensity 0.28 -> 0.20) - it is a full-screen multiply either way
    txt = txt.replace("  intensity:\n    m_OverrideState: 1\n    m_Value: 0.28", "  intensity:\n    m_OverrideState: 1\n    m_Value: 0.2", 1)
    assert "m_Name: Bloom" not in txt
    return txt


def main():
    write(os.path.join(SET, "URP_Renderer_Mobile.asset"), RENDERER, GUID_RENDERER)
    # Low: 30 fps floor phones - no shadows, per-vertex additional lights, render scale 0.8
    write(os.path.join(SET, "URP_Low.asset"), urp_asset("URP_Low", False, 0, 256, 1, "0.8", False, 1, 2, 0), GUID_LOW)
    # Balanced: mid-range target - hard shadows 20 m, 1024 map, single cascade, full scale
    write(os.path.join(SET, "URP_Balanced.asset"), urp_asset("URP_Balanced", True, 20, 1024, 1, "1", False, 1, 1, 1), GUID_BAL)
    # High: flagship - shadows 35 m, 2048 map, 2 cascades, MSAA 2x, soft shadows
    write(os.path.join(SET, "URP_High.asset"), urp_asset("URP_High", True, 35, 2048, 2, "1", True, 2, 1, 1), GUID_HIGH)
    write(os.path.join(SET, "PostProcess_Global.asset"), VOLUME, GUID_VOLUME)
    write(os.path.join(SET, "PostProcess_Low.asset"), volume_low(), GUID_VOLUME_LOW)
    open(os.path.join(ROOT, "ProjectSettings/GraphicsSettings.asset"), "w", encoding="utf-8", newline="\n").write(GRAPHICS)

    # Unity keeps one settings object per file: ProjectSettings.asset = PlayerSettings (!u!129) only,
    # QualitySettings.asset = !u!47, DynamicsManager.asset = PhysicsManager (!u!55). Earlier passes
    # appended QualitySettings/PhysicsManager docs to ProjectSettings.asset under wrong class ids
    # (!u!19 / !u!40) which the editor would silently discard - split them out (release pass P3).
    ps_path = os.path.join(ROOT, "ProjectSettings/ProjectSettings.asset")
    ps = open(ps_path, encoding="utf-8").read()
    docs = re.split(r"(?=^--- !u!)", ps, flags=re.M)
    header, bodies = docs[0], docs[1:]
    keep, physics = [], None
    for d in bodies:
        if d.startswith("--- !u!129 "):
            keep.append(d)
        elif "\nPhysicsManager:" in d:
            physics = re.sub(r"^--- !u!\d+ &1", "--- !u!55 &1", d.rstrip("\n") + "\n")
        # QualitySettings docs are dropped here and rewritten to their own file below
    open(ps_path, "w", encoding="utf-8", newline="\n").write(header + "".join(k.rstrip("\n") + "\n" for k in keep))
    open(os.path.join(ROOT, "ProjectSettings/QualitySettings.asset"), "w", encoding="utf-8", newline="\n").write(QUALITY)
    dyn_path = os.path.join(ROOT, "ProjectSettings/DynamicsManager.asset")
    if physics is not None:
        open(dyn_path, "w", encoding="utf-8", newline="\n").write("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + physics)

    # registry entries so validate_assets.py resolves the new GUIDs
    reg_path = os.path.join(ROOT, "scripts/hall_guids.json")
    import json
    reg = json.load(open(reg_path))
    for k, v in (("URP_Renderer_Mobile.asset", GUID_RENDERER), ("URP_Low.asset", GUID_LOW), ("URP_Balanced.asset", GUID_BAL),
                 ("URP_High.asset", GUID_HIGH), ("PostProcess_Global.asset", GUID_VOLUME),
                 ("PostProcess_Low.asset", GUID_VOLUME_LOW)):
        if k in reg and reg[k] != v:
            raise SystemExit("GUID conflict for %s" % k)
        reg[k] = v
    json.dump(reg, open(reg_path, "w"), indent=1)
    print("[RENDER] wrote URP renderer + 3 tier assets + post volume; GraphicsSettings/QualitySettings bound (default Balanced)")


if __name__ == "__main__":
    main()
