using System;

namespace Crossroads.Core
{
    /// <summary>
    /// UnityEngine-free 3D point + helpers so proximity logic stays headless-testable.
    /// PlayerInteraction converts transforms to Point3 on cache refresh.
    /// </summary>
    [Serializable]
    public struct Point3
    {
        public float x;
        public float y;
        public float z;

        public Point3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Point3 operator -(Point3 a, Point3 b)
        {
            return new Point3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Point3 operator +(Point3 a, Point3 b)
        {
            return new Point3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Point3 operator *(Point3 a, float d)
        {
            return new Point3(a.x * d, a.y * d, a.z * d);
        }

        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }

        /// <summary>Unit-length copy; zero vector stays zero (no NaN).</summary>
        public Point3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-6f ? new Point3(x / m, y / m, z / m) : new Point3(0f, 0f, 0f);
            }
        }

        public static Point3 MoveTowards(Point3 current, Point3 target, float maxDelta)
        {
            Point3 delta = target - current;
            float dist = delta.magnitude;
            if (dist <= maxDelta || dist < 1e-6f) return target;
            return current + delta * (maxDelta / dist);
        }

        public static float Dot(Point3 a, Point3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static float Distance(Point3 a, Point3 b)
        {
            Point3 d = a - b;
            return (float)Math.Sqrt(d.sqrMagnitude);
        }

        public static float SqrDistance(Point3 a, Point3 b)
        {
            Point3 d = a - b;
            return d.sqrMagnitude;
        }

        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
    }
}
