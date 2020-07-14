using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverMonoBehaviourTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void MonoBehaviourValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void MonoBehaviourSyncVar()
        {
            Assert.That(weaverErrors, Contains.Item("SyncVar potato must be inside a NetworkBehaviour.  MonoBehaviourSyncVar is not a NetworkBehaviour (at System.Int32 WeaverMonoBehaviourTests.MonoBehaviourSyncVar.MonoBehaviourSyncVar::potato)"));
        }

        [Test]
        public void MonoBehaviourSyncList()
        {
            Assert.That(weaverErrors, Contains.Item("potato is a SyncObject and must be inside a NetworkBehaviour.  MonoBehaviourSyncList is not a NetworkBehaviour (at Mirror.SyncListInt WeaverMonoBehaviourTests.MonoBehaviourSyncList.MonoBehaviourSyncList::potato)"));
        }

        [Test]
        public void MonoBehaviourServerRpc()
        {
            Assert.That(weaverErrors, Contains.Item("ServerRpc CmdThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour (at System.Void WeaverMonoBehaviourTests.MonoBehaviourServerRpc.MonoBehaviourServerRpc::CmdThisCantBeOutsideNetworkBehaviour())"));
        }

        [Test]
        public void MonoBehaviourClientRpc()
        {
            Assert.That(weaverErrors, Contains.Item("ClientRpc RpcThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour (at System.Void WeaverMonoBehaviourTests.MonoBehaviourClientRpc.MonoBehaviourClientRpc::RpcThisCantBeOutsideNetworkBehaviour())"));
        }

        [Test]
        public void MonoBehaviourTargetRpc()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpc TargetThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour (at System.Void WeaverMonoBehaviourTests.MonoBehaviourTargetRpc.MonoBehaviourTargetRpc::TargetThisCantBeOutsideNetworkBehaviour(Mirror.NetworkConnection))"));
        }

        [Test]
        public void MonoBehaviourServer()
        {
            Assert.That(weaverErrors, Contains.Item("Server method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour (at System.Void WeaverMonoBehaviourTests.MonoBehaviourServer.MonoBehaviourServer::ThisCantBeOutsideNetworkBehaviour())"));
        }

        [Test]
        public void MonoBehaviourServerCallback()
        {
            Assert.That(weaverErrors, Contains.Item("Server method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour (at System.Void WeaverMonoBehaviourTests.MonoBehaviourServerCallback.MonoBehaviourServerCallback::ThisCantBeOutsideNetworkBehaviour())"));
        }

        [Test]
        public void MonoBehaviourClient()
        {
            Assert.That(weaverErrors, Contains.Item("Client method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour (at System.Void WeaverMonoBehaviourTests.MonoBehaviourClient.MonoBehaviourClient::ThisCantBeOutsideNetworkBehaviour())"));
        }

        [Test]
        public void MonoBehaviourClientCallback()
        {
            Assert.That(weaverErrors, Contains.Item("Client method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour (at System.Void WeaverMonoBehaviourTests.MonoBehaviourClientCallback.MonoBehaviourClientCallback::ThisCantBeOutsideNetworkBehaviour())"));
        }
    }
}
