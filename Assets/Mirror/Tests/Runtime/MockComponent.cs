namespace Mirror.Tests
{
    public class MockComponent : NetworkBehaviour
    {
        public int cmdArg1;
        public string cmdArg2;

        [Command]
        public void CmdTest(int arg1, string arg2)
        {
            this.cmdArg1 = arg1;
            this.cmdArg2 = arg2;
        }

        public NetworkIdentity cmdNi;

        [Command]
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

        [TargetRpc]
        public void TargetRpcTest(INetworkConnection conn, int arg1, string arg2)
        {
            this.targetRpcConn = conn;
            this.targetRpcArg1 = arg1;
            this.targetRpcArg2 = arg2;
        }
    }
}
