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
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [SyncVar] System.Int32 MirrorTest.MirrorTestPlayer::potato must be inside a NetworkBehaviour.  MirrorTest.MirrorTestPlayer is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourSyncList()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Mirror.SyncListInt MirrorTest.MirrorTestPlayer::potato is a SyncObject and must be inside a NetworkBehaviour.  MirrorTest.MirrorTestPlayer is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourCommand()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [Command] System.Void MirrorTest.MirrorTestPlayer::CmdThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClientRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [ClientRpc] System.Void MirrorTest.MirrorTestPlayer::RpcThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourTargetRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [TargetRpc] System.Void MirrorTest.MirrorTestPlayer::TargetThisCantBeOutsideNetworkBehaviour(Mirror.NetworkConnection) must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourServer()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [Server] System.Void MirrorTest.MirrorTestPlayer::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourServerCallback()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [ServerCallback] System.Void MirrorTest.MirrorTestPlayer::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClient()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [Client] System.Void MirrorTest.MirrorTestPlayer::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClientCallback()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: [ClientCallback] System.Void MirrorTest.MirrorTestPlayer::ThisCantBeOutsideNetworkBehaviour() must be declared inside a NetworkBehaviour"));
        }
    }
}
