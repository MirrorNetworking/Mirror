using Mirror;

namespace WeaverMessageTests.AbstractMessageMethods
{
    abstract class AbstractMessage : NetworkMessage
    {
        public abstract void Deserialize(NetworkReader reader);
        public abstract void Serialize(NetworkWriter writer);
    }

    class OverrideMessage : AbstractMessage
    {
        public int someValue;

        // Mirror will fill out these empty methods

        public override void Serialize(NetworkWriter writer) { }
        public override void Deserialize(NetworkReader reader) { }
    }
}
