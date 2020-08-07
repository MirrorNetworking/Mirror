using Mirror;

namespace WeaverMessageTests.MessageNestedInheritance
{
    public class Message : MessageBase
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
