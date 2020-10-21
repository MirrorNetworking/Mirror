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
        public void ServerRpcAttributeTest()
        {
            var attrib = new ServerRpcAttribute();

            Assert.That(attrib.channel == Channel.Reliable);

            attrib.channel = Channel.Unreliable;

            Assert.That(attrib.channel == Channel.Unreliable);
        }

        [Test]
        public void ClientRPCAttributeTest()
        {
            var attrib = new ClientRpcAttribute();

            Assert.That(attrib.channel == Channel.Reliable);

            attrib.channel = Channel.Unreliable;

            Assert.That(attrib.channel == Channel.Unreliable);
        }
    }
}
