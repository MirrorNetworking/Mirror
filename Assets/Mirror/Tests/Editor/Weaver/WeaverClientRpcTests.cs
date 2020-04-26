using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientRpcTests : BatchedWeaverTests
    {
        [Test]
        public void ClientRpcValid()
        {
            Assert.That(weaverErrors, Is.All.Not.Match(".*MirrorTest.ClientRpcValid.*"));
        }

        [Test]
        public void ClientRpcStartsWithRpc()
        {
            Assert.That(weaverErrors, Contains.Item("DoesntStartWithRpc must start with Rpc.  Consider renaming it to RpcDoesntStartWithRpc (at System.Void MirrorTest.ClientRpcStartsWithRpc::DoesntStartWithRpc())"));
        }

        [Test]
        public void ClientRpcCantBeStatic()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantBeStatic must not be static (at System.Void MirrorTest.ClientRpcCantBeStatic::RpcCantBeStatic())"));
        }
    }
}
