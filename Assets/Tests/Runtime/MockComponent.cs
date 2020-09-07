namespace Mirror.Tests
{
    public class MockComponent : NetworkBehaviour
    {
        public int cmdArg1;
        public string cmdArg2;

        [ServerRpc]
        public void Test(int arg1, string arg2)
        {
            this.cmdArg1 = arg1;
            this.cmdArg2 = arg2;
        }

        public NetworkIdentity cmdNi;

        [ServerRpc]
        public void CmdNetworkIdentity(NetworkIdentity ni)
        {
            this.cmdNi = ni;
        }

        public int rpcArg1;
        public string rpcArg2;

        [ClientRpc]
        public void RpcTest(int arg1, string arg2)
        {
            this.rpcArg1 = arg1;
            this.rpcArg2 = arg2;
        }

        public int targetRpcArg1;
        public string targetRpcArg2;
        public INetworkConnection targetRpcConn;

        [ClientRpc(target = Mirror.Client.Connection)]
        public void ClientConnRpcTest(INetworkConnection conn, int arg1, string arg2)
        {
            this.targetRpcConn = conn;
            this.targetRpcArg1 = arg1;
            this.targetRpcArg2 = arg2;
        }

        public int rpcOwnerArg1;
        public string rpcOwnerArg2;

        [ClientRpc(target = Mirror.Client.Owner)]
        public void RpcOwnerTest(int arg1, string arg2)
        {
            this.rpcOwnerArg1 = arg1;
            this.rpcOwnerArg2 = arg2;
        }
    }
}
