using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Pure simulation core for the runtime VFX pool (art production pass - ART_GAPS:
    /// particle-based ability/hit feedback). Fixed-size ring of billboard particles:
    /// position/velocity/life integrated here, rendered by VfxDirector. No Unity object
    /// access beyond Vector3/Color math, so every rule below is unit-testable headlessly.
    ///
    /// Mobile budget: one pool of 64 quads total (abilities 22, hits 10, ambient dust 12,
    /// spare 20), simulated with pure math - no per-particle components, no allocations
    /// after construction.
    /// </summary>
    public static class VfxMath
    {
        public const int PoolSize = 64;
        public const int AbilityCount = 22;
        public const int HitCount = 10;
        public const int DustCount = 12;

        // ---------------------------------------------------------------- line palettes (matches HudTheme)
        public static Color ColorForLine(string line)
        {
            switch (line)
            {
                case "ember": return new Color(0.95f, 0.38f, 0.22f, 1f);
                case "tide": return new Color(0.25f, 0.80f, 0.85f, 1f);
                case "stone": return new Color(0.85f, 0.68f, 0.32f, 1f);
                case "hollow": return new Color(0.62f, 0.48f, 0.90f, 1f);
                default: return new Color(0.9f, 0.9f, 0.9f, 1f);
            }
        }

        /// <summary>Burst impulse for one particle: deterministic from (index, salt) so tests/headless runs reproduce.</summary>
        public static void BurstVelocity(int index, int salt, float speed, out Vector3 velocity)
        {
            // golden-angle fan around +Y-biased hemisphere, jittered by salt
            float a1 = index * 2.399963f + salt * 0.61803f;
            float a2 = ((index * 2654435761u) % 1000u) / 1000f * Mathf.PI;
            float vy = 0.35f + 0.65f * ((index * 40503u + salt) % 100u) / 100f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - vy * vy));
            velocity = new Vector3(Mathf.Cos(a1) * r, vy, Mathf.Sin(a1) * r) * speed;
        }

        /// <summary>Alpha over normalized life: fast fade-in, slow fade-out (reads as a burst, not a blink).</summary>
        public static float AlphaOverLife(float life01)
        {
            float c = Mathf.Clamp01(life01);
            float fadeIn = Mathf.Clamp01(c / 0.18f);
            float fadeOut = 1f - c;
            return fadeIn * fadeOut * fadeOut * (3f - 2f * fadeIn);
        }

        /// <summary>Size over normalized life: pop out then settle (ease-out).</summary>
        public static float SizeOverLife(float life01)
        {
            float c = Mathf.Clamp01(life01);
            float grow = 1f - (1f - Mathf.Clamp01(c / 0.25f)) * (1f - Mathf.Clamp01(c / 0.25f));
            return 0.35f + 0.65f * grow;
        }

        /// <summary>Gravity + drag integration for one particle. Returns false when the particle expired.</summary>
        public static bool Step(ref Vector3 position, ref Vector3 velocity, ref float life, float dt, float gravity = -3.2f, float drag = 1.6f)
        {
            life -= dt;
            if (life <= 0f) return false;
            velocity.y += gravity * dt;
            float damping = Mathf.Max(0f, 1f - drag * dt);
            velocity *= damping;
            position += velocity * dt;
            return true;
        }

        /// <summary>Maps a pool slot to a layer (0 ability / 1 hit / 2 dust / 3 spare) so bursts never fight for the same quads.</summary>
        public static int LayerOfSlot(int slot)
        {
            if (slot < AbilityCount) return 0;
            if (slot < AbilityCount + HitCount) return 1;
            if (slot < AbilityCount + HitCount + DustCount) return 2;
            return 3;
        }

        /// <summary>Next ring cursor for a layer, wrapped (pool cycling - oldest particle is always the one reused).</summary>
        public static int NextCursor(int cursor, int layer)
        {
            int start, count;
            LayerRange(layer, out start, out count);
            return start + (cursor - start + 1) % count;
        }

        public static void LayerRange(int layer, out int start, out int count)
        {
            switch (layer)
            {
                case 0: start = 0; count = AbilityCount; return;
                case 1: start = AbilityCount; count = HitCount; return;
                case 2: start = AbilityCount + HitCount; count = DustCount; return;
                default: start = AbilityCount + HitCount + DustCount; count = PoolSize - (AbilityCount + HitCount + DustCount); return;
            }
        }

        /// <summary>Dust drift velocity: slow, buoyant, camera-relative (hall motes ride the light shafts).</summary>
        public static Vector3 DustVelocity(int index)
        {
            float phase = index * 1.7f;
            return new Vector3(Mathf.Sin(phase) * 0.08f, 0.05f + 0.04f * Mathf.Cos(phase * 0.7f), Mathf.Cos(phase * 0.6f) * 0.08f);
        }
    }
}
