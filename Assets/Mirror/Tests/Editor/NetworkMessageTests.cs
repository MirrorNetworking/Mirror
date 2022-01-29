using NUnit.Framework;

namespace Mirror.Tests.MessageTests
{
    struct TestMessage : NetworkMessage
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;

        public TestMessage(int i, string s, double d)
        {
            IntValue = i;
            StringValue = s;
            DoubleValue = d;
        }

        public void Deserialize(NetworkReader reader)
        {
            IntValue = reader.ReadInt();
            StringValue = reader.ReadString();
            DoubleValue = reader.ReadDouble();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteInt(IntValue);
            writer.WriteString(StringValue);
            writer.WriteDouble(DoubleValue);
        }
    }

    struct StructWithEmptyMethodMessage : NetworkMessage
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;
    }

    [TestFixture]
    public class NetworkMessageTests
    {
        // with Serialize/Deserialize
        [Test]
        public void StructWithMethods()
        {
            byte[] bytes = MessagePackingTest.PackToByteArray(new TestMessage(1, "2", 3.3));
            TestMessage message = MessagePackingTest.UnpackFromByteArray<TestMessage>(bytes);

            Assert.AreEqual(1, message.IntValue);
        }

        // without Serialize/Deserialize. Weaver should handle it.
        [Test]
        public void StructWithEmptyMethods()
        {
            byte[] bytes = MessagePackingTest.PackToByteArray(new StructWithEmptyMethodMessage { IntValue = 1, StringValue = "2", DoubleValue = 3.3 });
            StructWithEmptyMethodMessage message = MessagePackingTest.UnpackFromByteArray<StructWithEmptyMethodMessage>(bytes);

            Assert.AreEqual(1, message.IntValue);
            Assert.AreEqual("2", message.StringValue);
            Assert.AreEqual(3.3, message.DoubleValue);
        }
    }
}
