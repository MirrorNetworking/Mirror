using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void ClientRpcValid()
        {
            Assert.That(weaverErrors, Is.Empty);
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
        }

        [Test]
        public void ClientRpcStartsWithRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.ClientRpcStartsWithRpc::DoesntStartWithRpc() must start with Rpc.  Consider renaming it to RpcDoesntStartWithRpc"));
        }

        [Test]
        public void ClientRpcCantBeStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.ClientRpcCantBeStatic::RpcCantBeStatic() must not be static"));
        }
    }
}
