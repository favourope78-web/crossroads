using Crossroads.Gameplay.Input;
using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>
    /// DEV OVERLAY GATE (visual transformation pass, VISUAL_TARGET §4).
    ///
    /// Normal players see a clean HUD. Raw diagnostic surfaces - the StateHUD stat dump,
    /// the full objective list, the developer location list - only exist when this gate is
    /// open, and the gate can only open where it belongs:
    ///
    ///   * release builds: never (compile-time stripped)
    ///   * editor / development builds: open by default, closable at runtime
    ///
    /// The preference rides the existing InputSettings file (devOverlays: -1 follow the
    /// build default, 0 force-off, 1 force-on) so it survives restarts on a dev device.
    /// The runtime toggle itself is a small triple-tap gesture on the top-left corner
    /// (DevOverlayToggle) - never a visible button a player could mistake for gameplay.
    /// </summary>
    public static class UIDebugGate
    {
        private static bool _resolved;
        private static bool _visible;

        /// <summary>Compile-time truth: is this a build where dev overlays may exist at all?</summary>
        public static bool DevBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Are the diagnostic overlays currently shown? (Always false in release.)</summary>
        public static bool OverlaysVisible
        {
            get
            {
                if (_resolved) return _visible;
                _resolved = true;
                _visible = DevBuild && ResolvePreference();
                return _visible;
            }
        }

        private static bool ResolvePreference()
        {
            InputSettings s = InputSettingsStore.Current;
            if (s == null) return true;
            return s.devOverlays != 0; // -1 (unset) or 1 -> follow dev default = on
        }

        /// <summary>Runtime toggle (dev builds only). Persists the choice.</summary>
        public static void SetOverlays(bool visible)
        {
            if (!DevBuild) return; // release can never open the gate
            _resolved = true;
            if (_visible == visible) return;
            _visible = visible;
            InputSettings s = InputSettingsStore.Current;
            if (s != null)
            {
                s.devOverlays = visible ? 1 : 0;
                InputSettingsStore.Save(s);
            }
            if (OverlaysChanged != null) OverlaysChanged(visible);
        }

        public static void Toggle() { SetOverlays(!OverlaysVisible); }

        /// <summary>Diagnostic surfaces subscribe to arrive/leave with the gate.</summary>
        public static event System.Action<bool> OverlaysChanged;

        /// <summary>Headless/tests: forget the resolved state (each test gets a fresh gate).</summary>
        public static void ResetForTests()
        {
            _resolved = false;
            _visible = false;
        }
    }

    /// <summary>
    /// The invisible corner gesture that flips the dev gate: three taps inside the
    /// top-left 300x160 px corner within 1.2 s (dev builds only - the component is inert
    /// otherwise and never intercepts gameplay touches: it only listens).
    /// </summary>
    public class DevOverlayToggle : MonoBehaviour
    {
        private float _lastTap;
        private int _taps;

        private void Update()
        {
            if (!UIDebugGate.DevBuild) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.F3)) UIDebugGate.Toggle();
#endif
        }

        /// <summary>Called by the HUD root for corner taps detected on the safe-area (pointer down).</summary>
        public void NotifyCornerTap()
        {
            if (!UIDebugGate.DevBuild) return;
            float now = Time.unscaledTime;
            if (now - _lastTap > 1.2f) _taps = 0;
            _lastTap = now;
            _taps++;
            if (_taps >= 3)
            {
                _taps = 0;
                UIDebugGate.Toggle();
            }
        }
    }
}
