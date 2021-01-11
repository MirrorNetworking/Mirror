using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverMonoBehaviourTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void MonoBehaviourValid()
        {
            IsSuccess();
        }

        [Test]
        public void MonoBehaviourSyncVar()
        {
            HasError("SyncVar potato must be inside a NetworkBehaviour.  MonoBehaviourSyncVar is not a NetworkBehaviour",
                "System.Int32 WeaverMonoBehaviourTests.MonoBehaviourSyncVar.MonoBehaviourSyncVar::potato");
        }

        [Test]
        public void MonoBehaviourSyncList()
        {
            HasError("potato is a SyncObject and must be inside a NetworkBehaviour.  MonoBehaviourSyncList is not a NetworkBehaviour",
                "Mirror.SyncList`1<System.Int32> WeaverMonoBehaviourTests.MonoBehaviourSyncList.MonoBehaviourSyncList::potato");
        }

        [Test]
        public void MonoBehaviourServerRpc()
        {
            HasError("ServerRpc CmdThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourServerRpc.MonoBehaviourServerRpc::CmdThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourClientRpc()
        {
            HasError("ClientRpc RpcThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourClientRpc.MonoBehaviourClientRpc::RpcThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourServer()
        {
            HasError("Server method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourServer.MonoBehaviourServer::ThisCantBeOutsideNetworkBehaviour()");
            HasError("ServerAttribute method ThisCantBeOutsideNetworkBehaviour must be declared in a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourServer.MonoBehaviourServer::ThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourServerCallback()
        {
            HasError("Server method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourServerCallback.MonoBehaviourServerCallback::ThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourClient()
        {
            HasError("Client method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourClient.MonoBehaviourClient::ThisCantBeOutsideNetworkBehaviour()");
            HasError("ClientAttribute method ThisCantBeOutsideNetworkBehaviour must be declared in a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourClient.MonoBehaviourClient::ThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourClientCallback()
        {
            HasError("Client method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourClientCallback.MonoBehaviourClientCallback::ThisCantBeOutsideNetworkBehaviour()");
        }
    }
}
