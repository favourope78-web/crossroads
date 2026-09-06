using UnityEngine;
using UnityEngine.Rendering;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Applies the saved graphics tier at boot and keeps the global post-process Volume in step
    /// with it (release pass P3). ProjectSettings bind Low / Balanced / High to their own URP
    /// assets (shadows, render scale, MSAA); this component covers the two things a URP asset
    /// cannot: the frame-rate cap and the post profile - Low gets <c>PostProcess_Low</c>
    /// (no Bloom, lighter vignette), the other tiers keep <c>PostProcess_Global</c>.
    ///
    /// Attached to the PostProcess_Global object by the scene generator; PauseMenuUI calls
    /// <see cref="Apply"/> whenever the player changes the quality stepper.
    /// </summary>
    public class QualityTierApplier : MonoBehaviour
    {
        [SerializeField] private Volume globalVolume;
        [SerializeField] private VolumeProfile standardProfile;
        [SerializeField] private VolumeProfile lowProfile;

        private static QualityTierApplier _instance;
        private int _applied = -1;

        public int AppliedTier { get { return _applied; } }

        private void Awake()
        {
            _instance = this;
            if (globalVolume == null) globalVolume = GetComponent<Volume>();
        }

        private void Start()
        {
            // settings were loaded by GameUIBootstrap.Awake (execution order: UI bootstrap first)
            Apply(Crossroads.Gameplay.Input.InputSettingsStore.Current.qualityLevel);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Static entry for the pause menu (no scene lookup needed).</summary>
        public static void ApplyTier(int tier)
        {
            if (_instance != null) _instance.Apply(tier);
        }

        public void Apply(int tier)
        {
            tier = Mathf.Clamp(tier, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != tier) QualitySettings.SetQualityLevel(tier, true);
            Application.targetFrameRate = TargetFrameRate(tier);
            VolumeProfile want = ProfileFor(tier, standardProfile, lowProfile);
            if (globalVolume != null && want != null && globalVolume.sharedProfile != want) globalVolume.sharedProfile = want;
            _applied = tier;
        }

        /// <summary>Pure policy: Low -> low profile (no bloom) when one exists; else standard.</summary>
        public static VolumeProfile ProfileFor(int tier, VolumeProfile standard, VolumeProfile low)
        {
            if (tier <= 0 && low != null) return low;
            return standard;
        }

        /// <summary>Low caps at 30 fps (battery + thermal headroom on 2019-class phones); others 60.</summary>
        public static int TargetFrameRate(int tier)
        {
            return tier <= 0 ? 30 : 60;
        }
    }
}
