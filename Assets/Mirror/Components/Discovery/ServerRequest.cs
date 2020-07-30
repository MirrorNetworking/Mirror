namespace Mirror.Discovery
{
    public struct ServerRequest : IMessageBase
    {
        // Weaver will generate serialization
        public void Serialize(NetworkWriter writer) {}
        public void Deserialize(NetworkReader reader) {}
    }
}
