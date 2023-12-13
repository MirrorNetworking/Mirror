// a simple snapshot with timestamp & interpolation
using UnityEngine;

namespace Mirror.Examples.LagCompensationDemo
{
    public struct Snapshot3D : Snapshot
    {
        public double remoteTime { get; set; }
        public double localTime { get; set; }
        public Vector3 position;

        public Snapshot3D(double remoteTime, double localTime, Vector3 position)
        {
            this.remoteTime = remoteTime;
            this.localTime = localTime;
            this.position = position;
        }

        public static Snapshot3D Interpolate(Snapshot3D from, Snapshot3D to, double t) =>
            new Snapshot3D(
                // interpolated snapshot is applied directly. don't need timestamps.
                0, 0,
                // lerp unclamped in case we ever need to extrapolate.
                Vector3.LerpUnclamped(from.position, to.position, (float)t));
    }
}
