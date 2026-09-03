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

        public float sqrMagnitude { get { return x * x + y * y + z * z; } }

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
    }
}
