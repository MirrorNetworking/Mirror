// simple 2D snapshot for the demo.
// this way we can place cubes on different Y
using UnityEngine;

namespace Mirror.Examples.SnapshotInterpolationDemo
{
    // a simple snapshot with timestamp & interpolation
    public struct Snapshot3D : Snapshot
    {
        public double remoteTimestamp { get; set; }
        public double localTimestamp { get; set; }
        public Vector2 position;

        public Snapshot3D(double remoteTimestamp, double localTimestamp, Vector2 position)
        {
            this.remoteTimestamp = remoteTimestamp;
            this.localTimestamp = localTimestamp;
            this.position = position;
        }

        public static Snapshot3D Interpolate(Snapshot3D from, Snapshot3D to, double t) =>
            new Snapshot3D(
                // TODO
                // interpolated snapshot is applied directly. don't need timestamps.
                0, 0,
                // TODO
                // lerp unclamped in case we ever need to extrapolate.
                Vector2.LerpUnclamped(from.position, to.position, (float)t));
    }
}
