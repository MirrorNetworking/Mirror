using NUnit.Framework;

namespace Mirror.Tests
{
    public class CustomAttrributeTest
    {
        [Test]
        public void SyncVarAttributeTest()
        {
            var attrib = new SyncVarAttribute();

            Assert.That(string.IsNullOrEmpty(attrib.hook));

            attrib.hook = "foo";

            Assert.That(attrib.hook.Equals("foo"));
        }

        [Test]
        public void CommandAttributeTest()
        {
            var attrib = new CommandAttribute();

            Assert.That(attrib.channel == Channels.DefaultReliable);

            attrib.channel = Channels.DefaultUnreliable;

            Assert.That(attrib.channel == Channels.DefaultUnreliable);
        }

        [Test]
        public void ClientRPCAttributeTest()
        {
            var attrib = new ClientRpcAttribute();

            Assert.That(attrib.channel == Channels.DefaultReliable);

            attrib.channel = Channels.DefaultUnreliable;

            Assert.That(attrib.channel == Channels.DefaultUnreliable);
        }

        [Test]
        public void TargetRPCAttributeTest()
        {
            var attrib = new TargetRpcAttribute();

            Assert.That(attrib.channel == Channels.DefaultReliable);

            attrib.channel = Channels.DefaultUnreliable;

            Assert.That(attrib.channel == 1);
        }

        [Test]
        public void SyncEventAttributeTest()
        {
            var attrib = new SyncEventAttribute();

            Assert.That(attrib.channel == Channels.DefaultReliable);

            attrib.channel = Channels.DefaultUnreliable;

            Assert.That(attrib.channel == Channels.DefaultUnreliable);
        }
    }
}
