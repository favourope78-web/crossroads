using System;
using Crossroads.Core;
using UnityEngine;

namespace Crossroads.Gameplay.Input
{
    /// <summary>Touch/desktop action buttons the rig can gate independently.</summary>
    public enum MobileButton
    {
        Interact = 0,
        Attack = 1,
        Dodge = 2,
        Ability = 3,
        Pause = 4
    }

    /// <summary>
    /// Single hub for ALL gameplay input (mobile player experience, GAME_DESIGN §8):
    /// UGUI widgets (virtual joystick, look pad, buttons) PRODUCE into the bus;
    /// the player controller, camera and combat controller CONSUME from it.
    /// Pure state, zero allocations, one place to gate input (InputLock, availability)
    /// and one place for tests to drive the whole game without touching rendering.
    /// </summary>
    public static class InputBus
    {
        /// <summary>Raised when a button's availability flips (UI enables/disables itself).</summary>
        public static event Action<MobileButton, bool> AvailabilityChanged;

        private static Vector2 _movement;
        private static Vector2 _lookAccum;
        private static readonly bool[] _pressed = new bool[5];
        private static readonly bool[] _available = { true, true, true, true, true };

        /// <summary>Movement axes, already dead-zone-filtered, magnitude in [0,1]. Zero while input is locked.</summary>
        public static Vector2 Movement
        {
            get { return InputLock.Active ? Vector2.zero : _movement; }
        }

        /// <summary>True when any touch/keyboard movement is active (before the lock check).</summary>
        public static bool HasMovementInput { get { return _movement.sqrMagnitude > 0.0001f; } }

        // ---------------------------------------------------------------- producers
        /// <summary>Joystick write: filters through <see cref="JoystickFilter"/> (circular deadzone + clamp).</summary>
        public static void SetMovement(float x, float y)
        {
            _movement = JoystickFilter.Apply(x, y, InputTuning.Deadzone, InputTuning.MaxRadius);
        }

        /// <summary>Look pad write: accumulates until consumed (pixels; consumers apply sensitivity).</summary>
        public static void AddLookDelta(float dx, float dy)
        {
            _lookAccum.x += dx;
            _lookAccum.y += dy;
        }

        /// <summary>Button press from a widget or key. Ignored while locked (dialogue/pause policy is the consumer's).</summary>
        public static void SetPressed(MobileButton button)
        {
            _pressed[(int)button] = true;
        }

        /// <summary>Coarse availability gate (e.g. ATTACK/DODGE exist only in combat, INTERACT only near a target).</summary>
        public static void SetAvailable(MobileButton button, bool available)
        {
            int i = (int)button;
            if (_available[i] == available) return;
            _available[i] = available;
            if (AvailabilityChanged != null) AvailabilityChanged(button, available);
        }

        // ---------------------------------------------------------------- consumers
        /// <summary>Consumes accumulated look delta (pixels since last call) and clears it. Zero while locked.</summary>
        public static Vector2 ConsumeLookDelta()
        {
            if (InputLock.Active) { _lookAccum = Vector2.zero; return Vector2.zero; }
            Vector2 d = _lookAccum;
            _lookAccum = Vector2.zero;
            return d;
        }

        /// <summary>True exactly once per press, and only while the button is available and input unlocked.</summary>
        public static bool ConsumePress(MobileButton button)
        {
            int i = (int)button;
            if (!_pressed[i]) return false;
            _pressed[i] = false;
            return !InputLock.Active && _available[i];
        }

        /// <summary>Availability query (UI + tests).</summary>
        public static bool IsAvailable(MobileButton button) { return _available[(int)button]; }

        /// <summary>Clears all live state (test seams + StateReset wiring). Availability is policy, kept.</summary>
        public static void Reset()
        {
            _movement = Vector2.zero;
            _lookAccum = Vector2.zero;
            for (int i = 0; i < _pressed.Length; i++) _pressed[i] = false;
        }
    }

    /// <summary>Deterministic joystick filtering shared by every stick (task: configurable controls).</summary>
    public static class JoystickFilter
    {
        /// <summary>
        /// Circular deadzone + clamp: outputs a vector whose magnitude is remapped into [0,1]
        /// between deadzone and maxRadius. Full tilt at the rim, zero inside the deadzone,
        /// no snapping (analog preserved for walk/run blend).
        /// </summary>
        public static Vector2 Apply(float x, float y, float deadzone, float maxRadius)
        {
            if (maxRadius <= 0.0001f) return Vector2.zero;
            Vector2 v = new Vector2(x, y);
            float len = v.magnitude;
            if (len <= deadzone) return Vector2.zero;
            float rim = Mathf.Max(maxRadius - deadzone, 0.0001f);
            float scaled = Mathf.Min((len - deadzone) / rim, 1f);
            return v / len * scaled;
        }
    }

    /// <summary>Shared tunables for the touch rig (kept next to the bus so tests and UI agree).</summary>
    public static class InputTuning
    {
        /// <summary>Inner deadzone of the virtual joystick, in joystick-local units.</summary>
        public static float Deadzone = 0.18f;
        /// <summary>Joystick rim radius in the same units.</summary>
        public static float MaxRadius = 1f;
        /// <summary>Minimum joystick travel before the character turns (filtering jitter).</summary>
        public const float MoveEpsilon = 0.0001f;
    }
}
