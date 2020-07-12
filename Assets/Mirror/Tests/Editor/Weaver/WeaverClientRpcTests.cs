using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void ClientRpcValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void ClientRpcCantBeStatic()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantBeStatic must not be static (at System.Void WeaverClientRpcTests.ClientRpcCantBeStatic.ClientRpcCantBeStatic::RpcCantBeStatic())"));
        }

        [Test]
        public void VirtualClientRpc()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualClientRpc()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AbstractClientRpc()
        {
            Assert.That(weaverErrors, Contains.Item("Abstract ClientRpc are currently not supported, use virtual method instead (at System.Void WeaverClientRpcTests.AbstractClientRpc.AbstractClientRpc::RpcDoSomething())"));
        }

        [Test]
        public void OverrideAbstractClientRpc()
        {
            Assert.That(weaverErrors, Contains.Item("Abstract ClientRpc are currently not supported, use virtual method instead (at System.Void WeaverClientRpcTests.OverrideAbstractClientRpc.BaseBehaviour::RpcDoSomething())"));
        }

        [Test]
        public void ClientRpcThatExcludesOwner()
        {
            Assert.That(weaverErrors, Is.Empty);
        }
    }
}
