using System;
using NUnit.Framework;

namespace Mirror.Tests.RemoteAttrributeTest
{
    public class TestMessageBehaviour : NetworkBehaviour
    {
        public static Action<MessageInRpcTest.MyMessage> onRpcCalled;

        [ClientRpc]
        public void RpcSendMessage(MessageInRpcTest.MyMessage message)
        {
            onRpcCalled?.Invoke(message);
        }
    }
    [TestFixture]
    public class MessageInRpcTest : RemoteTestBase
    {
        public class MyMessage : IMessageBase
        {
            public static Action onSerializeCalled;
            public static Action onDeserializeCalled;
            public const int FieldValue = 20;

            public int someField;

            public void Serialize(NetworkWriter writer)
            {
                onSerializeCalled?.Invoke();
            }
            public void Deserialize(NetworkReader reader)
            {
                someField = FieldValue;
                onDeserializeCalled?.Invoke();
            }
        }

        [Test]
        public void RpcUsesMessageBaseMethods()
        {
            int serializeCalled = 0;
            int deserializeCalled = 0;
            int rpcCalled = 0;
            MyMessage recievedMessage = null;

            MyMessage.onSerializeCalled = () => { serializeCalled++; };
            MyMessage.onDeserializeCalled = () => { deserializeCalled++; };
            TestMessageBehaviour.onRpcCalled = (msg) => { rpcCalled++; recievedMessage = msg; };

            MyMessage message = new MyMessage() { someField = 10 };
            TestMessageBehaviour behaviour = CreateHostObject<TestMessageBehaviour>(true);

            behaviour.RpcSendMessage(message);
            ProcessMessages();

            Assert.That(serializeCalled, Is.EqualTo(1));
            Assert.That(deserializeCalled, Is.EqualTo(1));
            Assert.That(rpcCalled, Is.EqualTo(1));

            Assert.That(recievedMessage, Is.Not.Null);
            Assert.That(recievedMessage.someField, Is.EqualTo(MyMessage.FieldValue), "Field is set to FieldValue in Deserialize not the 10 sent above");
        }
    }
}
