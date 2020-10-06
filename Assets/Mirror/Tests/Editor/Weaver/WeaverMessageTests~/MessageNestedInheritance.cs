using Mirror;

namespace WeaverMessageTests.MessageNestedInheritance
{
    public class Message : INetworkMessage
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
