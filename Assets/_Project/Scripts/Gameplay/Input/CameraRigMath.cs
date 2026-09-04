using UnityEngine;

namespace Crossroads.Gameplay.Input
{
    /// <summary>
    /// Pure math for the improved third-person orbit camera (task 4) so the rig's behaviour
    /// is unit-testable without a scene: yaw/pitch orbit offsets, pitch clamps and the
    /// collision "pull in fast, extend slow" policy that keeps the camera out of walls
    /// while refusing to give the player motion sickness.
    /// </summary>
    public static class CameraRigMath
    {
        public const float MinDistance = 1.1f;     // never closer than this, even cornered
        public const float PullMargin = 0.18f;     // probe clear distance minus this = clipped distance
        public const float DefaultExtendSpeed = 3.5f; // m/s the camera may ease back OUT

        /// <summary>Sensible indoor pitch limits: look up a little at the roof, down a lot at the floor.</summary>
        public static float ClampPitch(float pitch)
        {
            return Mathf.Clamp(pitch, 5f, 65f);
        }

        /// <summary>
        /// Orbit offset for yaw/pitch around the player. yaw 0 looks +Z (scene convention),
        /// pitch is degrees above horizontal. Distance is the orbit radius; heightBias lifts
        /// the pivot so indoor framing keeps the player low-center.
        /// </summary>
        public static Vector3 OrbitOffset(float yawDegrees, float pitchDegrees, float distance, float heightBias)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = ClampPitch(pitchDegrees) * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitch) * distance;
            // camera sits BEHIND the look direction: offset = -forward(yaw) * horizontal + up * sin(pitch)*distance
            return new Vector3(
                -Mathf.Sin(yaw) * horizontal,
                Mathf.Sin(pitch) * distance + heightBias,
                -Mathf.Cos(yaw) * horizontal);
        }

        /// <summary>
        /// Collision policy: if the sphere probe found less clearance than we want, snap IN
        /// immediately (never clip through geometry); ease back OUT at a bounded speed so
        /// the camera doesn't pop. Deterministic function of (desired, clearance, current).
        /// </summary>
        public static float ResolveDistance(float desired, float probeClearance, float current, float dt, float extendSpeed)
        {
            float target = desired;
            if (probeClearance > 0f && probeClearance < desired)
            {
                // wall (or not-yet-probed): allowed up to clearance minus margin
                target = Mathf.Max(probeClearance - PullMargin, MinDistance);
            }
            if (target < current) return target;                 // pull in: instant
            float maxStep = Mathf.Max(extendSpeed, 0.01f) * Mathf.Max(dt, 0f);
            return Mathf.Min(target, current + maxStep);         // extend: speed-limited
        }

        /// <summary>
        /// Indoor behaviour: when the roof probe is low (annex beams, door frames) reduce the
        /// pitch bias and pull the look height down so framing stays on the player, not the ceiling.
        /// </summary>
        public static float IndoorHeightBias(float headroomClearance, float normalBias)
        {
            if (headroomClearance <= 0f) return normalBias;
            if (headroomClearance >= 3.2f) return normalBias;
            // scale the bias down smoothly between 1.4m and 3.2m of headroom
            float t = Mathf.Clamp((headroomClearance - 1.4f) / (3.2f - 1.4f), 0f, 1f);
            return normalBias * (0.35f + 0.65f * t);
        }
    }
}
