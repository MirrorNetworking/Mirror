using NUnit.Framework;

namespace Mirror.Tests
{
    class ParentMessage : MessageBase
    {
        public int parentValue;
    }

    class ChildMessage : ParentMessage
    {
        public int childValue;
    }

    [TestFixture]
    public class MessageInheritanceTests
    {
        [Test]
        public void Roundtrip()
        {
            var w = new NetworkWriter();

            w.WriteMessage(new ChildMessage
            {
                parentValue = 3,
                childValue = 4
            });

            byte[] arr = w.ToArray();

            var r = new NetworkReader(arr);
            var received = new ChildMessage();
            received.Deserialize(r);

            Assert.AreEqual(3, received.parentValue);
            Assert.AreEqual(4, received.childValue);
        }
    }
}
