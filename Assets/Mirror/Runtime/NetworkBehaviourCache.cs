namespace Mirror
{
    /// <summary>
    /// <para>Data type for storing NetworkBehaviour's network ID and component index.</para>
    /// <para>Use <see cref="Get"/> to get reference to the stored NetworkBehaviour.</para>
    /// </summary>
    public struct NetworkBehaviourCache
    {
        public uint netId;
        public byte componentIndex;

        public NetworkBehaviourCache(uint netId, int componentIndex) : this()
        {
            this.netId = netId;
            this.componentIndex = (byte)componentIndex;
        }

        public NetworkBehaviourCache(NetworkBehaviour behaviour) : this()
        {
            if (behaviour == null)
            {
                this.netId = 0;
                this.componentIndex = 0;
                return;
            }
            this.netId = behaviour.netId;
            this.componentIndex = (byte)behaviour.ComponentIndex;
        }

        public bool Equals(NetworkBehaviourCache other)
        {
            return other.netId == netId && other.componentIndex == componentIndex;
        }

        public bool Equals(uint netId, int componentIndex)
        {
            return this.netId == netId && this.componentIndex == componentIndex;
        }

        public NetworkBehaviour Get()
        {
            if (!NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
                return null;
            return identity.NetworkBehaviours[componentIndex];
        }

        public override string ToString()
        {
            return $"[netId:{netId} compIndex:{componentIndex}]";
        }
    }
}
