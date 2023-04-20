// TODO Send only supports structs. Consider removing those tests.
using NUnit.Framework;

namespace Mirror.Tests.MessageTests
{
    class ParentMessage : NetworkMessage
    {
        public int parentValue;
    }

    class ChildMessage : ParentMessage
    {
        public int childValue;
    }


    public abstract class RequestMessageBase : NetworkMessage
    {
        public int responseId = 0;
    }
    public class ResponseMessage : RequestMessageBase
    {
        public int state;
        public string message = "";
        public int errorCode = 0; // optional for error codes
    }

    //reverseOrder to test this https://github.com/vis2k/Mirror/issues/1925
    public class ResponseMessageReverse : RequestMessageBaseReverse
    {
        public int state;
        public string message = "";
        public int errorCode = 0; // optional for error codes
    }
    public abstract class RequestMessageBaseReverse : NetworkMessage
    {
        public int responseId = 0;
    }

    [TestFixture]
    public class MessageInheritanceTest
    {
        [Test]
        public void SendsVauesInParentAndChildClass()
        {
            NetworkWriter writer = new NetworkWriter();

            writer.Write(new ChildMessage
            {
                parentValue = 3,
                childValue = 4
            });

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            ChildMessage received = reader.Read<ChildMessage>();

            Assert.AreEqual(3, received.parentValue);
            Assert.AreEqual(4, received.childValue);

            int writeLength = writer.Position;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }

        [Test]
        public void SendsVauesWhenUsingAbstractClass()
        {
            NetworkWriter writer = new NetworkWriter();

            const int state = 2;
            const string message = "hello world";
            const int responseId = 5;
            writer.Write(new ResponseMessage
            {
                state = state,
                message = message,
                responseId = responseId,
            });

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            ResponseMessage received = reader.Read<ResponseMessage>();

            Assert.AreEqual(state, received.state);
            Assert.AreEqual(message, received.message);
            Assert.AreEqual(responseId, received.responseId);

            int writeLength = writer.Position;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }

        [Test]
        public void SendsVauesWhenUsingAbstractClassReverseDefineOrder()
        {
            NetworkWriter writer = new NetworkWriter();

            const int state = 2;
            const string message = "hello world";
            const int responseId = 5;
            writer.Write(new ResponseMessageReverse
            {
                state = state,
                message = message,
                responseId = responseId,
            });

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            ResponseMessageReverse received = reader.Read<ResponseMessageReverse>();

            Assert.AreEqual(state, received.state);
            Assert.AreEqual(message, received.message);
            Assert.AreEqual(responseId, received.responseId);

            int writeLength = writer.Position;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }
    }
}
