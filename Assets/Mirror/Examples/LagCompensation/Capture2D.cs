using UnityEngine;

namespace Mirror.Examples.LagCompensationDemo
{
    public struct Capture2D : Capture
    {
        public double timestamp { get; set; }
        public Vector2 position;

        public static Capture2D Interpolate(Capture2D from, Capture2D to, double t) =>
                // interpolated snapshot is applied directly. don't need timestamps.
            new Capture2D{position=Vector3.LerpUnclamped(from.position, to.position, (float)t)};

        public override string ToString() => $"(time={timestamp} pos={position})";
    }
}
