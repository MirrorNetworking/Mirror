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
        public void ErrorWhenClientRpcDoesntStartWithRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.ErrorWhenClientRpcDoesntStartWithRpc::DoesntStartWithRpc() must start with Rpc.  Consider renaming it to RpcDoesntStartWithRpc"));
        }

        [Test]
        public void ErrorWhenClientRpcIsStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.ErrorWhenClientRpcIsStatic::RpcCantBeStatic() must not be static"));
        }
    }
}
