using System.IO;
using Crossroads.Core;
using UnityEngine;

namespace Crossroads.Gameplay.Input
{
    /// <summary>
    /// Player-facing configuration for the mobile experience (task: configurable controls,
    /// camera settings, pause menu). Pure serializable data - persisted as its own settings
    /// file next to the saves through the SAME IJsonSerializer/IPathProvider seams the
    /// SaveSystem uses, so it is headless-testable and never bumps the save schema.
    /// </summary>
    [System.Serializable]
    public class InputSettings
    {
        public float lookSensitivity = 1.0f;      // multiplies look-pad deltas
        public bool invertLookY = false;
        public float cameraDistance = 4.4f;       // orbit radius preference
        public float cameraSmoothing = 0.14f;     // follow damp time
        public float buttonScale = 1.0f;          // touch control size multiplier
        public float controlOpacity = 0.9f;       // 0.35..1 (ghost the rig when comfortable)
        public bool leftHanded = false;           // mirrors stick/action cluster on X
        public float audioVolume = 1.0f;          // 0..1
        public int qualityLevel = 1;              // 0 Low / 1 Balanced / 2 High
        public int showTouchControls = 0;         // 0 Auto / 1 Always / 2 Never

        /// <summary>Clamps every field into its sane range (defensive against hand-edited files).</summary>
        public void ApplyClamps()
        {
            lookSensitivity = Mathf.Clamp(lookSensitivity, 0.4f, 3.0f);
            cameraDistance = Mathf.Clamp(cameraDistance, 2.6f, 6.0f);
            cameraSmoothing = Mathf.Clamp(cameraSmoothing, 0.05f, 0.4f);
            buttonScale = Mathf.Clamp(buttonScale, 0.8f, 1.4f);
            controlOpacity = Mathf.Clamp(controlOpacity, 0.35f, 1f);
            audioVolume = Mathf.Clamp(audioVolume, 0f, 1f);
            qualityLevel = Mathf.Clamp(qualityLevel, 0, 2);
            showTouchControls = Mathf.Clamp(showTouchControls, 0, 2);
        }
    }

    /// <summary>Setting ids for <see cref="SettingsNudge"/> (int so it stays serialization-free).</summary>
    public static class SettingId
    {
        public const int Sensitivity = 0;
        public const int CameraDistance = 1;
        public const int Volume = 2;
        public const int Quality = 3;
        public const int ButtonScale = 4;
        public const int InvertLookY = 5;
    }

    /// <summary>
    /// Pure settings mutation shared by the pause menu and the tests: one place that knows
    /// each setting's step and clamp. Returns true when the value actually changed.
    /// </summary>
    public static class SettingsNudge
    {
        public static bool Apply(InputSettings s, int settingId, int direction)
        {
            if (s == null || direction == 0) return false;
            float before = 0f; bool beforeBool = false; bool isBool = settingId == SettingId.InvertLookY;
            if (isBool) beforeBool = s.invertLookY; else before = ValueOf(s, settingId);

            switch (settingId)
            {
                case SettingId.Sensitivity:
                    s.lookSensitivity = Mathf.Clamp(s.lookSensitivity + 0.2f * direction, 0.4f, 3.0f);
                    break;
                case SettingId.CameraDistance:
                    s.cameraDistance = Mathf.Clamp(s.cameraDistance + 0.4f * direction, 2.6f, 6.0f);
                    break;
                case SettingId.Volume:
                    s.audioVolume = Mathf.Clamp(s.audioVolume + 0.1f * direction, 0f, 1f);
                    break;
                case SettingId.Quality:
                    s.qualityLevel = Mathf.Clamp(s.qualityLevel + direction, 0, 2);
                    break;
                case SettingId.ButtonScale:
                    s.buttonScale = Mathf.Clamp(s.buttonScale + 0.1f * direction, 0.8f, 1.4f);
                    break;
                case SettingId.InvertLookY:
                    s.invertLookY = !s.invertLookY;
                    break;
            }
            return isBool ? s.invertLookY != beforeBool : ValueOf(s, settingId) != before;
        }

        private static float ValueOf(InputSettings s, int id)
        {
            switch (id)
            {
                case SettingId.Sensitivity: return s.lookSensitivity;
                case SettingId.CameraDistance: return s.cameraDistance;
                case SettingId.Volume: return s.audioVolume;
                case SettingId.Quality: return s.qualityLevel;
                case SettingId.ButtonScale: return s.buttonScale;
                default: return 0f;
            }
        }
    }

    /// <summary>
    /// Loads/saves the settings file. Bind(json, paths) is the test seam; unbound (the real
    /// game) it uses UnityJsonSerializer + PersistentDataPathProvider, i.e. the exact
    /// persistence directory the saves live in.
    /// </summary>
    public static class InputSettingsStore
    {
        public const string FileName = "player_settings.json";

        private static IJsonSerializer _json;
        private static IPathProvider _paths;
        private static InputSettings _current;

        public static InputSettings Current
        {
            get
            {
                if (_current == null) _current = new InputSettings();
                return _current;
            }
        }

        public static void Bind(IJsonSerializer json, IPathProvider paths)
        {
            _json = json;
            _paths = paths;
        }

        public static string Path
        {
            get
            {
                if (_paths == null) _paths = new PersistentDataPathProvider();
                return _paths.Resolve(FileName);
            }
        }

        /// <summary>Loads (falling back to defaults + clamps when missing or corrupt). Never throws.</summary>
        public static InputSettings Load()
        {
            if (_json == null) _json = new UnityJsonSerializer();
            try
            {
                string file = Path;
                if (File.Exists(file))
                {
                    InputSettings loaded = _json.FromJson<InputSettings>(File.ReadAllText(file));
                    if (loaded != null)
                    {
                        loaded.ApplyClamps();
                        _current = loaded;
                        return _current;
                    }
                }
            }
            catch (System.Exception)
            {
                // corrupt settings must never block launch - fall through to defaults
            }
            _current = new InputSettings();
            _current.ApplyClamps();
            return _current;
        }

        /// <summary>Persists the given settings (clamped) and makes them Current.</summary>
        public static void Save(InputSettings settings)
        {
            if (settings == null) return;
            settings.ApplyClamps();
            _current = settings;
            if (_json == null) _json = new UnityJsonSerializer();
            try
            {
                string file = Path;
                string dir = System.IO.Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(file, _json.ToJson(settings, false));
            }
            catch (System.Exception)
            {
                // read-only storage: settings stay in-memory for this session
            }
        }
    }
}
