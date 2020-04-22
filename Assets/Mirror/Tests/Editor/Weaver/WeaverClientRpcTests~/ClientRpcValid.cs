using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class ClientRpcValid : NetworkBehaviour
    {
        [ClientRpc]
        void RpcThatIsTotallyValid() {}
    }
}
