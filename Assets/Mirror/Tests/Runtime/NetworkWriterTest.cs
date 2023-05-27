// test for https://github.com/MirrorNetworking/Mirror/pull/3492/
// this always passes in editor tests.
// need runtime test to reproduce the FieldAccessException.
using NUnit.Framework;

namespace Mirror.Tests.Runtime
{
    [TestFixture]
    public class WeaveProtectedFields : MirrorEditModeTest
    {
        public class ClassWithProtected
        {
            // should serialize
            public int field1;

            // should NOT serialize
            protected int field2;
            private int field3;
        }

        public struct MyMessage : NetworkMessage
        {
            public ClassWithProtected field;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // start server & connect client because we need spawn functions
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [Test]
        public void WriteProtected()
        {
            NetworkWriter writer = new NetworkWriter();
            MyMessage message = new MyMessage();
            message.field = new ClassWithProtected { field1 = 42 };
            writer.Write(message);

            NetworkReader reader = new NetworkReader(writer);
            MyMessage read = reader.Read<MyMessage>();
            Assert.That(read.field.field1, Is.EqualTo(42));
        }
    }
}
