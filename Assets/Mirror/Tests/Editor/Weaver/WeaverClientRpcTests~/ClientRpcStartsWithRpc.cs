using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.ClientRpcStartsWithRpc
{
    class ClientRpcStartsWithRpc : NetworkBehaviour
    {
        [ClientRpc]
        void DoesntStartWithRpc() {}
    }
}
