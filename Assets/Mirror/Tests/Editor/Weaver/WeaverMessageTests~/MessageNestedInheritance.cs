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
    }
}
