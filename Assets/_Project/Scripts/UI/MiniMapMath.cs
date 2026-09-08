using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>
    /// Pure math for the circular radar mini-map (VISUAL_TARGET §4). Convention matches
    /// the game's camera rig: yaw 0 looks along world +Z, yaw 90 along +X
    /// (forward = (sin(yaw), 0, cos(yaw)); right = (cos(yaw), 0, -sin(yaw))).
    ///
    /// The radar is CAMERA-FORWARD-UP: joystick up = away from camera = up on the map, so
    /// the map never disagrees with the controls. North (+Z) rotates with the heading.
    /// Headless-testable (no Unity scene access).
    /// </summary>
    public static class MiniMapMath
    {
        /// <summary>World-space (x,z) delta rotated into camera-relative map space.</summary>
        public static Vector2 MapDelta(Vector2 worldDelta, float cameraYawDegrees)
        {
            float yaw = cameraYawDegrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(yaw);
            float c = Mathf.Cos(yaw);
            // map.x = dot(delta, camera right), map.y = dot(delta, camera forward)
            return new Vector2(
                worldDelta.x * c - worldDelta.y * s,
                worldDelta.x * s + worldDelta.y * c);
        }

        /// <summary>
        /// Final radar position for one world delta: scaled, clamped to the rim when out
        /// of range (classic radar edge-pinning). Returns pixels relative to the map centre.
        /// </summary>
        public static Vector2 ToMapPosition(Vector2 worldDelta, float cameraYawDegrees,
            float pixelsPerMeter, float radiusPixels, out bool pinned)
        {
            Vector2 m = MapDelta(worldDelta, cameraYawDegrees) * pixelsPerMeter;
            float len = Mathf.Sqrt(m.x * m.x + m.y * m.y);
            pinned = len > radiusPixels && len > 0.0001f;
            if (pinned) m = m * (radiusPixels / len);
            return m;
        }

        /// <summary>Heading (degrees, 0 = up, clockwise) where the north marker sits on the rim.</summary>
        public static float NorthHeading(float cameraYawDegrees)
        {
            Vector2 n = MapDelta(new Vector2(0f, 1f), cameraYawDegrees);
            return Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg;
        }

        /// <summary>Camera yaw (degrees) from a flattened forward direction.</summary>
        public static float YawFromForward(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>Range scale: 1 px per meter fraction the radar shows (helper for zoom presets).</summary>
        public static float PixelsPerMeterForRange(float rangeMeters, float radiusPixels)
        {
            return rangeMeters > 0.1f ? radiusPixels / rangeMeters : 1f;
        }
    }
}
