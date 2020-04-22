using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverMonoBehaviourTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void MonoBehaviourValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void MonoBehaviourSyncVar()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [SyncVar] System.Int32 MirrorTest.MonoBehaviourSyncVar::potato must be inside a NetworkBehaviour.  MirrorTest.MonoBehaviourSyncVar is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourSyncList()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Mirror.SyncListInt MirrorTest.MonoBehaviourSyncList::potato is a SyncObject and must be inside a NetworkBehaviour.  MirrorTest.MonoBehaviourSyncList is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourCommand()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [Command] System.Void MirrorTest.MonoBehaviourCommand::CmdThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClientRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [ClientRpc] System.Void MirrorTest.MonoBehaviourClientRpc::RpcThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourTargetRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [TargetRpc] System.Void MirrorTest.MonoBehaviourTargetRpc::TargetThisCantBeOutsideNetworkBehaviour(Mirror.NetworkConnection) must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourServer()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [Server] System.Void MirrorTest.MonoBehaviourServer::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourServerCallback()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [ServerCallback] System.Void MirrorTest.MonoBehaviourServerCallback::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClient()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [Client] System.Void MirrorTest.MonoBehaviourClient::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClientCallback()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [ClientCallback] System.Void MirrorTest.MonoBehaviourClientCallback::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }
    }
}
