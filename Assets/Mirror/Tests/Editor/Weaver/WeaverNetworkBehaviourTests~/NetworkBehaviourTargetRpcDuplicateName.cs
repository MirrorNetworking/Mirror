using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [TargetRpc]
        public void TargetRpcCantHaveSameName(INetworkConnection monkeyCon, int abc) { }

        [TargetRpc]
        public void TargetRpcCantHaveSameName(INetworkConnection monkeyCon, int abc, int def) { }
    }
}
