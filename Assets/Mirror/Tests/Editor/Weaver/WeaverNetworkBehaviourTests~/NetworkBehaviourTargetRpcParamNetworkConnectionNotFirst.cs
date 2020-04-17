using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveParamOptional(int abc, INetworkConnection monkeyCon) { }
    }
}
