namespace Mirror
{
    public interface NetworkMessage
    {
        // weaver populates (de)serialize automatically,
        // but we still need to provide empty methods until Unity gets
        // C# default interface methods
        void Deserialize(NetworkReader reader);
        void Serialize(NetworkWriter writer);
    }
}
