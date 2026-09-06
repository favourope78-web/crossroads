// ============================================================================
// CROSSROADS - Unity Build Automation (Unity Cloud / DevOps) hooks. Editor-only.
//
// Build Automation does NOT run our -executeMethod build entry points; it builds the
// project itself from EditorBuildSettings + PlayerSettings. These pre/post-export methods
// are what you type into the build target's Advanced settings so the cloud build gets the
// exact same configuration as scripts/build_android_apk.sh:
//
//   Pre-Export Method   Crossroads.EditorTools.CloudBuildHooks.PreExportRelease
//                       (or ...PreExportDev for a development/profiler build)
//   Post-Export Method  Crossroads.EditorTools.CloudBuildHooks.PostExport
//
// Full field-by-field setup: UNITY_CLOUD_BUILD_SETUP.md at the repo root.
// ============================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Crossroads.EditorTools
{
    public static class CloudBuildHooks
    {
        /// <summary>Release APK/AAB: full AndroidDevBuild.Configure() (API 24+, ARM64+ARMv7, IL2CPP
        /// Master, stripping, Vulkan+GLES3, ASTC, landscape, Balanced tier) + the shipped scene list.
        /// Development/profiler flags are left OFF - use the build target's "Development build"
        /// toggle only with PreExportDev.</summary>
        public static void PreExportRelease()
        {
            Apply(development: false);
        }

        /// <summary>Same configuration, but keeps Development + profiler connection so a first
        /// on-device profile (FINAL_RELEASE_REPORT.md section 7) can be captured.</summary>
        public static void PreExportDev()
        {
            Apply(development: true);
        }

        /// <summary>Called by Build Automation after the player is exported. Writes a small
        /// manifest next to the artifact so the build page shows what configuration produced it.</summary>
        public static void PostExport(string exportPath)
        {
            try
            {
                string dir = Directory.Exists(exportPath) ? exportPath : Path.GetDirectoryName(exportPath);
                if (string.IsNullOrEmpty(dir)) return;
                File.WriteAllText(Path.Combine(dir, "crossroads-build-info.txt"),
                    "CROSSROADS Android build\n" +
                    "unity            : " + Application.unityVersion + "\n" +
                    "bundle id        : " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) + "\n" +
                    "version          : " + PlayerSettings.bundleVersion + " (code " + PlayerSettings.Android.bundleVersionCode + ")\n" +
                    "scripting backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) + "\n" +
                    "architectures    : " + PlayerSettings.Android.targetArchitectures + "\n" +
                    "min sdk          : " + PlayerSettings.Android.minSdkVersion + "\n" +
                    "development      : " + EditorUserBuildSettings.development + "\n" +
                    "scenes           : " + string.Join(", ", Array.ConvertAll(EditorBuildSettings.scenes, s => s.path)) + "\n" +
                    "exported to      : " + exportPath + "\n" +
                    "utc              : " + DateTime.UtcNow.ToString("u") + "\n");
                Debug.Log("[CROSSROADS] PostExport: wrote crossroads-build-info.txt to " + dir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CROSSROADS] PostExport manifest skipped: " + ex.Message);
            }
        }

        private static void Apply(bool development)
        {
            AndroidDevBuild.Configure();      // authoritative Android player settings
            AndroidDevBuild.ApplySceneList(); // Assets/Scenes/Prototype/FirstLocation.unity only

            // Version stamp: Build Automation exposes its build number as UCB_BUILD_NUMBER; use it
            // as the Android versionCode so every cloud build is installable over the previous one
            // (Android refuses to update to an equal/lower versionCode).
            string buildNumber = Environment.GetEnvironmentVariable("UCB_BUILD_NUMBER");
            if (int.TryParse(buildNumber, out int n) && n > 0)
            {
                PlayerSettings.Android.bundleVersionCode = n;
                if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1")
                    PlayerSettings.bundleVersion = "0.1." + n;
            }

            // Optional build-minute saver: Environment variable CROSSROADS_ARCH=arm64 on the build
            // target drops the ARMv7 slice (halves IL2CPP time; Play Store only needs ARM64).
            string arch = Environment.GetEnvironmentVariable("CROSSROADS_ARCH");
            if (string.Equals(arch, "arm64", StringComparison.OrdinalIgnoreCase))
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.connectProfiler = development;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            EditorUserBuildSettings.allowDebugging = development;
            // ASTC is set by Configure(); make the export format explicit for the cloud builder.
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            AssetDatabase.SaveAssets();
            Debug.Log("[CROSSROADS] CloudBuildHooks: " + (development ? "DEVELOPMENT" : "RELEASE") +
                      " configuration applied (versionCode " + PlayerSettings.Android.bundleVersionCode +
                      ", version " + PlayerSettings.bundleVersion + ", scene " + AndroidDevBuild.MainScenePath + ")");
        }
    }
}
#endif
