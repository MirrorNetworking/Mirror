// snapshot for snapshot interpolation
// https://gafferongames.com/post/snapshot_interpolation/
// position, rotation, scale for compatibility for now.
using UnityEngine;

namespace Mirror.Experimental
{
    internal struct Snapshot
    {
        internal Vector3 position;
        internal Quaternion rotation;
        internal Vector3 scale;
        internal Snapshot(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }
}
