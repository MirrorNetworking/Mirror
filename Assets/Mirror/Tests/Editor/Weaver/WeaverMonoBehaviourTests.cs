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
                "Mirror.SyncListInt WeaverMonoBehaviourTests.MonoBehaviourSyncList.MonoBehaviourSyncList::potato");
        }

        [Test]
        public void MonoBehaviourCommand()
        {
            HasError("Command CmdThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourCommand.MonoBehaviourCommand::CmdThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourClientRpc()
        {
            HasError("ClientRpc RpcThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourClientRpc.MonoBehaviourClientRpc::RpcThisCantBeOutsideNetworkBehaviour()");
        }

        [Test]
        public void MonoBehaviourTargetRpc()
        {
            HasError("TargetRpc TargetThisCantBeOutsideNetworkBehaviour must be declared inside a NetworkBehaviour",
                "System.Void WeaverMonoBehaviourTests.MonoBehaviourTargetRpc.MonoBehaviourTargetRpc::TargetThisCantBeOutsideNetworkBehaviour(Mirror.NetworkConnection)");
        }

        [Test]
        public void MonoBehaviourServer()
        {
            IsSuccess();
        }

        [Test]
        public void MonoBehaviourServerCallback()
        {
            IsSuccess();
        }

        [Test]
        public void MonoBehaviourClient()
        {
            IsSuccess();
        }

        [Test]
        public void MonoBehaviourClientCallback()
        {
            IsSuccess();
        }
    }
}
