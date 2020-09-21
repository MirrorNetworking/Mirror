using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void ClientRpcValid()
        {
            IsSuccess();
        }

        [Test]
        public void ClientRpcCantBeStatic()
        {
            HasError("RpcCantBeStatic must not be static",
                "System.Void WeaverClientRpcTests.ClientRpcCantBeStatic.ClientRpcCantBeStatic::RpcCantBeStatic()");
        }

        [Test]
        public void VirtualClientRpc()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualClientRpc()
        {
            IsSuccess();
        }

        [Test]
        public void AbstractClientRpc()
        {
            HasError("Abstract ClientRpc are currently not supported, use virtual method instead",
                "System.Void WeaverClientRpcTests.AbstractClientRpc.AbstractClientRpc::RpcDoSomething()");
        }

        [Test]
        public void OverrideAbstractClientRpc()
        {
            HasError("Abstract ClientRpc are currently not supported, use virtual method instead",
                "System.Void WeaverClientRpcTests.OverrideAbstractClientRpc.BaseBehaviour::RpcDoSomething()");
        }

        [Test]
        public void ClientRpcThatExcludesOwner()
        {
            IsSuccess();
        }

        [Test]
        public void ClientRpcConnCantSkipNetworkConn()
        {
            Assert.That(weaverErrors, Contains.Item("ClientRpc with Client.Connection needs a network connection parameter (at System.Void WeaverClientRpcTests.ClientRpcConnCantSkipNetworkConn.ClientRpcConnCantSkipNetworkConn::ClientRpcMethod())"));
        }

        [Test]
        public void ClientRpcOwnerCantExcludeOwner()
        {
            Assert.That(weaverErrors, Contains.Item("ClientRpc with Client.Owner cannot have excludeOwner set as true (at System.Void WeaverClientRpcTests.ClientRpcOwnerCantExcludeOwner.ClientRpcOwnerCantExcludeOwner::ClientRpcMethod())"));
        }
    }
}
