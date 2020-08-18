using NUnit.Framework;

namespace Mirror.Tests.MessageTests
{
    class ParentMessage : MessageBase
    {
        public int parentValue;
    }

    class ChildMessage : ParentMessage
    {
        public int childValue;
    }


    public abstract class RequestMessageBase : MessageBase
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
    public abstract class RequestMessageBaseReverse : MessageBase
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

            writer.WriteMessage(new ChildMessage
            {
                parentValue = 3,
                childValue = 4
            });

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            ChildMessage received = new ChildMessage();
            received.Deserialize(reader);

            Assert.AreEqual(3, received.parentValue);
            Assert.AreEqual(4, received.childValue);

            int writeLength = writer.Length;
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
            writer.WriteMessage(new ResponseMessage
            {
                state = state,
                message = message,
                responseId = responseId,
            });

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            ResponseMessage received = new ResponseMessage();
            received.Deserialize(reader);

            Assert.AreEqual(state, received.state);
            Assert.AreEqual(message, received.message);
            Assert.AreEqual(responseId, received.responseId);

            int writeLength = writer.Length;
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
            writer.WriteMessage(new ResponseMessageReverse
            {
                state = state,
                message = message,
                responseId = responseId,
            });

            byte[] arr = writer.ToArray();

            NetworkReader reader = new NetworkReader(arr);
            ResponseMessageReverse received = new ResponseMessageReverse();
            received.Deserialize(reader);

            Assert.AreEqual(state, received.state);
            Assert.AreEqual(message, received.message);
            Assert.AreEqual(responseId, received.responseId);

            int writeLength = writer.Length;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }
    }
}
