using NUnit.Framework;

namespace Mirror.Tests
{
    struct TestMessage : IMessageBase
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
            IntValue = reader.ReadInt32();
            StringValue = reader.ReadString();
            DoubleValue = reader.ReadDouble();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteInt32(IntValue);
            writer.WriteString(StringValue);
            writer.WriteDouble(DoubleValue);
        }
    }

    [TestFixture]
    public class MessageBaseTests
    {
        [Test]
        public void Roundtrip()
        {
            NetworkWriter w = new NetworkWriter();
            w.Write(new TestMessage(1, "2", 3.3));

            byte[] arr = w.ToArray();

            NetworkReader r = new NetworkReader(arr);
            TestMessage t = new TestMessage();
            t.Deserialize(r);

            Assert.AreEqual(1, t.IntValue);
        }
    }
}
