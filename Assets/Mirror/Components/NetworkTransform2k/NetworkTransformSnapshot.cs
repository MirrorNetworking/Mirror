// snapshot for snapshot interpolation
// https://gafferongames.com/post/snapshot_interpolation/
// position, rotation, scale for compatibility for now.
using UnityEngine;

namespace Mirror
{
    // transform part of the Snapshot so we can send it over the wire without
    // timestamp.
    public struct NTSnapshotTransform
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public NTSnapshotTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }

    // NetworkTransform Snapshot
    public struct NTSnapshot : Snapshot
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
        public double remoteTimestamp { get; set; }
        public double localTimestamp { get; set; }

        public NTSnapshotTransform transform;

        public NTSnapshot(double remoteTimestamp, double localTimestamp, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.remoteTimestamp = remoteTimestamp;
            this.localTimestamp = localTimestamp;
            this.transform = new NTSnapshotTransform(position, rotation, scale);
        }

        public Snapshot Interpolate(Snapshot to, double t)
        {
            // NOTE:
            // Vector3 & Quaternion components are float anyway, so we can
            // keep using the functions with 't' as float instead of double.
            NTSnapshot toCasted = (NTSnapshot)to;
            return new NTSnapshot(
                // TODO no reason to interpolate time
                Mathd.LerpUnclamped(remoteTimestamp, toCasted.remoteTimestamp, t),
                Mathd.LerpUnclamped(localTimestamp, toCasted.localTimestamp, t),
                Vector3.LerpUnclamped(transform.position, toCasted.transform.position, (float)t),
                // IMPORTANT: LerpUnclamped(0, 60, 1.5) extrapolates to ~86.
                //            SlerpUnclamped(0, 60, 1.5) extrapolates to 90!
                //            (0, 90, 1.5) is even worse. for Lerp.
                //            => Slerp works way better for our euler angles.
                Quaternion.SlerpUnclamped(transform.rotation, toCasted.transform.rotation, (float)t),
                Vector3.LerpUnclamped(transform.scale, toCasted.transform.scale, (float)t)
            );
        }
    }
}
