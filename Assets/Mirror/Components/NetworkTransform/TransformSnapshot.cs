// snapshot for snapshot interpolation
// https://gafferongames.com/post/snapshot_interpolation/
// position, rotation, scale for compatibility for now.
using UnityEngine;

namespace Mirror
{
    // NetworkTransform Snapshot
    public struct TransformSnapshot : Snapshot
    {
        // time or sequence are needed to throw away older snapshots.
        //
        // glenn fiedler starts with a 16 bit sequence number.
        // supposedly this is meant as a simplified example.
        // in the end we need the remote timestamp for accurate interpolation
        // and buffering over time.
        //
        // note: in theory, IF server sends exactly(!) at the same interval then
        //       the 16 bit ushort timestamp would be enough to calculate the
        //       remote time (sequence * sendInterval). but Unity's update is
        //       not guaranteed to run on the exact intervals / do catchup.
        //       => remote timestamp is better for now
        //
        // [REMOTE TIME, NOT LOCAL TIME]
        // => DOUBLE for long term accuracy & batching gives us double anyway
        public double remoteTime { get; set; }

        // the local timestamp (when we received it)
        // used to know if the first two snapshots are old enough to start.
        public double localTime { get; set; }

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public TransformSnapshot(double remoteTime, double localTime, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.remoteTime = remoteTime;
            this.localTime = localTime;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public static TransformSnapshot Interpolate(TransformSnapshot from, TransformSnapshot to, double t)
        {
            // NOTE:
            // Vector3 & Quaternion components are float anyway, so we can
            // keep using the functions with 't' as float instead of double.
            return new TransformSnapshot(
                // interpolated snapshot is applied directly. don't need timestamps.
                0, 0,
                // lerp position/rotation/scale unclamped in case we ever need
                // to extrapolate. atm SnapshotInterpolation never does.
                Vector3.LerpUnclamped(from.position, to.position, (float)t),
                // IMPORTANT: LerpUnclamped(0, 60, 1.5) extrapolates to ~86.
                //            SlerpUnclamped(0, 60, 1.5) extrapolates to 90!
                //            (0, 90, 1.5) is even worse. for Lerp.
                //            => Slerp works way better for our euler angles.
                Quaternion.SlerpUnclamped(from.rotation, to.rotation, (float)t),
                Vector3.LerpUnclamped(from.scale, to.scale, (float)t)
            );
        }

        public override string ToString() =>
            $"TransformSnapshot(remoteTime={remoteTime:F2}, localTime={localTime:F2}, pos={position}, rot={rotation}, scale={scale})";
    }
}
