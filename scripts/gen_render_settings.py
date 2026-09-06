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
  Assets/Settings/PostProcess_Global.asset      VolumeProfile (Balanced/High): bloom (emissive Fracture light),
                                                vignette, colour grading (matches CHARACTER_REFERENCE dusk grade)
  Assets/Settings/PostProcess_Low.asset         VolumeProfile (Low): NO bloom, light vignette + the same grade
                                                (QualityTierApplier swaps the scene volume's profile per tier)
  ProjectSettings/GraphicsSettings.asset        default pipeline = Balanced, SRP batcher on
  ProjectSettings/QualitySettings.asset         (!u!47) tiers Low/Balanced/High each bound to its URP asset
  ProjectSettings/DynamicsManager.asset         (!u!55) physics defaults (was mis-bundled in ProjectSettings.asset)

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



# ---------------------------------------------------------------------------------------------
# ProjectSettings.asset (PlayerSettings, !u!129). Written from the REAL Unity 6000.0.23f1 schema
# (cross-checked against an editor-serialised file of the same version). Two rules Unity's YAML
# reader enforces that the earlier hand-written file broke ("Unexpected scalar when reading
# mapping" x2 in the cloud-build log): scriptingBackend and il2cppCompilerConfiguration are
# per-platform MAPPINGS, not scalars. Unknown keys are silently dropped, so only real keys are
# emitted; everything Android-relevant is also enforced programmatically by
# Assets/Editor/AndroidDevBuild.Configure() at build time.
# ---------------------------------------------------------------------------------------------
PLAYER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 26
  productGUID: c0a1fed2000000000000000000000f01
  AndroidProfiler: 0
  AndroidFilterTouchesWhenObscured: 0
  AndroidEnableSustainedPerformanceMode: 0
  defaultScreenOrientation: 4
  targetDevice: 2
  useOnDemandResources: 0
  accelerometerFrequency: 60
  companyName: favourope78-web
  productName: CROSSROADS
  defaultCursor: {fileID: 0}
  cursorHotspot: {x: 0, y: 0}
  m_SplashScreenBackgroundColor: {r: 0.043, g: 0.051, b: 0.063, a: 1}
  m_ShowUnitySplashScreen: 1
  m_ShowUnitySplashLogo: 1
  m_SplashScreenOverlayOpacity: 1
  m_SplashScreenAnimation: 1
  m_SplashScreenLogoStyle: 1
  m_SplashScreenDrawMode: 0
  m_SplashScreenBackgroundAnimationZoom: 1
  m_SplashScreenLogoAnimationZoom: 1
  m_SplashScreenBackgroundLandscapeAspect: 1
  m_SplashScreenBackgroundPortraitAspect: 1
  m_SplashScreenBackgroundLandscapeUvs:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  m_SplashScreenBackgroundPortraitUvs:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  m_SplashScreenLogos: []
  m_VirtualRealitySplashScreen: {fileID: 0}
  m_HolographicTrackingLossScreen: {fileID: 0}
  defaultScreenWidth: 1920
  defaultScreenHeight: 1080
  defaultScreenWidthWeb: 960
  defaultScreenHeightWeb: 600
  m_StereoRenderingPath: 0
  m_ActiveColorSpace: 1
  m_SpriteBatchVertexThreshold: 300
  m_MTRendering: 1
  mipStripping: 0
  numberOfMipsStripped: 0
  numberOfMipsStrippedPerMipmapLimitGroup: {}
  m_StackTraceTypes: 010000000100000001000000010000000100000001000000
  iosShowActivityIndicatorOnLoading: -1
  androidShowActivityIndicatorOnLoading: -1
  iosUseCustomAppBackgroundBehavior: 0
  allowedAutorotateToPortrait: 0
  allowedAutorotateToPortraitUpsideDown: 0
  allowedAutorotateToLandscapeRight: 1
  allowedAutorotateToLandscapeLeft: 1
  useOSAutorotation: 1
  use32BitDisplayBuffer: 1
  preserveFramebufferAlpha: 0
  disableDepthAndStencilBuffers: 0
  androidStartInFullscreen: 1
  androidRenderOutsideSafeArea: 1
  androidUseSwappy: 1
  androidBlitType: 0
  androidResizableWindow: 0
  androidDefaultWindowWidth: 1920
  androidDefaultWindowHeight: 1080
  androidMinimumWindowWidth: 400
  androidMinimumWindowHeight: 300
  androidFullscreenMode: 1
  defaultIsNativeResolution: 1
  macRetinaSupport: 1
  runInBackground: 0
  captureSingleScreen: 0
  muteOtherAudioSources: 0
  Prepare IOS For Recording: 0
  Force IOS Speakers When Recording: 0
  deferSystemGesturesMode: 0
  hideHomeButton: 0
  submitAnalytics: 0
  usePlayerLog: 1
  bakeCollisionMeshes: 0
  forceSingleInstance: 0
  useFlipModelSwapchain: 1
  resizableWindow: 0
  useMacAppStoreValidation: 0
  macAppStoreCategory: public.app-category.games
  gpuSkinning: 1
  xboxPIXTextureCapture: 0
  xboxEnableAvatar: 0
  xboxEnableKinect: 0
  xboxEnableKinectAutoTracking: 0
  xboxEnableFitness: 0
  visibleInBackground: 1
  allowFullscreenSwitch: 1
  fullscreenMode: 1
  xboxSpeechDB: 0
  xboxEnableHeadOrientation: 0
  xboxEnableGuest: 0
  xboxEnablePIXSampling: 0
  metalFramebufferOnly: 0
  xboxOneResolution: 0
  xboxOneSResolution: 0
  xboxOneXResolution: 3
  xboxOneMonoLoggingLevel: 0
  xboxOneLoggingLevel: 1
  xboxOneDisableEsram: 0
  xboxOneEnableTypeOptimization: 0
  xboxOnePresentImmediateThreshold: 0
  switchQueueCommandMemory: 0
  switchQueueControlMemory: 16384
  switchQueueComputeMemory: 262144
  switchNVNShaderPoolsGranularity: 33554432
  switchNVNDefaultPoolsGranularity: 16777216
  switchNVNOtherPoolsGranularity: 16777216
  switchGpuScratchPoolGranularity: 2097152
  switchAllowGpuScratchShrinking: 0
  switchNVNMaxPublicTextureIDCount: 0
  switchNVNMaxPublicSamplerIDCount: 0
  switchNVNGraphicsFirmwareMemory: 32
  stadiaPresentMode: 0
  stadiaTargetFramerate: 0
  vulkanNumSwapchainBuffers: 3
  vulkanEnableSetSRGBWrite: 0
  vulkanEnablePreTransform: 1
  vulkanEnableLateAcquireNextImage: 0
  vulkanEnableCommandBufferRecycling: 1
  loadStoreDebugModeEnabled: 0
  bundleVersion: 0.1
  preloadedAssets: []
  metroInputSource: 0
  wsaTransparentSwapchain: 0
  m_HolographicPauseOnTrackingLoss: 1
  xboxOneDisableKinectGpuReservation: 1
  xboxOneEnable7thCore: 1
  vrSettings:
    enable360StereoCapture: 0
  isWsaHolographicRemotingEnabled: 0
  enableFrameTimingStats: 1
  enableOpenGLProfilerGPURecorders: 1
  useHDRDisplay: 0
  hdrBitDepth: 0
  m_ColorGamuts: 00000000
  targetPixelDensity: 30
  resolutionScalingMode: 0
  resetResolutionOnWindowResize: 0
  androidSupportedAspectRatio: 1
  androidMaxAspectRatio: 2.1
  applicationIdentifier:
    Android: com.favourope78.crossroads
    Standalone: com.favourope78.crossroads
  buildNumber:
    Standalone: 0
    iPhone: 0
    tvOS: 0
  overrideDefaultApplicationIdentifier: 1
  AndroidBundleVersionCode: 1
  AndroidMinSdkVersion: 24
  AndroidTargetSdkVersion: 0
  AndroidPreferredInstallLocation: 1
  aotOptions: 
  stripEngineCode: 1
  iPhoneStrippingLevel: 0
  iPhoneScriptCallOptimization: 0
  ForceInternetPermission: 0
  ForceSDCardPermission: 0
  CreateWallpaper: 0
  APKExpansionFiles: 0
  keepLoadedShadersAlive: 0
  StripUnusedMeshComponents: 1
  strictShaderVariantMatching: 0
  VertexChannelCompressionMask: 4054
  iPhoneSdkVersion: 988
  iOSTargetOSVersionString: 12.0
  tvOSSdkVersion: 0
  tvOSRequireExtendedGameController: 0
  tvOSTargetOSVersionString: 12.0
  VisionOSSdkVersion: 0
  VisionOSTargetOSVersionString: 1.0
  uIPrerenderedIcon: 0
  uIRequiresPersistentWiFi: 0
  uIRequiresFullScreen: 1
  uIStatusBarHidden: 1
  uIExitOnSuspend: 0
  uIStatusBarStyle: 0
  appleTVSplashScreen: {fileID: 0}
  appleTVSplashScreen2x: {fileID: 0}
  tvOSSmallIconLayers: []
  tvOSSmallIconLayers2x: []
  tvOSLargeIconLayers: []
  tvOSLargeIconLayers2x: []
  tvOSTopShelfImageLayers: []
  tvOSTopShelfImageLayers2x: []
  tvOSTopShelfImageWideLayers: []
  tvOSTopShelfImageWideLayers2x: []
  iOSLaunchScreenType: 0
  iOSLaunchScreenPortrait: {fileID: 0}
  iOSLaunchScreenLandscape: {fileID: 0}
  iOSLaunchScreenBackgroundColor:
    serializedVersion: 2
    rgba: 0
  iOSLaunchScreenFillPct: 100
  iOSLaunchScreenSize: 100
  iOSLaunchScreenCustomXibPath: 
  iOSLaunchScreeniPadType: 0
  iOSLaunchScreeniPadImage: {fileID: 0}
  iOSLaunchScreeniPadBackgroundColor:
    serializedVersion: 2
    rgba: 0
  iOSLaunchScreeniPadFillPct: 100
  iOSLaunchScreeniPadSize: 100
  iOSLaunchScreeniPadCustomXibPath: 
  iOSLaunchScreenCustomStoryboardPath: 
  iOSLaunchScreeniPadCustomStoryboardPath: 
  iOSDeviceRequirements: []
  iOSURLSchemes: []
  macOSURLSchemes: []
  iOSBackgroundModes: 0
  iOSMetalForceHardShadows: 0
  metalEditorSupport: 1
  metalAPIValidation: 1
  iOSRenderExtraFrameOnPause: 0
  iosCopyPluginsCodeInsteadOfSymlink: 0
  appleDeveloperTeamID: 
  iOSManualSigningProvisioningProfileID: 
  tvOSManualSigningProvisioningProfileID: 
  VisionOSManualSigningProvisioningProfileID: 
  iOSManualSigningProvisioningProfileType: 0
  tvOSManualSigningProvisioningProfileType: 0
  VisionOSManualSigningProvisioningProfileType: 0
  appleEnableAutomaticSigning: 0
  iOSRequireARKit: 0
  iOSAutomaticallyDetectAndAddCapabilities: 1
  appleEnableProMotion: 0
  shaderPrecisionModel: 0
  clonedFromGUID: 00000000000000000000000000000000
  templatePackageId: 
  templateDefaultScene: Assets/Scenes/Prototype/FirstLocation.unity
  useCustomMainManifest: 0
  useCustomLauncherManifest: 0
  useCustomMainGradleTemplate: 0
  useCustomLauncherGradleManifest: 0
  useCustomBaseGradleTemplate: 0
  useCustomGradlePropertiesTemplate: 0
  useCustomGradleSettingsTemplate: 0
  useCustomProguardFile: 0
  AndroidTargetArchitectures: 3
  AndroidTargetDevices: 0
  AndroidSplashScreenScale: 0
  androidSplashScreen: {fileID: 0}
  AndroidKeystoreName: 
  AndroidKeyaliasName: 
  AndroidEnableArmv9SecurityFeatures: 0
  AndroidBuildApkPerCpuArchitecture: 0
  AndroidTVCompatibility: 0
  AndroidIsGame: 1
  AndroidEnableTango: 0
  androidEnableBanner: 1
  androidUseLowAccuracyLocation: 0
  androidUseCustomKeystore: 0
  m_AndroidBanners:
  - width: 320
    height: 180
    banner: {fileID: 0}
  androidGamepadSupportLevel: 0
  chromeosInputEmulation: 1
  AndroidMinifyRelease: 0
  AndroidMinifyDebug: 0
  AndroidValidateAppBundleSize: 1
  AndroidAppBundleSizeToValidate: 150
  m_BuildTargetIcons: []
  m_BuildTargetPlatformIcons: []
  m_BuildTargetBatching:
  - m_BuildTarget: Standalone
    m_StaticBatching: 1
    m_DynamicBatching: 0
  - m_BuildTarget: tvOS
    m_StaticBatching: 1
    m_DynamicBatching: 0
  - m_BuildTarget: Android
    m_StaticBatching: 1
    m_DynamicBatching: 0
  - m_BuildTarget: iPhone
    m_StaticBatching: 1
    m_DynamicBatching: 0
  - m_BuildTarget: WebGL
    m_StaticBatching: 0
    m_DynamicBatching: 0
  m_BuildTargetShaderSettings: []
  m_BuildTargetGraphicsJobs:
  - m_BuildTarget: AndroidPlayer
    m_GraphicsJobs: 0
  m_BuildTargetGraphicsJobMode: []
  m_BuildTargetGraphicsAPIs:
  - m_BuildTarget: AndroidPlayer
    m_APIs: 150000000b000000
    m_Automatic: 0
  m_BuildTargetVRSettings: []
  m_DefaultShaderChunkSizeInMB: 16
  m_DefaultShaderChunkCount: 0
  openGLRequireES31: 0
  openGLRequireES31AEP: 0
  openGLRequireES32: 0
  m_TemplateCustomTags: {}
  mobileMTRendering:
    Android: 1
    iPhone: 1
    tvOS: 1
  m_BuildTargetGroupLightmapEncodingQuality:
  - m_BuildTarget: Android
    m_EncodingQuality: 1
  m_BuildTargetGroupHDRCubemapEncodingQuality:
  - m_BuildTarget: Android
    m_EncodingQuality: 1
  m_BuildTargetGroupLightmapSettings: []
  m_BuildTargetGroupLoadStoreDebugModeSettings: []
  m_BuildTargetNormalMapEncoding:
  - m_BuildTarget: Android
    m_Encoding: 1
  m_BuildTargetDefaultTextureCompressionFormat:
  - m_BuildTarget: Android
    m_Format: 3
  playModeTestRunnerEnabled: 0
  runPlayModeTestAsEditModeTest: 0
  actionOnDotNetUnhandledException: 1
  enableInternalProfiler: 0
  logObjCUncaughtExceptions: 1
  enableCrashReportAPI: 0
  cameraUsageDescription: 
  locationUsageDescription: 
  microphoneUsageDescription: 
  bluetoothUsageDescription: 
  macOSTargetOSVersion: 10.13.0
  switchNMETAOverride: 
  switchNetLibKey: 
  switchSocketMemoryPoolSize: 6144
  switchSocketAllocatorPoolSize: 128
  switchSocketConcurrencyLimit: 14
  switchScreenResolutionBehavior: 2
  switchUseCPUProfiler: 0
  switchUseGOLDLinker: 0
  switchLTOSetting: 0
  switchApplicationID: 0x01004b9000490000
  switchNSODependencies: 
  switchCompilerFlags: 
  switchTitleNames_0: 
  switchTitleNames_1: 
  switchTitleNames_2: 
  switchTitleNames_3: 
  switchTitleNames_4: 
  switchTitleNames_5: 
  switchTitleNames_6: 
  switchTitleNames_7: 
  switchTitleNames_8: 
  switchTitleNames_9: 
  switchTitleNames_10: 
  switchTitleNames_11: 
  switchTitleNames_12: 
  switchTitleNames_13: 
  switchTitleNames_14: 
  switchTitleNames_15: 
  switchPublisherNames_0: 
  switchPublisherNames_1: 
  switchPublisherNames_2: 
  switchPublisherNames_3: 
  switchPublisherNames_4: 
  switchPublisherNames_5: 
  switchPublisherNames_6: 
  switchPublisherNames_7: 
  switchPublisherNames_8: 
  switchPublisherNames_9: 
  switchPublisherNames_10: 
  switchPublisherNames_11: 
  switchPublisherNames_12: 
  switchPublisherNames_13: 
  switchPublisherNames_14: 
  switchPublisherNames_15: 
  switchIcons_0: {fileID: 0}
  switchIcons_1: {fileID: 0}
  switchIcons_2: {fileID: 0}
  switchIcons_3: {fileID: 0}
  switchIcons_4: {fileID: 0}
  switchIcons_5: {fileID: 0}
  switchIcons_6: {fileID: 0}
  switchIcons_7: {fileID: 0}
  switchIcons_8: {fileID: 0}
  switchIcons_9: {fileID: 0}
  switchIcons_10: {fileID: 0}
  switchIcons_11: {fileID: 0}
  switchIcons_12: {fileID: 0}
  switchIcons_13: {fileID: 0}
  switchIcons_14: {fileID: 0}
  switchIcons_15: {fileID: 0}
  switchSmallIcons_0: {fileID: 0}
  switchSmallIcons_1: {fileID: 0}
  switchSmallIcons_2: {fileID: 0}
  switchSmallIcons_3: {fileID: 0}
  switchSmallIcons_4: {fileID: 0}
  switchSmallIcons_5: {fileID: 0}
  switchSmallIcons_6: {fileID: 0}
  switchSmallIcons_7: {fileID: 0}
  switchSmallIcons_8: {fileID: 0}
  switchSmallIcons_9: {fileID: 0}
  switchSmallIcons_10: {fileID: 0}
  switchSmallIcons_11: {fileID: 0}
  switchSmallIcons_12: {fileID: 0}
  switchSmallIcons_13: {fileID: 0}
  switchSmallIcons_14: {fileID: 0}
  switchSmallIcons_15: {fileID: 0}
  switchManualHTML: 
  switchAccessibleURLs: 
  switchLegalInformation: 
  switchMainThreadStackSize: 1048576
  switchPresenceGroupId: 
  switchLogoHandling: 0
  switchReleaseVersion: 0
  switchDisplayVersion: 1.0.0
  switchStartupUserAccount: 0
  switchSupportedLanguagesMask: 0
  switchLogoType: 0
  switchApplicationErrorCodeCategory: 
  switchUserAccountSaveDataSize: 0
  switchUserAccountSaveDataJournalSize: 0
  switchApplicationAttribute: 0
  switchCardSpecSize: -1
  switchCardSpecClock: -1
  switchRatingsMask: 0
  switchRatingsInt_0: 0
  switchRatingsInt_1: 0
  switchRatingsInt_2: 0
  switchRatingsInt_3: 0
  switchRatingsInt_4: 0
  switchRatingsInt_5: 0
  switchRatingsInt_6: 0
  switchRatingsInt_7: 0
  switchRatingsInt_8: 0
  switchRatingsInt_9: 0
  switchRatingsInt_10: 0
  switchRatingsInt_11: 0
  switchRatingsInt_12: 0
  switchLocalCommunicationIds_0: 
  switchLocalCommunicationIds_1: 
  switchLocalCommunicationIds_2: 
  switchLocalCommunicationIds_3: 
  switchLocalCommunicationIds_4: 
  switchLocalCommunicationIds_5: 
  switchLocalCommunicationIds_6: 
  switchLocalCommunicationIds_7: 
  switchParentalControl: 0
  switchAllowsScreenshot: 1
  switchAllowsVideoCapturing: 1
  switchAllowsRuntimeAddOnContentInstall: 0
  switchDataLossConfirmation: 0
  switchUserAccountLockEnabled: 0
  switchSystemResourceMemory: 16777216
  switchSupportedNpadStyles: 22
  switchNativeFsCacheSize: 32
  switchIsHoldTypeHorizontal: 0
  switchSupportedNpadCount: 8
  switchEnableTouchScreen: 1
  switchSocketConfigEnabled: 0
  switchTcpInitialSendBufferSize: 32
  switchTcpInitialReceiveBufferSize: 64
  switchTcpAutoSendBufferSizeMax: 256
  switchTcpAutoReceiveBufferSizeMax: 256
  switchUdpSendBufferSize: 9
  switchUdpReceiveBufferSize: 42
  switchSocketBufferEfficiency: 4
  switchSocketInitializeEnabled: 1
  switchNetworkInterfaceManagerInitializeEnabled: 1
  switchPlayerConnectionEnabled: 1
  switchUseNewStyleFilepaths: 1
  switchUseLegacyFmodPriorities: 0
  switchUseMicroSleepForYield: 1
  switchEnableRamDiskSupport: 0
  switchMicroSleepForYieldTime: 25
  switchRamDiskSpaceSize: 12
  ps4NPAgeRating: 12
  ps4NPTitleSecret: 
  ps4NPTrophyPackPath: 
  ps4ParentalLevel: 11
  ps4ContentID: ED1633-NPXX51362_00-0000000000000000
  ps4Category: 0
  ps4MasterVersion: 01.00
  ps4AppVersion: 01.00
  ps4AppType: 0
  ps4ParamSfxPath: 
  ps4VideoOutPixelFormat: 0
  ps4VideoOutInitialWidth: 1920
  ps4VideoOutBaseModeInitialWidth: 1920
  ps4VideoOutReprojectionRate: 60
  ps4PronunciationXMLPath: 
  ps4PronunciationSIGPath: 
  ps4BackgroundImagePath: 
  ps4StartupImagePath: 
  ps4StartupImagesFolder: 
  ps4IconImagesFolder: 
  ps4SaveDataImagePath: 
  ps4SdkOverride: 
  ps4BGMPath: 
  ps4ShareFilePath: 
  ps4ShareOverlayImagePath: 
  ps4PrivacyGuardImagePath: 
  ps4ExtraSceSysFile: 
  ps4NPtitleDatPath: 
  ps4RemotePlayKeyAssignment: -1
  ps4RemotePlayKeyMappingDir: 
  ps4PlayTogetherPlayerCount: 0
  ps4EnterButtonAssignment: 1
  ps4ApplicationParam1: 0
  ps4ApplicationParam2: 0
  ps4ApplicationParam3: 0
  ps4ApplicationParam4: 0
  ps4DownloadDataSize: 0
  ps4GarlicHeapSize: 2048
  ps4ProGarlicHeapSize: 2560
  playerPrefsMaxSize: 32768
  ps4Passcode: frAQBc8Wsa1xVPfvJcrgRYwTiizs2trQ
  ps4pnSessions: 1
  ps4pnPresence: 1
  ps4pnFriends: 1
  ps4pnGameCustomData: 1
  playerPrefsSupport: 0
  enableApplicationExit: 0
  resetTempFolder: 1
  restrictedAudioUsageRights: 0
  ps4UseResolutionFallback: 0
  ps4ReprojectionSupport: 0
  ps4UseAudio3dBackend: 0
  ps4UseLowGarlicFragmentationMode: 1
  ps4SocialScreenEnabled: 0
  ps4ScriptOptimizationLevel: 0
  ps4Audio3dVirtualSpeakerCount: 14
  ps4attribCpuUsage: 0
  ps4PatchPkgPath: 
  ps4PatchLatestPkgPath: 
  ps4PatchChangeinfoPath: 
  ps4PatchDayOne: 0
  ps4attribUserManagement: 0
  ps4attribMoveSupport: 0
  ps4attrib3DSupport: 0
  ps4attribShareSupport: 0
  ps4attribExclusiveVR: 0
  ps4disableAutoHideSplash: 0
  ps4videoRecordingFeaturesUsed: 0
  ps4contentSearchFeaturesUsed: 0
  ps4CompatibilityPS5: 0
  ps4AllowPS5Detection: 0
  ps4GPU800MHz: 1
  ps4attribEyeToEyeDistanceSettingVR: 0
  ps4IncludedModules: []
  ps4attribVROutputEnabled: 0
  monoEnv: 
  splashScreenBackgroundSourceLandscape: {fileID: 0}
  splashScreenBackgroundSourcePortrait: {fileID: 0}
  blurSplashScreenBackground: 1
  spritePackerPolicy: 
  scriptingDefineSymbols: {}
  additionalCompilerArguments: {}
  platformArchitecture: {}
  scriptingBackend:
    Android: 1
  il2cppCompilerConfiguration:
    Android: 2
  il2cppCodeGeneration: {}
  managedStrippingLevel:
    Android: 2
  incrementalIl2cppBuild: {}
  suppressCommonWarnings: 1
  allowUnsafeCode: 0
  useDeterministicCompilation: 1
  additionalIl2CppArgs: 
  scriptingRuntimeVersion: 1
  gcIncremental: 1
  gcWBarrierValidation: 0
  apiCompatibilityLevelPerPlatform: {}
  m_RenderingPath: 1
  m_MobileRenderingPath: 1
  platformCapabilities: {}
  vcxProjDefaultLanguage: 
  - enus
  vrEditorSettings: {}
  cloudServicesEnabled:
    UNet: 0
  luminIcon:
    m_Name: 
    m_ModelFolderPath: 
    m_PortalFolderPath: 
  luminCert:
    m_CertPath: 
    m_SignPackage: 1
  luminIsChannelApp: 0
  luminVersion:
    m_VersionCode: 1
    m_VersionName: 
  hmiPlayerDataPath: 
  hmiForceSRGBBlit: 1
  embeddedLinuxEnableGamepadInput: 1
  hmiLogStartupTiming: 0
  hmiCpuConfiguration: 
  apiCompatibilityLevel: 6
  activeInputHandler: 2
  windowsGamepadBackendHint: 0
  cloudProjectId: 
  framebufferDepthMemorylessMode: 0
  qualitySettingsNames: []
  projectName: 
  organizationId: 
  cloudEnabled: 0
  legacyClampBlendShapeWeights: 0
  hmiLoadingImage: {fileID: 0}
  platformRequiresReadableAssets: 0
  virtualTexturingSupportEnabled: 0
  insecureHttpOption: 0
"""

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
    # Rescue physics defaults if an older ProjectSettings.asset still carries a PhysicsManager doc
    # (pre-release-pass layout); otherwise the existing DynamicsManager.asset is kept as-is.
    physics = None
    if os.path.exists(ps_path):
        for d in re.split(r"(?=^--- !u!)", open(ps_path, encoding="utf-8").read(), flags=re.M)[1:]:
            if "\nPhysicsManager:" in d:
                physics = re.sub(r"^--- !u!\d+ &1", "--- !u!55 &1", d.rstrip("\n") + "\n")
    open(ps_path, "w", encoding="utf-8", newline="\n").write(PLAYER)
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
