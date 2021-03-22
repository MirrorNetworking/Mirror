using Mirror;

namespace WeaverMessageTests.MessageNestedInheritance
{
    public class Message : NetworkMessage
    {
        public class Request : Message
        {

        }

        public class Response : Message
        {
            public int errorCode;
        }

        public void Serialize(NetworkWriter writer) {}
        public void Deserialize(NetworkReader reader) {}
    }
}
