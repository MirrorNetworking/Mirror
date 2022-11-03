using System;

namespace Mirror
{
    // backing field for sync NetworkBehaviour
    public struct NetworkBehaviourSyncVar : IEquatable<NetworkBehaviourSyncVar>
    {
        public uint netId;
        // limited to 255 behaviours per identity
        public byte componentIndex;

        public NetworkBehaviourSyncVar(uint netId, int componentIndex) : this()
        {
            this.netId = netId;
            this.componentIndex = (byte)componentIndex;
        }

        public bool Equals(NetworkBehaviourSyncVar other)
        {
            return other.netId == netId && other.componentIndex == componentIndex;
        }

        public bool Equals(uint netId, int componentIndex)
        {
            return this.netId == netId && this.componentIndex == componentIndex;
        }

        public override string ToString()
        {
            return $"[netId:{netId} compIndex:{componentIndex}]";
        }
    }
}
