using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void ClientRpcCantBeStatic()
        {
            HasError("RpcCantBeStatic must not be static",
                "System.Void WeaverClientRpcTests.ClientRpcCantBeStatic.ClientRpcCantBeStatic::RpcCantBeStatic()");
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
    }
}
