using Mirror;

namespace WeaverMessageTests.MessageNestedInheritance
{
    public class Message : IMessageBase
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
