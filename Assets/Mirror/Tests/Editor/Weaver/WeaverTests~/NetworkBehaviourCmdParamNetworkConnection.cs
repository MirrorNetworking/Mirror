using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [Command]
        public void CmdCantHaveParamOptional(INetworkConnection monkeyCon) { }
    }
}
