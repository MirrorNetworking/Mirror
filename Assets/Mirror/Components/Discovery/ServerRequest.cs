namespace Mirror.Discovery
{
    public struct ServerRequest : NetworkMessage
    {
        // weaver populates (de)serialize automatically
        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
    }
}
