using NUnit.Framework;

namespace Mirror
{
    struct TestMessage
        : IMessageBase
    {
        public int I;
        public string S;
        public double D;

        public TestMessage(int i, string s, double d)
        {
            I = i;
            S = s;
            D = d;
        }

        public void Deserialize(NetworkReader reader)
        {
            I = reader.ReadInt32();
            S = reader.ReadString();
            D = reader.ReadDouble();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(I);
            writer.Write(S);
            writer.Write(D);
        }
    }

    [TestFixture]
    public class MessageBaseTests
    {
        [Test]
        public void Roundtrip()
        {
            var w = new NetworkWriter();
            w.Write(new TestMessage(1, "2", 3.3));

            var arr = w.ToArray();

            var r = new NetworkReader(arr);
            var t = new TestMessage();
            t.Deserialize(r);

            Assert.AreEqual(1, t.I);
        }
    }
}
