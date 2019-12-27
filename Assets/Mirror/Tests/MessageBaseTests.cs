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


    struct WovenTestMessageWithExclusion: IMessageBase
    {
        [WeaverExclude]
        public int IntValue;
        [WeaverExclude]
        public string StringValue;
        public double DoubleValue;

        public void Deserialize(NetworkReader reader) {}
        public void Serialize(NetworkWriter writer) {}
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

        [Test]
        public void WeaverExcludeOnFieldTest()
        {
            NetworkWriter w = new NetworkWriter();
            WovenTestMessageWithExclusion msg = new WovenTestMessageWithExclusion {IntValue = 123, StringValue = "456", DoubleValue = 3.3};
            w.Write(msg);

            byte[] arr = w.ToArray();

            NetworkReader r = new NetworkReader(arr);
            WovenTestMessageWithExclusion t = new WovenTestMessageWithExclusion();
            t.Deserialize(r);

            Assert.AreEqual(123, msg.IntValue);
            Assert.AreNotEqual(123, t.IntValue);
            Assert.AreEqual(default(int), t.IntValue);

            Assert.AreEqual("456", msg.StringValue);
            Assert.AreNotEqual("456", t.StringValue);
            Assert.AreEqual(default(string), t.StringValue);

            Assert.AreEqual(msg.DoubleValue, t.DoubleValue);
        }
    }
}
