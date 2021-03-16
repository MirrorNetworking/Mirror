using NUnit.Framework;

namespace Mirror.Tests
{
    public class CustomAttrributeTest
    {
        [Test]
        public void SyncVarAttributeTest()
        {
            SyncVarAttribute attrib = new SyncVarAttribute();

            Assert.That(string.IsNullOrEmpty(attrib.hook));

            attrib.hook = "foo";

            Assert.That(attrib.hook.Equals("foo"));
        }

        [Test]
        public void CommandAttributeTest()
        {
            CommandAttribute attrib = new CommandAttribute();

            Assert.That(attrib.channel == Channels.Reliable);

            attrib.channel = Channels.Unreliable;

            Assert.That(attrib.channel == Channels.Unreliable);
        }

        [Test]
        public void ClientRPCAttributeTest()
        {
            ClientRpcAttribute attrib = new ClientRpcAttribute();

            Assert.That(attrib.channel == Channels.Reliable);

            attrib.channel = Channels.Unreliable;

            Assert.That(attrib.channel == Channels.Unreliable);
        }

        [Test]
        public void TargetRPCAttributeTest()
        {
            TargetRpcAttribute attrib = new TargetRpcAttribute();

            Assert.That(attrib.channel == Channels.Reliable);

            attrib.channel = Channels.Unreliable;

            Assert.That(attrib.channel == 1);
        }
    }
}
