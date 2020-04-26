using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverClientRpcTests.ClientRpcStartsWithRpc
{
    class ClientRpcStartsWithRpc : NetworkBehaviour
    {
        [ClientRpc]
        void DoesntStartWithRpc() {}
    }
}
