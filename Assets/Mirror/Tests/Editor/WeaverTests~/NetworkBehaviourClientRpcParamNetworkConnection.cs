using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcCantHaveParamOptional(NetworkConnection monkeyCon) {}
    }
}
