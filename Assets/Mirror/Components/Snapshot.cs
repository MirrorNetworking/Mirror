// snapshot for snapshot interpolation
// https://gafferongames.com/post/snapshot_interpolation/
// position, rotation, scale for compatibility for now.
using UnityEngine;

namespace Mirror
{
    public struct Snapshot
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
        // TODO consider double for precision over days
        //
        // [REMOTE TIME, NOT LOCAL TIME]
        public float timestamp;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public Snapshot(float timestamp, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.timestamp = timestamp;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }
}
