// ============================================================================
// CROSSROADS Android development-APK builder (task 15). Editor-only script.
//
// One-click: Unity menu  Build -> CROSSROADS Dev APK (Android)
// Batchmode: Unity -batchmode -projectPath <repo> -executeMethod
//            Crossroads.EditorTools.AndroidDevBuild.BuildDevApk -quit
//            [-logFile build/android-build.log]
//
// Configure() enforces EVERY Android-relevant player setting programmatically
// (the YAML seed in ProjectSettings/ is belt-and-braces; this is authoritative),
// then builds a DEVELOPMENT build (debuggable, profiler-capable, development
// checkpoint markers) of the FirstLocation scene to Builds/CrossroadsDev.apk.
// ============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Crossroads.EditorTools
{
    public static class AndroidDevBuild
    {
        private const string ApkPath = "Builds/CrossroadsDev.apk";

        public static void Configure()
        {
            PlayerSettings.companyName = "favourope78-web";
            PlayerSettings.productName = "CROSSROADS";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.favourope78.crossroads");

            // orientation: landscape only (third-person action layout is designed wide)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Android device floor: Android 7.0 (API 24) covers the design's mid-range
            // target; IL2CPP + ARM64 (Play requirement), ARMv7 kept for old test phones.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.blitType = AndroidBlitType.Auto;

            // input: BOTH handlers (the code compiles under either define; auto-switching
            // between the touch rig and desktop fallbacks works in editor + device)
            PlayerSettings.activeInputHandling = ActiveInputHandling.Both;

            // mobile performance budget (GAME_DESIGN §14): 30 fps floor, no vsync stall
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            // graphics jobs off on Adreno/Mali drivers for stability; URP does the work
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);

            Debug.Log("[CROSSROADS] Android player settings configured (API24+, ARM64/ARMv7, IL2CPP, landscape)");
        }

        [MenuItem("Build/CROSSROADS Dev APK (Android)")]
        public static void BuildDevApk()
        {
            Configure();

            // scene list = the prototype scene (scene GUID comes from the generator registry)
            var sceneGuids = new[]
            {
                "c0a1fed200000000000000000000005a" // Assets/Scenes/Prototype/FirstLocation.unity
            };
            var scenes = new EditorBuildSettingsScene[sceneGuids.Length];
            for (int i = 0; i < sceneGuids.Length; i++) scenes[i] = new EditorBuildSettingsScene(sceneGuids[i], true);
            EditorBuildSettings.scenes = scenes;

            string abs = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ApkPath));
            Directory.CreateDirectory(Path.GetDirectoryName(abs));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Prototype/FirstLocation.unity" },
                locationPathName = abs,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("[CROSSROADS] DEV APK BUILT: " + abs + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[CROSSROADS] APK BUILD FAILED: " + report.summary.result);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
