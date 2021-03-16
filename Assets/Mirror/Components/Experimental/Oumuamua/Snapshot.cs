// snapshot for snapshot interpolation
// https://gafferongames.com/post/snapshot_interpolation/
// position, rotation, scale for compatibility for now.
using UnityEngine;

namespace Mirror.Experimental
{
    internal struct Snapshot
    {
        // "When we send snapshot data in packets, we include at the top a 16 bit
        // sequence number. This sequence number starts at zero and increases
        // with each packet sent. We use this sequence number on receive to
        // determine if the snapshot in a packet is newer or older than the most
        // recent snapshot received. If it’s older then it’s thrown away."
        internal ushort sequence;

        internal Vector3 position;
        internal Quaternion rotation;
        internal Vector3 scale;

        internal Snapshot(ushort sequence, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.sequence = sequence;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }
}
