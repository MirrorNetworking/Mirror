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

        [Test]
        public void ServerAttributeTest()
        {
            var attrib = new ServerAttribute();

            Assert.IsTrue(attrib.error);

            attrib.error = false;

            Assert.IsFalse(attrib.error);
        }

        [Test]
        public void ClientAttributeTest()
        {
            var attrib = new ClientAttribute();

            Assert.IsTrue(attrib.error);

            attrib.error = false;

            Assert.IsFalse(attrib.error);
        }

        [Test]
        public void HasAuthorityAttributeTest()
        {
            var attrib = new HasAuthorityAttribute();

            Assert.IsTrue(attrib.error);

            attrib.error = false;

            Assert.IsFalse(attrib.error);
        }

        [Test]
        public void LocalPlayerAttributeTest()
        {
            var attrib = new LocalPlayerAttribute();

            Assert.IsTrue(attrib.error);

            attrib.error = false;

            Assert.IsFalse(attrib.error);
        }
    }
}
