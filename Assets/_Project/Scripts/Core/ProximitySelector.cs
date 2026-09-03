using System;
using System.Collections.Generic;

namespace Crossroads.Core
{
    /// <summary>A candidate interactable, engine-free (mirrors Interactable's proximity contract).</summary>
    [Serializable]
    public class ProximityTarget
    {
        public string id = "";
        public Point3 position;
        public float radius = 2.5f;
        public float priority = 100f;   // lower = higher priority (design §8.3: quest > NPC > shrine > collectible)

        public ProximityTarget() { }
        public ProximityTarget(string id, Point3 position, float radius, float priority)
        {
            this.id = id; this.position = position; this.radius = radius; this.priority = priority;
        }
    }

    /// <summary>
    /// Nearest-valid-interactable selection (GAME_DESIGN §8.3): only targets whose
    /// radius covers the player compete; the closest wins, ties broken by priority.
    /// Pure C# so the "approach -> prompt appears" rule is unit-tested headlessly.
    /// </summary>
    public static class ProximitySelector
    {
        /// <summary>Returns the best target or null. selectedDistanceSqr reports how far it is.</summary>
        public static ProximityTarget Pick(Point3 playerPos, List<ProximityTarget> candidates, out float selectedDistanceSqr)
        {
            selectedDistanceSqr = float.MaxValue;
            if (candidates == null || candidates.Count == 0) return null;

            ProximityTarget best = null;
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                ProximityTarget c = candidates[i];
                if (c == null || c.radius <= 0f) continue;
                float dSqr = Point3.SqrDistance(playerPos, c.position);
                if (dSqr > c.radius * c.radius) continue; // out of range -> not a candidate

                bool better = best == null
                    || dSqr < bestDistSqr - 1e-4f
                    || (Math.Abs(dSqr - bestDistSqr) <= 1e-4f && c.priority < best.priority);
                if (better) { best = c; bestDistSqr = dSqr; }
            }

            selectedDistanceSqr = bestDistSqr;
            return best;
        }

        public static ProximityTarget Pick(Point3 playerPos, List<ProximityTarget> candidates)
        {
            float ignored;
            return Pick(playerPos, candidates, out ignored);
        }

        /// <summary>True when the player stands inside the target's interaction radius.</summary>
        public static bool InRange(Point3 playerPos, ProximityTarget target)
        {
            if (target == null || target.radius <= 0f) return false;
            return Point3.SqrDistance(playerPos, target.position) <= target.radius * target.radius;
        }
    }
}
