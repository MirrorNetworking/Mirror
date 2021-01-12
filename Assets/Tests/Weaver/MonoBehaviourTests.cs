using NUnit.Framework;

namespace Mirror.Weaver
{
    public class MonoBehaviourTests : TestsBuildFromTestName
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
                "System.Int32 MonoBehaviourTests.MonoBehaviourSyncVar.MonoBehaviourSyncVar::potato");
        }

        [Test]
        public void MonoBehaviourSyncList()
        {
            HasError("potato is a SyncObject and must be inside a NetworkBehaviour.  MonoBehaviourSyncList is not a NetworkBehaviour",
                "Mirror.SyncList`1<System.Int32> MonoBehaviourTests.MonoBehaviourSyncList.MonoBehaviourSyncList::potato");
        }

        [Test]
        public void MonoBehaviourServerRpc()
        {
            HasError("ServerRpc CmdThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourServerRpc.MonoBehaviourServerRpc::CmdThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourClientRpc()
        {
            HasError("ClientRpc RpcThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourClientRpc.MonoBehaviourClientRpc::RpcThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourServer()
        {
            HasError("Server method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourServer.MonoBehaviourServer::ThisCantBeOutsideNetworkBehaviour()");
            HasError("ServerAttribute method ThisCantBeOutsideNetworkBehaviour must be declared in a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourServer.MonoBehaviourServer::ThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourServerCallback()
        {
            HasError("Server method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourServerCallback.MonoBehaviourServerCallback::ThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourClient()
        {
            HasError("Client method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourClient.MonoBehaviourClient::ThisCantBeOutsideNetworkBehaviour()");
            HasError("ClientAttribute method ThisCantBeOutsideNetworkBehaviour must be declared in a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourClient.MonoBehaviourClient::ThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourClientCallback()
        {
            HasError("Client method ThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void MonoBehaviourTests.MonoBehaviourClientCallback.MonoBehaviourClientCallback::ThisCantBeOutsideNetworkBehaviour()");
        }
    }
}
