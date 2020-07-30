namespace Mirror.Discovery
{
    public struct ServerRequest : NetworkMessage
    {
        // Weaver will generate serialization
        public void Serialize(NetworkWriter writer) {}
        public void Deserialize(NetworkReader reader) {}
    }
}
