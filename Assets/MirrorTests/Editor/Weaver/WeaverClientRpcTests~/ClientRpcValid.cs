using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverClientRpcTests.ClientRpcValid
{
    class ClientRpcValid : NetworkBehaviour
    {
        [ClientRpc]
        void RpcThatIsTotallyValid() {}
    }
}
